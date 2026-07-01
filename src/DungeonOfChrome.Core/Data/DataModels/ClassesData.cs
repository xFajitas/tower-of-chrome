using System.Text.Json.Serialization;

namespace DungeonOfChrome.Core.Data.DataModels;

public sealed class ClassesFile
{
    [JsonPropertyName("classes")]
    public List<ClassDefinitionData> Classes { get; set; } = new();
}

public sealed class ClassDefinitionData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("resource")] public string Resource { get; set; } = "";
    [JsonPropertyName("resource_name")] public string ResourceName { get; set; } = "";
    [JsonPropertyName("base_stats")] public Dictionary<string, int> BaseStats { get; set; } = new();
    [JsonPropertyName("stat_growth")] public Dictionary<string, double> StatGrowth { get; set; } = new();
    [JsonPropertyName("weapon_types")] public List<string> WeaponTypes { get; set; } = new();
    [JsonPropertyName("armor_types")] public List<string> ArmorTypes { get; set; } = new();
    [JsonPropertyName("equipment_slots")] public List<string> EquipmentSlots { get; set; } = new();
    [JsonPropertyName("abilities")] public List<string> Abilities { get; set; } = new();
    [JsonPropertyName("starting_weapon")] public string StartingWeapon { get; set; } = "";
}
