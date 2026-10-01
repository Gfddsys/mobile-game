using System.Collections;
using System.IO;
using UnityEngine;

namespace TurtleBlaster
{
    public enum GameState { Menu, Playing, Paused, Dying, Summary, Garage }

    /// <summary>
    /// Game flow controller (GDD section 2 - Core Game Loop):
    /// Menu -> Start Run -> Playing -> (crash) Dying -> Summary -> Garage/Retry.
    /// Creates every runtime system itself, so a scene only needs this one component
    /// (see Editor menu "Turtle Blaster / Setup / Create Game Scene").
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; } = GameState.Menu;
        public WorldGenerator World { get; private set; }
        public TurtleController Player { get; private set; }
        public CameraFollow Cam { get; private set; }
        public UIRoot UI { get; private set; }
        public RunStats Stats { get; private set; } = new RunStats();
        public int Combo { get; private set; }
        public float ComboMultiplier => 1f + Mathf.Min(Combo, 8) * 0.25f;

        GameConfig cfg;
        int bonusScore;
        float grindScore;
        float startX;
        float menuResetTimer;
        Texture2D snapshot;
        Coroutine dyingRoutine;
        readonly System.Collections.Generic.List<GameObject> owned = new System.Collections.Generic.List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Instance == null && FindFirstObjectByType<GameManager>() == null)
                new GameObject("GameManager").AddComponent<GameManager>();
        }

        // =====================================================================
        // Setup
        // =====================================================================
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            cfg = GameConfig.Instance;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Time.fixedDeltaTime = 1f / 60f;
            Physics2D.gravity = new Vector2(0f, -9.81f);

            // camera
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                owned.Add(camGo);
            }
            Cam = cam.GetComponent<CameraFollow>();
            if (Cam == null) Cam = cam.gameObject.AddComponent<CameraFollow>();
            var bgGo = new GameObject("Background");
            owned.Add(bgGo);
            bgGo.AddComponent<ParallaxBackground>().Init(Cam);
            if (cam.GetComponent<AudioListener>() == null && FindFirstObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            // world, audio, ui
            var worldGo = new GameObject("World");
            owned.Add(worldGo);
            World = worldGo.AddComponent<WorldGenerator>();
            AudioManager.Create(transform);
            UI = new GameObject("UIRoot").AddComponent<UIRoot>();
            UI.transform.SetParent(transform, false);
            UI.Init(this);
        }

        void Start()
        {
            ShowMenu();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Time.timeScale = 1f;
            if (Player != null) Player.Dispose();
            foreach (var go in owned)
            {
                if (go == null) continue;
                go.tag = "Untagged";   // so Camera.main stops returning a camera that is about to die
                Destroy(go);
            }
            owned.Clear();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && State == GameState.Playing) Pause();
        }

        void Update()
        {
            HandleKeyboardShortcuts();

            if (Player != null && World != null)
            {
                World.UpdateAround(Cam.transform.position.x);

                if (State == GameState.Playing && Player.Alive)
                {
                    float dist = Mathf.Max(0f, Player.transform.position.x - startX);
                    Stats.distance = dist;
                    Stats.topSpeed = Mathf.Max(Stats.topSpeed, Player.Speed);
                    Stats.score = Mathf.FloorToInt(dist * cfg.pointsPerMeter) + bonusScore + Mathf.FloorToInt(grindScore);
                }
                else if (State == GameState.Menu)
                {
                    // attract mode: the turtle cruises along by itself behind the menu
                    menuResetTimer += Time.deltaTime;
                    if (!Player.Alive && menuResetTimer > 1.5f || Player.transform.position.x > 700f) StartAttractMode();
                    if (Player.Alive) menuResetTimer = 0f;
                }
            }
        }

        // =====================================================================
        // Keyboard shortcuts (PC testing) - gameplay keys live in GameInput
        // =====================================================================
        void HandleKeyboardShortcuts()
        {
            bool esc = Input.GetKeyDown(KeyCode.Escape);
            bool confirm = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            switch (State)
            {
                case GameState.Menu:
                    if (confirm) StartRun();
                    else if (Input.GetKeyDown(KeyCode.G)) OpenGarage();
                    break;
                case GameState.Playing:
                    if (esc || Input.GetKeyDown(KeyCode.P)) Pause();
                    else if (Input.GetKeyDown(KeyCode.R)) StartRun();   // quick restart
                    break;
                case GameState.Paused:
                    if (esc || Input.GetKeyDown(KeyCode.P) || confirm) Resume();
                    else if (Input.GetKeyDown(KeyCode.Q)) QuitRun();
                    break;
                case GameState.Summary:
                    if (confirm || Input.GetKeyDown(KeyCode.R)) StartRun();
                    else if (Input.GetKeyDown(KeyCode.G)) OpenGarage();
                    else if (esc || Input.GetKeyDown(KeyCode.M)) ShowMenu();
                    break;
                case GameState.Garage:
                    if (esc || Input.GetKeyDown(KeyCode.M)) ShowMenu();
                    else if (confirm) StartRun();
                    else
                        for (int i = 0; i < 4; i++)
                            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) UI.BuyUpgrade((UpgradeId)i);
                    break;
            }
        }

        // =====================================================================
        // Flow
        // =====================================================================
        public void ShowMenu()
        {
            Time.timeScale = 1f;
            State = GameState.Menu;
            StopDying();
            UI.ShowMenu();
            StartAttractMode();
            AudioManager.StartMusic();
        }

        public void OpenGarage()
        {
            Time.timeScale = 1f;
            State = GameState.Garage;
            UI.ShowGarage();
        }

        void StartAttractMode()
        {
            menuResetTimer = 0f;
            SpawnPlayer(Random.Range(0, 100000));
            GameInput.ClearOverride();
        }

        public void StartRun(int seed = -1)
        {
            Time.timeScale = 1f;
            StopDying();
            SpawnPlayer(seed >= 0 ? seed : Random.Range(0, 1000000));
            Stats = new RunStats();
            Combo = 0; bonusScore = 0; grindScore = 0f;
            startX = Player.transform.position.x;
            State = GameState.Playing;
            UI.ShowHud();
            AudioManager.StartMusic();
            UI.Popup("GO!", UIFactory.Accent, 90);
        }

        void SpawnPlayer(int seed)
        {
            if (Player != null) Player.Dispose();
            World.Reset(seed);
            float y = World.HeightAt(0f) + 0.6f;
            Player = TurtleController.Create(new Vector2(0f, y), SaveData.Current);
            Player.Body.linearVelocity = new Vector2(cfg.cruiseSpeed * 0.8f, 0f);
            Hook(Player);
            Cam.SetTarget(Player.FocusTarget, true);
            World.UpdateAround(Cam.transform.position.x);
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            UI.ShowPause(true);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            UI.ShowPause(false);
        }

        public void QuitRun()
        {
            Time.timeScale = 1f;
            ShowMenu();
        }

        void StopDying()
        {
            if (dyingRoutine != null) { StopCoroutine(dyingRoutine); dyingRoutine = null; }
            UI.SetVisible(true);
        }

        // =====================================================================
        // Player events -> score, popups, audio
        // =====================================================================
        void Hook(TurtleController p)
        {
            p.Landed += OnLanded;
            p.FlipInAir += OnFlipInAir;
            p.FlipsCommitted += OnFlipsCommitted;
            p.PickedUp += OnPickedUp;
            p.HazardHandled += OnHazardHandled;
            p.SuperBoostChanged += OnSuperBoost;
            p.Grinding += OnGrinding;
            p.Crashed += OnCrashed;
        }

        bool Active => State == GameState.Playing;

        void OnLanded(LandingGrade grade, float angle)
        {
            if (!Active) return;
                        switch (grade)
            {
                case LandingGrade.Perfect:
                    Combo++;
                    Stats.perfectLandings++;
                    Stats.maxCombo = Mathf.Max(Stats.maxCombo, Combo);
                    bonusScore += Mathf.RoundToInt(cfg.pointsPerPerfect * ComboMultiplier);
                    UI.Popup("PERFECT! +" + Mathf.RoundToInt(cfg.perfectSpeedBoost * 100) + "% SPEED", UIFactory.Good);
                    AudioManager.Sfx("perfect");
                    break;
                case LandingGrade.Clean:
                    UI.Popup("CLEAN LANDING", Color.white, 48);
                    break;
                case LandingGrade.Rough:
                    Combo = 0;
                    UI.Popup("ROUGH LANDING -" + Mathf.RoundToInt(cfg.roughSpeedPenalty * 100) + "%", UIFactory.Bad);
                    AudioManager.Sfx("rough");
                    Cam.Shake(0.25f);
                    break;
            }
        }

        void OnFlipInAir(int count)
        {
            if (!Active) return;
            UI.Popup("FLIP x" + count + "!", UIFactory.Accent, 56);
            AudioManager.Sfx("flip", 0.8f, 1f + count * 0.1f);
        }

        void OnFlipsCommitted(int count)
        {
            if (!Active) return;
            Stats.flips += count;
            int pts = Mathf.RoundToInt(count * cfg.pointsPerFlip * ComboMultiplier);
            bonusScore += pts;
            UI.Popup("+" + pts + " STUNT", new Color(0.6f, 0.9f, 1f), 48);
        }

        void OnPickedUp(PickupType type)
        {
            if (!Active) return;
            if (type == PickupType.Coin)
            {
                Stats.coins += cfg.coinsPerCoinPickup;
                bonusScore += cfg.pointsPerCoin;
                AudioManager.Sfx("coin", 0.6f, 1f + Mathf.Min(Stats.coins % 8, 7) * 0.04f);
            }
            else
            {
                Stats.parts++;
                bonusScore += cfg.pointsPerPart;
                UI.Popup("SPARE PART!", new Color(0.6f, 0.85f, 1f), 52);
                AudioManager.Sfx("part");
            }
        }

        void OnHazardHandled(Hazard hz, bool smashed)
        {
            if (!Active) return;
            if (smashed)
            {
                bonusScore += 25;
                UI.Popup("SMASH!", UIFactory.Accent, 52);
            }
            else
            {
                UI.Popup("BLOCKED! (" + Player.FreeHitsLeft + " left)", new Color(0.6f, 0.85f, 1f), 52);
            }
            AudioManager.Sfx("hit", 0.8f);
            Cam.Shake(smashed ? 0.15f : 0.3f);
        }

        void OnSuperBoost(bool on)
        {
            if (!Active) return;
            if (on)
            {
                UI.Popup("SUPER BOOST!", UIFactory.Accent, 96);
                AudioManager.Sfx("boost");
                Cam.Shake(0.2f);
            }
        }

        void OnGrinding(float dt)
        {
            if (!Active) return;
            Stats.grindTime += dt;
            grindScore += cfg.pointsGrindPerSecond * dt * ComboMultiplier;
        }

        void OnCrashed(string cause)
        {
            if (State == GameState.Menu)
            {
                AudioManager.Sfx("crash", 0.4f);
                return;
            }
            if (State != GameState.Playing) return;
            Stats.causeOfDeath = cause;
            State = GameState.Dying;
            AudioManager.Sfx("crash");
            Cam.Shake(0.6f);
            Cam.SetTarget(Player.FocusTarget, false);
            dyingRoutine = StartCoroutine(DyingRoutine());
        }

        IEnumerator DyingRoutine()
        {
            // brief slow motion on the crash
            Time.timeScale = 0.35f;
            yield return new WaitForSecondsRealtime(0.45f);

            // snapshot of the funniest fall (GDD 2). WaitForEndOfFrame never fires in
            // headless (-nographics) runs, so give it a short timeout and carry on without a picture.
            snapshot = null;
            bool captured = false;
            UI.SetVisible(false);
            StartCoroutine(CaptureRoutine(() => captured = true));
            float waited = 0f;
            while (!captured && waited < 0.5f) { waited += Time.unscaledDeltaTime; yield return null; }
            UI.SetVisible(true);

            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(1.1f);
            FinishRun();
        }

        IEnumerator CaptureRoutine(System.Action done)
        {
            yield return new WaitForEndOfFrame();
            snapshot = CaptureSnapshot();
            done();
        }

        Texture2D CaptureSnapshot()
        {
            try
            {
                var full = ScreenCapture.CaptureScreenshotAsTexture();
                // crop to a 3:2 polaroid window around the centre and shrink it
                int w = full.width, h = full.height;
                int cw = Mathf.Min(w, Mathf.RoundToInt(h * 1.5f));
                int x0 = (w - cw) / 2;
                var rt = RenderTexture.GetTemporary(600, 400, 0);
                var scale = new Vector2((float)cw / w, 1f);
                var offset = new Vector2((float)x0 / w, 0f);
                Graphics.Blit(full, rt, scale, offset);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(600, 400, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
                tex.ReadPixels(new Rect(0, 0, 600, 400), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Destroy(full);
                SaveSnapshotToDisk(tex);
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Snapshot failed: " + e.Message);
                return null;
            }
        }

        static void SaveSnapshotToDisk(Texture2D tex)
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "wipeouts");
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, "wipeout_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"), tex.EncodeToPNG());
            }
            catch (System.Exception) { /* not critical */ }
        }

        void FinishRun()
        {
            var save = SaveData.Current;
            Stats.newBestDistance = Mathf.FloorToInt(Stats.distance) > save.bestDistance;
            Stats.newBestScore = Stats.score > save.bestScore;
            save.bestDistance = Mathf.Max(save.bestDistance, Mathf.FloorToInt(Stats.distance));
            save.bestScore = Mathf.Max(save.bestScore, Stats.score);
            save.coins += Stats.coins;
            save.parts += Stats.parts;
            save.totalRuns++;
            save.Save();

            State = GameState.Summary;
            UI.ShowSummary(Stats, snapshot);
        }
    }
}
