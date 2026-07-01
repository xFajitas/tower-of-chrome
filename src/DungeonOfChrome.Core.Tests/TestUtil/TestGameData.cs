using DungeonOfChrome.Core.Combat;
using DungeonOfChrome.Core.Data;
using DungeonOfChrome.Core.Entities;
using DungeonOfChrome.Core.Loot;

namespace DungeonOfChrome.Core.Tests.TestUtil;

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
    public static DungeonOfChrome.Core.Data.DataModels.CombatConfigData NewCombatConfig() => NewDataSource().LoadCombatConfig();
    public static LootTables NewLootTables(DungeonOfChrome.Core.Rng.IRandomSource rng) => new(NewDataSource(), NewItemRegistry(), rng);
}
