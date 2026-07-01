using System.Collections.Immutable;
using DungeonOfChrome.Core.Data;
using DungeonOfChrome.Core.Rng;

namespace DungeonOfChrome.Core.Entities;

/// <summary>Port of entities/enemy.py's EnemyRegistry. Constructor-injected, not a Python-style
/// global singleton, so tests get a fresh instance from fixture data.</summary>
public sealed class EnemyRegistry
{
    // Mirrors the literal lists hard-coded in Python's random_encounter().
    private static readonly string[] Commons = { "neon_grunt", "chrome_hound", "glitch_witch", "wire_wraith", "nanobot_swarm", "pulse_drone" };
    private static readonly string[] Elites = { "neon_berserker", "steel_samurai", "grid_specter", "data_wraith" };

    private readonly Dictionary<string, EnemyDefinition> _defs = new();

    public EnemyRegistry(IGameDataSource dataSource)
    {
        foreach (var raw in dataSource.LoadEnemies().Enemies)
        {
            _defs[raw.Id] = new EnemyDefinition(
                Id: raw.Id,
                Name: raw.Name,
                Description: raw.Description,
                Tier: raw.Tier,
                BaseStats: raw.BaseStats,
                StatGrowth: raw.StatGrowth,
                Abilities: raw.Abilities.ToImmutableArray(),
                AiBehavior: raw.AiBehavior,
                XpReward: raw.XpReward,
                LootTable: raw.LootTable);
        }
    }

    public EnemyDefinition Get(string enemyId) =>
        _defs.TryGetValue(enemyId, out var def) ? def : throw new KeyNotFoundException($"Unknown enemy: '{enemyId}'");

    public Enemy Spawn(string enemyId, int floor = 1) => new(Get(enemyId), floor);

    /// <summary>
    /// Return a random group of enemies appropriate for the given floor.
    /// Floors 1-4: commons only. Floor 5: mini-boss solo. Floor &gt;=10: boss solo.
    /// Note: the elites[:2] + elites duplication when floor&gt;=6 is intentional — it mirrors
    /// Python's literal pool-building logic, which biases random.choices toward the first two
    /// elite IDs. Preserved verbatim rather than "fixed" to keep encounter-odds parity.
    /// </summary>
    public List<Enemy> RandomEncounter(int floor, int count, IRandomSource rng)
    {
        if (floor >= 10)
            return new List<Enemy> { Spawn("circuit_mage", floor) };
        if (floor == 5)
            return new List<Enemy> { Spawn("nexus_core", floor) };

        var pool = new List<string>(Commons);
        if (floor >= 3)
            pool.AddRange(Elites.Take(2));
        if (floor >= 6)
            pool.AddRange(Elites);

        var k = Math.Min(count, 4);
        var chosen = new List<string>(k);
        for (var i = 0; i < k; i++)
            chosen.Add(pool[rng.NextInt(0, pool.Count)]);

        return chosen.Select(id => Spawn(id, floor)).ToList();
    }
}
