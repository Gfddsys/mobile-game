using System;
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
                        return data;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("Load failed, starting fresh: " + e.Message); }
            return new SaveData();
        }

        /// <summary>Test helper / "reset progress" button.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            _current = new SaveData();
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
    }
}
