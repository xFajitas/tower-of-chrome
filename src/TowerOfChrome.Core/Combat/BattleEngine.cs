using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Loot;
using TowerOfChrome.Core.Rng;

namespace TowerOfChrome.Core.Combat;

/// <summary>
/// Drives a single combat encounter. Port of combat/battle.py.
///
/// Usage
/// -----
/// engine.Start(party, enemies);
/// // player turn:
/// var result = engine.SubmitPlayerAction(action);
/// // enemy turn:
/// var result = engine.AdvanceEnemyTurn();
///
/// The engine is intentionally stateless between turns; the UI/View layer drives it.
/// </summary>
public sealed class BattleEngine
{
    public const int MaxLogLines = 60;

    public BattlePhase Phase { get; private set; } = BattlePhase.Setup;
    public int Round { get; private set; }
    public CooldownRegistry Cooldowns { get; } = new();
    public StatusTracker Statuses { get; }
    public AbilityRegistry Abilities => _abilities;
    public List<XpAward> XpAwards { get; private set; } = new();
    public List<string> PendingDrops { get; private set; } = new();

    /// <summary>Invoked when loot generation throws (mirrors Python's except-and-print).</summary>
    public Action<string>? OnLootGenerationFailed { get; set; }

    private readonly AbilityRegistry _abilities;
    private readonly DamageCalculator _damage;
    private readonly IRandomSource _rng;
    private readonly LootTables _lootTables;

    private Party? _party;
    private List<Enemy> _enemies = new();
    private int _floor;
    private List<Combatant> _turnOrder = new();
    private int _turnIndex;
    private readonly List<string> _log = new();
    private int _xpPending;

    public BattleEngine(AbilityRegistry abilities, DamageCalculator damage, IRandomSource rng, LootTables lootTables)
    {
        _abilities = abilities;
        _damage = damage;
        _rng = rng;
        _lootTables = lootTables;
        Statuses = new StatusTracker(damage);
    }

    // ------------------------------------------------------------------
    // Startup
    // ------------------------------------------------------------------

    public void Start(Party party, List<Enemy> enemies, int floor = 1)
    {
        _party = party;
        _enemies = enemies;
        _floor = floor;
        Round = 1;
        Phase = BattlePhase.Ongoing;
        PendingDrops = new List<string>();
        XpAwards = new List<XpAward>();
        RollInitiativeForRound();
        LogLine($"── Round {Round} ──");
        LogLine("Initiative: " + string.Join(", ", _turnOrder.Select(c => c.Name)));
    }

    // ------------------------------------------------------------------
    // Public queries
    // ------------------------------------------------------------------

    public Combatant? CurrentActor => _turnOrder.Count == 0 ? null : _turnOrder[_turnIndex];

    public bool IsPlayerTurn
    {
        get
        {
            var actor = CurrentActor;
            return actor != null && IsAlly(actor);
        }
    }

    public bool IsAlly(Combatant combatant) => _party != null && _party.AllMembers.Any(m => ReferenceEquals(m, combatant));

    /// <summary>Living actors still to act after the current one, in this round's order.</summary>
    public List<Combatant> TurnQueue =>
        _turnOrder.Skip(_turnIndex + 1).Where(c => c.IsAlive).ToList();

    public List<Combatant> LivingParty => _party?.LivingMembers.Cast<Combatant>().ToList() ?? new List<Combatant>();

    public List<Combatant> LivingEnemies => _enemies.Where(e => e.IsAlive).Cast<Combatant>().ToList();

    public List<Combatant> AllCombatants
    {
        get
        {
            var members = _party?.AllMembers.Cast<Combatant>().ToList() ?? new List<Combatant>();
            members.AddRange(_enemies);
            return members;
        }
    }

    public List<string> Log => new(_log);

    public List<string> LastLogLines => _log.Skip(Math.Max(0, _log.Count - 8)).ToList();

    public List<Enemy> GetEnemyList() => new(_enemies);

    public bool CanUseAbility(Combatant combatant, string abilityId)
    {
        var ab = _abilities.Get(abilityId);
        if (combatant.CurrentMp < ab.MpCost)
            return false;
        return Cooldowns.IsReady(combatant, abilityId);
    }

    // ------------------------------------------------------------------
    // Player action
    // ------------------------------------------------------------------

