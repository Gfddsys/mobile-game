#if UNITY_ANDROID && UNITY_MOBILE_NOTIFICATIONS
using System;
using System.Collections;
using Unity.Notifications.Android;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Android local notification implementation backed by com.unity.mobile.notifications.
    /// Automatically active when targeting Android with the Mobile Notifications package installed.
    /// </summary>
    public class AndroidNotificationService : INotificationService
    {
        const string ChannelId = "turtle_blaster_energy";
        const int EnergyFullId = 1001;

        bool initialized;
        bool permissionRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Services.Notifications = new AndroidNotificationService();
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;

            try
            {
                var channel = new AndroidNotificationChannel
                {
                    Id = ChannelId,
                    Name = "Energy Refill",
                    Importance = Importance.Default,
                    Description = "Notifies when your energy is full (5/5)"
                };
                AndroidNotificationCenter.RegisterNotificationChannel(channel);

                if (!permissionRequested)
                {
                    permissionRequested = true;
                    Runner.Run(RequestPermission());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] Android init failed: " + e.Message);
            }
        }

        static IEnumerator RequestPermission()
        {
            var req = new PermissionRequest();
            while (req.Status == PermissionStatus.RequestPending) yield return null;
        }

        public void ScheduleEnergyFull(DateTime fireTimeUtc, int maxEnergy)
        {
            Initialize();
            try
            {
                AndroidNotificationCenter.CancelScheduledNotification(EnergyFullId);
                AndroidNotificationCenter.CancelDisplayedNotification(EnergyFullId);

                var notification = new AndroidNotification
                {
                    Title = "Energy Full!",
                    Text = "All " + maxEnergy + "/" + maxEnergy + " energy refilled! Time to ride!",
                    FireTime = fireTimeUtc.ToLocalTime(),
                    ShouldAutoCancel = true
                };
                AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, EnergyFullId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] Android schedule failed: " + e.Message);
            }
        }

        public void CancelEnergyFull()
        {
            try
            {
                AndroidNotificationCenter.CancelScheduledNotification(EnergyFullId);
                AndroidNotificationCenter.CancelDisplayedNotification(EnergyFullId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] Android cancel failed: " + e.Message);
            }
        }

        sealed class Runner : MonoBehaviour
        {
            static Runner instance;

            public static void Run(IEnumerator routine)
            {
                if (!Application.isPlaying) return;
                if (instance == null)
                {
                    var go = new GameObject("NotificationRunner");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Runner>();
                }
                instance.StartCoroutine(routine);
            }
        }
    }
}
#endif
