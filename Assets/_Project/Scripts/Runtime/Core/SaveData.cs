using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    public enum MissionKind { Distance, Flips, PerfectLandings, CoinsInRun, GrindSeconds, CloseCalls }

    public struct MissionDef
    {
        public MissionKind kind;
        public int target;
        public int rewardCoins;
        public string title;

        public int GetProgress(RunStats s)
        {
            if (s == null) return 0;
            switch (kind)
            {
                case MissionKind.Distance: return Mathf.FloorToInt(s.distance);
                case MissionKind.Flips: return s.flips;
                case MissionKind.PerfectLandings: return s.perfectLandings;
                case MissionKind.CoinsInRun: return s.coins;
                case MissionKind.GrindSeconds: return Mathf.FloorToInt(s.grindTime);
                case MissionKind.CloseCalls: return s.closeCalls;
                default: return 0;
            }
        }

        public bool IsComplete(RunStats s) => GetProgress(s) >= target;
    }

    public struct LoginRewardDef
    {
        public int day;
        public int coins;
        public int parts;
        public bool fullEnergy;
        public string label;
        public string iconSprite;
    }

    /// <summary>Persistent player profile. Stored as JSON in PlayerPrefs.</summary>
    [Serializable]
    public class SaveData
    {
        const string Key = "turtleblaster.save.v1";
        public const int MaxEnergy = 5;
        public const int EnergyRefillSeconds = 15 * 60;

        public static readonly LoginRewardDef[] DailyRewards =
        {
            new LoginRewardDef { day = 1, coins = 100, parts = 0, fullEnergy = false, label = "+100 Coins", iconSprite = "coin" },
            new LoginRewardDef { day = 2, coins = 0,   parts = 2, fullEnergy = false, label = "+2 Parts", iconSprite = "part" },
            new LoginRewardDef { day = 3, coins = 200, parts = 0, fullEnergy = false, label = "+200 Coins", iconSprite = "coin" },
            new LoginRewardDef { day = 4, coins = 150, parts = 0, fullEnergy = true,  label = "+150 Coins\nFull Energy", iconSprite = "energy" },
            new LoginRewardDef { day = 5, coins = 0,   parts = 4, fullEnergy = false, label = "+4 Parts", iconSprite = "part" },
            new LoginRewardDef { day = 6, coins = 350, parts = 2, fullEnergy = false, label = "+350 Coins\n+2 Parts", iconSprite = "coin" },
            new LoginRewardDef { day = 7, coins = 750, parts = 6, fullEnergy = true,  label = "+750 Coins\n+6 Parts\nFull Energy", iconSprite = "energy" },
        };

        public int coins;
        public int parts;
        public int[] upgradeLevels = new int[4];
        public int bestDistance;
        public int bestScore;
        public int totalRuns;
        public int missionIndex;
        public int energy = MaxEnergy;
        public bool energyInitialized;
        public long energyNextRefillUtcTicks;
        public int loginStreak;
        public int lastLoginClaimDay;
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

        public MissionDef GetCurrentMission()
        {
            int idx = Mathf.Max(0, missionIndex);
            int tier = idx / 6;
            switch (idx % 6)
            {
                case 0:
                {
                    int t = 150 + tier * 50;
                    return new MissionDef { kind = MissionKind.Distance, target = t, rewardCoins = 60 + tier * 15, title = "Reach " + t + "m in one run" };
                }
                case 1:
                {
                    int t = 3 + tier;
                    return new MissionDef { kind = MissionKind.Flips, target = t, rewardCoins = 75 + tier * 15, title = "Land " + t + " flips in one run" };
                }
                case 2:
                {
                    int t = 2 + tier;
                    return new MissionDef { kind = MissionKind.PerfectLandings, target = t, rewardCoins = 80 + tier * 15, title = "Hit " + t + " Perfect Landings" };
                }
                case 3:
                {
                    int t = 15 + tier * 5;
                    return new MissionDef { kind = MissionKind.CoinsInRun, target = t, rewardCoins = 70 + tier * 15, title = "Collect " + t + " coins in one run" };
                }
                case 4:
                {
                    int t = 2 + tier;
                    return new MissionDef { kind = MissionKind.GrindSeconds, target = t, rewardCoins = 90 + tier * 15, title = "Grind on rails for " + t + "s" };
                }
                default:
                {
                    int t = 2 + tier;
                    return new MissionDef { kind = MissionKind.CloseCalls, target = t, rewardCoins = 100 + tier * 20, title = "Get " + t + " Close Call dodges" };
                }
            }
        }

        public void CompleteCurrentMission()
        {
            var m = GetCurrentMission();
            coins += m.rewardCoins;
            missionIndex++;
            Save();
        }

        // ---------------- energy ----------------
        public bool RefreshEnergy(DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            if (energy >= MaxEnergy)
            {
                if (energyNextRefillUtcTicks != 0)
                {
                    energyNextRefillUtcTicks = 0;
                    Save();
                    return true;
                }
                return false;
            }

            long intervalTicks = TimeSpan.FromSeconds(EnergyRefillSeconds).Ticks;
            if (energyNextRefillUtcTicks <= 0)
            {
                energyNextRefillUtcTicks = now.Ticks + intervalTicks;
                Save();
                return true;
            }

            // Guard against device clock moving backwards
            if (energyNextRefillUtcTicks > now.Ticks + intervalTicks)
            {
                energyNextRefillUtcTicks = now.Ticks + intervalTicks;
                Save();
                return true;
            }

            if (now.Ticks < energyNextRefillUtcTicks) return false;

            int gained = 1 + (int)((now.Ticks - energyNextRefillUtcTicks) / intervalTicks);
            energy = Mathf.Min(MaxEnergy, energy + gained);
            if (energy >= MaxEnergy) energyNextRefillUtcTicks = 0;
            else energyNextRefillUtcTicks += gained * intervalTicks;
            Save();
            return true;
        }

        public bool TryConsumeEnergy(int amount = 1, DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            RefreshEnergy(now);
            if (amount <= 0) return true;
            if (energy < amount) return false;

            bool wasFull = energy >= MaxEnergy;
            energy -= amount;
            if (wasFull && energy < MaxEnergy)
                energyNextRefillUtcTicks = now.Ticks + TimeSpan.FromSeconds(EnergyRefillSeconds).Ticks;
            Save();
            return true;
        }

        public bool AddEnergy(int amount = 1, DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            RefreshEnergy(now);
            if (amount <= 0 || energy >= MaxEnergy) return false;

            energy = Mathf.Min(MaxEnergy, energy + amount);
            if (energy >= MaxEnergy) energyNextRefillUtcTicks = 0;
            Save();
            return true;
        }

        public TimeSpan GetTimeUntilNextEnergy(DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            if (energy >= MaxEnergy || energyNextRefillUtcTicks <= 0) return TimeSpan.Zero;
            long rem = energyNextRefillUtcTicks - now.Ticks;
            return rem > 0 ? TimeSpan.FromTicks(rem) : TimeSpan.Zero;
        }

        public TimeSpan GetTimeUntilFullEnergy(DateTime? nowUtc = null)
        {
            DateTime now = nowUtc ?? DateTime.UtcNow;
            if (energy >= MaxEnergy) return TimeSpan.Zero;
            int remainingAfterNext = Mathf.Max(0, MaxEnergy - energy - 1);
            return GetTimeUntilNextEnergy(now) + TimeSpan.FromSeconds(remainingAfterNext * EnergyRefillSeconds);
        }

        // ---------------- 7-day daily login rewards ----------------
        public static int GetDayNumber(DateTime localDate) => (int)(localDate.Date.Ticks / TimeSpan.TicksPerDay);

        public void GetDailyLoginState(out int claimedCount, out int nextDayIndex, out bool canClaimToday, DateTime? nowLocal = null)
        {
            int today = GetDayNumber(nowLocal ?? DateTime.Now);
            int streak = Mathf.Clamp(loginStreak, 0, DailyRewards.Length);

            if (lastLoginClaimDay <= 0 || streak == 0)
            {
                claimedCount = 0;
                nextDayIndex = 0;
                canClaimToday = true;
                return;
            }

            int diff = today - lastLoginClaimDay;
            if (diff <= 0)
            {
                // Same day (already claimed) or clock moved backwards
                claimedCount = streak;
                nextDayIndex = streak % DailyRewards.Length;
                canClaimToday = false;
            }
            else if (diff == 1)
            {
                // Consecutive day (loop back to Day 1 cleanly after Day 7)
                claimedCount = streak >= DailyRewards.Length ? 0 : streak;
                nextDayIndex = claimedCount;
                canClaimToday = true;
            }
            else
            {
                // Missed one or more days -> restart streak at Day 1
                claimedCount = 0;
                nextDayIndex = 0;
                canClaimToday = true;
            }
        }

        public bool CanClaimDailyLogin(DateTime? nowLocal = null)
        {
            GetDailyLoginState(out _, out _, out bool canClaimToday, nowLocal);
            return canClaimToday;
        }

        public bool TryClaimDailyLogin(out LoginRewardDef claimed, DateTime? nowLocal = null)
        {
            DateTime now = nowLocal ?? DateTime.Now;
            GetDailyLoginState(out _, out int nextDayIndex, out bool canClaimToday, now);
            if (!canClaimToday)
            {
                claimed = default;
                return false;
            }

            claimed = DailyRewards[nextDayIndex];
            coins += claimed.coins;
            parts += claimed.parts;
            if (claimed.fullEnergy)
            {
                energy = MaxEnergy;
                energyNextRefillUtcTicks = 0;
            }
            loginStreak = nextDayIndex + 1;
            lastLoginClaimDay = GetDayNumber(now);
            Save();
            return true;
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
            if (!d.energyInitialized)
            {
                d.energy = MaxEnergy;
                d.energyInitialized = true;
                d.energyNextRefillUtcTicks = 0;
            }
            d.energy = Mathf.Clamp(d.energy, 0, MaxEnergy);
            if (d.energy >= MaxEnergy) d.energyNextRefillUtcTicks = 0;
            else if (d.energyNextRefillUtcTicks <= 0) d.energyNextRefillUtcTicks = DateTime.UtcNow.AddSeconds(EnergyRefillSeconds).Ticks;
            d.loginStreak = Mathf.Clamp(d.loginStreak, 0, DailyRewards.Length);
            if (d.lastLoginClaimDay < 0) d.lastLoginClaimDay = 0;
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
        public int closeCalls;
        public int missionsCompleted;
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
