using System.Text.Json.Serialization;

namespace DungeonOfChrome.Core.Data.DataModels;

public sealed class LevelingData
{
    [JsonPropertyName("max_level")] public int MaxLevel { get; set; }
    [JsonPropertyName("xp_base")] public int XpBase { get; set; }
    [JsonPropertyName("xp_exponent")] public double XpExponent { get; set; }
}
