namespace TowerOfChrome.Core.Dungeon;

/// <summary>
/// Runtime state for a single dungeon level: the room graph, the player's current position,
/// and navigation/access-control helpers (boss floors lock the stairs). Port of dungeon/floor.py.
/// </summary>
public sealed class DungeonFloor
{
    private static readonly IReadOnlyDictionary<string, (int Dx, int Dy)> DirVecs = new Dictionary<string, (int, int)>
    {
        ["right"] = (1, 0),
        ["left"] = (-1, 0),
        ["down"] = (0, 1),
        ["up"] = (0, -1),
    };

    public int FloorNumber { get; }
    public Dictionary<int, Room> Rooms { get; }
    public List<(int A, int B)> Corridors { get; }
    public int PlayerRoomId { get; private set; }

    public DungeonFloor(int floorNumber, IEnumerable<Room> rooms, IEnumerable<(int A, int B)> corridors, int playerRoomId)
    {
        FloorNumber = floorNumber;
        Rooms = rooms.ToDictionary(r => r.Id);
        Corridors = corridors.ToList();
        PlayerRoomId = playerRoomId;
        Rooms[playerRoomId].Visited = true;
    }

    public Room CurrentRoom => Rooms[PlayerRoomId];

    public List<Room> ConnectedRooms =>
        CurrentRoom.Connections.Where(Rooms.ContainsKey).Select(cid => Rooms[cid]).ToList();

    public bool IsBossFloor => FloorNumber % 5 == 0;

    /// <summary>Stairs are freely accessible on normal floors. On boss floors every BOSS room
    /// must be cleared first.</summary>
    public bool CanAccessStairs() => Rooms.Values.All(r => r.RoomType != RoomType.Boss || r.Cleared);

    /// <summary>Move the player to `roomId` if it is adjacent. Returns true on success.</summary>
    public bool MoveTo(int roomId)
    {
        if (!CurrentRoom.Connections.Contains(roomId))
            return false;
        PlayerRoomId = roomId;
        Rooms[roomId].Visited = true;
        return true;
    }

    /// <summary>
    /// Return the ID of the connected room that lies most in `direction`
    /// ("left" | "right" | "up" | "down"), or null if none qualifies.
    ///
    /// Uses cosine similarity: the neighbour whose unit-vector from the current room's centre
    /// has the highest dot product with the direction vector, provided the dot product
    /// strictly exceeds 0.30 (~within 73 degrees of axis).
    /// </summary>
    public int? RoomInDirection(string direction)
    {
        var (dx, dy) = DirVecs[direction];
        var (cx, cy) = CurrentRoom.Center;

        var bestScore = 0.30;
        int? bestId = null;

        foreach (var room in ConnectedRooms)
        {
            var rx = room.Cx - cx;
            var ry = room.Cy - cy;
            var dist = Math.Sqrt(rx * rx + ry * ry);
            if (dist < 1)
                continue;
            var dot = (rx * dx + ry * dy) / dist;
            if (dot > bestScore)
            {
                bestScore = dot;
                bestId = room.Id;
            }
        }

        return bestId;
    }
}
