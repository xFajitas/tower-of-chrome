using DungeonOfChrome.Core.Dungeon;
using DungeonOfChrome.Core.Entities;
using DungeonOfChrome.Core.Persistence;
using DungeonOfChrome.Core.Rng;
using DungeonOfChrome.Core.Tests.TestUtil;

namespace DungeonOfChrome.Core.Tests.Persistence;

public class SaveLoadRoundtripTests : IDisposable
{
    private readonly string _tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dungeonofchrome_test_{Guid.NewGuid():N}.json");
    private static readonly string SampleFixturePath = System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", "sample_savegame.json");

    private static SaveLoadService NewService() =>
        new(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

    public void Dispose()
    {
        if (File.Exists(_tempPath))
            File.Delete(_tempPath);
    }

    [Fact]
    public void LoadGame_RealFixture_RestoresAllFields()
    {
        var service = NewService();

        var ok = service.LoadGame(SampleFixturePath, out var state);

        Assert.True(ok);
        Assert.NotNull(state);
        Assert.Equal(3, state!.CurrentFloor);
        Assert.Equal(45, state.EnemiesDefeated);

        var members = state.Party.AllMembers;
        Assert.Equal(4, members.Count);

        var gareth = members.Single(m => m.Name == "Sir Gareth");
        Assert.Equal("knight", gareth.ClassDef.Id);
        Assert.Equal(3, gareth.Level);
        Assert.Equal(30, gareth.CurrentXp);
        Assert.Equal(136, gareth.CurrentHp);
        Assert.Equal(43, gareth.CurrentMp);
        Assert.Equal("steel_sword", gareth.Equipment["weapon"]);
        Assert.Equal("iron_plate", gareth.Equipment["armor"]);
        Assert.Null(gareth.Equipment["accessory"]);
        Assert.Equal("defender_charm", gareth.Equipment["charm"]);
        Assert.Contains("antidote", gareth.Inventory.Bag);
        Assert.Contains("bloodied_claymore", gareth.Inventory.Bag);
        Assert.Contains("blessed", gareth.StatusEffects);

        Assert.NotNull(state.DungeonFloor);
        Assert.Equal(3, state.DungeonFloor!.FloorNumber);
        Assert.Equal(3, state.DungeonFloor.PlayerRoomId);
        Assert.Equal(6, state.DungeonFloor.Rooms.Count);
        Assert.Equal(RoomType.Treasure, state.DungeonFloor.Rooms[4].RoomType);
        Assert.Equal(new[] { "defender_charm" }, state.DungeonFloor.Rooms[4].Loot);
        Assert.True(state.DungeonFloor.Rooms[1].Cleared);
        Assert.False(state.DungeonFloor.Rooms[5].Visited);
        Assert.Contains((0, 2), state.DungeonFloor.Corridors);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_ToSemanticaEquivalentState()
    {
        var service = NewService();
        service.LoadGame(SampleFixturePath, out var original);
        Assert.NotNull(original);

        var saved = service.SaveGame(original!, _tempPath);
        Assert.True(saved);

        var loaded = service.LoadGame(_tempPath, out var roundTripped);
        Assert.True(loaded);
        Assert.NotNull(roundTripped);

        Assert.Equal(original!.CurrentFloor, roundTripped!.CurrentFloor);
        Assert.Equal(original.EnemiesDefeated, roundTripped.EnemiesDefeated);

        var originalMembers = original.Party.AllMembers;
        var roundTrippedMembers = roundTripped.Party.AllMembers;
        Assert.Equal(originalMembers.Count, roundTrippedMembers.Count);
        for (var i = 0; i < originalMembers.Count; i++)
        {
            Assert.Equal(originalMembers[i].Name, roundTrippedMembers[i].Name);
            Assert.Equal(originalMembers[i].ClassDef.Id, roundTrippedMembers[i].ClassDef.Id);
            Assert.Equal(originalMembers[i].Level, roundTrippedMembers[i].Level);
            Assert.Equal(originalMembers[i].CurrentHp, roundTrippedMembers[i].CurrentHp);
            Assert.Equal(originalMembers[i].Inventory.Bag, roundTrippedMembers[i].Inventory.Bag);
            Assert.Equal(originalMembers[i].StatusEffects, roundTrippedMembers[i].StatusEffects);
        }

        Assert.Equal(original.DungeonFloor!.Rooms.Count, roundTripped.DungeonFloor!.Rooms.Count);
        foreach (var id in original.DungeonFloor.Rooms.Keys)
        {
            Assert.Equal(original.DungeonFloor.Rooms[id].RoomType, roundTripped.DungeonFloor.Rooms[id].RoomType);
            Assert.Equal(original.DungeonFloor.Rooms[id].Connections, roundTripped.DungeonFloor.Rooms[id].Connections);
        }
    }

    [Fact]
    public void SaveExists_ReflectsFileSystemState()
    {
        var service = NewService();
        Assert.False(service.SaveExists(_tempPath));

        service.SaveGame(MakeMinimalState(), _tempPath);
        Assert.True(service.SaveExists(_tempPath));
    }

    [Fact]
    public void GetSaveMetadata_ReturnsFloor_WhenSaveExists()
    {
        var service = NewService();
        service.SaveGame(MakeMinimalState(currentFloor: 7), _tempPath);

        var meta = service.GetSaveMetadata(_tempPath);

        Assert.NotNull(meta);
        Assert.Equal(7, meta!.Floor);
    }

    [Fact]
    public void GetSaveMetadata_ReturnsNull_WhenNoSaveExists()
    {
        var service = NewService();
        Assert.Null(service.GetSaveMetadata(_tempPath));
    }

    [Fact]
    public void DeleteSave_RemovesFile_AndIsSafeToCallWhenAlreadyMissing()
    {
        var service = NewService();
        service.SaveGame(MakeMinimalState(), _tempPath);
        Assert.True(service.SaveExists(_tempPath));

        service.DeleteSave(_tempPath);
        Assert.False(service.SaveExists(_tempPath));

        service.DeleteSave(_tempPath); // should not throw
    }

    [Fact]
    public void SaveGame_WithNullDungeonFloor_RoundTripsAsNull()
    {
        var service = NewService();
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());
        var state = new GameSessionState(1, 0, party, dungeonFloor: null);

        service.SaveGame(state, _tempPath);
        service.LoadGame(_tempPath, out var loaded);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.DungeonFloor);
    }

    [Fact]
    public void SaveGame_PartyWithEmptySlots_RoundTripsNullsCorrectly()
    {
        var service = NewService();
        var party = new Party();
        party.AddMember(new Character("Solo", TestGameData.NewClassRegistry().Get("knight"),
            TestGameData.NewItemRegistry(), TestGameData.NewLeveling()), slot: 0);
        var state = new GameSessionState(1, 0, party, null);

        service.SaveGame(state, _tempPath);
        service.LoadGame(_tempPath, out var loaded);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Party.AllMembers);
        Assert.NotNull(loaded.Party.GetMember(0));
        Assert.Null(loaded.Party.GetMember(1));
        Assert.Null(loaded.Party.GetMember(2));
        Assert.Null(loaded.Party.GetMember(3));
    }

    private static GameSessionState MakeMinimalState(int currentFloor = 1, DungeonOfChrome.Core.Dungeon.DungeonFloor? dungeonFloor = null)
    {
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());
        dungeonFloor ??= DungeonGenerator.GenerateFloor(currentFloor, new SystemRandomSource(seed: 1));
        return new GameSessionState(currentFloor, 0, party, dungeonFloor);
    }
}
