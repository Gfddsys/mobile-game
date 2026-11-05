using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurtleBlaster.Tests
{
    /// <summary>
    /// Manual visual check (not part of the normal test run). Renders each screen and saves PNGs to
    /// Builds/Screenshots. Needs a GPU, so do NOT pass -nographics:
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter Screenshots
    /// </summary>
    [Explicit]
    public class ScreenshotTests
    {
        static string Dir => Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Screenshots");

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(Dir);
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator Screenshots_AllScreens()
        {
            foreach (var gm0 in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm0.gameObject);
            PlayerPrefs.DeleteAll(); SaveData.ResetAll();
            SaveData.Current.coins = 260; SaveData.Current.parts = 3; SaveData.Current.bestDistance = 412;

            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return new WaitForSeconds(1.5f);
            yield return Shot("1_menu");

            gm.OpenGarage();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("2_garage");

            gm.OpenSkins();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("2b_skins");
            gm.OpenShop();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("2c_shop");
            var lb = new LocalLeaderboard();
            lb.Submit(new LeaderboardEntry { name = SaveData.Current.playerName, distance = 412, score = 1880, date = "2026-11-05" });
            lb.Submit(new LeaderboardEntry { name = SaveData.Current.playerName, distance = 233, score = 940, date = "2026-11-04" });
            gm.OpenLeaderboard();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("2d_ranks");
            gm.ShowMenu();
            yield return new WaitForSeconds(0.5f);

            gm.StartRun(11);
            GameInput.UseOverride = true;
            float t = 0f;
            while (t < 9f && gm.State == GameState.Playing) { t += Time.deltaTime; yield return null; }
            yield return Shot("3_run");
            // jump in the air with thrust
            GameInput.OverrideUp = true;
            yield return new WaitForSeconds(1.2f);
            yield return Shot("4_run_thrust");
            GameInput.ClearOverride();

            gm.Player.Crash("Hit a cone");
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot("5_crash");
            float guard = 0f;
            while (gm.State != GameState.Reviving && gm.State != GameState.Summary && guard < 6f) { guard += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("5b_revive");
            if (gm.State == GameState.Reviving) gm.UI.ReviveChoose(false);
            guard = 0f;
            while (gm.State != GameState.Summary && guard < 6f) { guard += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("6_summary");
            gm.WatchAdForDoubleCoins();   // opens the test-ad overlay
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("7_test_ad");
            Object.DestroyImmediate(gm.gameObject);
        }
    }
}
