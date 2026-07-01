using System.Collections.Generic;
using TowerOfChrome.Core.Dungeon;
using UnityEngine;

namespace TowerOfChrome.Unity.Views
{
    /// <summary>Room-type map icons (Kenney "Roguelike/RPG Pack", CC0), loaded from
    /// Resources/Art/RoomIcons. See docs/art-credits.md for sourcing.</summary>
    public static class RoomIcons
    {
        private static readonly IReadOnlyDictionary<RoomType, string> ResourceNames = new Dictionary<RoomType, string>
        {
            [RoomType.Start] = "Art/RoomIcons/icon_start",
            [RoomType.Normal] = "Art/RoomIcons/icon_normal",
            [RoomType.Encounter] = "Art/RoomIcons/icon_encounter",
            [RoomType.Treasure] = "Art/RoomIcons/icon_treasure",
            [RoomType.Boss] = "Art/RoomIcons/icon_boss",
            [RoomType.Stairs] = "Art/RoomIcons/icon_stairs",
        };

        private static readonly Dictionary<RoomType, Texture2D> Cache = new();

        public static Texture2D Get(RoomType roomType)
        {
            if (Cache.TryGetValue(roomType, out var cached))
                return cached;

            var texture = ResourceNames.TryGetValue(roomType, out var path) ? Resources.Load<Texture2D>(path) : null;
            Cache[roomType] = texture;
            return texture;
        }
    }
}
