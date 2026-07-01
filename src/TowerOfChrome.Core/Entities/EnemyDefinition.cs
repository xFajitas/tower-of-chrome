using System.Collections.Immutable;

namespace TowerOfChrome.Core.Entities;

/// <summary>Port of entities/enemy.py's EnemyDefinition (frozen dataclass).</summary>
public sealed record EnemyDefinition(
    string Id,
    string Name,
    string Description,
    string Tier,
    IReadOnlyDictionary<string, int> BaseStats,
    IReadOnlyDictionary<string, double> StatGrowth,
    ImmutableArray<string> Abilities,
    string AiBehavior,
    int XpReward,
    string LootTable);
