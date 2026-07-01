using System.Text.Json;
using DungeonOfChrome.Core.Data.DataModels;

namespace DungeonOfChrome.Core.Data;

/// <summary>Loads the 7 JSON data files from a directory on disk. Used directly by Core tests
/// today, and will remain usable standalone (e.g. modding/dev tools) once Unity exists.</summary>
public sealed class FileSystemGameDataSource : IGameDataSource
{
    private readonly string _dataRoot;
    private static readonly JsonSerializerOptions Options = new() { ReadCommentHandling = JsonCommentHandling.Skip };

    public FileSystemGameDataSource(string dataRootPath) => _dataRoot = dataRootPath;

    private string Path(string fileName) => System.IO.Path.Combine(_dataRoot, fileName);

    private T LoadFile<T>(string fileName) where T : new()
    {
        var json = File.ReadAllText(Path(fileName));
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidDataException($"'{fileName}' deserialized to null.");
    }

    public ClassesFile LoadClasses() => LoadFile<ClassesFile>("classes.json");
    public ItemsFile LoadItems() => LoadFile<ItemsFile>("items.json");
    public EnemiesFile LoadEnemies() => LoadFile<EnemiesFile>("enemies.json");
    public AbilitiesFile LoadAbilities() => LoadFile<AbilitiesFile>("abilities.json");
    public CombatConfigData LoadCombatConfig() => LoadFile<CombatConfigData>("combat_config.json");
    public LevelingData LoadLeveling() => LoadFile<LevelingData>("leveling.json");
    public SettingsData LoadSettings() => LoadFile<SettingsData>("settings.json");

    /// <summary>
    /// loot_tables.json has no wrapping object — table names are top-level keys alongside
    /// a "_description" comment key, e.g. {"_description": "...", "common_floor1": {...}, ...}.
    /// Parse as a raw document and skip any key starting with "_".
    /// </summary>
    public IReadOnlyDictionary<string, LootTableData> LoadLootTables()
    {
        var json = File.ReadAllText(Path("loot_tables.json"));
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

        var result = new Dictionary<string, LootTableData>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name.StartsWith("_"))
                continue;

            var table = JsonSerializer.Deserialize<LootTableData>(prop.Value.GetRawText(), Options)
                ?? throw new InvalidDataException($"Loot table '{prop.Name}' deserialized to null.");
            result[prop.Name] = table;
        }
        return result;
    }
}
