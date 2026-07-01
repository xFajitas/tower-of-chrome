# Python -> C# Porting Notes

Living doc of behavioral decisions made while porting the game logic of
*Dungeon of Chrome* (the original Python/pygame game) to C# as *Tower of
Chrome* (`TowerOfChrome.Core`). Read this before "fixing" anything that looks
odd — most oddities are intentional parity with the original, not bugs
introduced during the port.

## Randomness — no cross-language reproducibility

Every Core system needing randomness takes an `IRandomSource` via constructor
injection (`Rng/IRandomSource.cs`). This gives deterministic, seeded C# unit
tests, but **a given seed will not reproduce the same output as the Python
version**, and that is not a goal. Python's `random.sample`/`random.shuffle`
use different underlying algorithms than any hand-rolled C# equivalent, even
sharing a seed value. The bar is *internal* determinism (same seed -> same C#
output, every time) and *formula/behavior* parity — not an identical random
stream to Python. Concretely: old Python-seeded dungeon layouts are not
reproducible in the C# port; a save file's `dungeon_floor` is just data and
loads fine regardless, but *fresh* generation from a seed will differ.

Also: in the original Python, `loot/drops.py`'s `generate_drops()` always used
the global `random` module, never the seeded `random.Random(seed)` instance
passed into `generate_floor()`. This meant a seed only ever controlled room
layout, never loot rolls. The C# port preserves this decoupling naturally —
`DungeonGenerator.GenerateFloor` takes its own `rng` for layout, and
`LootTables` carries an independently-injected `IRandomSource`.

## Combat formulas — verified quirks, preserve exactly

- **Magical crit chance** (`Combat/DamageCalculator.cs`, `CalcMagical`):
  `combat_config.json`'s `magical` block has no `crit_base_chance` key. The
  formula is `physical.CritBaseChance + actor.Luck * magical.LuckCritFactor`
  — a hybrid, not a full fallback to physical's own luck factor. The crit
  multiplier on a magical crit is always `physical.CritMultiplier`. There is a
  named regression test for this exact formula
  (`DamageCalculatorTests.CalcMagical_CritChance_UsesPhysicalBaseChance_CombinedWithMagicalLuckFactor`)
  — this is the single highest silent-drift risk in the whole port.
- **Per-round full initiative re-roll** (`Combat/BattleEngine.cs`, `NewRound`):
  turn order is completely re-rolled every round, not incrementally
  re-sorted. A combatant who acted last can act first next round.
- **Deferred XP on victory, not per-kill**: XP from each enemy KO accumulates
  in a private counter and is only distributed once, when victory is
  detected. Split = `Math.Max(1, xpPending / Math.Max(1, livingParty.Count))`
  — living members only, integer division, remainder silently dropped.
- **Ability resolution order** (`ResolveAbility`): MP spend -> cooldown set
  **unconditionally** (even if targets end up empty) -> self_damage_ratio ->
  mp_to_hp_ratio (early return) -> flat_heal_pct (early return) ->
  cleanse_debuffs (defaults to self if no target) -> per-target: damage (by
  type) -> drain_ratio lifesteal -> healing (mutually exclusive with damage)
  -> MP drain flags -> status rolls.
- **Stun only blocks enemy turns.** `AdvanceEnemyTurn` checks `HasStatus("stun")`
  before running AI; `SubmitPlayerAction` has no equivalent check at all. A
  stunned player-controlled Character can still act if something submits an
  action for them — this is a real asymmetry in the source, not fixed here.
- **A cooldown of N is often effectively N-1 turns of unavailability**: 
  `PostTurn` ticks cooldowns immediately after resolving the very action that
  set them. An ability with `cooldown=1` becomes ready again in the same
  `SubmitPlayerAction` call that used it.
- **Initiative tiebreak precomputation** (`Combat/Initiative.cs`): the random
  tiebreak is computed once per combatant *before* sorting, mirroring
  Python's `list.sort(key=...)` semantics (key computed once per element,
  not re-evaluated per comparison). Never call `rng.NextDouble()` inside a
  `Comparison<T>` delegate — call count there is sort-algorithm-dependent.
