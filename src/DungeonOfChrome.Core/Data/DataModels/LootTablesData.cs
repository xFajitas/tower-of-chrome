using System.Text.Json.Serialization;

namespace DungeonOfChrome.Core.Data.DataModels;

/// <summary>
/// One entry from loot_tables.json. The top-level JSON has no wrapping "tables" object —
/// table names are top-level keys alongside a "_description" comment key — so this type is
/// deserialized per-entry by GameDataProvider, not as a whole-file POCO.
/// </summary>
public sealed class LootTableData
{
    [JsonPropertyName("drop_chance")] public double DropChance { get; set; }
    [JsonPropertyName("rolls")] public int Rolls { get; set; }
    [JsonPropertyName("item_rarities")] public List<string> ItemRarities { get; set; } = new();
    [JsonPropertyName("rarity_weights")] public List<int> RarityWeights { get; set; } = new();
    [JsonPropertyName("categories")] public List<string> Categories { get; set; } = new();
    [JsonPropertyName("category_weights")] public List<int> CategoryWeights { get; set; } = new();
}
