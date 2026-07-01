using TowerOfChrome.Core.Loot;
using TowerOfChrome.Core.Rng;

namespace TowerOfChrome.Core.Dungeon;

/// <summary>
/// Procedural dungeon floor generator. Port of dungeon/generator.py.
///
/// Algorithm
/// ---------
/// 1. Divide the map area into a Cols x Rows zone grid.
/// 2. Shuffle the zones and place one room per selected zone (random size/position within zone bounds).
/// 3. Sort rooms diagonally (left-to-right, top-to-bottom) for a natural progression direction.
/// 4. Build a minimum-spanning-tree (Prim's) so every room is reachable.
/// 5. Add 0-2 random extra connections for loops.
/// 6. Assign room types (Start, Stairs/Boss, Treasure, Encounter, Normal).
/// 7. Populate Treasure rooms with loot.
///
/// Note: in the original Python, loot generation always used the global `random` module, not
/// the seeded generator instance passed to generate_floor — meaning a seed only ever
/// controlled room layout, never loot rolls. This port preserves that decoupling naturally:
/// `lootTables` carries its own independently-injected IRandomSource.
/// </summary>
public static class DungeonGenerator
{
    // Layout constants (pixel units; must match ExploreScreen's map area in the Unity view layer).
    public const int MapW = 940;
    public const int MapH = 525;
    public const int Cols = 4;
    public const int Rows = 3;
    public const int RoomWMin = 105, RoomWMax = 165;
    public const int RoomHMin = 68, RoomHMax = 105;
    public const int ZonePad = 14;

    public static DungeonFloor GenerateFloor(int floorNumber, IRandomSource rng, LootTables? lootTables = null)
    {
        var nRooms = Math.Min(10, 6 + (floorNumber - 1) / 3);
        var rooms = PlaceRooms(rng, nRooms);
        rooms = SortRooms(rooms);
        Reindex(rooms);
        var corridors = BuildConnections(rng, rooms);
        AssignTypes(rng, floorNumber, rooms);
        PopulateLoot(floorNumber, rooms, lootTables);

        return new DungeonFloor(floorNumber, rooms, corridors, rooms[0].Id);
    }

    private static List<Room> PlaceRooms(IRandomSource rng, int nRooms)
    {
        var zoneW = MapW / Cols;
        var zoneH = MapH / Rows;

        var allZones = new List<(int Col, int Row)>();
        for (var c = 0; c < Cols; c++)
            for (var r = 0; r < Rows; r++)
                allZones.Add((c, r));

        var zones = rng.Sample(allZones, Math.Min(nRooms, allZones.Count));

        var rooms = new List<Room>();
        for (var i = 0; i < zones.Count; i++)
        {
            var (col, row) = zones[i];
            var zx1 = col * zoneW + ZonePad;
            var zy1 = row * zoneH + ZonePad;
            var zx2 = (col + 1) * zoneW - ZonePad;
            var zy2 = (row + 1) * zoneH - ZonePad;

            var rw = rng.NextInt(RoomWMin, Math.Min(RoomWMax, zx2 - zx1) + 1);
            var rh = rng.NextInt(RoomHMin, Math.Min(RoomHMax, zy2 - zy1) + 1);
            var rx = rng.NextInt(zx1, Math.Max(zx1, zx2 - rw) + 1);
            var ry = rng.NextInt(zy1, Math.Max(zy1, zy2 - rh) + 1);

            rooms.Add(new Room(i, rx, ry, rw, rh));
        }
        return rooms;
    }

    /// <summary>Sort rooms diagonally (left-to-right primary, top-to-bottom secondary).
    /// Uses a stable sort (LINQ OrderBy) rather than List.Sort — .NET's List.Sort is NOT
    /// guaranteed stable, unlike Python's list.sort(), and exact key ties are possible.</summary>
    private static List<Room> SortRooms(List<Room> rooms) =>
        rooms.OrderBy(r => r.Cx + r.Cy * 0.35).ToList();

    /// <summary>Assign sequential IDs 0..n-1 after sorting.</summary>
    private static void Reindex(List<Room> rooms)
    {
        for (var i = 0; i < rooms.Count; i++)
            rooms[i].Id = i;
    }

