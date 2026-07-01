using DungeonOfChrome.Core.Data.DataModels;
using DungeonOfChrome.Core.Entities;
using DungeonOfChrome.Core.Rng;

namespace DungeonOfChrome.Core.Combat;

/// <summary>
/// Damage and healing formulas. Port of combat/damage.py. Constructor-injected config + RNG
/// (not Python's module-level cached config + global random), so tests can use fixture config
/// and a seeded/scripted RNG.
///
/// Physical:   raw = str * power * base_mult;  final = max(1, (raw - vit*def_factor) * variance)
/// Magical:    raw = int * power * base_mult;  final = max(1, (raw - int*res_factor) * variance)
/// P/M hybrid: average of both formulas
/// Healing:    amount = int * power * heal_mult * variance
///
/// Status modifiers are applied BEFORE the variance roll so they stack predictably.
/// </summary>
public sealed class DamageCalculator
{
    private readonly CombatConfigData _cfg;
    private readonly IRandomSource _rng;

    public DamageCalculator(CombatConfigData cfg, IRandomSource rng)
    {
        _cfg = cfg;
        _rng = rng;
    }

    /// <param name="ignoreDefensePct">0.0-1.0 fraction of target's defense to bypass.</param>
    public (int Damage, bool WasCrit) CalcPhysical(
        Combatant actor, Combatant target, double power = 1.0, double ignoreDefensePct = 0.0, double critBonus = 0.0)
    {
        var c = _cfg.Physical;
        var se = _cfg.StatusEffects;
        var atk = actor.Strength * power * c.BaseMultiplier;
        var def = target.Vitality * c.DefenseFactor * (1.0 - ignoreDefensePct);

        if (actor.HasStatus("berserk")) atk *= se.BerserkAtkFactor;
        if (actor.HasStatus("blessed")) atk *= se.BlessedAtkFactor;
        if (actor.HasStatus("weakened")) atk *= se.WeakenedDefFactor;
        if (target.HasStatus("guarding")) def /= se.GuardingDmgFactor; // guarding raises effective def
        if (target.HasStatus("weakened")) def *= se.WeakenedDefFactor;
        if (target.HasStatus("berserk")) def *= se.BerserkDefFactor;

        var critChance = c.CritBaseChance + actor.Luck * c.LuckCritFactor + critBonus;
        var wasCrit = _rng.NextDouble() < critChance;
        var raw = Math.Max(1.0, atk - def);
        if (wasCrit)
            raw *= c.CritMultiplier;

        var variance = _rng.NextDouble(1.0 - c.Variance, 1.0 + c.Variance);
        return (Math.Max(1, (int)(raw * variance)), wasCrit);
    }

    public (int Damage, bool WasCrit) CalcMagical(
        Combatant actor, Combatant target, double power = 1.0, double ignoreDefensePct = 0.0)
    {
        var c = _cfg.Magical;
        var se = _cfg.StatusEffects;
        var atk = actor.Intelligence * power * c.BaseMultiplier;
        var res = target.Intelligence * c.ResistanceFactor * (1.0 - ignoreDefensePct);

        if (actor.HasStatus("blessed")) atk *= se.BlessedAtkFactor;
        if (target.HasStatus("weakened")) res *= se.WeakenedDefFactor;

        // Verified quirk: magical.json has no crit_base_chance, so this always takes the
        // "absent" branch — physical's base chance combined with MAGICAL's own luck factor
        // (not a full fallback to physical's luck factor). Do not "simplify" this away.
        var critChance = c.CritBaseChance.HasValue
            ? c.CritBaseChance.Value + actor.Luck * c.LuckCritFactor
            : _cfg.Physical.CritBaseChance + actor.Luck * c.LuckCritFactor;
        var wasCrit = _rng.NextDouble() < critChance;

        var raw = Math.Max(1.0, atk - res);
        if (wasCrit)
            raw *= _cfg.Physical.CritMultiplier; // always physical's multiplier, even on a magical crit

        var variance = _rng.NextDouble(1.0 - c.Variance, 1.0 + c.Variance);
        return (Math.Max(1, (int)(raw * variance)), wasCrit);
    }

    /// <summary>Hybrid: average of physical and magical damage.</summary>
    public (int Damage, bool WasCrit) CalcPhysicalMagical(
        Combatant actor, Combatant target, double power = 1.0, double ignoreDefensePct = 0.0)
    {
        var (pDmg, pCrit) = CalcPhysical(actor, target, power, ignoreDefensePct);
        var (mDmg, mCrit) = CalcMagical(actor, target, power, ignoreDefensePct);
        return ((pDmg + mDmg) / 2, pCrit || mCrit); // integer division truncates, like Python's //
    }

    public int CalcHeal(Combatant actor, double power = 1.0)
    {
        var c = _cfg.Healing;
        var raw = actor.Intelligence * power * c.BaseMultiplier;
        if (actor.HasStatus("blessed"))
            raw *= _cfg.StatusEffects.BlessedAtkFactor;
        var variance = _rng.NextDouble(1.0 - c.Variance, 1.0 + c.Variance);
        return Math.Max(1, (int)(raw * variance));
    }

    /// <summary>Damage from end-of-turn DoT effects (poison, burn) — both stack additively.</summary>
    public int CalcDot(Combatant target)
    {
        var total = 0;
        var se = _cfg.StatusEffects;
        if (target.HasStatus("poison"))
            total += Math.Max(1, (int)(target.MaxHp * se.PoisonDotPercent));
        if (target.HasStatus("burn"))
            total += (int)se.BurnFlatDamage;
        return total;
    }

    /// <summary>Healing from end-of-turn regen.</summary>
    public int CalcRegen(Combatant target)
    {
        if (!target.HasStatus("regenerating"))
            return 0;
        return Math.Max(1, (int)(target.MaxHp * _cfg.StatusEffects.RegenHealPercent));
    }

    public double FleeChance(double partyAvgSpeed, double enemyAvgSpeed)
    {
        var c = _cfg.Flee;
        var delta = partyAvgSpeed - enemyAvgSpeed;
        return Math.Min(0.95, Math.Max(0.05, c.BaseChance + delta * c.SpeedFactor));
    }
}
