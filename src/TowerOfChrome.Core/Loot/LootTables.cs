using TowerOfChrome.Core.Data;
using TowerOfChrome.Core.Data.DataModels;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Rng;

namespace TowerOfChrome.Core.Loot;

/// <summary>Post-combat loot generation. Port of loot/drops.py. Constructor-injected
/// (ItemRegistry + loot table data + IRandomSource), not Python-style module globals.</summary>
public sealed class LootTables
{
    private readonly IReadOnlyDictionary<string, LootTableData> _tables;
    private readonly ItemRegistry _items;
    private readonly IRandomSource _rng;

    public LootTables(IGameDataSource dataSource, ItemRegistry items, IRandomSource rng)
    {
        _tables = dataSource.LoadLootTables();
        _items = items;
        _rng = rng;
    }

    /// <summary>Roll a loot table and return a list of item IDs to award. An empty list means
    /// nothing dropped (either the table doesn't exist, or the drop-chance roll failed).</summary>
    public List<string> GenerateDrops(string lootTableId, int floor)
    {
        if (!_tables.TryGetValue(lootTableId, out var table))
            return new List<string>();

        // Higher roll FAILS the drop check — success needs roll <= dropChance. Easy to invert
        // by mistake; ported verbatim from Python's `if random.random() > drop_chance: return []`.
        if (_rng.NextDouble() > table.DropChance)
            return new List<string>();

        var drops = new List<string>();
        for (var i = 0; i < table.Rolls; i++)
        {
            var chosenRarity = WeightedChoice(table.ItemRarities, table.RarityWeights);
            var chosenCategory = WeightedChoice(table.Categories, table.CategoryWeights);

            var pool = _items.ByRarityAndCategory(RarityExtensions.FromString(chosenRarity), chosenCategory);
            if (pool.Count > 0)
                drops.Add(pool[_rng.NextInt(0, pool.Count)].Id);
            // Silent skip if the pool is empty for that rarity/category combo — a real gap in
            // the source data, preserved rather than "fixed" by falling back to a nearby rarity.
        }
        return drops;
    }

    /// <summary>Distribute `drops` to party members, preferring whoever has the most free bag
    /// space. Returns human-readable log lines.</summary>
    public List<string> AwardDrops(Party party, IReadOnlyList<string> drops)
    {
        var log = new List<string>();
        if (drops.Count == 0)
            return log;

        foreach (var itemId in drops)
        {
            var candidates = party.AllMembers
                .Where(m => !m.Inventory.BagFull)
                .Select(m => (Member: m, FreeSpace: Inventory.MaxBagSize - m.Inventory.BagSize))
                .OrderByDescending(x => x.FreeSpace)
                .ToList();

            if (candidates.Count == 0)
            {
                var name = TryGetItemName(itemId);
                log.Add(name != null ? $"  {name} — lost (bags full)!" : $"  Unknown item {itemId} — lost.");
                continue;
            }

            var target = candidates[0].Member;
            if (target.Inventory.Add(itemId))
            {
                var item = TryGetItem(itemId);
                log.Add(item != null
                    ? $"  {target.Name} found: {item.Name} [{item.Rarity}]!"
                    : $"  {target.Name} found: {itemId}!");
            }
        }

        return log;
    }

    private ItemDefinition? TryGetItem(string itemId)
    {
        try { return _items.Get(itemId); }
        catch (KeyNotFoundException) { return null; }
    }

    private string? TryGetItemName(string itemId) => TryGetItem(itemId)?.Name;

    /// <summary>Weighted-random pick, analogous to Python's random.choices(items, weights, k=1)[0].</summary>
    private string WeightedChoice(IReadOnlyList<string> items, IReadOnlyList<int> weights)
    {
        var total = weights.Sum();
        var roll = _rng.NextDouble() * total;
        double cumulative = 0;
        for (var i = 0; i < items.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return items[i];
        }
        return items[^1]; // floating-point edge case fallback
    }
}