    /// <summary>Connect rooms with a minimum spanning tree (Prim's algorithm), then add 0-2
    /// random extra edges for short loops.</summary>
    private static List<(int A, int B)> BuildConnections(IRandomSource rng, List<Room> rooms)
    {
        if (rooms.Count < 2)
            return new List<(int, int)>();

        var edges = new List<(int A, int B)>();
        var inTree = new HashSet<int> { rooms[0].Id };
        var roomMap = rooms.ToDictionary(r => r.Id);

        while (inTree.Count < rooms.Count)
        {
            var bestDist = double.PositiveInfinity;
            (int A, int B)? bestEdge = null;

            foreach (var a in rooms)
            {
                if (!inTree.Contains(a.Id))
                    continue;
                foreach (var b in rooms)
                {
                    if (inTree.Contains(b.Id))
                        continue;
                    var d = a.DistanceTo(b);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestEdge = (a.Id, b.Id);
                    }
                }
            }

            if (bestEdge is not (int A, int B) edge)
                break; // defensive only — unreachable given the while condition's invariant

            edges.Add(edge);
            inTree.Add(edge.B);
            Link(roomMap, edge.A, edge.B);
        }

        var extra = rng.NextInt(0, Math.Min(2, rooms.Count - 2) + 1);
        var edgeSet = new HashSet<(int, int)>(edges.Select(e => (Math.Min(e.A, e.B), Math.Max(e.A, e.B))));

        // Python computes range(extra*10) ONCE before the loop, locking in the iteration cap
        // using extra's value at that moment — extra is then decremented inside the loop body,
        // but that doesn't change how many iterations the range() already produced.
        var cap = extra * 10;
        for (var i = 0; i < cap; i++)
        {
            if (extra <= 0)
                break;

            var pair = rng.Sample(rooms, 2);
            var a = pair[0];
            var b = pair[1];
            var key = (Math.Min(a.Id, b.Id), Math.Max(a.Id, b.Id));
            if (!edgeSet.Contains(key))
            {
                edgeSet.Add(key);
                edges.Add((a.Id, b.Id));
                Link(roomMap, a.Id, b.Id);
                extra -= 1;
            }
        }

        return edges;
    }

    /// <summary>Add a bidirectional connection between two rooms.</summary>
    private static void Link(Dictionary<int, Room> roomMap, int idA, int idB)
    {
        if (!roomMap[idA].Connections.Contains(idB))
            roomMap[idA].Connections.Add(idB);
        if (!roomMap[idB].Connections.Contains(idA))
            roomMap[idB].Connections.Add(idA);
    }

    /// <summary>
    /// Assign room types:
    /// - rooms[0]  -> Start
    /// - rooms[-1] -> Boss (boss floors) or Stairs (normal floors)
    /// - 1 middle room -> Treasure
    /// - 0-1 middle room -> Normal (empty)
    /// - rest -> Encounter
    /// - single-room edge case overrides everything to Stairs.
    /// </summary>
    private static void AssignTypes(IRandomSource rng, int floorNumber, List<Room> rooms)
    {
        var n = rooms.Count;
        rooms[0].RoomType = RoomType.Start;

        var isBossFloor = floorNumber % 5 == 0;
        rooms[n - 1].RoomType = isBossFloor ? RoomType.Boss : RoomType.Stairs;

        var middle = Enumerable.Range(1, Math.Max(0, n - 2)).ToList();
        rng.Shuffle(middle);

        if (middle.Count > 0)
        {
            var idx = middle[^1];
            middle.RemoveAt(middle.Count - 1);
            rooms[idx].RoomType = RoomType.Treasure;
        }

        if (middle.Count > 0 && rng.NextDouble() < 0.4)
        {
            var idx = middle[^1];
            middle.RemoveAt(middle.Count - 1);
            rooms[idx].RoomType = RoomType.Normal;
        }

        foreach (var idx in middle)
            rooms[idx].RoomType = RoomType.Encounter;

        if (n == 1)
            rooms[0].RoomType = RoomType.Stairs;
    }

    /// <summary>Generate guaranteed loot for Treasure rooms. No-op if lootTables is null
    /// (mirrors Python's `except ImportError: return`).</summary>
    private static void PopulateLoot(int floorNumber, List<Room> rooms, LootTables? lootTables)
    {
        if (lootTables == null)
            return;

        var table = floorNumber < 5 ? "elite_floor1" : "mini_boss";

        foreach (var room in rooms)
        {
            if (room.RoomType != RoomType.Treasure)
                continue;

            for (var i = 0; i < 3; i++)
            {
                var drops = lootTables.GenerateDrops(table, floorNumber);
                if (drops.Count > 0)
                {
                    room.Loot = drops;
                    break;
                }
            }
            if (room.Loot.Count == 0)
                room.Loot = new List<string> { "health_potion_small" };
        }
    }
}
