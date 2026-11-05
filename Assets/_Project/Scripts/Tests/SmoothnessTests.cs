using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurtleBlaster.Tests
{
    /// <summary>
    /// Quantifies "feel": sudden speed jumps, rotation jitter while riding, camera snapping.
    /// Numbers are logged with the [SMOOTH] prefix so they can be compared before/after a tuning change.
    /// </summary>
    public class SmoothnessTests
    {
        [SetUp]
        public void SetUp()
        {
            foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm.gameObject);
            PlayerPrefs.DeleteAll(); SaveData.ResetAll();
            GameInput.ClearOverride(); GameInput.ClearTouch();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameInput.ClearOverride();
            foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(gm.gameObject);
        }

        struct Metrics
        {
            public float maxSpeedStep;      // biggest change of HORIZONTAL speed in one physics step (m/s); landing impacts on vy are physical so ignored
            public float hfAngRms;          // high-frequency part of angular velocity while grounded (rad/s) = jitter
            public int speedJumps;          // steps with a change above 1.5 m/s
            public float groundAngVelRms;   // rad/s while grounded
            public int groundSpinFlips;     // sign changes of angular velocity while grounded
            public float maxRotStepDeg;     // biggest rotation in one step while grounded (deg)
            public float maxCamStep;        // biggest camera move in one frame (world units)
            public float maxCamStepRatio;   // ... relative to what the turtle moved that frame
            public float seconds;
        }

        IEnumerator Measure(GameManager gm, int seed, float maxSeconds, System.Action<TurtleController> drive, System.Action<Metrics> done)
        {
            gm.StartRun(seed);
            yield return null;
            var p = gm.Player;
            var m = new Metrics();
            float prevSpeed = p.SpeedX, lpAv = 0f, hfSq = 0f, prevAng = p.Body.angularVelocity, prevRot = p.Body.rotation;
            float sumSq = 0f; int groundSteps = 0;
            var cam = gm.Cam.transform;
            Vector3 prevCam = cam.position, prevPlayer = p.transform.position;
            float t = 0f;
            int frameSkip = 0;

            while (t < maxSeconds && p.Alive && gm.State == GameState.Playing)
            {
                drive?.Invoke(p);
                yield return new WaitForFixedUpdate();
                t += Time.fixedDeltaTime;
                float sp = p.SpeedX;
                float d = Mathf.Abs(sp - prevSpeed);
                if (frameSkip > 5)
                {
                    m.maxSpeedStep = Mathf.Max(m.maxSpeedStep, d);
                    if (d > 1.5f) m.speedJumps++;
                    if (p.IsGrounded)
                    {
                        float av = p.Body.angularVelocity * Mathf.Deg2Rad;
                        sumSq += av * av; groundSteps++;
                        lpAv += (av - lpAv) * 0.07f; float hf = av - lpAv; hfSq += hf * hf;
                        if (Mathf.Sign(av) != Mathf.Sign(prevAng * Mathf.Deg2Rad) && Mathf.Abs(av) > 0.15f) m.groundSpinFlips++;
                        m.maxRotStepDeg = Mathf.Max(m.maxRotStepDeg, Mathf.Abs(Mathf.DeltaAngle(prevRot, p.Body.rotation)));
                    }
                }
                frameSkip++;
                prevSpeed = sp; prevAng = p.Body.angularVelocity; prevRot = p.Body.rotation;

                // camera is moved in LateUpdate of the rendered frame
                yield return null;
                var cp = cam.position; var pp = p.transform.position;
                float camStep = Mathf.Abs(cp.x - prevCam.x) + Mathf.Abs(cp.y - prevCam.y);
                float plStep = Mathf.Abs(pp.x - prevPlayer.x) + Mathf.Abs(pp.y - prevPlayer.y);
                if (frameSkip > 10)
                {
                    m.maxCamStep = Mathf.Max(m.maxCamStep, camStep);
                    if (plStep > 0.01f) m.maxCamStepRatio = Mathf.Max(m.maxCamStepRatio, camStep / plStep);
                }
                prevCam = cp; prevPlayer = pp;
            }
            m.seconds = t;
            m.groundAngVelRms = groundSteps > 0 ? Mathf.Sqrt(sumSq / groundSteps) : 0f;
            m.hfAngRms = groundSteps > 0 ? Mathf.Sqrt(hfSq / groundSteps) : 0f;
            done(m);
        }

        static string Fmt(string name, Metrics m, float flipsPerSec) =>
            "[SMOOTH] " + name + " t=" + m.seconds.ToString("0.0") + "s maxSpeedStep=" + m.maxSpeedStep.ToString("0.00") + " speedJumps=" + m.speedJumps +
            " jitter(hfAng)=" + m.hfAngRms.ToString("0.000") + " groundAngRms=" + m.groundAngVelRms.ToString("0.00") + " spinFlips/s=" + flipsPerSec.ToString("0.0") +
            " maxRotStep=" + m.maxRotStepDeg.ToString("0.00") + " camStep=" + m.maxCamStep.ToString("0.00") + " camRatio=" + m.maxCamStepRatio.ToString("0.00");

        [UnityTest]
        public IEnumerator Riding_HillsAreSmooth()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            Metrics m = default;
            // pure auto-cruise over the hills; stop before the first hazards
            yield return Measure(gm, 11, 9f, null, r => m = r);
            Debug.Log(Fmt("hills", m, m.groundSpinFlips / Mathf.Max(1f, m.seconds)));
            Assert.Less(m.groundAngVelRms, 2.5f, "board wobbles while riding on the ground");
            Assert.Less(m.maxCamStepRatio, 3f, "camera moves much more than the turtle in one frame");
        }

        [UnityTest]
        public IEnumerator Landings_DoNotSnapSpeed()
        {
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            yield return null;
            GameInput.UseOverride = true;
            Metrics m = default;
            float nextJump = 1.5f, tt = 0f;
            yield return Measure(gm, 11, 14f, p =>
            {
                tt += Time.fixedDeltaTime;
                // hop every ~2s and level in the air so landings are Perfect/Clean
                GameInput.OverrideJump = tt > nextJump && p.IsGrounded;
                if (GameInput.OverrideJump) nextJump = tt + 2f;
                GameInput.OverrideLeft = GameInput.OverrideRight = false;
                if (!p.IsGrounded)
                {
                    var rb = p.Body;
                    float desired = gm.World.SlopeDegAt(rb.position.x + Mathf.Max(0.5f, rb.linearVelocity.x * 0.35f));
                    float diff = Mathf.DeltaAngle(rb.rotation, desired) - rb.angularVelocity * 0.12f;
                    GameInput.OverrideLeft = diff > 4f; GameInput.OverrideRight = diff < -4f;
                }
            }, r => m = r);
            GameInput.ClearOverride();
            Debug.Log(Fmt("hops", m, m.groundSpinFlips / Mathf.Max(1f, m.seconds)));
            Assert.Less(m.maxSpeedStep, 3.5f, "speed changed by more than 3.5 m/s in a single physics step");
        }

        [UnityTest, Explicit]
        public IEnumerator Diagnose_JitterVariants()
        {
            var cfg = GameConfig.Instance;
            var backup = Object.Instantiate(cfg);
            var variants = new (string, System.Action)[]
            {
                ("default", () => { }),
                ("noDrive", () => cfg.driveAccel = 0f),
                ("stiff0", () => cfg.groundLevelStiffness = 0f),
                ("damp30", () => cfg.groundLevelDamping = 30f),
                ("grav1", () => cfg.gravityScale = 1f),
                ("noLevelNoDrive", () => { cfg.groundLevelStiffness = 0f; cfg.driveAccel = 0f; cfg.groundLevelDamping = 0f; }),
            };
            foreach (var (name, apply) in variants)
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(backup), cfg);
                apply();
                foreach (var g in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None)) Object.DestroyImmediate(g.gameObject);
                var gm = new GameObject("GameManager").AddComponent<GameManager>();
                yield return null;
                Metrics m = default;
                yield return Measure(gm, 11, 6f, null, r => m = r);
                Debug.Log(Fmt("VAR " + name, m, 0f));
            }
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(backup), cfg);
        }
    }
}
