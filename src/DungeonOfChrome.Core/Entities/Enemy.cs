using DungeonOfChrome.Core.Combat;
using DungeonOfChrome.Core.Rng;

namespace DungeonOfChrome.Core.Entities;

/// <summary>Enemy combatant with floor-scaled stats frozen at spawn time. Port of entities/enemy.py.</summary>
public sealed class Enemy : Combatant
{
    private static int _instanceCounter;

    public EnemyDefinition EnemyDef { get; }
    public int Floor { get; }

    private readonly Dictionary<string, int> _stats;

    /// <summary>Unique per spawned instance — substitutes for Python's id(self) object identity,
    /// which has no direct .NET equivalent with the same uniqueness guarantee.</summary>
    private readonly int _instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);

    public Enemy(EnemyDefinition enemyDef, int floor = 1) : base(enemyDef.Name)
    {
        EnemyDef = enemyDef;
        Floor = floor;

        // Freeze stats at creation time — they don't change mid-combat.
        _stats = StatKeys.All.ToDictionary(
            k => k,
            k => enemyDef.BaseStats.GetValueOrDefault(k, 0)
                 + (int)(enemyDef.StatGrowth.GetValueOrDefault(k, 0.0) * Math.Max(0, floor - 1)));

        CurrentHpRaw = _stats["hp"];
        CurrentMpRaw = _stats["mp"];
    }

    public override int MaxHp => _stats["hp"];
    public override int MaxMp => _stats["mp"];
    public override int GetStat(string stat) => _stats.GetValueOrDefault(stat, 0);

    public override string CombatantId() => $"enemy_{EnemyDef.Id}_{_instanceId}";

    // ------------------------------------------------------------------
    // AI — port of entities/enemy.py's choose_action/_pick_targets/_pick_attack_target
    // ------------------------------------------------------------------

    public CombatAction ChooseAction(BattleEngine battle, IRandomSource rng)
    {
        var livingParty = battle.LivingParty;
        if (livingParty.Count == 0)
            return new CombatAction(this, ActionType.Defend);

        var behavior = EnemyDef.AiBehavior;
        var abilityIds = EnemyDef.Abilities.Where(a => a != "basic_attack").ToList();

        // Rough readiness check on just the first non-basic ability, matching Python literally
        // (not "fixed" to check any ready ability — this is the source behavior).
        var useAbility = abilityIds.Count > 0
            && battle.Cooldowns.IsReady(this, abilityIds[0])
            && (
                (behavior == "aggressive" && rng.NextDouble() < 0.25)
                || (behavior == "tactical" && rng.NextDouble() < 0.45)
                || (behavior == "support" && rng.NextDouble() < 0.55)
            );

        if (useAbility)
        {
            var ready = abilityIds.Where(a => battle.Cooldowns.IsReady(this, a)).ToList();
            if (ready.Count > 0)
            {
                var abilityId = ready[rng.NextInt(0, ready.Count)];
                var ability = battle.Abilities.Get(abilityId);
                var targets = PickTargets(ability, battle, rng);
                if (targets != null)
                    return new CombatAction(this, ActionType.Ability, abilityId, targets);
            }
        }

        var target = PickAttackTarget(livingParty, behavior, rng);
        return new CombatAction(this, ActionType.Attack, targets: new List<Combatant> { target });
    }

    private static Combatant PickAttackTarget(IReadOnlyList<Combatant> livingParty, string behavior, IRandomSource rng)
    {
        if (behavior == "tactical")
            return livingParty.OrderBy(c => c.HpFraction).First(); // prefer lowest HP fraction
        // "support" and "aggressive": random.
        return livingParty[rng.NextInt(0, livingParty.Count)];
    }

    /// <summary>Return the target list for an ability, or null if no valid targets.</summary>
    private List<Combatant>? PickTargets(AbilityDefinition ability, BattleEngine battle, IRandomSource rng)
    {
        var lp = battle.LivingParty;
        var le = battle.LivingEnemies;

        switch (ability.Targeting)
        {
            case "SINGLE_ALLY":
                // For enemies, "ally" means other enemies (or self).
                return le.Count > 0 ? new List<Combatant> { le[rng.NextInt(0, le.Count)] } : null;
            case "ALL_ALLIES":
                return le.Count > 0 ? le : null;
            case "SINGLE_ENEMY":
                return lp.Count > 0 ? new List<Combatant> { lp[rng.NextInt(0, lp.Count)] } : null;
            case "ALL_ENEMIES":
                return lp.Count > 0 ? lp : null;
            case "SELF":
                return new List<Combatant> { this };
            default:
                return null;
        }
    }
}
