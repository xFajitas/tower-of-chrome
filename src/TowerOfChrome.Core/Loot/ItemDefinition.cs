namespace TowerOfChrome.Core.Loot;

public sealed record ItemEffect(string Type, int? Value, string? Status);

public sealed record ItemDefinition(
    string Id,
    string Name,
    string Description,
    string Category,
    string? Slot,
    Rarity Rarity,
    string? WeaponType,
    string? ArmorType,
    IReadOnlyDictionary<string, int> StatBonuses,
    bool Consumable,
    ItemEffect? Effect,
    int Value);
