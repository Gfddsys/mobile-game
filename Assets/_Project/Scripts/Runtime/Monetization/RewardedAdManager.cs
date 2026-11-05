using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;

namespace TurtleBlaster
{
    /// <summary>
    /// LevelPlay rewarded ads (revive + x2 coins). Sits on the persistent "Ads" GameObject next to
    /// LevelPlayInitializer and registers itself as Services.Ads, so the game's existing revive and
    /// x2-coins buttons show real ads. On platforms LevelPlay does not support (PC), the game keeps
    /// using MockAdService.
    /// </summary>
    public class RewardedAdManager : MonoBehaviour, IAdService
    {
        [SerializeField] private string adUnitId = "47vhqd4idq0opuxu";
        [SerializeField] private float retryDelaySeconds = 30f;

        private LevelPlayRewardedAd rewardedAd;
        private bool isLoadingAd;

        // the request currently being shown
        private Action<bool> pendingCallback;
        private bool rewardEarned;

        public bool IsRewardedReady => rewardedAd != null && rewardedAd.IsAdReady();

        void Awake()
        {
#if UNITY_ANDROID || UNITY_IOS
            Services.Ads = this;
            LevelPlayInitializer.Initialized += CreateAd;
            Debug.Log("RewardedAdManager: using LevelPlay rewarded ads (ad unit " + adUnitId + ")");
#else
            Debug.LogWarning("RewardedAdManager: the active build target is not Android/iOS, so LevelPlay cannot load ads " +
                             "(not even mock ads in the Editor). Using the game's TEST AD screen instead. " +
                             "Switch platform in File > Build Profiles > Android to test LevelPlay.");
#endif
        }

        void Start()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (LevelPlayInitializer.IsInitialized) CreateAd();   // initialized before we subscribed
#endif
        }

        void OnDestroy()
        {
            LevelPlayInitializer.Initialized -= CreateAd;
            if (ReferenceEquals(Services.Ads, this)) Services.Ads = new MockAdService();

            if (rewardedAd != null)
            {
                rewardedAd.OnAdLoaded -= OnAdLoaded;
                rewardedAd.OnAdLoadFailed -= OnAdLoadFailed;
                rewardedAd.OnAdDisplayed -= OnAdDisplayed;
                rewardedAd.OnAdDisplayFailed -= OnAdDisplayFailed;
                rewardedAd.OnAdRewarded -= OnAdRewarded;
                rewardedAd.OnAdClosed -= OnAdClosed;
                rewardedAd.OnAdClicked -= OnAdClicked;
                rewardedAd.OnAdInfoChanged -= OnAdInfoChanged;
                rewardedAd.OnAdImpressionDataReady -= OnImpressionDataReady;
            }
        }

        private void CreateAd()
        {
            if (rewardedAd != null) return;

            // Create the ad only after the SDK is initialized
            rewardedAd = new LevelPlayRewardedAd(adUnitId);

            // ILRD (SDK 9.5.0+): impression-level revenue arrives on the ad object
            rewardedAd.OnAdImpressionDataReady += OnImpressionDataReady;

            rewardedAd.OnAdLoaded += OnAdLoaded;
            rewardedAd.OnAdLoadFailed += OnAdLoadFailed;
            rewardedAd.OnAdDisplayed += OnAdDisplayed;
            rewardedAd.OnAdDisplayFailed += OnAdDisplayFailed;
            rewardedAd.OnAdRewarded += OnAdRewarded;
            rewardedAd.OnAdClosed += OnAdClosed;
            rewardedAd.OnAdClicked += OnAdClicked;
            rewardedAd.OnAdInfoChanged += OnAdInfoChanged;

            // Eager preload (conscious choice): the revive offer only stays on screen for a few
            // seconds, so an ad must already be loaded when the player crashes.
            LoadAd();
        }

        public void LoadAd()
        {
            if (rewardedAd == null || isLoadingAd || rewardedAd.IsAdReady()) return;   // avoid error 1037
            isLoadingAd = true;
            Debug.Log("Loading rewarded ad...");
            rewardedAd.LoadAd();
        }

        // ---------------------------------------------------------------- IAdService

        /// <summary>Shows the ad. onComplete(true) only when the player earned the reward.</summary>
        public void ShowRewarded(string placement, Action<bool> onComplete)
        {
            if (pendingCallback != null) { onComplete?.Invoke(false); return; }   // one ad at a time

            if (rewardedAd == null || !rewardedAd.IsAdReady())
            {
                Debug.LogWarning("Rewarded ad is not ready yet");
                onComplete?.Invoke(false);
                LoadAd();
                return;
            }

            pendingCallback = onComplete;
            rewardEarned = false;
            Debug.Log("Showing rewarded ad (" + placement + ")");
            // Placements ("revive", "double_coins") are only passed to ShowAd once they exist in the
            // LevelPlay dashboard; until then the default placement is used.
            rewardedAd.ShowAd();
        }

        private void Complete(bool rewarded)
        {
            var cb = pendingCallback;
            pendingCallback = null;
            cb?.Invoke(rewarded);
        }

        // ---------------------------------------------------------------- callbacks

        private void OnAdLoaded(LevelPlayAdInfo adInfo)
        {
            isLoadingAd = false;
            Debug.Log("Rewarded ad loaded successfully");
        }

        private void OnAdLoadFailed(LevelPlayAdError error)
        {
            isLoadingAd = false;
            Debug.LogWarning($"Rewarded ad failed to load: {error.ErrorCode} {error.ErrorMessage}");
            CancelInvoke(nameof(LoadAd));
            Invoke(nameof(LoadAd), retryDelaySeconds);
        }

        private void OnAdDisplayed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("Rewarded ad displayed");
        }

        private void OnAdDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
        {
            Debug.LogError($"Rewarded ad failed to display: {error.ErrorMessage}");
            Complete(false);
            LoadAd();
        }

        private void OnAdRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
        {
            // Grant here, not in OnAdClosed (the player may close early)
            Debug.Log($"User earned reward: {reward.Amount} {reward.Name}");
            rewardEarned = true;
            Complete(true);
        }

        private void OnAdClosed(LevelPlayAdInfo adInfo)
        {
            Debug.Log("Rewarded ad closed");
            // OnAdRewarded can arrive after OnAdClosed: wait a moment before treating it as "skipped".
            if (pendingCallback != null && !rewardEarned) StartCoroutine(CompleteIfNoReward());
            LoadAd();   // keep one ready for the next crash / summary
        }

        private IEnumerator CompleteIfNoReward()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (pendingCallback != null && !rewardEarned) Complete(false);
        }

        private void OnAdClicked(LevelPlayAdInfo adInfo)
        {
            Debug.Log("Rewarded ad clicked");
        }

        private void OnAdInfoChanged(LevelPlayAdInfo adInfo)
        {
            Debug.Log($"Rewarded ad info changed - network: {adInfo.AdNetwork}, revenue: ${adInfo.Revenue}");
        }

        private void OnImpressionDataReady(LevelPlayImpressionData impressionData)
        {
            // Forward to your analytics platform here (Firebase, AppsFlyer, ...). See docs/MONETIZATION.md.
            Debug.Log($"ILRD: {impressionData.AdNetwork} / {impressionData.AdFormat} / ${impressionData.Revenue}");
        }
    }
}
