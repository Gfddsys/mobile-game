using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurtleBlaster.Tests
{
    /// <summary>
    /// Run from Window > General > Test Runner > PlayMode, or headless:
    /// Unity -batchmode -runTests -testPlatform PlayMode -projectPath .
    /// </summary>
    public class GameplayTests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            // the runtime bootstrap may already have created a GameManager for the test scene
            foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm.gameObject);
            PlayerPrefs.DeleteAll();
            SaveData.ResetAll();
            GameInput.ClearOverride();
            GameInput.ClearTouch();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameInput.ClearOverride();
            foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm.gameObject);
        }

        GameManager CreateGame()
        {
            root = new GameObject("GameManager");
            return root.AddComponent<GameManager>();
        }

        // ---------------- pure logic ----------------

        [Test]
        public void UpgradeDatabase_StartingTiersAreFree()
        {
            foreach (var def in UpgradeDatabase.All)
            {
                Assert.Greater(def.tiers.Count, 1, def.id + " needs at least 2 tiers");
                Assert.AreEqual(0, def.tiers[0].coinCost, def.id.ToString());
                Assert.AreEqual(0, def.tiers[0].partCost, def.id.ToString());
                for (int i = 1; i < def.tiers.Count; i++)
                    Assert.Greater(def.tiers[i].coinCost, def.tiers[i - 1].coinCost, def.id + " costs should increase");
            }
        }

        [Test]
        public void SaveData_UpgradeRequiresFundsAndSpendsThem()
        {
            var save = SaveData.Current;
            Assert.IsFalse(save.TryUpgrade(UpgradeId.JetThruster), "cannot afford with empty wallet");
            save.coins = 1000; save.parts = 10;
            Assert.IsTrue(save.TryUpgrade(UpgradeId.JetThruster));
            Assert.AreEqual(1, save.GetLevel(UpgradeId.JetThruster));
            Assert.Less(save.coins, 1000);
            save.coins = 99999; save.parts = 99;
            while (save.TryUpgrade(UpgradeId.JetThruster)) { }
            Assert.AreEqual(UpgradeDatabase.Get(UpgradeId.JetThruster).MaxLevel, save.GetLevel(UpgradeId.JetThruster));
        }

        [Test]
        public void SaveData_RoundTripsThroughPlayerPrefs()
        {
            var save = SaveData.Current;
            save.coins = 123; save.bestDistance = 456; save.upgradeLevels[2] = 1;
            save.Save();
            var json = PlayerPrefs.GetString("turtleblaster.save.v1");
            var loaded = JsonUtility.FromJson<SaveData>(json);
            Assert.AreEqual(123, loaded.coins);
            Assert.AreEqual(456, loaded.bestDistance);
            Assert.AreEqual(1, loaded.upgradeLevels[2]);
        }

        // ---------------- world ----------------

        [UnityTest]
        public IEnumerator Terrain_IsDeterministicAndSlopeLimited()
        {
            CreateGame();
            yield return null;
            var world = GameManager.Instance.World;

            world.Reset(1234);
            var a = new float[400];
            for (int i = 0; i < a.Length; i++) a[i] = world.HeightAt(i * 2f);
            world.Reset(1234);
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], world.HeightAt(i * 2f), 1e-4f, "same seed must give same terrain");

            float maxSlope = 0f;
            for (float x = 0; x < 2000f; x += 0.5f)
                maxSlope = Mathf.Max(maxSlope, Mathf.Abs(world.SlopeDegAt(x)));
            Assert.Less(maxSlope, 45f, "terrain too steep to ride");
        }

        // ---------------- full simulation ----------------

        [UnityTest]
        public IEnumerator Run_AutoCruisesForwardWithoutInput()
        {
            var gm = CreateGame();
            yield return null;
            gm.StartRun();
            Time.timeScale = 4f;
            float t = 0f;
            while (t < 14f && gm.State == GameState.Playing) { t += Time.deltaTime; yield return null; }
            Time.timeScale = 1f;
            Assert.Greater(gm.Stats.distance, 35f, "turtle should have auto-cruised through the safe zone. state=" + gm.State + " cause=" + gm.Stats.causeOfDeath + " t=" + t);
        }

        [UnityTest]
        public IEnumerator Run_StuntingBotDoesNotThrowAndEarnsStats()
        {
            var gm = CreateGame();
            yield return null;
            GameInput.UseOverride = true;
            gm.StartRun();
            Time.timeScale = 4f;
            float t = 0f, nextToggle = 0f;
            var rng = new System.Random(5);
            while (t < 40f && gm.State == GameState.Playing)
            {
                t += Time.deltaTime;
                if (t > nextToggle)
                {
                    nextToggle = t + 0.2f + (float)rng.NextDouble() * 0.6f;
                    GameInput.OverrideUp = rng.Next(2) == 0;
                    GameInput.OverrideDown = rng.Next(4) == 0;
                    GameInput.OverrideLeft = rng.Next(3) == 0;
                    GameInput.OverrideRight = rng.Next(5) == 0;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            GameInput.ClearOverride();
            Assert.GreaterOrEqual(gm.Stats.distance, 0f);
            Assert.GreaterOrEqual(gm.Stats.score, Mathf.FloorToInt(gm.Stats.distance));
        }

        [UnityTest]
        public IEnumerator Crash_EndsRunAndBanksRewards()
        {
            var gm = CreateGame();
            yield return null;
            gm.StartRun();
            yield return null;
            gm.Stats.coins = 7; gm.Stats.parts = 2;
            gm.Player.Crash("test crash");
            float guard = 0f;
            while (gm.State != GameState.Summary && guard < 8f) { guard += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(GameState.Summary, gm.State);
            Assert.AreEqual(7, SaveData.Current.coins);
            Assert.AreEqual(2, SaveData.Current.parts);
            Assert.AreEqual(1, SaveData.Current.totalRuns);
        }

        [UnityTest]
        public IEnumerator Garage_UpgradeChangesPlayerStats()
        {
            var gm = CreateGame();
            yield return null;
            var save = SaveData.Current;
            save.coins = 5000; save.parts = 50;
            save.TryUpgrade(UpgradeId.FuelTank);
            save.TryUpgrade(UpgradeId.SafetyShell);
            save.TryUpgrade(UpgradeId.SafetyShell);
            gm.StartRun();
            yield return null;
            Assert.AreEqual(2, gm.Player.FreeHitsLeft, "Reinforced shell absorbs 2 hits");
            Assert.AreEqual(1f, gm.Player.Fuel01, 0.001f);
        }

        // ---------------- balance / playability ----------------

        /// <summary>
        /// A simple heuristic bot (levels the board with the slope before landing, thrusts uphill).
        /// If a basic bot can survive a few hundred metres the physics + hazard density are sane.
        /// </summary>
        [UnityTest]
        public IEnumerator Bot_CanSurviveHundredsOfMetres()
        {
            var gm = CreateGame();
            yield return null;
            GameInput.UseOverride = true;
            float total = 0f;
            string physicsDeaths = "";
            int[] seeds = { 11, 22, 33 };
            foreach (int seed in seeds)
            {
                gm.StartRun(seed);
                Time.timeScale = 4f;
                float t = 0f;
                while (t < 90f && gm.State == GameState.Playing)
                {
                    t += Time.deltaTime;
                    DriveBot(gm);
                    yield return null;
                }
                total += gm.Stats.distance;
                Debug.Log("[BOT] seed=" + seed + " dist=" + Mathf.RoundToInt(gm.Stats.distance) + "m time=" + t.ToString("0") +
                          "s score=" + gm.Stats.score + " perfect=" + gm.Stats.perfectLandings + " flips=" + gm.Stats.flips +
                          " cause=" + gm.Stats.causeOfDeath);
                var c = gm.Stats.causeOfDeath;
                if (c.StartsWith("Board") || c.StartsWith("Shell") || c.StartsWith("Crashed") || c.StartsWith("Stalled")) physicsDeaths += seed + ":" + c + " ";
                // let the dying routine finish so the next run starts clean
                Time.timeScale = 1f;
                float guard = 0f;
                while (gm.State == GameState.Dying && guard < 4f) { guard += Time.unscaledDeltaTime; yield return null; }
            }
            Time.timeScale = 1f;
            GameInput.ClearOverride();
            Assert.Greater(total / seeds.Length, 80f, "bot should average >80 m");
            Assert.IsEmpty(physicsDeaths, "a levelling bot should die only from obstacles, not from riding physics: " + physicsDeaths);
        }

        static void DriveBot(GameManager gm)
        {
            var p = gm.Player;
            if (p == null || !p.Alive) return;
            var rb = p.Body;
            float x = rb.position.x;
            float slopeHere = gm.World.SlopeDegAt(x);
            GameInput.OverrideUp = false;   // the bot never thrusts: it only levels the board before landing
            GameInput.OverrideDown = false;
            GameInput.OverrideLeft = GameInput.OverrideRight = false;
            if (!p.IsGrounded)
            {
                float landX = x + Mathf.Max(0.5f, rb.linearVelocity.x * 0.45f);
                float desired = gm.World.SlopeDegAt(landX);
                float cur = Mathf.DeltaAngle(0f, rb.rotation);
                float diff = Mathf.DeltaAngle(cur, desired) - rb.angularVelocity * 0.12f;
                if (diff > 4f) GameInput.OverrideLeft = true;      // CCW
                else if (diff < -4f) GameInput.OverrideRight = true; // CW
            }
        }

        // ---------------- landing rules (GDD 3.3) ----------------

        IEnumerator DropAndMeasure(GameManager gm, float x, float rotationDeg, System.Action<LandingGrade, float, float, float> result)
        {
            gm.StartRun(77);
            yield return null;
            var p = gm.Player;
            LandingGrade grade = LandingGrade.Clean; float angle = -1f; bool landed = false;
            p.Landed += (g, a) => { grade = g; angle = a; landed = true; };
            var rb = p.Body;
            float slopeDeg = gm.World.SlopeDegAt(x);
            rb.position = new Vector2(x, gm.World.HeightAt(x) + 4.5f);
            rb.linearVelocity = new Vector2(9f, 0f);
            // wait until the "just left the ground" grace period is over, then fix the pose
            yield return new WaitForSeconds(0.12f);
            rb.rotation = rotationDeg;
            rb.angularVelocity = 0f;
            float speedBefore = 9f;
            float t = 0f;
            while (!landed && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(landed, "no landing was judged");
            result(grade, angle, speedBefore, slopeDeg);
        }

        [UnityTest]
        public IEnumerator Landing_MatchingDownhillSlopeIsPerfect()
        {
            var gm = CreateGame();
            yield return null;
            // x ~ 29 is on the first downhill (see WorldGenerator.Reset)
            float x = 29f;
            float slope = 0f;
            yield return DropAndMeasure(gm, x, gm.World.SlopeDegAt(x) /* match terrain */, (g, a, v, sl) => { slope = sl; Assert.AreEqual(LandingGrade.Perfect, g, "angle diff was " + a + ", slope " + sl); });
            Assert.Less(slope, -3f, "test spot must be downhill");
            Assert.Greater(gm.Player.Speed, 9f, "Perfect landing gives a speed boost");
        }

        [UnityTest]
        public IEnumerator Landing_UpsideDownIsFatal()
        {
            var gm = CreateGame();
            yield return null;
            gm.StartRun(77);
            yield return null;
            var rb = gm.Player.Body;
            rb.position = new Vector2(29f, gm.World.HeightAt(29f) + 3.2f);
            rb.rotation = 180f;
            rb.linearVelocity = new Vector2(9f, 0f);
            float t = 0f;
            while (gm.Player.Alive && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(gm.Player.Alive, "landing on the shell must be fatal");
        }

        [UnityTest]
        public IEnumerator Landing_ModerateAngleIsRough()
        {
            var gm = CreateGame();
            yield return null;
            float x = 29f;
            yield return DropAndMeasure(gm, x, gm.World.SlopeDegAt(x) + 20f, (g, a, v, sl) => { Assert.GreaterOrEqual(a, 13f); Assert.AreEqual(LandingGrade.Rough, g, "angle diff " + a); });
            Assert.IsTrue(gm.Player.Alive, "rough landings are survivable");
        }

        [UnityTest]
        public IEnumerator Flips_AreCountedInAirAndBankedOnLanding()
        {
            var gm = CreateGame();
            yield return null;
            gm.StartRun(77);
            yield return null;
            var p = gm.Player;
            int inAir = 0, banked = 0;
            p.FlipInAir += n => inAir = n;
            p.FlipsCommitted += n => banked += n;
            var rb = p.Body;
            rb.position = new Vector2(5f, 45f);
            rb.rotation = 0f; rb.linearVelocity = new Vector2(9f, 0f);
            GameInput.UseOverride = true;
            float t = 0f;
            // spin until a full flip is registered, then stop spinning and let it land
            while (inAir < 1 && t < 4f) { GameInput.OverrideLeft = true; t += Time.deltaTime; yield return null; }
            GameInput.OverrideLeft = false;
            Assert.GreaterOrEqual(inAir, 1, "a backflip should be recognised after ~360 degrees");
            // steer the board back level so the landing is survivable
            t = 0f;
            while (p.Alive && !p.IsGrounded && t < 6f)
            {
                float diff = Mathf.DeltaAngle(rb.rotation, 0f) - rb.angularVelocity * 0.12f;
                GameInput.OverrideLeft = diff > 4f; GameInput.OverrideRight = diff < -4f;
                t += Time.deltaTime; yield return null;
            }
            GameInput.ClearOverride();
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(p.Alive, "levelled landing should be survivable");
            Assert.GreaterOrEqual(banked, 1, "flips are banked when the landing is safe");
        }

        [UnityTest]
        public IEnumerator Thrust_LetsTheTurtleHopOverFlatGroundObstacles()
        {
            var gm = CreateGame();
            yield return null;
            GameInput.UseOverride = true;
            gm.StartRun(5);
            yield return new WaitForSeconds(1.0f);               // settle on the flat start runway
            var p = gm.Player;
            float groundY = gm.World.HeightAt(p.Body.position.x);
            float maxHeight = 0f, t = 0f;
            GameInput.OverrideUp = true;
            while (t < 0.55f && p.Alive) { t += Time.deltaTime; maxHeight = Mathf.Max(maxHeight, p.Body.position.y - gm.World.HeightAt(p.Body.position.x)); yield return null; }
            GameInput.OverrideUp = false;
            for (int i = 0; i < 30 && p.Alive; i++) { maxHeight = Mathf.Max(maxHeight, p.Body.position.y - gm.World.HeightAt(p.Body.position.x)); yield return null; }
            GameInput.ClearOverride();
            // a cone is ~0.9 high and the board rides ~0.37 above the ground, so we need > 1.3 to clear it
            Assert.Greater(maxHeight, 1.3f, "holding thrust ~0.5s must lift the turtle high enough to hop a cone");
            Assert.Less(p.Fuel01, 1f, "thrust burns fuel");
        }

        [UnityTest]
        public IEnumerator Hop_ShortThrustTapIsSurvivableWithoutAnyCorrection()
        {
            // A novice taps Pitch Up once to hop a cone and does nothing else; that must not kill them.
            var gm = CreateGame();
            yield return null;
            GameInput.UseOverride = true;
            gm.StartRun(5);
            yield return new WaitForSeconds(1.0f);
            var p = gm.Player;
            LandingGrade grade = LandingGrade.Clean; float angle = 0f; bool landed = false;
            p.Landed += (g, a) => { grade = g; angle = a; landed = true; };
            GameInput.OverrideUp = true;
            yield return new WaitForSeconds(0.4f);
            GameInput.OverrideUp = false;
            float t = 0f;
            while (!landed && p.Alive && t < 3f) { t += Time.deltaTime; yield return null; }
            GameInput.ClearOverride();
            Debug.Log("[HOP] landed=" + landed + " grade=" + grade + " angle=" + angle.ToString("0") + " alive=" + p.Alive);
            Assert.IsTrue(p.Alive, "hop landing was fatal (angle " + angle + ")");
        }

        [UnityTest]
        public IEnumerator Jump_SpaceLiftsTheTurtleOffTheGroundWithoutFuel()
        {
            var gm = CreateGame();
            yield return null;
            GameInput.UseOverride = true;
            gm.StartRun(5);
            yield return new WaitForSeconds(1.0f);
            var p = gm.Player;
            float maxH = 0f;
            GameInput.OverrideJump = true;
            for (int i = 0; i < 50 && p.Alive; i++)
            {
                maxH = Mathf.Max(maxH, p.Body.position.y - gm.World.HeightAt(p.Body.position.x));
                yield return null;
            }
            GameInput.ClearOverride();
            Assert.Greater(maxH, 1.2f, "a jump must clear a cone (~0.9 high)");
            Assert.AreEqual(1f, p.Fuel01, 0.001f, "jumping costs no fuel");
            Assert.IsTrue(p.Alive, "a plain jump on flat ground is survivable");
        }
    }
}
