using System.Collections.Generic;

namespace TurtleBlaster
{
    public enum UpgradeId
    {
        JetThruster = 0,
        FuelTank = 1,
        Wheels = 2,
        SafetyShell = 3
    }

    public class UpgradeTier
    {
        public string name;
        public int coinCost;
        public int partCost;
        /// <summary>Meaning depends on the upgrade, see UpgradeDefinition.valueLabel.</summary>
        public float value;
        /// <summary>Secondary value (e.g. fuel burn multiplier, hits absorbed).</summary>
        public float value2;
    }

    public class UpgradeDefinition
    {
        public UpgradeId id;
        public string title;
        public string description;
        public string valueLabel;
        public List<UpgradeTier> tiers = new List<UpgradeTier>();
        public int MaxLevel => tiers.Count - 1;
    }

    /// <summary>
    /// GDD section 5 - Garage &amp; Upgrade Economy. Add or rebalance tiers here.
    /// Level 0 is always the free starting equipment.
    /// </summary>
    public static class UpgradeDatabase
    {
        static readonly Dictionary<UpgradeId, UpgradeDefinition> defs = Build();

        public static IEnumerable<UpgradeDefinition> All => defs.Values;
        public static UpgradeDefinition Get(UpgradeId id) => defs[id];

        static Dictionary<UpgradeId, UpgradeDefinition> Build()
        {
            var d = new Dictionary<UpgradeId, UpgradeDefinition>();

            // value = thrust multiplier
            d[UpgradeId.JetThruster] = new UpgradeDefinition
            {
                id = UpgradeId.JetThruster,
                title = "JET THRUSTER",
                description = "More thrust power & acceleration",
                valueLabel = "Thrust",
                tiers = new List<UpgradeTier>
                {
                    new UpgradeTier { name = "Spray Can",       coinCost = 0,   partCost = 0, value = 1.00f },
                    new UpgradeTier { name = "Fire Extinguisher", coinCost = 150, partCost = 2, value = 1.30f },
                    new UpgradeTier { name = "Rocket Firework", coinCost = 450, partCost = 6, value = 1.65f },
                }
            };

            // value = capacity multiplier, value2 = burn-rate multiplier
            d[UpgradeId.FuelTank] = new UpgradeDefinition
            {
                id = UpgradeId.FuelTank,
                title = "DIY FUEL TANK",
                description = "Bigger tank & efficient burn",
                valueLabel = "Capacity",
                tiers = new List<UpgradeTier>
                {
                    new UpgradeTier { name = "Plastic Bottle", coinCost = 0,   partCost = 0, value = 1.0f, value2 = 1.0f },
                    new UpgradeTier { name = "Metal Gallon",   coinCost = 220, partCost = 3, value = 1.7f, value2 = 0.8f },
                }
            };

            // value = cruise speed multiplier, value2 = wheel friction (grip)
            d[UpgradeId.Wheels] = new UpgradeDefinition
            {
                id = UpgradeId.Wheels,
                title = "SKATE WHEELS",
                description = "More grip, less friction",
                valueLabel = "Speed",
                tiers = new List<UpgradeTier>
                {
                    new UpgradeTier { name = "Plastic Wheels",    coinCost = 0,   partCost = 0, value = 1.00f, value2 = 0.45f },
                    new UpgradeTier { name = "Street Rubber",     coinCost = 120, partCost = 1, value = 1.08f, value2 = 0.65f },
                    new UpgradeTier { name = "Ceramic Bearings",  coinCost = 380, partCost = 4, value = 1.18f, value2 = 0.85f },
                }
            };

            // value = free hits absorbed per run, value2 = speed loss multiplier for light hits
            d[UpgradeId.SafetyShell] = new UpgradeDefinition
            {
                id = UpgradeId.SafetyShell,
                title = "SAFETY SHELL & PADS",
                description = "Survive light obstacle hits",
                valueLabel = "Hits",
                tiers = new List<UpgradeTier>
                {
                    new UpgradeTier { name = "No Protection",   coinCost = 0,   partCost = 0, value = 0, value2 = 1.0f },
                    new UpgradeTier { name = "Knee Pads",       coinCost = 100, partCost = 1, value = 1, value2 = 0.5f },
                    new UpgradeTier { name = "Reinforced Shell", coinCost = 320, partCost = 3, value = 2, value2 = 0.3f },
                }
            };

            return d;
        }
    }
}
