using System.IO;
using UnityEditor;
using UnityEditor.Build;
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
        const string PlaceholderDir = "Assets/_Project/Art/Placeholders";

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

        [MenuItem("Turtle Blaster/Art/Export Placeholder Sprites to PNG")]
        public static void ExportPlaceholderSprites()
        {
            Directory.CreateDirectory(PlaceholderDir);
            foreach (var name in SpriteLibrary.Names)
            {
                var sprite = SpriteLibrary.Get(name);
                var tex = sprite.texture;
                string path = PlaceholderDir + "/" + name + ".png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spritePixelsPerUnit = sprite.pixelsPerUnit;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                }
            }
            AssetDatabase.Refresh();
            Debug.Log("[TurtleBlaster] Exported placeholder sprites to " + PlaceholderDir +
                      ". To replace one in the game, put your PNG in Assets/_Project/Resources/Sprites with the SAME file name.");
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
