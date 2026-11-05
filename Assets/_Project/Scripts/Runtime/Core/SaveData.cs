using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>Persistent player profile. Stored as JSON in PlayerPrefs.</summary>
    [Serializable]
    public class SaveData
    {
        const string Key = "turtleblaster.save.v1";

        public int coins;
        public int parts;
        public int[] upgradeLevels = new int[4];
        public int bestDistance;
        public int bestScore;
        public int totalRuns;
        public bool sfxOn = true;
        public bool musicOn = true;

        // skins
        public List<string> ownedSkins = new List<string> { SkinDatabase.DefaultId };
        public string selectedSkin = SkinDatabase.DefaultId;

        // leaderboard (on-device; see LocalLeaderboard)
        public string playerName = "";
        public List<LeaderboardEntry> leaderboard = new List<LeaderboardEntry>();

        public event Action Changed;

        static SaveData _current;

        public static SaveData Current
        {
            get
            {
                if (_current == null) _current = Load();
                return _current;
            }
        }

        public int GetLevel(UpgradeId id) => upgradeLevels[(int)id];

        public UpgradeTier GetTier(UpgradeId id)
        {
            var def = UpgradeDatabase.Get(id);
            return def.tiers[Mathf.Clamp(GetLevel(id), 0, def.MaxLevel)];
        }

        public bool CanAfford(UpgradeTier t) => coins >= t.coinCost && parts >= t.partCost;

        /// <summary>Buys the next tier of the given upgrade. Returns true on success.</summary>
        public bool TryUpgrade(UpgradeId id)
        {
            var def = UpgradeDatabase.Get(id);
            int level = GetLevel(id);
            if (level >= def.MaxLevel) return false;
            var next = def.tiers[level + 1];
            if (!CanAfford(next)) return false;
            coins -= next.coinCost;
            parts -= next.partCost;
            upgradeLevels[(int)id] = level + 1;
            Save();
            return true;
        }

        // ---------------- skins ----------------
        public bool OwnsSkin(string id) => ownedSkins.Contains(id);

        public void GrantSkin(string id)
        {
            if (!OwnsSkin(id)) ownedSkins.Add(id);
        }

        /// <summary>Buys a coin skin. Premium (real-money) skins go through Store.Buy instead.</summary>
        public bool TryBuySkin(string id)
        {
            var skin = SkinDatabase.Get(id);
            if (skin.id != id || skin.IsPremium || OwnsSkin(id) || coins < skin.coinPrice) return false;
            coins -= skin.coinPrice;
            GrantSkin(id);
            selectedSkin = id;
            Save();
            return true;
        }

        public bool EquipSkin(string id)
        {
            if (!OwnsSkin(id)) return false;
            selectedSkin = id;
            Save();
            return true;
        }

        public void Save()
        {
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
            Changed?.Invoke();
        }

        static SaveData Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(Key))
                {
                    var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                    if (data != null)
                    {
                        if (data.upgradeLevels == null || data.upgradeLevels.Length < 4)
                        {
                            var fixedLevels = new int[4];
                            if (data.upgradeLevels != null) Array.Copy(data.upgradeLevels, fixedLevels, data.upgradeLevels.Length);
                            data.upgradeLevels = fixedLevels;
                        }
                        return Repair(data);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("Load failed, starting fresh: " + e.Message); }
            return Repair(new SaveData());
        }

        /// <summary>Fills fields missing from older saves.</summary>
        static SaveData Repair(SaveData d)
        {
            if (d.ownedSkins == null) d.ownedSkins = new List<string>();
            if (!d.ownedSkins.Contains(SkinDatabase.DefaultId)) d.ownedSkins.Add(SkinDatabase.DefaultId);
            if (string.IsNullOrEmpty(d.selectedSkin) || !d.ownedSkins.Contains(d.selectedSkin)) d.selectedSkin = SkinDatabase.DefaultId;
            if (d.leaderboard == null) d.leaderboard = new List<LeaderboardEntry>();
            if (string.IsNullOrEmpty(d.playerName)) d.playerName = "Turtle" + UnityEngine.Random.Range(1000, 10000);
            return d;
        }

        /// <summary>Test helper / "reset progress" button.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            _current = Repair(new SaveData());
            _current.Changed?.Invoke();
        }
    }

    /// <summary>Statistics of a single run, shown on the summary screen.</summary>
    public class RunStats
    {
        public float distance;
        public int score;
        public int coins;
        public int parts;
        public int flips;
        public int perfectLandings;
        public int maxCombo;
        public float topSpeed;
        public float grindTime;
        public string causeOfDeath = "";
        public bool newBestDistance;
        public bool newBestScore;
        public int leaderboardRank;       // 0 = not in the top list
        public bool revived;              // the "watch ad to revive" was used this run
        public bool coinsDoubled;         // the "watch ad for x2 coins" was used this run
    }
}
