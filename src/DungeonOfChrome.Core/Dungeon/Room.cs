namespace DungeonOfChrome.Core.Dungeon;

public enum RoomType
{
    Start,
    Normal,
    Encounter,
    Treasure,
    Boss,
    Stairs,
}

public static class RoomTypeExtensions
{
    /// <summary>Lowercase string matching the Python enum's .value, used for save serialization.</summary>
    public static string ToSaveString(this RoomType t) => t switch
    {
        RoomType.Start => "start",
        RoomType.Normal => "normal",
        RoomType.Encounter => "encounter",
        RoomType.Treasure => "treasure",
        RoomType.Boss => "boss",
        RoomType.Stairs => "stairs",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };

    public static RoomType RoomTypeFromSaveString(string s) => s switch
    {
        "start" => RoomType.Start,
        "normal" => RoomType.Normal,
        "encounter" => RoomType.Encounter,
        "treasure" => RoomType.Treasure,
        "boss" => RoomType.Boss,
        "stairs" => RoomType.Stairs,
        _ => throw new ArgumentOutOfRangeException(nameof(s), $"Unknown room type: '{s}'"),
    };
}

/// <summary>A dungeon room. Mutable — Id is reassigned during generation's reindex step, and
/// RoomType/Connections/Cleared/Visited/Loot all change as the floor is generated and played.
/// Port of dungeon/room.py.</summary>
public sealed class Room
{
    public int Id { get; set; }
    public int X { get; }
    public int Y { get; }
    public int W { get; }
    public int H { get; }
    public RoomType RoomType { get; set; } = RoomType.Normal;
    public List<int> Connections { get; } = new();
    public bool Cleared { get; set; }
    public bool Visited { get; set; }
    public List<string> Loot { get; set; } = new();

    public Room(int id, int x, int y, int w, int h)
    {
        Id = id;
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public int Cx => X + W / 2;
    public int Cy => Y + H / 2;
    public (int X, int Y) Center => (Cx, Cy);

    public double DistanceTo(Room other)
    {
        var dx = Cx - other.Cx;
        var dy = Cy - other.Cy;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