    /// <summary>Called when the player confirms their action. Resolves it, ticks status on the
    /// actor, advances the turn. Returns an ActionResult with log lines for the UI.</summary>
    public ActionResult SubmitPlayerAction(CombatAction action)
    {
        var result = Resolve(action);
        PostTurn(action.Actor);
        CheckBattleEnd();
        return result;
    }

    // ------------------------------------------------------------------
    // Enemy auto-turn
    // ------------------------------------------------------------------

    /// <summary>Resolve the current enemy's turn automatically.</summary>
    public ActionResult AdvanceEnemyTurn()
    {
        var actor = CurrentActor;
        if (actor == null || IsPlayerTurn)
            return new ActionResult(new CombatAction(actor!, ActionType.Attack)) { Success = false };

        // Stunned enemies lose their turn. Note: this check exists ONLY on the enemy path —
        // a stunned player-controlled Character is never blocked here, matching Python exactly
        // (submit_player_action has no equivalent stun check; any UI-side gating is a View concern).
        if (actor.HasStatus("stun"))
        {
            LogLine($"{actor.Name} is stunned and loses their turn!");
            actor.RemoveStatus("stun");
            PostTurn(actor);
            CheckBattleEnd();
            var stunResult = new ActionResult(new CombatAction(actor, ActionType.Defend));
            stunResult.LogLines.Add($"{actor.Name} is stunned!");
            return stunResult;
        }

        if (actor is not Enemy enemy)
        {
            // Shouldn't happen, but degrade gracefully.
            PostTurn(actor);
            return new ActionResult(new CombatAction(actor, ActionType.Defend));
        }

        var action = enemy.ChooseAction(this, _rng);
        var result = Resolve(action);
        PostTurn(actor);
        CheckBattleEnd();
        return result;
    }

    // ------------------------------------------------------------------
    // Resolution core
    // ------------------------------------------------------------------

    private ActionResult Resolve(CombatAction action)
    {
        var result = new ActionResult(action);
        var actor = action.Actor;

        switch (action.ActionType)
        {
            case ActionType.Defend:
                actor.AddStatus("guarding");
                Statuses.Apply(actor, "guarding", turns: 1);
                var defendMsg = $"{actor.Name} takes a defensive stance.";
                LogLine(defendMsg);
                result.LogLines.Add(defendMsg);
                return result;

            case ActionType.Flee:
                return ResolveFlee(result);

            case ActionType.Attack:
                return ResolveBasicAttack(action, result);

            case ActionType.Ability:
                return ResolveAbility(action, result);

            default:
                return result;
        }
    }

    private ActionResult ResolveBasicAttack(CombatAction action, ActionResult result)
    {
        foreach (var target in action.Targets)
        {
            if (!target.IsAlive)
                continue;

            var (damage, crit) = _damage.CalcPhysical(action.Actor, target);
            var taken = target.TakeDamage(damage);
            var hit = new HitResult(target) { Damage = taken, WasCrit = crit };
            result.Hits.Add(hit);

            var critTxt = crit ? " CRITICAL!" : "";
            var msg = $"{action.Actor.Name} attacks {target.Name} for {taken} dmg.{critTxt}";
            LogLine(msg);
            result.LogLines.Add(msg);

            if (target.IsKo)
                OnKo(target, result);
        }
        return result;
    }

    private static string ResourceNameOf(Combatant actor) => actor is Character character ? character.ClassDef.ResourceName : "MP";

