using System.Collections.Generic;
using UnityEngine;

namespace TowerOfChrome.Unity.Views
{
    /// <summary>
    /// Party/enemy avatar icons (Kenney "Roguelike Characters" pack, CC0), loaded from
    /// Resources/Art/Characters. There are 16 playable classes across 8 roles but only 5 hand
    /// -picked archetype sprites -- each role maps onto the closest-fitting archetype rather than
    /// sourcing 16 bespoke portraits. Enemies all reuse one base sprite, tinted per id via a
    /// small fixed palette so the 12 enemies stay visually distinguishable without 12 bespoke
    /// sprites. See docs/art-credits.md for sourcing and rationale.
    /// </summary>
    public static class ArchetypeIcons
    {
        private static readonly IReadOnlyDictionary<string, string> RoleToArchetype = new Dictionary<string, string>
        {
            ["tank"] = "warrior",
            ["melee_dps"] = "warrior",
            ["healer"] = "cleric",
            ["support"] = "cleric",
            ["magic_dps"] = "mage",
            ["summoner"] = "mage",
            ["ranged_dps"] = "ranger",
            ["hybrid"] = "rogue",
        };

        private static readonly Color[] EnemyTintPalette =
        {
            new(0.85f, 0.35f, 0.35f), // red
            new(0.35f, 0.75f, 0.85f), // cyan
            new(0.75f, 0.45f, 0.85f), // purple
            new(0.85f, 0.65f, 0.30f), // amber
            new(0.45f, 0.85f, 0.55f), // green
            new(0.55f, 0.55f, 0.90f), // indigo
            new(0.90f, 0.50f, 0.70f), // pink
            new(0.60f, 0.70f, 0.40f), // olive
        };

        private static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D ForRole(string role)
        {
            var archetype = RoleToArchetype.GetValueOrDefault(role, "warrior");
            return Load($"Art/Characters/archetype_{archetype}");
        }

        public static Texture2D EnemyBase() => Load("Art/Characters/enemy_base");

        /// <summary>Deterministic per-id tint so repeated enemies always render the same color,
        /// and distinct enemies are usually visually distinguishable, without bespoke art.</summary>
        public static Color EnemyTint(string enemyId)
        {
            var index = (enemyId.GetHashCode() & int.MaxValue) % EnemyTintPalette.Length;
            return EnemyTintPalette[index];
        }

        private static Texture2D Load(string resourcePath)
        {
            if (Cache.TryGetValue(resourcePath, out var cached))
                return cached;
            var texture = Resources.Load<Texture2D>(resourcePath);
            Cache[resourcePath] = texture;
            return texture;
        }
    }
}
