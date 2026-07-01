using System.Text.Json.Serialization;

namespace TowerOfChrome.Core.Persistence;

/// <summary>DTOs matching save/savegame.json's exact on-disk shape, verified against a real
/// sample save file. Port of the dicts built/read by core/save_load.py.</summary>
public sealed class SaveGameDto
{
    [JsonPropertyName("version")] public string Version { get; set; } = "1.0";
    [JsonPropertyName("current_floor")] public int CurrentFloor { get; set; }
    [JsonPropertyName("enemies_defeated")] public int EnemiesDefeated { get; set; }
    [JsonPropertyName("party")] public PartyDto Party { get; set; } = new();
    [JsonPropertyName("dungeon_floor")] public DungeonFloorDto? DungeonFloor { get; set; }
}

public sealed class PartyDto
{
    [JsonPropertyName("slots")] public List<CharacterDto?> Slots { get; set; } = new();
}

public sealed class CharacterDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("class_id")] public string ClassId { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("current_xp")] public int CurrentXp { get; set; }
    [JsonPropertyName("current_hp")] public int CurrentHp { get; set; }
    [JsonPropertyName("current_mp")] public int CurrentMp { get; set; }
    [JsonPropertyName("equipment")] public EquipmentDto Equipment { get; set; } = new();
    [JsonPropertyName("inventory")] public InventoryDto Inventory { get; set; } = new();
    [JsonPropertyName("status_effects")] public List<string> StatusEffects { get; set; } = new();
}

public sealed class EquipmentDto
{
    [JsonPropertyName("weapon")] public string? Weapon { get; set; }
    [JsonPropertyName("armor")] public string? Armor { get; set; }
    [JsonPropertyName("accessory")] public string? Accessory { get; set; }
    [JsonPropertyName("charm")] public string? Charm { get; set; }
}

public sealed class InventoryDto
{
    [JsonPropertyName("bag")] public List<string> Bag { get; set; } = new();
}

public sealed class DungeonFloorDto
{
    [JsonPropertyName("floor_number")] public int FloorNumber { get; set; }
    [JsonPropertyName("player_room_id")] public int PlayerRoomId { get; set; }
    [JsonPropertyName("corridors")] public List<int[]> Corridors { get; set; } = new();
    [JsonPropertyName("rooms")] public List<RoomDto> Rooms { get; set; } = new();
}

public sealed class RoomDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }
    [JsonPropertyName("room_type")] public string RoomType { get; set; } = "normal";
    [JsonPropertyName("connections")] public List<int> Connections { get; set; } = new();
    [JsonPropertyName("cleared")] public bool Cleared { get; set; }
    [JsonPropertyName("visited")] public bool Visited { get; set; }
    [JsonPropertyName("loot")] public List<string> Loot { get; set; } = new();
}