- **`room_in_direction`** (`Dungeon/DungeonFloor.cs`): the similarity
  threshold is `> 0.30`, strictly greater — not `>=`.
- **Dungeon generator's extra-loop-edge cap**: Python computes
  `range(extra * 10)` once, locking the iteration count at that value before
  `extra` gets decremented inside the loop body. Ported as `var cap = extra * 10;`
  followed by a bounded `for` loop that also checks `extra <= 0` each
  iteration for early exit.
- **`List<T>.Sort` is not stable in .NET**, unlike Python's `list.sort()`
  (Timsort). Anywhere Python relies on stable-sort tie-breaking (e.g. the
  diagonal room sort in `DungeonGenerator.SortRooms`), the C# port uses LINQ
  `OrderBy` (documented stable) instead of `List<T>.Sort`.

## Dead code / gaps — ported faithfully, not "fixed"

These exist in the original and are preserved rather than cleaned up, since
"fixing" them would silently change gameplay balance versus the source:

- `ActionType.Item` — declared, never wired into resolution (no in-battle
  consumable-use path exists; items are only usable from the inventory
  screen between encounters).
- `HitResult.WasMiss` — always false; no evasion/miss mechanic implemented.
- `TargetMode` enum — redundant with `AbilityDefinition.Targeting` (a plain
  string); kept anyway.
- `CombatConfigData.StatusEffects.HasteSpeedBonus` /
  `.ThornsReflectPct` / `DefendConfigData.DamageFactor` — present in the
  JSON schema, never read by any formula. `DEFEND` only applies a `guarding`
  status; it does not read `damage_factor` at all.
- `ItemDefinition.Value` (gold) — stored on every item; no shop/economy
  system exists anywhere to spend or earn it.
- `EventType` members beyond `StateChanged` — declared, never dispatched in
  the original Python UI. (Non-breaking improvement flagged for Phase 2: the
  Unity View layer may legitimately start consuming `TurnStarted`/`PlayerDied`
  etc. for floating combat text or SFX — that's new consumption of an
  existing contract, not a Core behavior change.)

## Architecture decisions

- **Constructor-injected registries, not singletons.** `ClassRegistry`,
  `ItemRegistry`, `EnemyRegistry`, `AbilityRegistry` all take an
  `IGameDataSource` via constructor rather than exposing a Python-style
  `.instance()` global. This is friendlier to unit testing (fresh registry
  per test from fixture data) and avoids Unity domain-reload static-state
  pitfalls in Phase 2.
- **`netstandard2.1` target for Core**, not `net8.0`, for guaranteed
  binary/API compatibility with Unity's Mono/IL2CPP runtimes once Phase 2
  begins. Needed two small compatibility shims as a result:
  - `Compatibility/IsExternalInit.cs` — polyfill required for C# 9
    `record`/init-only-property support on netstandard2.1 (the real type
    ships in .NET 5+; the compiler only needs its presence).
  - `System.Text.Json` and `System.Collections.Immutable` NuGet package
    references — neither ships in the netstandard2.1 reference assemblies.
- **`to_dict()`/`from_dict()` moved out of the entity classes** and into
  `Persistence/SaveLoadService.cs`. Python's `Character.to_dict()` etc. are
  instance methods; in C#, reconstructing a `Character` needs
  `ClassRegistry`/`ItemRegistry`/`Leveling` injected, which the save/load
  layer already holds — keeping serialization there avoids threading those
  dependencies through the entity constructors solely for (de)serialization.
- **`Enemy.ChooseAction` lives on `Enemy`, not in a separate AI class**,
  matching Python's `entities/enemy.py` structure exactly (`choose_action`/
  `_pick_targets`/`_pick_attack_target` are methods on `Enemy` there too).
  It takes `BattleEngine` and `IRandomSource` as parameters rather than
  storing them, since `Enemy` instances are constructed by `EnemyRegistry`
  before a battle exists.
