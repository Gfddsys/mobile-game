#if UNITY_IOS && UNITY_MOBILE_NOTIFICATIONS
using System;
using System.Collections;
using Unity.Notifications.iOS;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// iOS local notification implementation backed by com.unity.mobile.notifications.
    /// Automatically active when targeting iOS with the Mobile Notifications package installed.
    /// </summary>
    public class IosNotificationService : INotificationService
    {
        const string EnergyFullIdentifier = "turtle_blaster_energy_full";
        const string TestIdentifier = "turtle_blaster_test_notif";

        bool initialized;
        long lastScheduledFireTicks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Services.Notifications = new IosNotificationService();
        }

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Runner.Run(RequestAuthorization());
        }

        static IEnumerator RequestAuthorization()
        {
            var options = AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;
            using (var req = new AuthorizationRequest(options, false))
            {
                while (!req.IsFinished) yield return null;
            }
        }

        public void ScheduleEnergyFull(DateTime fireTimeUtc, int maxEnergy)
        {
            Initialize();
            DateTime minFireUtc = DateTime.UtcNow.AddSeconds(2);
            if (fireTimeUtc < minFireUtc) fireTimeUtc = minFireUtc;
            if (lastScheduledFireTicks != 0 &&
                Math.Abs(fireTimeUtc.Ticks - lastScheduledFireTicks) < TimeSpan.TicksPerSecond * 2)
            {
                return;
            }

            try
            {
                iOSNotificationCenter.RemoveScheduledNotification(EnergyFullIdentifier);
                iOSNotificationCenter.RemoveDeliveredNotification(EnergyFullIdentifier);

                double seconds = Math.Max(1.0, (fireTimeUtc - DateTime.UtcNow).TotalSeconds);
                var notification = new iOSNotification
                {
                    Identifier = EnergyFullIdentifier,
                    Title = "Energy Full!",
                    Body = "All " + maxEnergy + "/" + maxEnergy + " energy refilled! Time to ride!",
                    ShowInForeground = false,
                    Trigger = new iOSNotificationTimeIntervalTrigger
                    {
                        TimeInterval = TimeSpan.FromSeconds(seconds),
                        Repeats = false
                    }
                };
                iOSNotificationCenter.ScheduleNotification(notification);
                lastScheduledFireTicks = fireTimeUtc.Ticks;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] iOS schedule failed: " + e.Message);
            }
        }

        public void ScheduleTestNotification(int delaySeconds = 10)
        {
            Initialize();
            int secs = Math.Max(1, delaySeconds);
            try
            {
                iOSNotificationCenter.RemoveScheduledNotification(TestIdentifier);
                iOSNotificationCenter.RemoveDeliveredNotification(TestIdentifier);

                var notification = new iOSNotification
                {
                    Identifier = TestIdentifier,
                    Title = "Turtle Blaster Test",
                    Body = "Local notifications are working! (" + secs + "s test)",
                    ShowInForeground = true,
                    Trigger = new iOSNotificationTimeIntervalTrigger
                    {
                        TimeInterval = TimeSpan.FromSeconds(secs),
                        Repeats = false
                    }
                };
                iOSNotificationCenter.ScheduleNotification(notification);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] iOS test schedule failed: " + e.Message);
            }
        }

        public void CancelEnergyFull()
        {
            lastScheduledFireTicks = 0;
            try
            {
                iOSNotificationCenter.RemoveScheduledNotification(EnergyFullIdentifier);
                iOSNotificationCenter.RemoveDeliveredNotification(EnergyFullIdentifier);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Notifications] iOS cancel failed: " + e.Message);
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
