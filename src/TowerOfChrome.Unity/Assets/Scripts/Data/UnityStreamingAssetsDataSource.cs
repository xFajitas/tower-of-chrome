using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TowerOfChrome.Core.Data;
using TowerOfChrome.Core.Data.DataModels;
using UnityEngine;
using UnityEngine.Networking;

namespace TowerOfChrome.Unity.Data
{
    /// <summary>
    /// Loads the 7 JSON data files from Assets/StreamingAssets/data/ at runtime. This is the
    /// only Unity-aware code in the entire data pipeline — everything downstream (registries,
    /// combat formulas, dungeon generation) consumes the plain IGameDataSource interface and
    /// has no idea Unity exists.
    ///
    /// Application.streamingAssetsPath is a plain filesystem path on Windows Standalone and in
    /// the Editor, so System.IO.File works directly there. On Android, StreamingAssets live
    /// inside the compressed APK and aren't filesystem-accessible, so ReadText goes through
    /// UnityWebRequest instead, blocking on isDone. GameManager.Awake() constructs the Core
    /// registries synchronously, so this stays synchronous rather than becoming a coroutine —
    /// safe here because a local jar:/APK-asset read completes off a native callback, not a
    /// frame tick, so spin-waiting the main thread doesn't stall waiting on itself.
    /// </summary>
    public sealed class UnityStreamingAssetsDataSource : IGameDataSource
    {
        private static readonly JsonSerializerOptions Options = new() { ReadCommentHandling = JsonCommentHandling.Skip };

        private static string DataDir => Path.Combine(Application.streamingAssetsPath, "data");

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string ReadText(string fileName)
        {
            var path = Path.Combine(DataDir, fileName);
            using var request = UnityWebRequest.Get(path);
            var op = request.SendWebRequest();
            while (!op.isDone) { }

            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException($"Failed to load '{fileName}' from StreamingAssets: {request.error}");

            return request.downloadHandler.text;
        }
#else
        private static string ReadText(string fileName) => File.ReadAllText(Path.Combine(DataDir, fileName));
#endif

        private static T LoadFile<T>(string fileName) where T : new()
        {
            var json = ReadText(fileName);
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
        /// a "_description" comment key. Parse as a raw document and skip keys starting with "_".
        /// (Mirrors FileSystemGameDataSource's identical handling in Core.)
        /// </summary>
        public IReadOnlyDictionary<string, LootTableData> LoadLootTables()
        {
            var json = ReadText("loot_tables.json");
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
}
