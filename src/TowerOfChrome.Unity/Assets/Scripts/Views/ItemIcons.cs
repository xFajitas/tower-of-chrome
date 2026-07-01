using System.Collections.Generic;
using TowerOfChrome.Core.Loot;
using UnityEngine;

namespace TowerOfChrome.Unity.Views
{
    /// <summary>
    /// Item icons (Kenney "Roguelike Characters" + "Roguelike/RPG Pack", both CC0), loaded from
    /// Resources/Art/Items. There are 9 weapon_types and only 6 distinct weapon icons -- greatsword
    /// reuses the sword blade, wand and grimoire both reuse the staff icon -- rather than sourcing
    /// bespoke art for every one. Armor/accessory/charm each get one icon regardless of
    /// armor_type/specific item, for the same reason. See docs/art-credits.md.
    /// </summary>
    public static class ItemIcons
    {
        private static readonly IReadOnlyDictionary<string, string> WeaponTypeToResource = new Dictionary<string, string>
        {
            ["sword"] = "weapon_blade",
            ["greatsword"] = "weapon_blade",
            ["dagger"] = "weapon_dagger",
            ["axe"] = "weapon_axe",
            ["mace"] = "weapon_mace",
            ["bow"] = "weapon_bow",
            ["staff"] = "weapon_staff",
            ["wand"] = "weapon_staff",
            ["grimoire"] = "weapon_staff",
        };

        private static readonly Dictionary<string, Texture2D> Cache = new();

        /// <summary>Best-icon-for-item: weapon_type for weapons, otherwise one icon per
        /// category (armor/accessory/charm/potion). Returns null if nothing matches (e.g. an
        /// unrecognised category), so callers can skip rendering an icon gracefully.</summary>
        public static Texture2D For(ItemDefinition item)
        {
            if (item.Category == "weapon" && item.WeaponType != null && WeaponTypeToResource.TryGetValue(item.WeaponType, out var weaponIcon))
                return Load(weaponIcon);

            return item.Category switch
            {
                "armor" => Load("armor_icon"),
                "accessory" => Load("accessory_icon"),
                "charm" => Load("charm_icon"),
                "potion" => Load("potion_icon"),
                _ => null,
            };
        }

        /// <summary>For a specific equipment slot's item id (or null if the slot is empty).</summary>
        public static Texture2D ForItemId(ItemRegistry registry, string? itemId) =>
            itemId == null ? null : For(registry.Get(itemId));

        private static Texture2D Load(string resourceName)
        {
            if (Cache.TryGetValue(resourceName, out var cached))
                return cached;
            var texture = Resources.Load<Texture2D>($"Art/Items/{resourceName}");
            Cache[resourceName] = texture;
            return texture;
        }
    }
}
