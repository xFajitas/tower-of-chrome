using System.Text.Json.Serialization;

namespace TowerOfChrome.Core.Data.DataModels;

public sealed class ItemsFile
{
    [JsonPropertyName("items")]
    public List<ItemDefinitionData> Items { get; set; } = new();
}

public sealed class ItemDefinitionData
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("slot")] public string? Slot { get; set; }
    [JsonPropertyName("rarity")] public string Rarity { get; set; } = "";
    [JsonPropertyName("weapon_type")] public string? WeaponType { get; set; }
    [JsonPropertyName("armor_type")] public string? ArmorType { get; set; }
    [JsonPropertyName("stat_bonuses")] public Dictionary<string, int> StatBonuses { get; set; } = new();
    [JsonPropertyName("consumable")] public bool Consumable { get; set; }
    [JsonPropertyName("effect")] public ItemEffectData? Effect { get; set; }
    [JsonPropertyName("value")] public int Value { get; set; }
}

public sealed class ItemEffectData
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("value")] public int? Value { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
}
