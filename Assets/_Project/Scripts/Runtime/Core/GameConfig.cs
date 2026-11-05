using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// All gameplay tuning values live here so designers can balance the game
    /// without touching code. Edit Assets/_Project/Resources/GameConfig.asset.
    /// If the asset is missing, defaults below are used.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Turtle Blaster/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Physics (GDD 3.2)")]
        public float gravityScale = 2.4f;
        [Tooltip("Base auto-cruise speed in units/sec")]
        public float cruiseSpeed = 9f;
        [Tooltip("Acceleration applied along the ground while below cruise speed")]
        public float driveAccel = 14f;
        public float maxSpeed = 34f;
        [Tooltip("Torque for Pitch Up / Pitch Down (Up/Down arrow)")]
        public float pitchTorque = 6f;
        [Tooltip("Torque for Backflip / Frontflip (Left/Right arrow)")]
        public float flipTorque = 38f;
        [Tooltip("Angular drag while airborne - gives rotational inertia")]
        public float airAngularDrag = 1.2f;
        [Tooltip("Forward jet acceleration while Pitch Up is held")]
        public float thrustAccel = 20f;
        [Tooltip("Jet acceleration along the board's up axis - lets the turtle hop over flat-ground obstacles (must beat gravity to lift off)")]
        public float thrustLift = 32f;
        public float slamAccel = 30f;
        [Header("Jump (Space)")]
        [Tooltip("Speed added along the ground normal when jumping (units/sec)")]
        public float jumpSpeed = 9f;
        [Tooltip("A jump pressed this long (sec) before touching the ground still fires")]
        public float jumpBufferTime = 0.15f;
        public float jumpCooldown = 0.25f;
        [Header("Smoothness")]
        [Tooltip("Seconds for pitch/flip input to ramp from 0 to full")]
        public float inputRampTime = 0.07f;
        [Tooltip("How fast the measured ground slope is filtered (higher = snappier)")]
        public float slopeFilterRate = 14f;
        [Tooltip("Degrees of error ignored by the ground self-levelling (stops twitching)")]
        public float levelDeadZone = 0.8f;
        [Tooltip("Landing boost / penalty / hit slowdowns are blended in at this rate (1/sec)")]
        public float speedChangeRate = 9f;
        [Tooltip("Strength (0-1) of the weak air self-levelling used when not flipping; 0 = pure inertia")]
        public float airAssistStrength = 0.35f;
        [Tooltip("Air self-levelling switches off above this spin speed (rad/s) so flips keep their inertia")]
        public float airAssistMaxSpin = 2.0f;
        [Tooltip("Seconds over which the jump kick is applied")]
        public float jumpKickTime = 0.08f;

        [Header("Ground handling")]
        [Tooltip("Max nose-up lean (degrees from the slope) while holding Pitch Up on the ground")]
        public float maxWheelieAngle = 12f;
        public float maxNoseDownAngle = 8f;
        public float groundLevelStiffness = 0.8f;
        public float groundLevelDamping = 12f;
        public float groundLevelMaxTorque = 60f;
        [Tooltip("Shell/deck touching the ground faster than this (m/s) is a fatal crash")]
        public float bodyCrashSpeed = 7f;

        [Header("Landing (GDD 3.3)")]
        public float perfectAngle = 15f;
        public float roughAngle = 55f;
        public float perfectSpeedBoost = 0.25f;
        [Tooltip("Seconds the Perfect Landing speed boost is sustained (it eases out during the last second)")]
        public float perfectBoostDuration = 2.5f;
        public float perfectFuelRefund = 0.05f;
        public float roughSpeedPenalty = 0.20f;
        [Tooltip("Minimum air time (sec) for a touchdown to be judged as a landing")]
        public float minAirTimeForLanding = 0.25f;

        [Header("Stunts (GDD 3.4)")]
        [Tooltip("Stunt meter gained per full 360 flip (0-1)")]
        public float stuntPerFlip = 0.34f;
        public float superBoostDuration = 5f;
        public float superBoostSpeedMult = 1.7f;
        [Tooltip("A flip counts when rotation passes this many degrees")]
        public float flipThreshold = 330f;

        [Header("Fuel")]
        public float baseFuelCapacity = 100f;
        public float thrustFuelPerSecond = 16f;

        [Header("Hazards")]
        [Tooltip("Speed lost when hitting a light obstacle with no protection")]
        public float lightHitSpeedLoss = 0.25f;
        [Tooltip("Seconds of immunity to light obstacles after a hit, so one scrape cannot chain into a crash")]
        public float hitInvulnTime = 1.2f;
        public float stallSpeed = 0.7f;
        public float stallTimeToGameOver = 3.5f;

        [Header("Monetization")]
        [Tooltip("A revive is only offered after this many metres")]
        public float reviveMinDistance = 30f;
        [Tooltip("Seconds the revive offer stays on screen")]
        public float reviveOfferSeconds = 5f;
        [Tooltip("Seconds of full invulnerability after a revive")]
        public float reviveShieldSeconds = 3f;
        [Tooltip("Obstacles this far ahead of the revive point are removed")]
        public float reviveClearDistance = 25f;

        [Header("Scoring")]
        public int pointsPerMeter = 1;
        public int pointsPerFlip = 150;
        public int pointsPerPerfect = 200;
        public int pointsPerCoin = 10;
        public int pointsPerPart = 50;
        public int pointsGrindPerSecond = 120;
        public int coinsPerCoinPickup = 1;

        [Header("World generation")]
        public float chunkWidth = 24f;
        public float sampleSpacing = 0.25f;
        public float minHillLength = 12f;
        public float maxHillLength = 30f;
        public float minHillDelta = 1.5f;
        public float maxHillDelta = 5.0f;
        [Tooltip("Distance (m) at which difficulty reaches 1.0")]
        public float difficultyRampMeters = 1500f;

        static GameConfig _instance;

        public static GameConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<GameConfig>("GameConfig");
                    if (_instance == null) _instance = CreateInstance<GameConfig>();
                }
                return _instance;
            }
        }
    }
}
