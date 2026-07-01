using System.Collections.Immutable;

namespace DungeonOfChrome.Core.Entities;

/// <summary>Canonical stat order used everywhere stats are iterated or displayed.</summary>
public static class StatKeys
{
    public static readonly ImmutableArray<string> All = ImmutableArray.Create("hp", "mp", "str", "dex", "int", "vit", "spd", "luck");

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["hp"] = "HP",
        ["mp"] = "MP / Resource",
        ["str"] = "Strength",
        ["dex"] = "Dexterity",
        ["int"] = "Intelligence",
        ["vit"] = "Vitality",
        ["spd"] = "Speed",
        ["luck"] = "Luck",
    };
}

/// <summary>Immutable description of a playable class loaded from JSON. Port of ClassDefinition (frozen dataclass).</summary>
public sealed record ClassDefinition(
    string Id,
    string Name,
    string Description,
    string Role,
    string Resource,
    string ResourceName,
    IReadOnlyDictionary<string, int> BaseStats,
    IReadOnlyDictionary<string, double> StatGrowth,
    ImmutableArray<string> WeaponTypes,
    ImmutableArray<string> ArmorTypes,
    ImmutableArray<string> EquipmentSlots,
    ImmutableArray<string> Abilities,
    string StartingWeapon)
{
    /// <summary>Compute the integer value of `stat` at the given level. Truncates, does not compound.</summary>
    public int StatAtLevel(string stat, int level)
    {
        var baseVal = BaseStats.GetValueOrDefault(stat, 0);
        var growth = StatGrowth.GetValueOrDefault(stat, 0.0);
        return baseVal + (int)(growth * (level - 1));
    }

    public IReadOnlyDictionary<string, int> AllStatsAtLevel(int level) =>
        StatKeys.All.ToDictionary(k => k, k => StatAtLevel(k, level));
}
