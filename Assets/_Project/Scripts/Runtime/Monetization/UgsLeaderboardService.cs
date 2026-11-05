using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using UgsModels = Unity.Services.Leaderboards.Models;

namespace TurtleBlaster
{
    /// <summary>
    /// Online leaderboard backed by Unity Gaming Services (UGS) Leaderboards.
    /// Falls back to local storage if offline or if the service is unreachable.
    /// </summary>
    public class UgsLeaderboardService : ILeaderboardService
    {
        public string LeaderboardId { get; set; } = "High_Scores";

        private readonly LocalLeaderboard localFallback = new LocalLeaderboard();
        private bool isInitializing;
        private TaskCompletionSource<bool> initTcs;

        public UgsLeaderboardService(string leaderboardId = "High_Scores")
        {
            LeaderboardId = string.IsNullOrEmpty(leaderboardId) ? "High_Scores" : leaderboardId;
        }

        [Serializable]
        private class EntryMetadata
        {
            public string name;
            public int score;
            public string date;
        }

        /// <summary>
        /// Ensures UnityServices is initialized and the player is signed in anonymously.
        /// </summary>
        public async Task<bool> EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized &&
                AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn)
            {
                return true;
            }

            if (isInitializing && initTcs != null)
                return await initTcs.Task;

            isInitializing = true;
            initTcs = new TaskCompletionSource<bool>();

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    Debug.Log("[UgsLeaderboard] Initializing Unity Services...");
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    Debug.Log("[UgsLeaderboard] Signing in anonymously...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[UgsLeaderboard] Signed in as Player ID: {AuthenticationService.Instance.PlayerId}");
                }

                initTcs.TrySetResult(true);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UgsLeaderboard] Initialization/Sign-in failed (playing offline): {ex.Message}");
                initTcs.TrySetResult(false);
                return false;
            }
            finally
            {
                isInitializing = false;
            }
        }

        public void Submit(LeaderboardEntry entry, Action<int> onRank)
        {
            // 1. Always record in local leaderboard as on-device backup
            int localRank = localFallback.Submit(entry);

            // 2. Submit online to UGS Leaderboards
            SubmitOnline(entry, onRank, localRank);
        }

        private async void SubmitOnline(LeaderboardEntry entry, Action<int> onRank, int localRank)
        {
            try
            {
                bool signedIn = await EnsureSignedInAsync();
                if (!signedIn)
                {
                    onRank?.Invoke(localRank);
                    return;
                }

                // Try updating player name on auth service so UGS displays the name
                if (!string.IsNullOrWhiteSpace(entry.name))
                {
                    try
                    {
                        string sanitized = System.Text.RegularExpressions.Regex.Replace(entry.name.Trim(), @"[^\w]", "_");
                        if (sanitized.Length > 50) sanitized = sanitized.Substring(0, 50);
                        if (sanitized.Length >= 3 && AuthenticationService.Instance.PlayerName != sanitized)
                        {
                            await AuthenticationService.Instance.UpdatePlayerNameAsync(sanitized);
                        }
                    }
                    catch
                    {
                        // Some names or characters may be rejected by auth validation; metadata will still preserve it
                    }
                }

                var meta = new EntryMetadata
                {
                    name = entry.name,
                    score = entry.score,
                    date = entry.date
                };

                var options = new AddPlayerScoreOptions
                {
                    Metadata = meta
                };

                // In Turtle Blaster, the primary competitive score is distance
                var res = await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, entry.distance, options);
                int onlineRank = res.Rank + 1; // UGS rank is 0-indexed, game UI uses 1-indexed

                Debug.Log($"[UgsLeaderboard] Score submitted to '{LeaderboardId}': {entry.distance} m (Rank #{onlineRank})");
                onRank?.Invoke(onlineRank);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UgsLeaderboard] Submit failed, using local rank #{localRank}: {ex.Message}");
                onRank?.Invoke(localRank);
            }
        }

        public void GetTop(int count, Action<IList<LeaderboardEntry>> onResult)
        {
            GetTopOnline(count, onResult);
        }

        private async void GetTopOnline(int count, Action<IList<LeaderboardEntry>> onResult)
        {
            try
            {
                bool signedIn = await EnsureSignedInAsync();
                if (!signedIn)
                {
                    localFallback.GetTop(count, onResult);
                    return;
                }

                var options = new GetScoresOptions
                {
                    Limit = count,
                    IncludeMetadata = true
                };

                var page = await LeaderboardsService.Instance.GetScoresAsync(LeaderboardId, options);
                var list = new List<LeaderboardEntry>();

                if (page?.Results != null)
                {
                    foreach (var item in page.Results)
                    {
                        string name = item.PlayerName;
                        int score = 0;
                        string date = item.UpdatedTime.ToString("yyyy-MM-dd");

                        if (!string.IsNullOrEmpty(item.Metadata))
                        {
                            try
                            {
                                var meta = JsonUtility.FromJson<EntryMetadata>(item.Metadata);
                                if (meta != null)
                                {
                                    if (!string.IsNullOrEmpty(meta.name)) name = meta.name;
                                    score = meta.score;
                                    if (!string.IsNullOrEmpty(meta.date)) date = meta.date;
                                }
                            }
                            catch { }
                        }

                        if (string.IsNullOrEmpty(name))
                        {
                            string pid = item.PlayerId ?? "";
                            name = "Player#" + (pid.Length > 4 ? pid.Substring(0, 4) : pid);
                        }

                        list.Add(new LeaderboardEntry
                        {
                            name = name,
                            distance = Mathf.RoundToInt((float)item.Score),
                            score = score,
                            date = date
                        });
                    }
                }

                if (list.Count == 0)
                {
                    // If online leaderboard is completely empty yet, show local entries if available
                    localFallback.GetTop(count, localList =>
                    {
                        if (localList != null && localList.Count > 0)
                            onResult?.Invoke(localList);
                        else
                            onResult?.Invoke(list);
                    });
                }
                else
                {
                    onResult?.Invoke(list);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UgsLeaderboard] GetTop failed, using local fallback: {ex.Message}");
                localFallback.GetTop(count, onResult);
            }
        }
    }
}
