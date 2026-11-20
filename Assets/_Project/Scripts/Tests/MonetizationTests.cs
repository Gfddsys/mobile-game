using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurtleBlaster.Tests
{
    /// <summary>Skins, store, leaderboard, revive and x2-coins flows (using the mock services).</summary>
    public class MonetizationTests
    {
        IAdService prevAds;
        IPurchaseService prevPurchases;
        ILeaderboardService prevLeaderboard;
        INotificationService prevNotifications;
        IScreenshotService prevScreenshot;

        [SetUp]
        public void SetUp()
        {
            foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm.gameObject);
            PlayerPrefs.DeleteAll();
            SaveData.ResetAll();
            GameInput.ClearOverride(); GameInput.ClearTouch();
            prevAds = Services.Ads;
            prevPurchases = Services.Purchases;
            prevLeaderboard = Services.Leaderboard;
            prevNotifications = Services.Notifications;
            prevScreenshot = Services.Screenshot;
            Services.Ads = new MockAdService();
            Services.Purchases = new MockPurchaseService();
            Services.Leaderboard = new LocalLeaderboard();
            Services.Notifications = new FallbackNotificationService();
            Services.Screenshot = new DefaultScreenshotService();
            MockAdService.AutoResult = true;
            MockPurchaseService.AutoResult = true;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            MockAdService.AutoResult = null;
            MockPurchaseService.AutoResult = null;
            GameInput.ClearOverride();
            foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm.gameObject);
            Services.Ads = prevAds;
            Services.Purchases = prevPurchases;
            Services.Leaderboard = prevLeaderboard;
            Services.Notifications = prevNotifications;
            Services.Screenshot = prevScreenshot;
        }

        // ---------------- skins ----------------

        [Test]
        public void Skins_SixPlusOnePremiumExist()
        {
            int coin = 0, premium = 0;
            foreach (var s in SkinDatabase.All)
            {
                if (s.id == SkinDatabase.DefaultId) continue;
                if (s.IsPremium) premium++; else coin++;
                Assert.IsNotNull(SpriteLibrary.GetTurtle(s.id), s.id);
            }
            Assert.AreEqual(5, coin, "5 skins bought with coins");
            Assert.AreEqual(1, premium, "1 skin bought with real money");
        }

        [Test]
        public void Skins_BuyWithCoinsThenEquip()
        {
            var save = SaveData.Current;
            Assert.IsTrue(save.OwnsSkin(SkinDatabase.DefaultId));
            Assert.IsFalse(save.TryBuySkin("blue"), "no coins yet");
            save.coins = 1000;
            Assert.IsTrue(save.TryBuySkin("blue"));
            Assert.AreEqual(700, save.coins);
            Assert.AreEqual("blue", save.selectedSkin, "buying equips it");
            Assert.IsFalse(save.TryBuySkin("blue"), "cannot buy twice");
            Assert.IsTrue(save.EquipSkin(SkinDatabase.DefaultId));
            Assert.IsFalse(save.EquipSkin("golden"), "cannot equip a skin you do not own");
            Assert.IsFalse(save.TryBuySkin("rainbow"), "premium skin is not sold for coins");
        }

        [Test]
        public void Store_PremiumSkinOnlyAfterConfirmedPayment()
        {
            MockPurchaseService.AutoResult = false;
            Store.Buy(ProductCatalog.SkinRainbow, null);
            Assert.IsFalse(SaveData.Current.OwnsSkin("rainbow"), "cancelled purchase must not grant");

            MockPurchaseService.AutoResult = true;
            bool result = false;
            Store.Buy(ProductCatalog.SkinRainbow, ok => result = ok);
            Assert.IsTrue(result);
            Assert.IsTrue(SaveData.Current.OwnsSkin("rainbow"));

            Store.Buy(ProductCatalog.SkinRainbow, ok => result = ok);
            Assert.IsFalse(result, "an owned non-consumable is not sold again");
        }

        [Test]
        public void Store_CoinPacksAddCoins()
        {
            Store.Buy(ProductCatalog.CoinsSmall, null);
            Assert.AreEqual(1000, SaveData.Current.coins);
            Store.Buy(ProductCatalog.CoinsMedium, null);
            Assert.AreEqual(6500, SaveData.Current.coins);
        }

        [Test]
        public void Store_TurboVipSubscriptionActivatesVipAndRefillsEnergy()
        {
            Assert.IsTrue(SaveData.Current.TryConsumeEnergy(2));
            Assert.AreEqual(3, SaveData.Current.energy);

            bool result = false;
            Store.Buy(ProductCatalog.TurboVip, ok => result = ok);
            Assert.IsTrue(result);
            Assert.IsTrue(SaveData.VipActiveThisSession);
            Assert.AreEqual(SaveData.MaxEnergy, SaveData.Current.energy);

            result = true;
            Store.Buy(ProductCatalog.TurboVip, ok => result = ok);
            Assert.IsFalse(result, "active VIP subscription cannot be purchased twice in the same session");
        }

        [Test]
        public void Screenshot_DefaultServiceSavesPngToPcFolder()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGB24, false);
            byte[] pngBytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            ScreenshotResult result = default;
            try
            {
                new DefaultScreenshotService().SaveScreenshot(pngBytes, "unit_test_shot.png", r => result = r);
                Assert.IsTrue(result.success);
                Assert.IsFalse(string.IsNullOrEmpty(result.filePath));
                Assert.IsTrue(File.Exists(result.filePath));

                byte[] saved = File.ReadAllBytes(result.filePath);
                Assert.GreaterOrEqual(saved.Length, 8);
                Assert.AreEqual(0x89, saved[0]);
                Assert.AreEqual(0x50, saved[1]);
                Assert.AreEqual(0x4E, saved[2]);
                Assert.AreEqual(0x47, saved[3]);
            }
            finally
            {
                if (!string.IsNullOrEmpty(result.filePath) && File.Exists(result.filePath))
                    File.Delete(result.filePath);
            }
        }

        // ---------------- leaderboard ----------------

        [Test]
        public void Leaderboard_KeepsBestDistancesSortedAndCapped()
        {
            var lb = new LocalLeaderboard();
            for (int i = 0; i < 30; i++)
                lb.Submit(new LeaderboardEntry { name = "p" + i, distance = (i * 37) % 500, score = i, date = "x" });
            int rank = lb.Submit(new LeaderboardEntry { name = "best", distance = 9999, score = 1, date = "x" });
            Assert.AreEqual(1, rank);
            Assert.AreEqual(0, lb.Submit(new LeaderboardEntry { name = "worst", distance = -1, score = 0, date = "x" }), "too low to be listed");
            lb.GetTop(100, list =>
            {
                Assert.AreEqual(LocalLeaderboard.MaxEntries, list.Count);
                for (int i = 1; i < list.Count; i++) Assert.GreaterOrEqual(list[i - 1].distance, list[i].distance);
            });
        }

        // ---------------- run flows ----------------

        IEnumerator RunUntilDistance(GameManager gm, float metres)
        {
            gm.StartRun(5);
            float t = 0f;
            while (gm.Stats.distance < metres && t < 10f && gm.Player.Alive) { t += Time.deltaTime; yield return null; }
            Assert.GreaterOrEqual(gm.Stats.distance, metres, "precondition: ride far enough to be offered a revive");
        }

        IEnumerator WaitFor(System.Func<bool> cond, float seconds = 6f)
        {
            float t = 0f;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator Revive_WatchAdContinuesTheRunOnce()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            yield return RunUntilDistance(gm, 32f);
            float before = gm.Stats.distance;
            gm.Player.Crash("test crash");
            yield return WaitFor(() => gm.State == GameState.Reviving);
            Assert.AreEqual(GameState.Reviving, gm.State, "revive should be offered");
            gm.UI.ReviveChoose(true);
            yield return WaitFor(() => gm.State == GameState.Playing);
            Assert.AreEqual(GameState.Playing, gm.State);
            Assert.IsTrue(gm.Player.Alive);
            Assert.IsTrue(gm.Player.Shielded, "revived turtle gets a short shield");
            Assert.IsTrue(gm.Stats.revived);
            Assert.GreaterOrEqual(gm.Stats.distance, before, "distance is kept");

            // shield blocks a crash, then a second crash after it wears off ends the run (no second revive)
            gm.Player.Crash("blocked by shield");
            Assert.IsTrue(gm.Player.Alive);
            yield return new WaitForSeconds(GameConfig.Instance.reviveShieldSeconds + 0.2f);
            if (gm.Player.Alive) gm.Player.Crash("second crash");
            yield return WaitFor(() => gm.State == GameState.Summary, 8f);
            Assert.AreEqual(GameState.Summary, gm.State, "only one revive per run");
        }

        [UnityTest]
        public IEnumerator Revive_DeclineGoesToSummary()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            yield return RunUntilDistance(gm, 32f);
            gm.Player.Crash("test crash");
            yield return WaitFor(() => gm.State == GameState.Reviving);
            gm.UI.ReviveChoose(false);
            yield return WaitFor(() => gm.State == GameState.Summary);
            Assert.AreEqual(GameState.Summary, gm.State);
            Assert.GreaterOrEqual(gm.Stats.leaderboardRank, 1, "the run was submitted to the leaderboard");
        }

        [UnityTest]
        public IEnumerator DoubleCoins_AdDoublesTheRunCoinsOnce()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            gm.StartRun(5);
            yield return null;
            gm.Stats.coins = 10;
            gm.Player.Crash("test crash");   // < 30 m: no revive offer
            yield return WaitFor(() => gm.State == GameState.Summary);
            Assert.AreEqual(GameState.Summary, gm.State);
            Assert.AreEqual(10, SaveData.Current.coins);

            Assert.IsTrue(gm.CanDoubleCoins);
            gm.WatchAdForDoubleCoins();
            Assert.AreEqual(20, SaveData.Current.coins);
            Assert.AreEqual(20, gm.Stats.coins);
            gm.WatchAdForDoubleCoins();
            Assert.AreEqual(20, SaveData.Current.coins, "only once per run");
        }

        [UnityTest]
        public IEnumerator DoubleCoins_SkippedAdGivesNothing()
        {
            MockAdService.AutoResult = false;
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            gm.StartRun(5);
            yield return null;
            gm.Stats.coins = 10;
            gm.Player.Crash("test crash");
            yield return WaitFor(() => gm.State == GameState.Summary);
            gm.WatchAdForDoubleCoins();
            Assert.AreEqual(10, SaveData.Current.coins);
        }

        [UnityTest]
        public IEnumerator EquippedSkinIsUsedInTheRun()
        {
            SaveData.Current.coins = 5000;
            SaveData.Current.TryBuySkin("ninja");
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            gm.StartRun(5);
            yield return null;
            Assert.AreEqual("ninja", gm.Player.SkinId);
        }

        // ---------------- energy & notifications ----------------

        [Test]
        public void Energy_RefillsOneEvery15MinutesAndCapsAtFive()
        {
            var save = SaveData.Current;
            Assert.AreEqual(SaveData.MaxEnergy, save.energy);
            Assert.AreEqual(5, SaveData.MaxEnergy);

            var t0 = new System.DateTime(2026, 11, 19, 12, 0, 0, System.DateTimeKind.Utc);
            Assert.IsTrue(save.TryConsumeEnergy(3, t0));
            Assert.AreEqual(2, save.energy);

            // Just before 15 minutes: still 2
            save.RefreshEnergy(t0.AddMinutes(14).AddSeconds(59));
            Assert.AreEqual(2, save.energy);

            // At 15 minutes: +1 energy
            save.RefreshEnergy(t0.AddMinutes(15));
            Assert.AreEqual(3, save.energy);

            // At 45 minutes total: all 3 missing energies refilled, capped at 5
            save.RefreshEnergy(t0.AddMinutes(45));
            Assert.AreEqual(5, save.energy);
            Assert.AreEqual(0, save.energyNextRefillUtcTicks);
            Assert.AreEqual(System.TimeSpan.Zero, save.GetTimeUntilFullEnergy(t0.AddMinutes(45)));
        }

        [UnityTest]
        public IEnumerator Energy_ConsumesOnStartAndRestartAndBlocksAtZero()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;

            Assert.AreEqual(5, SaveData.Current.energy);
            Assert.IsTrue(gm.TryStartRun(5));
            Assert.AreEqual(4, SaveData.Current.energy, "starting a run consumes 1 energy");

            Assert.IsTrue(gm.TryStartRun(6));
            Assert.AreEqual(3, SaveData.Current.energy, "restarting consumes 1 energy");

            // Drain remaining 3 energies
            Assert.IsTrue(gm.TryStartRun(7));
            Assert.IsTrue(gm.TryStartRun(8));
            Assert.IsTrue(gm.TryStartRun(9));
            Assert.AreEqual(0, SaveData.Current.energy);

            gm.ShowMenu();
            yield return null;
            Assert.IsFalse(gm.TryStartRun(10), "cannot start a run with 0 energy");
            Assert.AreNotEqual(GameState.Playing, gm.State);
        }

        [UnityTest]
        public IEnumerator Energy_RewardedAdGrantsPlusOneUpToMax()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;

            Assert.IsTrue(SaveData.Current.TryConsumeEnergy(2));
            Assert.AreEqual(3, SaveData.Current.energy);

            bool adDone = false, adOk = false;
            gm.WatchAdForEnergy(ok => { adOk = ok; adDone = true; });
            yield return WaitFor(() => adDone, 2f);
            Assert.IsTrue(adOk);
            Assert.AreEqual(4, SaveData.Current.energy, "rewarded ad adds +1 energy");

            MockAdService.AutoResult = false;
            adDone = false; adOk = true;
            gm.WatchAdForEnergy(ok => { adOk = ok; adDone = true; });
            yield return WaitFor(() => adDone, 2f);
            Assert.IsFalse(adOk);
            Assert.AreEqual(4, SaveData.Current.energy, "skipped ad grants no energy");

            MockAdService.AutoResult = true;
            adDone = false;
            gm.WatchAdForEnergy(ok => { adDone = true; });
            yield return WaitFor(() => adDone, 2f);
            Assert.AreEqual(5, SaveData.Current.energy);

            // Already at max (5/5): cannot exceed MaxEnergy
            adDone = false; adOk = true;
            gm.WatchAdForEnergy(ok => { adOk = ok; adDone = true; });
            yield return WaitFor(() => adDone, 2f);
            Assert.IsFalse(adOk);
            Assert.AreEqual(5, SaveData.Current.energy);
        }

        [UnityTest]
        public IEnumerator Energy_SchedulesAndCancelsFullNotification()
        {
            var notif = Services.Notifications as FallbackNotificationService;
            Assert.IsNotNull(notif);

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            Assert.IsFalse(notif.IsScheduled, "no notification when energy starts full (5/5)");

            // Start 1st run -> 4/5 -> full in ~15 minutes
            Assert.IsTrue(gm.TryStartRun(5));
            Assert.IsTrue(notif.IsScheduled);
            Assert.AreEqual(5, notif.ScheduledMaxEnergy);
            var firstFullUtc = notif.ScheduledFireTimeUtc;
            double firstMinutes = (firstFullUtc - System.DateTime.UtcNow).TotalMinutes;
            Assert.That(firstMinutes, Is.InRange(14.5, 15.1));

            // Start 2nd run -> 3/5 -> full in ~30 minutes
            Assert.IsTrue(gm.TryStartRun(6));
            Assert.IsTrue(notif.IsScheduled);
            double secondMinutes = (notif.ScheduledFireTimeUtc - System.DateTime.UtcNow).TotalMinutes;
            Assert.That(secondMinutes, Is.InRange(29.5, 30.1));

            // Watch rewarded ad (+1 energy -> 4/5) -> full moves 15m earlier (~15m)
            bool done = false;
            gm.WatchAdForEnergy(_ => done = true);
            yield return WaitFor(() => done, 2f);
            Assert.IsTrue(notif.IsScheduled);
            double afterAdMinutes = (notif.ScheduledFireTimeUtc - System.DateTime.UtcNow).TotalMinutes;
            Assert.That(afterAdMinutes, Is.InRange(14.5, 15.1));

            // Watch rewarded ad again (+1 energy -> 5/5) -> notification cancelled
            done = false;
            gm.WatchAdForEnergy(_ => done = true);
            yield return WaitFor(() => done, 2f);
            Assert.AreEqual(5, SaveData.Current.energy);
            Assert.IsFalse(notif.IsScheduled, "notification is cancelled once energy is full");
        }

        // ---------------- 3 level biomes & 7-day login rewards ----------------

        [Test]
        public void LevelBiomes_CyclesThroughThreeDistinctVariants()
        {
            Assert.AreEqual(3, LevelBiomes.Count);
            Assert.AreEqual(0, LevelBiomes.GetIndex(0f));
            Assert.AreEqual(1, LevelBiomes.GetIndex(250f));
            Assert.AreEqual(2, LevelBiomes.GetIndex(500f));
            Assert.AreEqual(0, LevelBiomes.GetIndex(750f));

            var names = new System.Collections.Generic.HashSet<string>();
            var colors = new System.Collections.Generic.HashSet<Color32>();
            var fallbackSq = SpriteLibrary.Get("square");

            for (int i = 0; i < LevelBiomes.Count; i++)
            {
                var b = LevelBiomes.Get(i);
                Assert.IsTrue(names.Add(b.name), "biome name should be unique");
                Assert.IsTrue(colors.Add(b.topA), "biome ground top color should be distinct");

                Assert.AreNotSame(fallbackSq, SpriteLibrary.Get(b.skySprite), b.skySprite);
                Assert.AreNotSame(fallbackSq, SpriteLibrary.Get(b.farSprite), b.farSprite);
                Assert.AreNotSame(fallbackSq, SpriteLibrary.Get(b.nearSprite), b.nearSprite);
                Assert.AreNotSame(fallbackSq, SpriteLibrary.Get(b.coneSprite), b.coneSprite);
                Assert.AreNotSame(fallbackSq, SpriteLibrary.Get(b.boulderSprite), b.boulderSprite);
                Assert.IsNotNull(b.decorSprites);
                Assert.Greater(b.decorSprites.Length, 0);
                foreach (var d in b.decorSprites) Assert.AreNotSame(fallbackSq, SpriteLibrary.Get(d), d);
            }
        }

        [Test]
        public void DailyLogin_SevenDayRewardsStreakResetAndDuplicatePrevention()
        {
            var save = SaveData.Current;
            var d0 = new System.DateTime(2026, 11, 1, 9, 0, 0);
            int expectedCoins = 0, expectedParts = 0;

            for (int day = 0; day < 7; day++)
            {
                var cur = d0.AddDays(day);
                Assert.IsTrue(save.CanClaimDailyLogin(cur), "day " + (day + 1) + " should be claimable");
                if (day == 3 || day == 6) Assert.IsTrue(save.TryConsumeEnergy(2, cur));

                Assert.IsTrue(save.TryClaimDailyLogin(out var reward, cur));
                Assert.AreEqual(day + 1, reward.day);
                expectedCoins += reward.coins;
                expectedParts += reward.parts;

                Assert.AreEqual(expectedCoins, save.coins);
                Assert.AreEqual(expectedParts, save.parts);
                if (reward.fullEnergy) Assert.AreEqual(SaveData.MaxEnergy, save.energy, "fullEnergy day refills energy");

                Assert.IsFalse(save.CanClaimDailyLogin(cur), "cannot claim twice on same day");
                Assert.IsFalse(save.TryClaimDailyLogin(out _, cur));
            }

            // Day 8 consecutive loops back to Day 1
            var day8 = d0.AddDays(7);
            Assert.IsTrue(save.TryClaimDailyLogin(out var r8, day8));
            Assert.AreEqual(1, r8.day);

            // Skipping a day resets streak back to Day 1
            Assert.IsTrue(save.TryClaimDailyLogin(out var rSkip, day8.AddDays(1)));
            Assert.AreEqual(2, rSkip.day);
            Assert.IsTrue(save.TryClaimDailyLogin(out var rReset, day8.AddDays(3)));
            Assert.AreEqual(1, rReset.day, "missing a day resets streak back to Day 1");
        }
    }
}
