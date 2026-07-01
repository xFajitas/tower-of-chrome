using System.Text.Json.Serialization;

namespace DungeonOfChrome.Core.Data.DataModels;

public sealed class EnemiesFile
{
    [JsonPropertyName("enemies")]
    public List<EnemyDefinitionData> Enemies { get; set; } = new();
}

public sealed class EnemyDefinitionData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("tier")] public string Tier { get; set; } = "";
    [JsonPropertyName("base_stats")] public Dictionary<string, int> BaseStats { get; set; } = new();
    [JsonPropertyName("stat_growth")] public Dictionary<string, double> StatGrowth { get; set; } = new();
    [JsonPropertyName("abilities")] public List<string> Abilities { get; set; } = new();
    [JsonPropertyName("ai_behavior")] public string AiBehavior { get; set; } = "";
    [JsonPropertyName("xp_reward")] public int XpReward { get; set; }
    [JsonPropertyName("loot_table")] public string LootTable { get; set; } = "";
}