    private ActionResult ResolveAbility(CombatAction action, ActionResult result)
    {
        var abId = action.AbilityId!;
        var ab = _abilities.Get(abId);
        var actor = action.Actor;

        if (!actor.SpendMp(ab.MpCost))
        {
            var failMsg = $"{actor.Name} doesn't have enough {ResourceNameOf(actor)} to use {ab.Name}!";
            LogLine(failMsg);
            result.Success = false;
            result.LogLines.Add(failMsg);
            return result;
        }

        result.MpSpent = ab.MpCost;

        // Cooldown is set unconditionally once MP is spent, even if targets end up empty below.
        Cooldowns.SetCooldown(actor, abId, ab.Cooldown);

        var header = $"{actor.Name} uses {ab.Name}!";
        LogLine(header);
        result.LogLines.Add(header);

        if (ab.Flags.TryGetValue("self_damage_ratio", out var selfDmgRatio))
        {
            var selfDmg = Math.Max(1, (int)(actor.MaxHp * selfDmgRatio));
            var taken = actor.TakeDamage(selfDmg);
            result.ActorSelfDmg = taken;
            var msg = $"  {actor.Name} loses {taken} HP from the exertion.";
            LogLine(msg);
            result.LogLines.Add(msg);
        }

        if (ab.Flags.TryGetValue("mp_to_hp_ratio", out var mpToHpRatio))
        {
            var convert = (int)(actor.CurrentMp * mpToHpRatio);
            actor.CurrentMp -= convert;
            var healed = actor.Heal(convert);
            var msg = $"  {actor.Name} converts {convert} MP -> {healed} HP.";
            LogLine(msg);
            result.LogLines.Add(msg);
            return result;
        }

        if (ab.Flags.TryGetValue("flat_heal_pct", out var flatHealPct))
        {
            var amount = Math.Max(1, (int)(actor.MaxHp * flatHealPct));
            var healed = actor.Heal(amount);
            var msg = $"  {actor.Name} recovers {healed} HP.";
            LogLine(msg);
            result.LogLines.Add(msg);
            return result;
        }

        if (ab.Flags.ContainsKey("cleanse_debuffs") && action.Targets.Count == 0)
            action.Targets = new List<Combatant> { actor };

        foreach (var target in action.Targets)
        {
            if (!target.IsAlive && !ab.IsHeal)
                continue;

            var hit = new HitResult(target);

            if (ab.Flags.ContainsKey("cleanse_debuffs"))
            {
                var removed = target.ClearDebuffs();
                if (removed > 0)
                {
                    var msg = $"  {target.Name}'s debuffs are cleansed.";
                    LogLine(msg);
                    result.LogLines.Add(msg);
                }
            }

            if (ab.IsOffensive && ab.Power > 0)
            {
                var ignoreDef = ab.Flags.GetValueOrDefault("ignore_defense", 0.0);
                int dmgAmount;
                bool crit;
                if (ab.Type == "physical")
                    (dmgAmount, crit) = _damage.CalcPhysical(actor, target, ab.Power, ignoreDef, ab.Flags.GetValueOrDefault("crit_bonus", 0.0));
                else if (ab.Type == "magical")
                    (dmgAmount, crit) = _damage.CalcMagical(actor, target, ab.Power, ignoreDef);
                else
                    (dmgAmount, crit) = _damage.CalcPhysicalMagical(actor, target, ab.Power, ignoreDef);

                var taken = target.TakeDamage(dmgAmount);
                hit.Damage = taken;
                hit.WasCrit = crit;
                var critTxt = crit ? " CRITICAL!" : "";
                var msg = $"  {target.Name} takes {taken} dmg.{critTxt}";
                LogLine(msg);
                result.LogLines.Add(msg);

                if (ab.Flags.TryGetValue("drain_ratio", out var drainRatio))
                {
                    var drainHeal = Math.Max(1, (int)(taken * drainRatio));
                    actor.Heal(drainHeal);
                    hit.DrainHeal = drainHeal;
                    var drainMsg = $"  {actor.Name} absorbs {drainHeal} HP.";
                    LogLine(drainMsg);
                    result.LogLines.Add(drainMsg);
                }

                if (target.IsKo)
                    OnKo(target, result);
            }
            else if (ab.IsHeal && ab.Power > 0)
            {
                var amount = _damage.CalcHeal(actor, ab.Power);
                var healed = target.Heal(amount);
                hit.Healing = healed;
                var msg = $"  {target.Name} recovers {healed} HP.";
                LogLine(msg);
                result.LogLines.Add(msg);
            }

            if (ab.Flags.TryGetValue("drain_mp_pct", out var drainMpPct))
            {
                var drained = Math.Max(1, (int)(target.MaxMp * drainMpPct));
                var actual = Math.Min(drained, target.CurrentMp);
                target.CurrentMp -= actual;
                hit.MpDrained = actual;
                var msg = $"  {target.Name} loses {actual} MP.";
                LogLine(msg);
                result.LogLines.Add(msg);
            }

            if (ab.Flags.TryGetValue("drain_mp_flat", out var drainMpFlat))
            {
                var actual = Math.Min((int)drainMpFlat, target.CurrentMp);
                target.CurrentMp -= actual;
                hit.MpDrained = actual;
                var msg = $"  {target.Name} loses {actual} MP.";
                LogLine(msg);
                result.LogLines.Add(msg);
            }

            if (ab.StatusEffects.Length > 0)
            {
                foreach (var effect in ab.StatusEffects)
                {
                    if (_rng.NextInt(1, 101) <= ab.StatusChance)
                    {
                        var statusMsg = Statuses.Apply(target, effect);
                        hit.Statuses.Add(effect);
                        LogLine($"  {statusMsg}");
                        result.LogLines.Add($"  {statusMsg}");
                    }
                }
            }

            result.Hits.Add(hit);
        }

        return result;
    }

