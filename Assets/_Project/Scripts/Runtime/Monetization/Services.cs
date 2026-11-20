using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TurtleBlaster
{
    // =====================================================================================
    //  Service layer. The game only talks to these interfaces; swap the implementations in
    //  GameManager.Awake (Services.Ads = ..., Services.Purchases = ..., Services.Leaderboard = ...)
    //  to plug in LevelPlay / Unity IAP / Unity Gaming Services. See docs/MONETIZATION.md.
    //
    //  The Mock* implementations are for the Editor and Development builds ONLY. In a release
    //  build they refuse to grant anything, so a missing SDK can never give away free rewards.
    // =====================================================================================

    public static class Services
    {
        public static IAdService Ads = new MockAdService();
        public static IPurchaseService Purchases = new MockPurchaseService();
        public static ILeaderboardService Leaderboard = new UgsLeaderboardService("High_Scores");
        public static INotificationService Notifications = new FallbackNotificationService();
        public static IScreenshotService Screenshot = new DefaultScreenshotService();

        public static event Action PurchasesChanged;
        public static bool LastPurchaseAlreadyGranted;
        public static void NotifyPurchasesChanged() => PurchasesChanged?.Invoke();

        /// <summary>True where test/mock behaviour is allowed (Editor or Development Build).</summary>
        public static bool MockAllowed => Application.isEditor || Debug.isDebugBuild;
    }

    // ------------------------------------------------------------------ ads

    public interface IAdService
    {
        /// <summary>Is a rewarded ad available to show right now?</summary>
        bool IsRewardedReady { get; }
        /// <summary>Show a rewarded ad. onComplete(true) only if the player watched it to the end.</summary>
        void ShowRewarded(string placement, Action<bool> onComplete);
    }

    /// <summary>Fake ad: a 3 second "TEST AD" overlay. Editor / Development builds only.</summary>
    public class MockAdService : IAdService
    {
        /// <summary>Tests set this to skip the overlay and finish instantly (null = show overlay).</summary>
        public static bool? AutoResult;

        public bool IsRewardedReady => AutoResult.HasValue || Services.MockAllowed;

        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (AutoResult.HasValue) { onComplete?.Invoke(AutoResult.Value); return; }
            if (!Services.MockAllowed || GameManager.Instance == null) { onComplete?.Invoke(false); return; }
            GameManager.Instance.UI.ShowMockAd(placement, onComplete);
        }
    }

    // ------------------------------------------------------------------ purchases

    public interface IPurchaseService
    {
        bool IsAvailable { get; }
        string StoreStatus { get; }
        void Initialize();
        /// <summary>Localised price text such as "$0.99" ("" if unknown).</summary>
        string GetPriceString(string productId);
        /// <summary>Start a purchase. onResult(true) only after the store confirmed payment.</summary>
        void Purchase(string productId, Action<bool> onResult);
        /// <summary>Re-deliver non-consumable purchases (skins) after a reinstall.</summary>
        void RestorePurchases(Action<IList<string>> onRestored);
    }

    /// <summary>Fake store: shows a confirm dialog, charges nothing. Editor / Development builds only.</summary>
    public class MockPurchaseService : IPurchaseService
    {
        public static bool? AutoResult;

        public bool IsAvailable => AutoResult.HasValue || Services.MockAllowed;
        public string StoreStatus => IsAvailable
            ? "TEST STORE: purchases here are simulated - no real money is charged."
            : "Store is not configured in this build.";

        public void Initialize() { }

        public string GetPriceString(string productId)
        {
            var p = ProductCatalog.Get(productId);
            return p == null ? "" : p.mockPrice;
        }

        public void Purchase(string productId, Action<bool> onResult)
        {
            if (AutoResult.HasValue) { onResult?.Invoke(AutoResult.Value); return; }
            if (!Services.MockAllowed || GameManager.Instance == null) { onResult?.Invoke(false); return; }
            var p = ProductCatalog.Get(productId);
            GameManager.Instance.UI.ShowMockPurchase(p != null ? p.title : productId, GetPriceString(productId), onResult);
        }

        public void RestorePurchases(Action<IList<string>> onRestored)
        {
            // The mock keeps no receipts; ownership already lives in SaveData.
            onRestored?.Invoke(new List<string>());
        }
    }

    public enum ProductKind { Coins, Skin, Subscription }

    public class Product
    {
        public string id, title;
        public ProductKind kind;
        public int coins;               // for Coins
        public string skinId;           // for Skin
        public string mockPrice;
    }

    /// <summary>Everything sold for real money. Product ids must match the store consoles.</summary>
    public static class ProductCatalog
    {
        public const string CoinsSmall = "coins_small";
        public const string CoinsMedium = "coins_medium";
        public const string CoinsLarge = "coins_large";
        public const string SkinRainbow = "skin_rainbow";
        public const string TurboVip = "turbo_vip";

        static readonly List<Product> list = new List<Product>
        {
            new Product { id = CoinsSmall,  title = "Pocket Change",     kind = ProductKind.Coins,        coins = 1000,  mockPrice = "$0.99" },
            new Product { id = CoinsMedium, title = "Coin Bucket",       kind = ProductKind.Coins,        coins = 5500,  mockPrice = "$4.99" },
            new Product { id = CoinsLarge,  title = "Treasure Chest",    kind = ProductKind.Coins,        coins = 12000, mockPrice = "$9.99" },
            new Product { id = SkinRainbow, title = "Rainbow Hero skin", kind = ProductKind.Skin,         skinId = "rainbow", mockPrice = "$2.99" },
            new Product { id = TurboVip,    title = "Turbo VIP Pass",    kind = ProductKind.Subscription, mockPrice = "$4.99/mo" },
        };

        public static IReadOnlyList<Product> All => list;
        public static Product Get(string id) => list.Find(p => p.id == id);
    }

    /// <summary>Delivers purchased goods. Call Grant only after the store confirmed the payment.</summary>
    public static class Store
    {
        public static void Buy(string productId, Action<bool> done)
        {
            var product = ProductCatalog.Get(productId);
            if (product == null || !Services.Purchases.IsAvailable) { done?.Invoke(false); return; }
            if (product.kind == ProductKind.Skin && SaveData.Current.OwnsSkin(product.skinId)) { done?.Invoke(false); return; }
            if (product.kind == ProductKind.Subscription && SaveData.VipActiveThisSession) { done?.Invoke(false); return; }
            Services.LastPurchaseAlreadyGranted = false;
            Services.Purchases.Purchase(productId, ok =>
            {
                if (ok && !Services.LastPurchaseAlreadyGranted) Grant(product);
                done?.Invoke(ok);
            });
        }

        public static void Grant(Product product)
        {
            var save = SaveData.Current;
            if (product.kind == ProductKind.Coins) save.coins += product.coins;
            else if (product.kind == ProductKind.Skin) save.GrantSkin(product.skinId);
            else if (product.kind == ProductKind.Subscription)
            {
                SaveData.VipActiveThisSession = true;
                save.energy = SaveData.MaxEnergy;
                save.energyNextRefillUtcTicks = 0;
            }
            save.Save();
        }
    }

    // ------------------------------------------------------------------ leaderboard

    [Serializable]
    public class LeaderboardEntry
    {
        public string name;
        public int distance;
        public int score;
        public string date;
    }

    public interface ILeaderboardService
    {
        /// <summary>Records a finished run. onRank gets the 1-based rank of the run (0 if not ranked);
        /// online services call it later, when the server answers.</summary>
        void Submit(LeaderboardEntry entry, Action<int> onRank);
        /// <summary>Best runs, best distance first.</summary>
        void GetTop(int count, Action<IList<LeaderboardEntry>> onResult);
    }

    /// <summary>On-device top list (best distance). Replace with an online service (UGS Leaderboards) for global ranks.</summary>
    public class LocalLeaderboard : ILeaderboardService
    {
        public const int MaxEntries = 20;

        public void Submit(LeaderboardEntry entry, Action<int> onRank) => onRank?.Invoke(Submit(entry));

        public int Submit(LeaderboardEntry entry)
        {
            var list = SaveData.Current.leaderboard;
            list.Add(entry);
            list.Sort((a, b) => b.distance != a.distance ? b.distance.CompareTo(a.distance) : b.score.CompareTo(a.score));
            int rank = list.IndexOf(entry) + 1;
            if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
            SaveData.Current.Save();
            return rank <= MaxEntries ? rank : 0;
        }

        public void GetTop(int count, Action<IList<LeaderboardEntry>> onResult)
        {
            var list = SaveData.Current.leaderboard;
            onResult?.Invoke(list.GetRange(0, Mathf.Min(count, list.Count)));
        }
    }

    // ------------------------------------------------------------------ notifications

    public interface INotificationService
    {
        void Initialize();
        void ScheduleEnergyFull(DateTime fireTimeUtc, int maxEnergy);
        void ScheduleTestNotification(int delaySeconds = 10);
        void CancelEnergyFull();
    }

    /// <summary>
    /// Tracks scheduled full-energy notification state when com.unity.mobile.notifications is not
    /// installed (or on non-mobile platforms / tests). Platform adapters override Services.Notifications
    /// automatically when com.unity.mobile.notifications is installed on Android or iOS.
    /// </summary>
    public class FallbackNotificationService : INotificationService
    {
        public bool IsScheduled { get; private set; }
        public DateTime ScheduledFireTimeUtc { get; private set; }
        public DateTime LastTestFireTimeUtc { get; private set; }
        public int ScheduledMaxEnergy { get; private set; }

        bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && !UNITY_MOBILE_NOTIFICATIONS
            Debug.Log("[Notifications] Install com.unity.mobile.notifications via Package Manager for OS-level background notifications.");
#endif
        }

        public void ScheduleEnergyFull(DateTime fireTimeUtc, int maxEnergy)
        {
            Initialize();
            IsScheduled = true;
            ScheduledFireTimeUtc = fireTimeUtc;
            ScheduledMaxEnergy = maxEnergy;
            Debug.Log("[Notifications] Scheduled 'Energy Full (" + maxEnergy + "/" + maxEnergy + ")' for " + fireTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        }

        public void ScheduleTestNotification(int delaySeconds = 10)
        {
            Initialize();
            int secs = Math.Max(1, delaySeconds);
            LastTestFireTimeUtc = DateTime.UtcNow.AddSeconds(secs);
            Debug.Log("[Notifications] Scheduled test notification for " + LastTestFireTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + " (" + secs + "s)");
        }

        public void CancelEnergyFull()
        {
            if (!IsScheduled) return;
            IsScheduled = false;
            ScheduledFireTimeUtc = default;
            Debug.Log("[Notifications] Cancelled energy full notification.");
        }
    }

    // ------------------------------------------------------------------ screenshots

    public struct ScreenshotResult
    {
        public bool success;
        public string filePath;      // PC path (Editor/Standalone) or temp cache file path (Mobile) used for NativeShare
        public string galleryPath;   // Mobile Gallery path (empty on PC)
        public string statusMessage; // Human-readable message for UI
    }

    public interface IScreenshotService
    {
        string LastSavedPath { get; }
        string LastStatusMessage { get; }
        void SaveScreenshot(byte[] pngBytes, string fileName, Action<ScreenshotResult> onComplete);
        void ShareScreenshot(string filePath, string subject, string text, Action<bool> onComplete = null);
        void OpenFolder(string filePath);
    }

    /// <summary>
    /// Fallback screenshot handler used in tests or when NativeGallery/NativeShare packages are absent.
    /// Saves PNGs to PC project Screenshots folder (in Editor) or persistentDataPath.
    /// </summary>
    public class DefaultScreenshotService : IScreenshotService
    {
        public string LastSavedPath { get; private set; }
        public string LastStatusMessage { get; private set; }

        public void SaveScreenshot(byte[] pngBytes, string fileName, Action<ScreenshotResult> onComplete)
        {
            if (pngBytes == null || pngBytes.Length == 0 || string.IsNullOrEmpty(fileName))
            {
                LastStatusMessage = "Screenshot failed: empty image data.";
                onComplete?.Invoke(new ScreenshotResult { success = false, statusMessage = LastStatusMessage });
                return;
            }

            try
            {
#if UNITY_EDITOR
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
                Directory.CreateDirectory(folder);
                string fullPath = Path.Combine(folder, fileName);
                File.WriteAllBytes(fullPath, pngBytes);
                LastSavedPath = fullPath;
                LastStatusMessage = "Saved to PC: Screenshots/" + fileName;
                Debug.Log("[Screenshot] Saved to PC: " + fullPath);
#else
                string folder = Path.Combine(Application.persistentDataPath, "Screenshots");
                Directory.CreateDirectory(folder);
                string fullPath = Path.Combine(folder, fileName);
                File.WriteAllBytes(fullPath, pngBytes);
                LastSavedPath = fullPath;
                LastStatusMessage = "Saved locally: " + fullPath;
                Debug.Log("[Screenshot] Saved locally: " + fullPath);
#endif
                onComplete?.Invoke(new ScreenshotResult
                {
                    success = true,
                    filePath = LastSavedPath,
                    galleryPath = string.Empty,
                    statusMessage = LastStatusMessage
                });
            }
            catch (Exception e)
            {
                LastStatusMessage = "Save failed: " + e.Message;
                Debug.LogWarning("[Screenshot] " + LastStatusMessage);
                onComplete?.Invoke(new ScreenshotResult { success = false, statusMessage = LastStatusMessage });
            }
        }

        public void ShareScreenshot(string filePath, string subject, string text, Action<bool> onComplete = null)
        {
            Debug.Log("[Screenshot] Native Share requires Android/iOS mobile build. File: " + filePath);
            onComplete?.Invoke(false);
        }

        public void OpenFolder(string filePath) => OpenPathInFileBrowser(filePath);

        public static void OpenPathInFileBrowser(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            string folder = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            try
            {
                if (File.Exists(filePath))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + filePath.Replace('/', '\\') + "\"");
                else if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + folder.Replace('/', '\\') + "\"");
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Screenshot] Explorer open failed: " + e.Message);
            }
#endif
            if (!string.IsNullOrEmpty(folder))
                Application.OpenURL("file://" + folder.Replace('\\', '/'));
        }
    }
}
