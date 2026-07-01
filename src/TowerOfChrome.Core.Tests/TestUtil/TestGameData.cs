using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Data;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Loot;

namespace TowerOfChrome.Core.Tests.TestUtil;

/// <summary>Shared fixture registries loaded once from the real TestData/*.json files.</summary>
public static class TestGameData
{
    private static readonly string TestDataDir = System.IO.Path.Combine(AppContext.BaseDirectory, "TestData");

    public static IGameDataSource NewDataSource() => new FileSystemGameDataSource(TestDataDir);

    public static ClassRegistry NewClassRegistry() => new(NewDataSource());
    public static ItemRegistry NewItemRegistry() => new(NewDataSource());
    public static EnemyRegistry NewEnemyRegistry() => new(NewDataSource());
    public static AbilityRegistry NewAbilityRegistry() => new(NewDataSource());
    public static Leveling NewLeveling() => new(NewDataSource());
    public static TowerOfChrome.Core.Data.DataModels.CombatConfigData NewCombatConfig() => NewDataSource().LoadCombatConfig();
    public static LootTables NewLootTables(TowerOfChrome.Core.Rng.IRandomSource rng) => new(NewDataSource(), NewItemRegistry(), rng);
}
