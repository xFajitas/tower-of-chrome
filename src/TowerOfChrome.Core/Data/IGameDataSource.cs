using TowerOfChrome.Core.Data.DataModels;

namespace TowerOfChrome.Core.Data;

/// <summary>
/// Abstracts *where* the 7 JSON data files come from. Core only ever depends on this
/// interface; a Unity-side adapter (reading Application.streamingAssetsPath) implements
/// the same interface in Phase 2 without Core needing to know Unity exists.
/// </summary>
public interface IGameDataSource
{
    ClassesFile LoadClasses();
    ItemsFile LoadItems();
    EnemiesFile LoadEnemies();
    AbilitiesFile LoadAbilities();

    /// <summary>Keyed by table id (e.g. "common_floor1") — the JSON has no wrapping object.</summary>
    IReadOnlyDictionary<string, LootTableData> LoadLootTables();

    CombatConfigData LoadCombatConfig();
    LevelingData LoadLeveling();
    SettingsData LoadSettings();
}
