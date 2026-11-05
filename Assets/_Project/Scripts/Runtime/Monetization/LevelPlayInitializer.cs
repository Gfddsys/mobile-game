using UnityEngine;
using Unity.Services.LevelPlay;

namespace TurtleBlaster
{
    /// <summary>
    /// Initializes the LevelPlay ads SDK. Lives on the "Ads" GameObject in Game.unity
    /// (added by Turtle Blaster > Setup > 4. Add LevelPlay Ads Object); the App Key is set
    /// in the Inspector on this component. GameManager holds a reference to it.
    /// </summary>
    public class LevelPlayInitializer : MonoBehaviour
    {
        [SerializeField] private string appKey;

        /// <summary>True once LevelPlay reported OnInitSuccess. Ad managers use this so they work no matter
        /// whether their own Start runs before or after initialization.</summary>
        public static bool IsInitialized { get; private set; }
        public static event System.Action Initialized;

        void Awake()
        {
            // Persist across scene loads so ads stay initialized
            DontDestroyOnLoad(gameObject);
            IsInitialized = false;   // statics survive Play sessions when domain reload is off
        }

        void Start()
        {
            // Register initialization callbacks
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;

            // Initialize the SDK with your App Key
            LevelPlay.Init(appKey);
        }

        private void OnInitSuccess(LevelPlayConfiguration config)
        {
            Debug.Log("LevelPlay SDK initialized successfully");
            // SDK is now ready to load ads
            IsInitialized = true;
            Initialized?.Invoke();
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogError($"LevelPlay initialization failed: {error.ErrorMessage}");
        }

        void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnInitSuccess;
            LevelPlay.OnInitFailed -= OnInitFailed;
        }
    }
}
