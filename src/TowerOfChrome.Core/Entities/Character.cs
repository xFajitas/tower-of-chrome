using TowerOfChrome.Core.Loot;

namespace TowerOfChrome.Core.Entities;

/// <summary>A player party member. Port of entities/character.py.</summary>
public sealed class Character : Combatant
{
    public ClassDefinition ClassDef { get; }
    public int Level { get; private set; }
    public int CurrentXp { get; private set; }
    public Dictionary<string, string?> Equipment { get; internal set; }
    public Inventory Inventory { get; internal set; }

    private readonly Leveling _leveling;

    /// <summary>Invoked with a log line whenever the character levels up (mirrors Python's print()).</summary>
    public Action<string>? OnLevelUp { get; set; }

    public Character(
        string name,
        ClassDefinition classDef,
        ItemRegistry itemRegistry,
        Leveling leveling,
        int level = 1,
        int currentXp = 0,
        int? currentHp = null,
        int? currentMp = null)
        : base(name)
    {
        ClassDef = classDef;
        _leveling = leveling;
        Level = Math.Max(1, Math.Min(level, leveling.MaxLevel));
        CurrentXp = currentXp;

        Equipment = classDef.EquipmentSlots.ToDictionary(slot => slot, _ => (string?)null);

        // Inventory must exist before CurrentHpRaw/CurrentMpRaw are set so MaxHp/MaxMp
        // (which include equipment bonuses) are already computable.
        Inventory = new Inventory(this, itemRegistry);

        CurrentHpRaw = currentHp ?? MaxHp;
        CurrentMpRaw = currentMp ?? MaxMp;
    }

    public override int MaxHp => Math.Max(1, ClassDef.StatAtLevel("hp", Level) + Inventory.EquippedStatBonus("hp"));
    public override int MaxMp => Math.Max(0, ClassDef.StatAtLevel("mp", Level) + Inventory.EquippedStatBonus("mp"));

    public override int GetStat(string stat) =>
        Math.Max(0, ClassDef.StatAtLevel(stat, Level) + Inventory.EquippedStatBonus(stat));

    public IReadOnlyDictionary<string, int> Stats => StatKeys.All.ToDictionary(s => s, GetStat);

    /// <summary>Stable across saves/loads — used as the cooldown/status tracker key.</summary>
    public override string CombatantId() => $"char_{Name}";

    public int XpToNextLevel => Level >= _leveling.MaxLevel ? 0 : _leveling.XpToNext(Level);

    public double XpProgressFraction
    {
        get
        {
            var needed = XpToNextLevel;
            return needed > 0 ? (double)CurrentXp / needed : 1.0;
        }
    }

    /// <summary>Add XP; returns true if the character leveled up at least once. Loops through
    /// multiple level-ups if XP overflows a single threshold.</summary>
    public bool GainXp(int amount)
    {
        if (Level >= _leveling.MaxLevel)
            return false;

        CurrentXp += amount;
        var leveled = false;
        while (Level < _leveling.MaxLevel)
        {
            var needed = _leveling.XpToNext(Level);
            if (CurrentXp >= needed)
            {
                CurrentXp -= needed;
                LevelUp();
                leveled = true;
            }
            else
            {
                break;
            }
        }
        return leveled;
    }

    private void LevelUp()
    {
        var oldMaxHp = MaxHp;
        var oldMaxMp = MaxMp;
        Level += 1;
        CurrentHpRaw = Math.Min(CurrentHpRaw + (MaxHp - oldMaxHp), MaxHp);
        CurrentMpRaw = Math.Min(CurrentMpRaw + (MaxMp - oldMaxMp), MaxMp);
        OnLevelUp?.Invoke($"[Leveling] {Name} -> level {Level}!");
    }

    // NOTE: to_dict()/from_dict() equivalents live in Persistence/SaveLoadService.cs rather
    // than on Character itself, since Character now needs ItemRegistry/ClassRegistry/Leveling
    // dependencies injected to reconstruct — the save/load layer already holds those.
}
