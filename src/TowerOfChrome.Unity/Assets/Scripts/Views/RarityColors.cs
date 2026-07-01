using System.Collections.Generic;
using TowerOfChrome.Core.Loot;
using UnityEngine;

namespace TowerOfChrome.Unity.Views
{
    /// <summary>Display colors per rarity tier, shared by Combat's loot rendering and
    /// Inventory's bag listing. Port of loot/rarity.py's RARITY_COLORS.</summary>
    public static class RarityColors
    {
        public static readonly IReadOnlyDictionary<Rarity, Color> Map = new Dictionary<Rarity, Color>
        {
            [Rarity.Common] = new Color(170 / 255f, 170 / 255f, 170 / 255f),
            [Rarity.Uncommon] = new Color(60 / 255f, 200 / 255f, 60 / 255f),
            [Rarity.Rare] = new Color(60 / 255f, 130 / 255f, 230 / 255f),
            [Rarity.Epic] = new Color(170 / 255f, 60 / 255f, 230 / 255f),
            [Rarity.Legendary] = new Color(230 / 255f, 170 / 255f, 30 / 255f),
        };

        public static Color Get(Rarity rarity) => Map.GetValueOrDefault(rarity, Color.white);
    }
}
