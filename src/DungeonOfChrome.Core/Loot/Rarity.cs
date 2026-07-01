namespace DungeonOfChrome.Core.Loot;

public enum Rarity
{
    Common = 1,
    Uncommon = 2,
    Rare = 3,
    Epic = 4,
    Legendary = 5,
}

public static class RarityExtensions
{
    public static Rarity FromString(string name) =>
        (Rarity)Enum.Parse(typeof(Rarity), name, ignoreCase: true);

    /// <summary>Fallback reference constant, mirroring loot/rarity.py's RARITY_WEIGHTS — actual
    /// per-table weights come from loot_tables.json at runtime; this is documentation-only.</summary>
    public static readonly IReadOnlyDictionary<Rarity, int> FallbackWeights = new Dictionary<Rarity, int>
    {
        [Rarity.Common] = 100,
        [Rarity.Uncommon] = 40,
        [Rarity.Rare] = 15,
        [Rarity.Epic] = 4,
        [Rarity.Legendary] = 1,
    };

    public static readonly IReadOnlyDictionary<Rarity, string> Labels = new Dictionary<Rarity, string>
    {
        [Rarity.Common] = "Common",
        [Rarity.Uncommon] = "Uncommon",
        [Rarity.Rare] = "Rare",
        [Rarity.Epic] = "Epic",
        [Rarity.Legendary] = "Legendary",
    };
}
