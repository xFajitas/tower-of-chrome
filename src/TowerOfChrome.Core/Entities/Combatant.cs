namespace TowerOfChrome.Core.Entities;

/// <summary>
/// Abstract base shared by Character (player) and Enemy. Direct port of entities/combatant.py.
///
/// Initialization note (mirrors the Python docstring): subclasses must finish setting up
/// whatever max_hp/max_mp depend on (class_def+inventory, or frozen _stats) BEFORE assigning
/// CurrentHpRaw/CurrentMpRaw directly, since those bypass clamping and MaxHp must already be
/// computable at that point.
/// </summary>
public abstract class Combatant
{
    private static readonly HashSet<string> Debuffs = new() { "poison", "burn", "stun", "weakened", "cursed", "marked", "silenced" };
    private static readonly HashSet<string> Buffs = new() { "blessed", "guarding", "haste", "regenerating", "berserk", "thorns", "shielded" };

    public string Name { get; }
    public List<string> StatusEffects { get; protected set; } = new();

    /// <summary>Direct field access for subclass constructors — bypasses the clamped CurrentHp property setter.</summary>
    protected int CurrentHpRaw;
    protected int CurrentMpRaw;

    protected Combatant(string name)
    {
        Name = name;
    }

    public abstract int MaxHp { get; }
    public abstract int MaxMp { get; }
    public abstract int GetStat(string stat);

    public int Speed => GetStat("spd");
    public int Strength => GetStat("str");
    public int Intelligence => GetStat("int");
    public int Vitality => GetStat("vit");
    public int Dexterity => GetStat("dex");
    public int Luck => GetStat("luck");

    public int CurrentHp
    {
        get => CurrentHpRaw;
        set => CurrentHpRaw = Math.Max(0, Math.Min(value, MaxHp));
    }

    public int CurrentMp
    {
        get => CurrentMpRaw;
        set => CurrentMpRaw = Math.Max(0, Math.Min(value, MaxMp));
    }

    public bool IsAlive => CurrentHpRaw > 0;
    public bool IsKo => !IsAlive;
    public double HpFraction => MaxHp != 0 ? (double)CurrentHpRaw / MaxHp : 0.0;
    public double MpFraction => MaxMp != 0 ? (double)CurrentMpRaw / MaxMp : 0.0;

    /// <summary>Apply damage (clamped). Returns actual HP lost.</summary>
    public int TakeDamage(int amount)
    {
        var actual = Math.Min(Math.Max(0, amount), CurrentHpRaw);
        CurrentHpRaw -= actual;
        return actual;
    }

    /// <summary>Restore HP (clamped to max). Returns actual HP restored.</summary>
    public int Heal(int amount)
    {
        var actual = Math.Min(Math.Max(0, amount), MaxHp - CurrentHpRaw);
        CurrentHpRaw += actual;
        return actual;
    }

    /// <summary>Restore MP (clamped). Returns actual MP restored.</summary>
    public int RestoreMp(int amount)
    {
        var actual = Math.Min(Math.Max(0, amount), MaxMp - CurrentMpRaw);
        CurrentMpRaw += actual;
        return actual;
    }

    /// <summary>Deduct MP if sufficient; returns false without change if not.</summary>
    public bool SpendMp(int amount)
    {
        if (CurrentMpRaw < amount)
            return false;
        CurrentMpRaw -= amount;
        return true;
    }

    public bool HasStatus(string effect) => StatusEffects.Contains(effect);

    public void AddStatus(string effect)
    {
        if (!StatusEffects.Contains(effect))
            StatusEffects.Add(effect);
    }

    public void RemoveStatus(string effect) => StatusEffects.RemoveAll(e => e == effect);

    /// <summary>Wholesale replace the status effect list — used when restoring from a save.</summary>
    public void RestoreStatusEffects(IEnumerable<string> effects) => StatusEffects = effects.ToList();

    /// <summary>Removes all debuffs, returns the count removed.</summary>
    public int ClearDebuffs()
    {
        var removed = StatusEffects.Count(e => Debuffs.Contains(e));
        StatusEffects = StatusEffects.Where(e => !Debuffs.Contains(e)).ToList();
        return removed;
    }

    /// <summary>Removes all buffs, returns the count removed.</summary>
    public int ClearBuffs()
    {
        var removed = StatusEffects.Count(e => Buffs.Contains(e));
        StatusEffects = StatusEffects.Where(e => !Buffs.Contains(e)).ToList();
        return removed;
    }

    /// <summary>Unique key for cooldown/status tracking. Overridden by Character (stable across
    /// saves) and Enemy (unique per spawned instance).</summary>
    public virtual string CombatantId() => $"{GetType().Name}_{Name}_{GetHashCode()}";

    public override string ToString() => $"{GetType().Name}({Name}, {CurrentHpRaw}/{MaxHp} HP)";
}
