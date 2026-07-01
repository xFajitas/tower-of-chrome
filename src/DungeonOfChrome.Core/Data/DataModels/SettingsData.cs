using System.Text.Json.Serialization;

namespace DungeonOfChrome.Core.Data.DataModels;

public sealed class SettingsData
{
    [JsonPropertyName("window")] public WindowSettingsData Window { get; set; } = new();
    [JsonPropertyName("fps")] public int Fps { get; set; } = 60;
    [JsonPropertyName("debug")] public bool Debug { get; set; }
}

public sealed class WindowSettingsData
{
    [JsonPropertyName("width")] public int Width { get; set; } = 1280;
    [JsonPropertyName("height")] public int Height { get; set; } = 720;
    [JsonPropertyName("title")] public string Title { get; set; } = "Dungeon of Chrome";
}
