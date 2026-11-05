using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    public enum SkinAccessory { None, Shades, Crown, Headband }

    public class SkinDef
    {
        public string id;
        public string name;
        /// <summary>Short description of the skin's passive gameplay perk.</summary>
        public string perkDesc = "Balanced all-rounder";
        /// <summary>Price in in-game coins. 0 with a productId = sold for real money only.</summary>
        public int coinPrice;
        /// <summary>Store product id for skins sold for real money (see ProductCatalog). Empty = coin skin.</summary>
        public string productId = "";
        public Color32 shell, shellLight, skin, helmet;
        public SkinAccessory accessory;
        /// <summary>Animated rainbow tint (premium skin).</summary>
        public bool rainbow;

        // ---- Skin Perks ----
        public float fuelBurnMult = 1f;
        public float stuntGainMult = 1f;
        public int bonusFreeHits = 0;
        public float doubleCoinChance = 0f;
        public float flipSpeedMult = 1f;
        public float coinBonusMult = 1f;

        public bool IsPremium => !string.IsNullOrEmpty(productId);
    }

    /// <summary>
    /// All turtle skins: 1 free default, 5 bought with coins, 1 premium bought with real money.
    /// Each skin grants a unique passive gameplay perk in addition to its custom pixel art.
    /// </summary>
    public static class SkinDatabase
    {
        public const string DefaultId = "classic";

        static readonly List<SkinDef> list = Build();
        static readonly Dictionary<string, SkinDef> byId = new Dictionary<string, SkinDef>();

        public static IReadOnlyList<SkinDef> All => list;

        public static SkinDef Get(string id)
        {
            if (byId.Count == 0) foreach (var s in list) byId[s.id] = s;
            return id != null && byId.TryGetValue(id, out var d) ? d : byId[DefaultId];
        }

        static Color32 C(string hex) => PixelCanvas.C(hex);

        static List<SkinDef> Build()
        {
            return new List<SkinDef>
            {
                new SkinDef { id = DefaultId, name = "Classic Green", coinPrice = 0,
                    perkDesc = "Balanced Ride",
                    shell = C("#2f9e44"), shellLight = C("#7bdc5a"), skin = C("#6ad24f"), helmet = C("#e63946") },
                new SkinDef { id = "blue", name = "Ocean Blue", coinPrice = 300,
                    perkDesc = "-15% Jet Fuel Burn", fuelBurnMult = 0.85f,
                    shell = C("#2b6cb0"), shellLight = C("#63b3ed"), skin = C("#6ad24f"), helmet = C("#ffd23f") },
                new SkinDef { id = "sunset", name = "Sunset Orange", coinPrice = 600,
                    perkDesc = "+25% Stunt Meter Gain", stuntGainMult = 1.25f,
                    shell = C("#d9731a"), shellLight = C("#ffb25c"), skin = C("#8bd450"), helmet = C("#7b2cbf") },
                new SkinDef { id = "bubblegum", name = "Bubblegum", coinPrice = 900,
                    perkDesc = "+1 Extra Shell Shield", bonusFreeHits = 1,
                    shell = C("#e5539b"), shellLight = C("#ff9ccb"), skin = C("#8ee07a"), helmet = C("#3a86ff"), accessory = SkinAccessory.Shades },
                new SkinDef { id = "golden", name = "Golden Boss", coinPrice = 1500,
                    perkDesc = "35% Chance 2x Coin", doubleCoinChance = 0.35f,
                    shell = C("#d4a017"), shellLight = C("#ffe066"), skin = C("#8bd450"), helmet = C("#fff3b0"), accessory = SkinAccessory.Crown },
                new SkinDef { id = "ninja", name = "Shadow Ninja", coinPrice = 2200,
                    perkDesc = "+20% Air Flip Speed", flipSpeedMult = 1.20f,
                    shell = C("#2b2d42"), shellLight = C("#5c6078"), skin = C("#6ad24f"), helmet = C("#14141f"), accessory = SkinAccessory.Headband },
                new SkinDef { id = "rainbow", name = "Rainbow Hero", coinPrice = 0, productId = ProductCatalog.SkinRainbow,
                    perkDesc = "+25% Coins & +1 Shield", coinBonusMult = 1.25f, bonusFreeHits = 1, fuelBurnMult = 0.9f,
                    shell = C("#7b2cbf"), shellLight = C("#ff6bd6"), skin = C("#a0f080"), helmet = C("#4dd2ff"), accessory = SkinAccessory.Shades, rainbow = true },
            };
        }
    }
}
