using TowerOfChrome.Core.Dungeon;
using TowerOfChrome.Core.Rng;

namespace TowerOfChrome.Core.Tests.Dungeon;

public class DungeonGeneratorTests
{
    [Theory]
    [InlineData(1, 6)]
    [InlineData(3, 6)]
    [InlineData(4, 7)]
    [InlineData(6, 7)]
    [InlineData(7, 8)]
    [InlineData(25, 10)]  // min(10, 6 + 24/3) = min(10, 14) = 10
    [InlineData(100, 10)] // capped at 10
    public void RoomCount_MatchesFormula_CappedAtTen(int floorNumber, int expectedRooms)
    {
        var floor = DungeonGenerator.GenerateFloor(floorNumber, new SystemRandomSource(seed: 1));
        Assert.Equal(expectedRooms, floor.Rooms.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(42)]
    [InlineData(999)]
    public void AllRooms_AreReachableFromStartRoom_ViaConnectionGraph(int seed)
    {
        var floor = DungeonGenerator.GenerateFloor(3, new SystemRandomSource(seed: seed));

        // BFS from the start room over the connection graph — the MST guarantees full connectivity.
        var visited = new HashSet<int> { floor.PlayerRoomId };
        var queue = new Queue<int>();
        queue.Enqueue(floor.PlayerRoomId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in floor.Rooms[current].Connections)
            {
                if (visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        Assert.Equal(floor.Rooms.Count, visited.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void NonBossFloor_LastRoomIsStairs_NotBoss(int floorNumber)
    {
        var floor = DungeonGenerator.GenerateFloor(floorNumber, new SystemRandomSource(seed: 2));
        var lastRoom = floor.Rooms.Values.OrderByDescending(r => r.Id).First();
        Assert.Equal(RoomType.Stairs, lastRoom.RoomType);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void BossFloor_LastRoomIsBoss(int floorNumber)
    {
        var floor = DungeonGenerator.GenerateFloor(floorNumber, new SystemRandomSource(seed: 2));
        var lastRoom = floor.Rooms.Values.OrderByDescending(r => r.Id).First();
        Assert.Equal(RoomType.Boss, lastRoom.RoomType);
    }

    [Fact]
    public void FirstRoom_IsAlwaysStart()
    {
        var floor = DungeonGenerator.GenerateFloor(5, new SystemRandomSource(seed: 4));
        Assert.Equal(RoomType.Start, floor.Rooms[0].RoomType);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(500)]
    public void ExactlyOneTreasureRoom_Exists(int seed)
    {
        var floor = DungeonGenerator.GenerateFloor(4, new SystemRandomSource(seed: seed));
        Assert.Single(floor.Rooms.Values.Where(r => r.RoomType == RoomType.Treasure));
    }

    [Fact]
    public void PlayerRoomId_StartsAtRoomZero()
    {
        var floor = DungeonGenerator.GenerateFloor(1, new SystemRandomSource(seed: 8));
        Assert.Equal(0, floor.PlayerRoomId);
        Assert.Equal(0, floor.Rooms[0].Id);
    }

    [Fact]
    public void RoomIds_AreSequentialFromZero_AfterReindex()
    {
        var floor = DungeonGenerator.GenerateFloor(7, new SystemRandomSource(seed: 9));
        var ids = floor.Rooms.Keys.OrderBy(x => x).ToList();
        Assert.Equal(Enumerable.Range(0, floor.Rooms.Count), ids);
    }

    [Fact]
    public void TreasureRoom_HasNoLoot_WhenLootTablesNotProvided()
    {
        var floor = DungeonGenerator.GenerateFloor(4, new SystemRandomSource(seed: 1), lootTables: null);
        var treasureRoom = floor.Rooms.Values.Single(r => r.RoomType == RoomType.Treasure);
        Assert.Empty(treasureRoom.Loot);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void GenerateFloor_IsDeterministic_ForAGivenSeed(int seed)
    {
        var floorA = DungeonGenerator.GenerateFloor(6, new SystemRandomSource(seed: seed));
        var floorB = DungeonGenerator.GenerateFloor(6, new SystemRandomSource(seed: seed));

        Assert.Equal(floorA.Rooms.Count, floorB.Rooms.Count);
        foreach (var id in floorA.Rooms.Keys)
        {
            Assert.Equal(floorA.Rooms[id].RoomType, floorB.Rooms[id].RoomType);
            Assert.Equal(floorA.Rooms[id].Connections.OrderBy(x => x), floorB.Rooms[id].Connections.OrderBy(x => x));
        }
    }
}
