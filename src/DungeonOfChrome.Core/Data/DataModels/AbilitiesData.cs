using System.Text.Json.Serialization;

namespace DungeonOfChrome.Core.Data.DataModels;

public sealed class AbilitiesFile
{
    /// <summary>Keyed by ability ID (the JSON "abilities" object is a dict, not a list).</summary>
    [JsonPropertyName("abilities")]
    public Dictionary<string, AbilityDefinitionData> Abilities { get; set; } = new();
}

public sealed class AbilityDefinitionData
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("targeting")] public string Targeting { get; set; } = "";
    [JsonPropertyName("mp_cost")] public int MpCost { get; set; }
    [JsonPropertyName("cooldown")] public int Cooldown { get; set; }
    [JsonPropertyName("power")] public double Power { get; set; }
    [JsonPropertyName("status_effects")] public List<string> StatusEffects { get; set; } = new();
    [JsonPropertyName("status_chance")] public int StatusChance { get; set; }
    [JsonPropertyName("flags")] public Dictionary<string, double> Flags { get; set; } = new();
}
