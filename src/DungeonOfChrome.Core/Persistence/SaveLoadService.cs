using System.Text.Json;
using DungeonOfChrome.Core.Dungeon;
using DungeonOfChrome.Core.Entities;
using DungeonOfChrome.Core.Loot;

namespace DungeonOfChrome.Core.Persistence;

/// <summary>
/// Serialize/restore full game state to/from a savegame.json path. Port of core/save_load.py.
/// Constructor-injected registries (not Python-style module-level functions) since
/// reconstructing Characters requires ClassRegistry/ItemRegistry/Leveling.
/// </summary>
public sealed class SaveLoadService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly ClassRegistry _classes;
    private readonly ItemRegistry _items;
    private readonly Leveling _leveling;

    public SaveLoadService(ClassRegistry classes, ItemRegistry items, Leveling leveling)
    {
        _classes = classes;
        _items = items;
        _leveling = leveling;
    }

    public bool SaveExists(string path) => File.Exists(path);

    public SaveMetadata? GetSaveMetadata(string path)
    {
        if (!SaveExists(path))
            return null;
        try
        {
            var dto = JsonSerializer.Deserialize<SaveGameDto>(File.ReadAllText(path), Options);
            return dto == null ? null : new SaveMetadata(dto.CurrentFloor);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool SaveGame(GameSessionState state, string path)
    {
        try
        {
            var dto = new SaveGameDto
            {
                Version = "1.0",
                CurrentFloor = state.CurrentFloor,
                EnemiesDefeated = state.EnemiesDefeated,
                Party = ToPartyDto(state.Party),
                DungeonFloor = state.DungeonFloor == null ? null : ToDungeonFloorDto(state.DungeonFloor),
            };

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, JsonSerializer.Serialize(dto, Options));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Restores party/floor/counters. Mid-combat state is never persisted — the caller
    /// should always resume in Explore after a successful load, exactly like the Python engine.</summary>
    public bool LoadGame(string path, out GameSessionState? state)
    {
        state = null;
        try
        {
            var dto = JsonSerializer.Deserialize<SaveGameDto>(File.ReadAllText(path), Options);
            if (dto == null)
                return false;

            var party = FromPartyDto(dto.Party);
            var dungeonFloor = dto.DungeonFloor == null ? null : FromDungeonFloorDto(dto.DungeonFloor);

            state = new GameSessionState(dto.CurrentFloor, dto.EnemiesDefeated, party, dungeonFloor);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void DeleteSave(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // Mirrors Python's `except FileNotFoundError: pass`.
        }
    }

    // ------------------------------------------------------------------
    // Domain <-> DTO conversion
    // ------------------------------------------------------------------

    private static PartyDto ToPartyDto(Party party)
    {
        var dto = new PartyDto();
        foreach (var member in party.Members)
            dto.Slots.Add(member == null ? null : ToCharacterDto(member));
        return dto;
    }

    private static CharacterDto ToCharacterDto(Character c) => new()
    {
        Name = c.Name,
        ClassId = c.ClassDef.Id,
        Level = c.Level,
        CurrentXp = c.CurrentXp,
        CurrentHp = c.CurrentHp,
        CurrentMp = c.CurrentMp,
        Equipment = new EquipmentDto
        {
            Weapon = c.Equipment.GetValueOrDefault("weapon"),
            Armor = c.Equipment.GetValueOrDefault("armor"),
            Accessory = c.Equipment.GetValueOrDefault("accessory"),
            Charm = c.Equipment.GetValueOrDefault("charm"),
        },
        Inventory = new InventoryDto { Bag = c.Inventory.Bag.ToList() },
        StatusEffects = c.StatusEffects.ToList(),
    };

    private Party FromPartyDto(PartyDto dto)
    {
        var party = new Party();
        for (var i = 0; i < dto.Slots.Count; i++)
        {
            var slot = dto.Slots[i];
            if (slot != null)
                party.SetSlot(i, FromCharacterDto(slot));
        }
        return party;
    }

    private Character FromCharacterDto(CharacterDto dto)
    {
        var classDef = _classes.Get(dto.ClassId);
        var character = new Character(
            dto.Name, classDef, _items, _leveling,
            dto.Level, dto.CurrentXp, dto.CurrentHp, dto.CurrentMp);

        character.Equipment["weapon"] = dto.Equipment.Weapon;
        character.Equipment["armor"] = dto.Equipment.Armor;
        character.Equipment["accessory"] = dto.Equipment.Accessory;
        character.Equipment["charm"] = dto.Equipment.Charm;

        character.Inventory.RestoreBag(dto.Inventory.Bag);
        character.RestoreStatusEffects(dto.StatusEffects);

        return character;
    }

    private static DungeonFloorDto ToDungeonFloorDto(DungeonFloor floor) => new()
    {
        FloorNumber = floor.FloorNumber,
        PlayerRoomId = floor.PlayerRoomId,
        Corridors = floor.Corridors.Select(c => new[] { c.A, c.B }).ToList(),
        Rooms = floor.Rooms.Values.Select(r => new RoomDto
        {
            Id = r.Id,
            X = r.X,
            Y = r.Y,
            W = r.W,
            H = r.H,
            RoomType = r.RoomType.ToSaveString(),
            Connections = r.Connections.ToList(),
            Cleared = r.Cleared,
            Visited = r.Visited,
            Loot = r.Loot.ToList(),
        }).ToList(),
    };

    private static DungeonFloor FromDungeonFloorDto(DungeonFloorDto dto)
    {
        var rooms = dto.Rooms.Select(rd =>
        {
            var room = new Room(rd.Id, rd.X, rd.Y, rd.W, rd.H)
            {
                RoomType = RoomTypeExtensions.RoomTypeFromSaveString(rd.RoomType),
                Cleared = rd.Cleared,
                Visited = rd.Visited,
                Loot = rd.Loot.ToList(),
            };
            room.Connections.AddRange(rd.Connections);
            return room;
        }).ToList();

        var corridors = dto.Corridors.Select(c => (c[0], c[1]));
        return new DungeonFloor(dto.FloorNumber, rooms, corridors, dto.PlayerRoomId);
    }
}
