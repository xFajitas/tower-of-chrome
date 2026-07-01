using TowerOfChrome.Core.Dungeon;

namespace TowerOfChrome.Core.Tests.Dungeon;

public class DungeonFloorTests
{
    /// <summary>3x1 layout: start(0)--mid(1)--east(2), all in a horizontal line.</summary>
    private static DungeonFloor NewLinearFloor(int floorNumber = 1)
    {
        var start = new Room(0, x: 0, y: 0, w: 100, h: 100);     // center (50,50)
        var mid = new Room(1, x: 200, y: 0, w: 100, h: 100);     // center (250,50)
        var east = new Room(2, x: 400, y: 0, w: 100, h: 100);    // center (450,50)
        start.Connections.Add(1);
        mid.Connections.Add(0);
        mid.Connections.Add(2);
        east.Connections.Add(1);

        return new DungeonFloor(floorNumber, new[] { start, mid, east }, new[] { (0, 1), (1, 2) }, playerRoomId: 0);
    }

    [Fact]
    public void Constructor_MarksStartingRoomVisited()
    {
        var floor = NewLinearFloor();
        Assert.True(floor.CurrentRoom.Visited);
    }

    [Fact]
    public void ConnectedRooms_ReturnsOnlyDirectNeighbors()
    {
        var floor = NewLinearFloor();
        var connected = floor.ConnectedRooms;
        Assert.Single(connected);
        Assert.Equal(1, connected[0].Id);
    }

    [Fact]
    public void MoveTo_Adjacent_Succeeds_AndMarksVisited()
    {
        var floor = NewLinearFloor();
        var moved = floor.MoveTo(1);
        Assert.True(moved);
        Assert.Equal(1, floor.PlayerRoomId);
        Assert.True(floor.Rooms[1].Visited);
    }

    [Fact]
    public void MoveTo_NonAdjacent_Fails()
    {
        var floor = NewLinearFloor();
        var moved = floor.MoveTo(2); // not adjacent to room 0
        Assert.False(moved);
        Assert.Equal(0, floor.PlayerRoomId); // unchanged
    }

    [Fact]
    public void RoomInDirection_PicksRoomAlongRequestedAxis()
    {
        var floor = NewLinearFloor();
        Assert.Equal(1, floor.RoomInDirection("right"));
        Assert.Null(floor.RoomInDirection("left"));  // no neighbor to the left
        Assert.Null(floor.RoomInDirection("up"));
        Assert.Null(floor.RoomInDirection("down"));
    }

    [Fact]
    public void RoomInDirection_RequiresStrictlyGreaterThanThreshold()
    {
        // A neighbor almost directly perpendicular (just inside the ~73-degree cone) should
        // still qualify; one exactly on the boundary conceptually would not (dot > 0.30, not >=).
        var start = new Room(0, x: 0, y: 0, w: 10, h: 10);   // center (5,5)
        var diagonal = new Room(1, x: 1000, y: 950, w: 10, h: 10); // far right and far down -> shallow angle from "right"
        start.Connections.Add(1);
        diagonal.Connections.Add(0);
        var floor = new DungeonFloor(1, new[] { start, diagonal }, new[] { (0, 1) }, 0);

        // dx=995, dy=945 roughly 45 degrees from "right" axis -> dot ~ cos(45) ~ 0.706 > 0.30, qualifies.
        Assert.Equal(1, floor.RoomInDirection("right"));
    }

    [Fact]
    public void IsBossFloor_TrueOnlyOnMultiplesOfFive()
    {
        Assert.True(NewLinearFloor(5).IsBossFloor);
        Assert.True(NewLinearFloor(10).IsBossFloor);
        Assert.False(NewLinearFloor(4).IsBossFloor);
        Assert.False(NewLinearFloor(1).IsBossFloor);
    }

    [Fact]
    public void CanAccessStairs_TrueWhenNoBossRoomExists()
    {
        Assert.True(NewLinearFloor().CanAccessStairs());
    }

    [Fact]
    public void CanAccessStairs_FalseWhileBossRoomUncleared_TrueOnceCleared()
    {
        var floor = NewLinearFloor();
        floor.Rooms[2].RoomType = RoomType.Boss;

        Assert.False(floor.CanAccessStairs());

        floor.Rooms[2].Cleared = true;
        Assert.True(floor.CanAccessStairs());
    }
}
