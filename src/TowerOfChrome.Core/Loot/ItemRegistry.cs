using TowerOfChrome.Core.Data;

namespace TowerOfChrome.Core.Loot;

/// <summary>Port of loot/item.py's ItemRegistry. Constructor-injected (not a Python-style
/// global singleton) so tests get a fresh instance from fixture data.</summary>
public sealed class ItemRegistry
{
    private readonly Dictionary<string, ItemDefinition> _items = new();

    public ItemRegistry(IGameDataSource dataSource)
    {
        foreach (var raw in dataSource.LoadItems().Items)
        {
            _items[raw.Id] = new ItemDefinition(
                Id: raw.Id,
                Name: raw.Name,
                Description: raw.Description,
                Category: raw.Category,
                Slot: raw.Slot,
                Rarity: RarityExtensions.FromString(raw.Rarity),
                WeaponType: raw.WeaponType,
                ArmorType: raw.ArmorType,
                StatBonuses: raw.StatBonuses,
                Consumable: raw.Consumable,
                Effect: raw.Effect is null ? null : new ItemEffect(raw.Effect.Type, raw.Effect.Value, raw.Effect.Status),
                Value: raw.Value);
        }
    }

    public ItemDefinition Get(string itemId) =>
        _items.TryGetValue(itemId, out var item) ? item : throw new KeyNotFoundException($"ItemRegistry: unknown item '{itemId}'");

    public IReadOnlyCollection<ItemDefinition> All() => _items.Values;

    public IReadOnlyList<ItemDefinition> ByRarityAndCategory(Rarity rarity, string category) =>
        _items.Values.Where(i => i.Rarity == rarity && i.Category == category).ToList();
}