    private ActionResult ResolveFlee(ActionResult result)
    {
        var living = LivingParty;
        var eSpeeds = LivingEnemies.Select(e => (double)e.Speed).ToList();
        var pSpeeds = living.Select(c => (double)c.Speed).ToList();
        var pAvg = pSpeeds.Count > 0 ? pSpeeds.Average() : 0.0;
        var eAvg = eSpeeds.Count > 0 ? eSpeeds.Average() : 0.0;
        var chance = _damage.FleeChance(pAvg, eAvg);

        string msg;
        if (_rng.NextDouble() < chance)
        {
            Phase = BattlePhase.Fled;
            msg = "The party escapes!";
        }
        else
        {
            msg = "Couldn't escape!";
        }

        LogLine(msg);
        result.LogLines.Add(msg);
        return result;
    }

    // ------------------------------------------------------------------
    // Post-turn housekeeping
    // ------------------------------------------------------------------

    private void PostTurn(Combatant actor)
    {
        foreach (var line in Statuses.Tick(actor))
            LogLine(line);

        Cooldowns.Tick(actor);
        AdvanceTurnIndex();
    }

    private void AdvanceTurnIndex()
    {
        _turnIndex++;
        while (_turnIndex < _turnOrder.Count && !_turnOrder[_turnIndex].IsAlive)
            _turnIndex++;

        if (_turnIndex >= _turnOrder.Count)
            NewRound();
    }

    private void NewRound()
    {
        Round++;
        RollInitiativeForRound();
        LogLine($"── Round {Round} ──");
    }

    private void RollInitiativeForRound()
    {
        _turnOrder = Initiative.RollInitiative(AllCombatants, _rng);
        _turnIndex = 0;
        while (_turnIndex < _turnOrder.Count && !_turnOrder[_turnIndex].IsAlive)
            _turnIndex++;
    }

    private void OnKo(Combatant target, ActionResult result)
    {
        var msg = $"  {target.Name} is knocked out!";
        LogLine(msg);
        result.LogLines.Add(msg);
        Statuses.Clear(target);

        if (target is Enemy enemy)
        {
            var xp = enemy.EnemyDef.XpReward;
            _xpPending += xp;
            LogLine($"  +{xp} XP");
        }
    }

    private void CheckBattleEnd()
    {
        if (Phase is BattlePhase.Victory or BattlePhase.Defeat or BattlePhase.Fled)
            return;

        if (LivingEnemies.Count == 0)
        {
            Phase = BattlePhase.Victory;
            LogLine("Victory! All enemies defeated.");
            AwardXp();
            GenerateLoot();
            return;
        }

        if (LivingParty.Count == 0)
        {
            Phase = BattlePhase.Defeat;
            LogLine("Defeat! The party has been wiped out.");
        }
    }

    private void GenerateLoot()
    {
        try
        {
            var drops = new List<string>();
            foreach (var enemy in _enemies)
            {
                var tableId = enemy.EnemyDef.LootTable;
                if (!string.IsNullOrEmpty(tableId))
                    drops.AddRange(_lootTables.GenerateDrops(tableId, _floor));
            }

            var logLines = _party != null ? _lootTables.AwardDrops(_party, drops) : new List<string>();
            foreach (var line in logLines)
                LogLine(line);
            PendingDrops = drops;
        }
        catch (Exception exc)
        {
            OnLootGenerationFailed?.Invoke($"[BattleEngine] Loot generation failed: {exc.Message}");
        }
    }

    private void AwardXp()
    {
        if (_party == null || _xpPending <= 0)
            return;

        var living = LivingParty.Cast<Character>().ToList();
        var share = Math.Max(1, _xpPending / Math.Max(1, living.Count));

        foreach (var member in living)
        {
            var leveled = member.GainXp(share);
            LogLine($"  {member.Name} gains {share} XP.");
            if (leveled)
                LogLine($"  {member.Name} levelled up to {member.Level}!");
            XpAwards.Add(new XpAward(member.Name, share, leveled, member.Level));
        }

        _xpPending = 0;
    }

    private void LogLine(string text)
    {
        _log.Add(text);
        if (_log.Count > MaxLogLines)
            _log.RemoveRange(0, _log.Count - MaxLogLines);
    }
}
