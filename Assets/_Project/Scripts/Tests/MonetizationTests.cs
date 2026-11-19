using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurtleBlaster.Tests
{
    /// <summary>Skins, store, leaderboard, revive and x2-coins flows (using the mock services).</summary>
    public class MonetizationTests
    {
        IAdService prevAds;
        ILeaderboardService prevLeaderboard;
        INotificationService prevNotifications;

        [SetUp]
        public void SetUp()
        {
            foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm.gameObject);
            PlayerPrefs.DeleteAll();
            SaveData.ResetAll();
            GameInput.ClearOverride(); GameInput.ClearTouch();
            prevAds = Services.Ads;
            prevLeaderboard = Services.Leaderboard;
            prevNotifications = Services.Notifications;
            Services.Ads = new MockAdService();
            Services.Leaderboard = new LocalLeaderboard();
            Services.Notifications = new FallbackNotificationService();
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
            Services.Leaderboard = prevLeaderboard;
            Services.Notifications = prevNotifications;
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
        public IEnumerator Energy_InterstitialAdGrantsPlusOneUpToMax()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;

            Assert.IsTrue(SaveData.Current.TryConsumeEnergy(2));
            Assert.AreEqual(3, SaveData.Current.energy);

            bool adDone = false, adOk = false;
            gm.WatchAdForEnergy(ok => { adOk = ok; adDone = true; });
            yield return WaitFor(() => adDone, 2f);
            Assert.IsTrue(adOk);
            Assert.AreEqual(4, SaveData.Current.energy, "interstitial ad adds +1 energy");

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

            // Watch interstitial ad (+1 energy -> 4/5) -> full moves 15m earlier (~15m)
            bool done = false;
            gm.WatchAdForEnergy(_ => done = true);
            yield return WaitFor(() => done, 2f);
            Assert.IsTrue(notif.IsScheduled);
            double afterAdMinutes = (notif.ScheduledFireTimeUtc - System.DateTime.UtcNow).TotalMinutes;
            Assert.That(afterAdMinutes, Is.InRange(14.5, 15.1));

            // Watch interstitial ad again (+1 energy -> 5/5) -> notification cancelled
            done = false;
            gm.WatchAdForEnergy(_ => done = true);
            yield return WaitFor(() => done, 2f);
            Assert.AreEqual(5, SaveData.Current.energy);
            Assert.IsFalse(notif.IsScheduled, "notification is cancelled once energy is full");
        }
    }
}
