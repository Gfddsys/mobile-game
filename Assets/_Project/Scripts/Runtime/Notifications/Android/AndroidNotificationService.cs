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
        const string ChannelId = "turtle_blaster_energy_v2";
        const int EnergyFullId = 1001;
        const int TestNotifId = 1002;

        bool initialized;
        bool permissionRequested;
        long lastScheduledFireTicks;

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
                    Importance = Importance.High,
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
            DateTime minFireUtc = DateTime.UtcNow.AddSeconds(2);
            if (fireTimeUtc < minFireUtc) fireTimeUtc = minFireUtc;

            try
            {
                if (lastScheduledFireTicks != 0 &&
                    Math.Abs(fireTimeUtc.Ticks - lastScheduledFireTicks) < TimeSpan.TicksPerSecond * 2 &&
                    AndroidNotificationCenter.CheckScheduledNotificationStatus(EnergyFullId) == NotificationStatus.Scheduled)
                {
                    return;
                }

                AndroidNotificationCenter.CancelDisplayedNotification(EnergyFullId);

                var notification = new AndroidNotification
                {
                    Title = "Energy Full!",
                    Text = "All " + maxEnergy + "/" + maxEnergy + " energy refilled! Time to ride!",
                    FireTime = fireTimeUtc.ToLocalTime(),
                    ShouldAutoCancel = true,
                    ShowInForeground = true
                };
                AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, EnergyFullId);
                lastScheduledFireTicks = fireTimeUtc.Ticks;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] Android schedule failed: " + e.Message);
            }
        }

        public void ScheduleTestNotification(int delaySeconds = 10)
        {
            Initialize();
            int secs = Math.Max(1, delaySeconds);
            try
            {
                AndroidNotificationCenter.CancelDisplayedNotification(TestNotifId);

                var notification = new AndroidNotification
                {
                    Title = "Turtle Blaster Test",
                    Text = "Local notifications are working! (" + secs + "s test)",
                    FireTime = DateTime.Now.AddSeconds(secs),
                    ShouldAutoCancel = true,
                    ShowInForeground = true
                };
                AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, TestNotifId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] Android test schedule failed: " + e.Message);
            }
        }

        public void CancelEnergyFull()
        {
            lastScheduledFireTicks = 0;
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
