using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TurtleBlaster.EditorTools
{
    /// <summary>
    /// One-click project setup and build helpers. Everything is under the
    /// "Turtle Blaster" menu. "Setup / Run All" is idempotent - safe to re-run.
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Game.unity";
        const string ConfigPath = "Assets/_Project/Resources/GameConfig.asset";
        public const string AppIconPath = "Assets/_Project/Art/AppIcon.png";

        static AddRequest inputSystemAddReq;

        [InitializeOnLoadMethod]
        static void OnEditorLoaded()
        {
            EditorApplication.delayCall += () =>
            {
                EnsureInputSystemPackage();
                var currentIcons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
                if (currentIcons == null || currentIcons.Length == 0 || currentIcons[0] == null)
                    EnsureAppIconAssigned();
            };
        }

        static void EnsureInputSystemPackage()
        {
            if (UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.inputsystem") != null)
                return;
            if (inputSystemAddReq != null) return;

            Debug.Log("[TurtleBlaster] Installing com.unity.inputsystem for Active Input Handling (New)...");
            inputSystemAddReq = Client.Add("com.unity.inputsystem");
            EditorApplication.update += PollInputSystemInstall;
        }

        static void PollInputSystemInstall()
        {
            if (inputSystemAddReq == null || !inputSystemAddReq.IsCompleted) return;
            EditorApplication.update -= PollInputSystemInstall;
            if (inputSystemAddReq.Status == StatusCode.Success)
                Debug.Log("[TurtleBlaster] Installed " + inputSystemAddReq.Result.packageId);
            else if (inputSystemAddReq.Error != null)
                Debug.LogWarning("[TurtleBlaster] Could not auto-install com.unity.inputsystem: " + inputSystemAddReq.Error.message);
            inputSystemAddReq = null;
        }

        [MenuItem("Turtle Blaster/Setup/Run All (settings + config + scene)")]
        public static void RunAll()
        {
            ConfigureProjectSettings();
            CreateGameConfig();
            CreateGameScene();
            Debug.Log("[TurtleBlaster] Setup complete. Open Assets/_Project/Scenes/Game.unity and press Play.");
        }

        /// <summary>Entry point for command line: Unity -batchmode -executeMethod TurtleBlaster.EditorTools.ProjectSetup.RunAllBatch</summary>
        public static void RunAllBatch()
        {
            RunAll();
            EditorApplication.Exit(0);
        }

        [MenuItem("Turtle Blaster/Setup/1. Configure Project Settings (Android landscape)")]
        public static void ConfigureProjectSettings()
        {
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;

            PlayerSettings.companyName = "TurtleBlaster";
            PlayerSettings.productName = "Turtle Blaster";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.turtleblaster.game");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.turtleblaster.game");
            PlayerSettings.bundleVersion = "0.1.0";

            // Landscape only (the dual-thumb control layout needs it)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.useAnimatedAutorotation = true;

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            // Fixed timestep tuned for the physics feel (also set at runtime in GameManager)
            Time.fixedDeltaTime = 1f / 60f;

            EnsureAppIconAssigned();

            AssetDatabase.SaveAssets();
            Debug.Log("[TurtleBlaster] Project settings configured.");
        }

        [MenuItem("Turtle Blaster/Setup/2. Create Game Config Asset")]
        public static void CreateGameConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[TurtleBlaster] Created " + ConfigPath);
        }

        [MenuItem("Turtle Blaster/Setup/3. Create Game Scene")]
        public static void CreateGameScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraFollow>();
            camGo.transform.position = new Vector3(0, 2, -10);

            new GameObject("GameManager").AddComponent<GameManager>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[TurtleBlaster] Created " + ScenePath);
        }

        const string LevelPlayAppKey = "28c4a79cd";

        /// <summary>
        /// Adds a persistent "Ads" GameObject with LevelPlayInitializer (App Key filled in) to Game.unity
        /// and links it to GameManager. Safe to run again: it reuses an existing Ads object.
        /// </summary>
        [MenuItem("Turtle Blaster/Setup/4. Add LevelPlay Ads Object (init + rewarded)")]
        public static void AddLevelPlayAdsObject()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var init = Object.FindFirstObjectByType<LevelPlayInitializer>();
            if (init == null)
            {
                var go = new GameObject("Ads");
                init = go.AddComponent<LevelPlayInitializer>();
                Undo.RegisterCreatedObjectUndo(go, "Add LevelPlay Ads Object");
            }

            // rewarded ads (revive + x2 coins) live on the same persistent object
            if (init.GetComponent<RewardedAdManager>() == null)
                Undo.AddComponent<RewardedAdManager>(init.gameObject);

            var so = new SerializedObject(init);
            var key = so.FindProperty("appKey");
            if (string.IsNullOrEmpty(key.stringValue)) key.stringValue = LevelPlayAppKey;
            so.ApplyModifiedPropertiesWithoutUndo();

            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                var gso = new SerializedObject(gm);
                gso.FindProperty("levelPlayInitializer").objectReferenceValue = init;
                gso.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[TurtleBlaster] No GameManager in " + ScenePath + " - run Setup / 3. Create Game Scene first.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = init.gameObject;
            Debug.Log("[TurtleBlaster] LevelPlay Ads object ready in " + ScenePath + " (App Key: " + so.FindProperty("appKey").stringValue + ").");
        }

        /// <summary>
        /// Renders the Main Menu Title mascot (default turtle riding its skateboard with thruster &amp; flame
        /// launched at 16 deg over the Meadow sky and hills) to <see cref="AppIconPath"/> and sets it as the
        /// Default Icon in PlayerSettings.
        /// </summary>
        [MenuItem("Turtle Blaster/Setup/5. Generate & Assign App Icon (from Title Mascot)")]
        public static void GenerateAndAssignAppIcon()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppIconPath));
            var tex = RenderTitleMascotIcon(512);
            File.WriteAllBytes(AppIconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(AppIconPath, ImportAssetOptions.ForceUpdate);
            EnsureAppIconAssigned();
        }

        public static void EnsureAppIconAssigned()
        {
            if (!File.Exists(AppIconPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppIconPath));
                var generated = RenderTitleMascotIcon(512);
                File.WriteAllBytes(AppIconPath, generated.EncodeToPNG());
                Object.DestroyImmediate(generated);
            }
            AssetDatabase.ImportAsset(AppIconPath, ImportAssetOptions.ForceUpdate);
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if (iconTex != null)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { iconTex }, IconKind.Any);
                AssetDatabase.SaveAssets();
                Debug.Log("[TurtleBlaster] Assigned program icon from " + AppIconPath);
            }
        }

        struct IconLayer
        {
            public Color32[] pixels;
            public int tw, th;
            public float cx, cy, w, h;
            public Color tint;
        }

        static IconLayer MakeIconLayer(string spriteName, float cx, float cy, float w, float h, Color tint)
        {
            var s = SpriteLibrary.CreatePlaceholder(spriteName);
            var tex = s.texture;
            var layer = new IconLayer
            {
                pixels = tex.GetPixels32(),
                tw = tex.width,
                th = tex.height,
                cx = cx,
                cy = cy,
                w = w,
                h = h,
                tint = tint
            };
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(s);
            return layer;
        }

        static Texture2D RenderTitleMascotIcon(int size)
        {
            Color32 skyBot = PixelCanvas.C("#ffd9a0");
            Color32 skyTop = PixelCanvas.C("#5ec2ff");
            Color32 hillFar = PixelCanvas.C("#7fb7d6");
            Color32 hillNear = PixelCanvas.C("#5f9f6e");
            Color32 borderOuter = PixelCanvas.C("#1b1b2f");
            Color32 borderGold = PixelCanvas.C("#ffd147");

            // Exact layers, positions, sizes, and tints from UIRoot.BuildMenu ("TitleMascot")
            var layers = new[]
            {
                MakeIconLayer("turbo_flame", -102f, -13f, 78f,   39f,   new Color(1f, 0.45f, 0.12f, 0.96f)),
                MakeIconLayer("turbo_core",  -92f,  -13f, 49.5f, 22f,   new Color(1f, 0.92f, 0.50f, 0.98f)),
                MakeIconLayer("thruster_1",  -50f,  -13f, 58f,   29f,   Color.white),
                MakeIconLayer("board",        0f,   -24f, 144f,  31.5f, Color.white),
                MakeIconLayer("wheel",       -47f,  -33f, 36f,   36f,   Color.white),
                MakeIconLayer("wheel",        47f,  -33f, 36f,   36f,   Color.white),
                MakeIconLayer("turtle",       -1f,   16f, 117f,  90f,   Color.white),
            };

            float rad = -16f * Mathf.Deg2Rad;
            float cosA = Mathf.Cos(rad);
            float sinA = Mathf.Sin(rad);
            float scale = size / 512f * 2.15f;
            const float centerRx = -24f;
            const float centerRy = 6f;

            var outPixels = new Color32[size * size];
            int block = Mathf.Max(1, size / 64);

            for (int y = 0; y < size; y++)
            {
                int qy = y / block;
                float skyT = Mathf.Pow(qy / 63f, 0.8f);
                Color skyCol = Color.Lerp(skyBot, skyTop, skyT);

                for (int x = 0; x < size; x++)
                {
                    int qx = x / block;
                    Color col = skyCol;

                    float tHill = qx / 64f * Mathf.PI * 2f;
                    int farTop = Mathf.RoundToInt(17f + Mathf.Sin(tHill * 2f) * 3.5f + Mathf.Sin(tHill * 5f + 1f) * 1.8f);
                    int nearTop = Mathf.RoundToInt(11f + Mathf.Sin(tHill * 2f + 0.8f) * 2.5f + Mathf.Sin(tHill * 5f + 2f) * 1.2f);
                    if (qy < nearTop) col = hillNear;
                    else if (qy < farTop) col = hillFar;

                    float rx = (x + 0.5f - size * 0.5f) / scale + centerRx;
                    float ry = (y + 0.5f - size * 0.5f) / scale + centerRy;
                    float lx = rx * cosA - ry * sinA;
                    float ly = rx * sinA + ry * cosA;

                    for (int i = 0; i < layers.Length; i++)
                    {
                        var L = layers[i];
                        float x0 = L.cx - L.w * 0.5f;
                        float y0 = L.cy - L.h * 0.5f;
                        if (lx >= x0 && lx < x0 + L.w && ly >= y0 && ly < y0 + L.h)
                        {
                            int tx = Mathf.Clamp((int)(((lx - x0) / L.w) * L.tw), 0, L.tw - 1);
                            int ty = Mathf.Clamp((int)(((ly - y0) / L.h) * L.th), 0, L.th - 1);
                            Color32 sc = L.pixels[ty * L.tw + tx];
                            if (sc.a > 0)
                            {
                                float sa = (sc.a / 255f) * L.tint.a;
                                float sr = (sc.r / 255f) * L.tint.r;
                                float sg = (sc.g / 255f) * L.tint.g;
                                float sb = (sc.b / 255f) * L.tint.b;
                                col.r = col.r * (1f - sa) + sr * sa;
                                col.g = col.g * (1f - sa) + sg * sa;
                                col.b = col.b * (1f - sa) + sb * sa;
                                col.a = 1f;
                            }
                        }
                    }

                    int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    if (edge < 8) col = borderOuter;
                    else if (edge < 14) col = borderGold;
                    else if (edge < 18) col = borderOuter;

                    outPixels[y * size + x] = col;
                }
            }

            var icon = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            icon.SetPixels32(outPixels);
            icon.Apply(false, false);
            return icon;
        }

        [MenuItem("Turtle Blaster/Debug/Reset Save Data")]
        public static void ResetSave()
        {
            SaveData.ResetAll();
            Debug.Log("[TurtleBlaster] Save data reset.");
        }

        [MenuItem("Turtle Blaster/Debug/Give 1000 Coins + 20 Parts")]
        public static void GiveCurrency()
        {
            var s = SaveData.Current;
            s.coins += 1000; s.parts += 20; s.Save();
            Debug.Log("[TurtleBlaster] Added currency.");
        }

        [MenuItem("Turtle Blaster/Debug/Refill Energy (5/5)")]
        public static void RefillEnergy()
        {
            var s = SaveData.Current;
            s.energy = SaveData.MaxEnergy;
            s.energyNextRefillUtcTicks = 0;
            s.Save();
            if (GameManager.Instance != null) GameManager.Instance.SyncEnergyNotification();
            Debug.Log("[TurtleBlaster] Energy refilled to 5/5.");
        }

        [MenuItem("Turtle Blaster/Debug/Drain Energy (0/5)")]
        public static void DrainEnergy()
        {
            var s = SaveData.Current;
            s.TryConsumeEnergy(s.energy);
            if (GameManager.Instance != null) GameManager.Instance.SyncEnergyNotification();
            Debug.Log("[TurtleBlaster] Energy drained to 0/5.");
        }

        [MenuItem("Turtle Blaster/Debug/Advance Login Day (+1 Day)")]
        public static void AdvanceLoginDay()
        {
            var s = SaveData.Current;
            if (s.lastLoginClaimDay > 0) s.lastLoginClaimDay -= 1;
            s.Save();
            Debug.Log("[TurtleBlaster] Advanced daily login by +1 day.");
        }

        [MenuItem("Turtle Blaster/Debug/Reset Login Streak")]
        public static void ResetLoginStreak()
        {
            var s = SaveData.Current;
            s.loginStreak = 0;
            s.lastLoginClaimDay = 0;
            s.Save();
            Debug.Log("[TurtleBlaster] Daily login streak reset.");
        }

        [MenuItem("Turtle Blaster/Build/Android APK")]
        public static void BuildAndroid()
        {
            ConfigureProjectSettings();
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/TurtleBlaster.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            Debug.Log("[TurtleBlaster] Build result: " + report.summary.result + " -> " + report.summary.outputPath);
        }
    }
}
