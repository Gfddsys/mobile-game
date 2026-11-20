using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    public enum LandingGrade { Clean, Perfect, Rough, Fatal }

    /// <summary>
    /// The turtle + skateboard. Implements GDD sections 3.1 - 3.4:
    /// dual-axis control, auto-cruise physics, landing judgement
    /// (Perfect / Rough / Fatal), fuel and the stunt / Super Boost meter.
    /// The object is built in code by <see cref="Create"/> so the whole rig can
    /// be tweaked in one place; an artist can later turn it into a prefab.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TurtleController : MonoBehaviour
    {
        // ---------------- events (GameManager listens to these) ----------------
        public event Action<LandingGrade, float> Landed;          // grade, angle diff
        public event Action<int> FlipInAir;                        // flips completed so far this jump
        public event Action<int> FlipsCommitted;                   // flips banked on a safe landing
        public event Action<string> Crashed;                       // cause
        public event Action<PickupType> PickedUp;
        public event Action<Hazard, bool> HazardHandled;           // hazard, smashed(true) / blocked(false)
        public event Action<Hazard> CloseCall;                     // narrowly dodged an air hazard
        public event Action<bool> SuperBoostChanged;
        public event Action<float> Grinding;                       // dt while grinding

        // ---------------- public state ----------------
        public bool Alive { get; private set; } = true;
        public bool IsGrounded => grounded;
        public bool IsDucking { get; private set; }
        public bool SuperBoost { get; private set; }
        public float Fuel01 => fuelCapacity > 0 ? fuel / fuelCapacity : 0f;
        public float Stunt01 { get; private set; }
        public float Speed => rb.linearVelocity.magnitude;
        public float SpeedX => rb.linearVelocity.x;
        public int FreeHitsLeft => freeHits;
        public bool Thrusting { get; private set; }
        public Transform FocusTarget => ragdoll != null ? ragdoll.transform : transform;
        public Rigidbody2D Body => rb;
        public bool Shielded => shieldTimer > 0f;
        public bool MagnetActive => magnetTimer > 0f;
        public float MagnetTimeLeft => magnetTimer;
        public string SkinId => skin != null ? skin.id : SkinDatabase.DefaultId;
        public SkinDef Skin => skin;

        // Use the body's rotation (not the interpolated transform) for physics decisions.
        Vector2 Forward => Quaternion.Euler(0f, 0f, rb.rotation) * Vector2.right;
        Vector2 Up => Quaternion.Euler(0f, 0f, rb.rotation) * Vector2.up;

        // ---------------- internals ----------------
        GameConfig cfg;
        Rigidbody2D rb;
        CircleCollider2D wheelFront, wheelRear;
        BoxCollider2D deck, hurtBox, feetBox;
        CircleCollider2D shell;
        SpriteRenderer turtleSr, boardSr, auraSr;
        SpriteRenderer flameOuterSr, flameCoreSr, flameGlowSr;
        Transform turtleT, wheelRearT, wheelFrontT, thrusterT;
        TrailRenderer turboTrail;
        ParticleSystem exhaust, sparks, dust, boostTrail;
        GameObject ragdoll;
        readonly Vector2 thrusterBasePos = new Vector2(-1.18f, 0.3f);
        int thrusterTier;

        // upgrade & skin-derived values
        float thrustMult = 1f, fuelCapacity = 100f, burnMult = 1f, cruiseMult = 1f, traction = 1f, hitLossMult = 1f;
        float skinBurnMult = 1f, flipSpeedMult = 1f, stuntGainMult = 1f;
        int freeHits;

        float fuel;
        bool contactThisStep, onRailThisStep, grounded, onRail;
        float groundedTimer, airTime, jumpBuffer, jumpCooldownTimer;

        // smoothing state (see the Smoothness section of GameConfig)
        float smoothSlopeDeg;          // low-pass filtered ground slope angle
        float levelWeight;             // 1 = on the ground (self-levelling), 0 = in the air (free rotation)
        float sPitch, sFlip;           // input values ramped instead of snapping 0 -> 1
        float speedLogRemaining;       // pending speed change, as ln(factor), spread over a few physics steps
        Vector2 jumpDir; float jumpKickLeft;
        float hitInvulnTimer;
        float shieldTimer;             // full invulnerability (after a revive)
        float magnetTimer;             // coin/part magnet power-up timer
        SkinDef skin;
        float landingBoostTimer;       // after a Perfect Landing the cruise target stays raised for a while
        Vector2 normalSum; int normalCount;
        Vector2 groundNormal = Vector2.up;
        float lastAngle, rotAccum;
        int announcedFlips;
        bool landedThisAir;
        float superTimer, stallTimer;
        float hitFlash;

        // visual juice: wheel spin, dynamic rider motion, squash & stretch, ghost afterimages, turbo VFX
        float wheelSpinDeg, wheelSpinRate;
        float turtleBobPhase, turtleLeanDeg, turtleShiftX;
        float duckScaleY = 1f;
        Vector2 squashStretch = Vector2.one;
        float ghostTimer;
        float thrustFlame01, superVisual01, ringTimer;
        int trailMode = -1;
        Gradient superTrailGrad, boostTrailGrad, thrustTrailGrad;
        readonly List<(SpriteRenderer sr, float age)> ghosts = new List<(SpriteRenderer, float)>();
        readonly List<(SpriteRenderer sr, float age, float maxScale)> turboRings = new List<(SpriteRenderer, float, float)>();

        // ================================================================
        // Construction
        // ================================================================
        public static TurtleController Create(Vector2 position, SaveData save)
        {
            var go = new GameObject("Turtle");
            go.transform.position = position;
            var rb = go.AddComponent<Rigidbody2D>();
            var tc = go.AddComponent<TurtleController>();
            tc.Build(rb, save);
            return tc;
        }

        void Build(Rigidbody2D body, SaveData save)
        {
            cfg = GameConfig.Instance;
            rb = body;
            rb.mass = 1f;
            rb.gravityScale = cfg.gravityScale;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            rb.linearDamping = 0.02f;
            rb.angularDamping = cfg.airAngularDrag;
            rb.inertia = 1.2f;

            // ---- upgrades ----
            thrustMult = save.GetTier(UpgradeId.JetThruster).value;
            var tank = save.GetTier(UpgradeId.FuelTank);
            fuelCapacity = cfg.baseFuelCapacity * tank.value;
            burnMult = tank.value2;
            var wheels = save.GetTier(UpgradeId.Wheels);
            cruiseMult = wheels.value;
            traction = wheels.value2;
            var safety = save.GetTier(UpgradeId.SafetyShell);
            freeHits = Mathf.RoundToInt(safety.value);
            hitLossMult = safety.value2;
            fuel = fuelCapacity;

            // ---- physics colliders ----
            var wheelMat = new PhysicsMaterial2D("Wheel") { friction = 0.04f, bounciness = 0f };
            var bodyMat = new PhysicsMaterial2D("Body") { friction = 0.4f, bounciness = 0f };

            wheelRear = AddWheel(new Vector2(-0.66f, -0.15f), wheelMat);
            wheelFront = AddWheel(new Vector2(0.66f, -0.15f), wheelMat);

            deck = gameObject.AddComponent<BoxCollider2D>();
            deck.size = new Vector2(1.9f, 0.18f);
            deck.offset = new Vector2(0f, 0.04f);
            deck.sharedMaterial = bodyMat;

            shell = gameObject.AddComponent<CircleCollider2D>();
            shell.radius = 0.42f;
            shell.offset = new Vector2(-0.05f, 0.58f);
            shell.sharedMaterial = bodyMat;
            deck.excludeLayers = Layers.RailMask;
            shell.excludeLayers = Layers.RailMask;

            // trigger sensors for pickups & obstacles
            hurtBox = gameObject.AddComponent<BoxCollider2D>();
            hurtBox.isTrigger = true;
            feetBox = gameObject.AddComponent<BoxCollider2D>();
            feetBox.isTrigger = true;
            feetBox.size = new Vector2(1.5f, 0.4f);
            feetBox.offset = new Vector2(0f, -0.12f);
            ApplyDuck(false);

            // ---- visuals & skin perks ----
            boardSr = MakeSprite("Board", "board", new Vector2(0f, 0.02f), 13);
            wheelRearT = MakeSprite("WheelR", "wheel", wheelRear.offset, 14).transform;
            wheelFrontT = MakeSprite("WheelF", "wheel", wheelFront.offset, 14).transform;
            thrusterTier = Mathf.Clamp(save.GetLevel(UpgradeId.JetThruster), 0, 2);
            string thrusterName = "thruster_" + thrusterTier;
            thrusterT = MakeSprite("Thruster", thrusterName, thrusterBasePos, 12).transform;
            BuildTurboVfx();
            skin = SkinDatabase.Get(save.selectedSkin);
            ApplySkinPerks(skin, true);

            var turtleGo = MakeSprite("TurtleSprite", "turtle", new Vector2(-0.02f, 0.56f), 15);
            turtleGo.sprite = SpriteLibrary.GetTurtle(skin.id);
            turtleSr = turtleGo;
            turtleT = turtleGo.transform;

            auraSr = MakeSprite("Aura", "shield_orb", new Vector2(-0.04f, 0.58f), 9);
            auraSr.transform.localScale = new Vector3(2.45f, 2.05f, 1f);
            auraSr.enabled = false;

            BuildParticles();
            lastAngle = rb.rotation;
        }

        void BuildTurboVfx()
        {
            flameGlowSr = MakeChildSprite(thrusterT, "FlameGlow", "turbo_glow", new Vector2(-0.06f, 0f), 8);
            flameOuterSr = MakeChildSprite(thrusterT, "FlameOuter", "turbo_flame", new Vector2(0.04f, 0f), 10);
            flameCoreSr = MakeChildSprite(thrusterT, "FlameCore", "turbo_core", new Vector2(0.04f, 0f), 11);
            flameGlowSr.enabled = false;
            flameOuterSr.enabled = false;
            flameCoreSr.enabled = false;

            var trailGo = new GameObject("TurboTrail");
            trailGo.transform.SetParent(thrusterT, false);
            trailGo.transform.localPosition = new Vector3(-0.08f, 0f, 0f);
            turboTrail = trailGo.AddComponent<TrailRenderer>();
            turboTrail.sharedMaterial = SpriteLibrary.SpriteMaterial;
            turboTrail.sortingOrder = 7;
            turboTrail.minVertexDistance = 0.14f;
            turboTrail.numCapVertices = 2;
            turboTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            turboTrail.emitting = false;

            superTrailGrad = Gradient3Alpha(PixelCanvas.C("#ffffff"), PixelCanvas.C("#24f0ff"), PixelCanvas.C("#ff3d9a"), PixelCanvas.C("#ffd93d"), 0.85f);
            boostTrailGrad = Gradient3Alpha(PixelCanvas.C("#ffffff"), PixelCanvas.C("#7df9ff"), PixelCanvas.C("#4dd2ff"), PixelCanvas.C("#3a86ff"), 0.65f);
            thrustTrailGrad = Gradient3Alpha(PixelCanvas.C("#fff3a0"), PixelCanvas.C("#ff9f1c"), PixelCanvas.C("#ff5400"), PixelCanvas.C("#ff5400"), 0.60f);
        }

        static Gradient Gradient3Alpha(Color a, Color b, Color c, Color d, float startAlpha)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.25f), new GradientColorKey(c, 0.65f), new GradientColorKey(d, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha * 0.8f, 0.45f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static SpriteRenderer MakeChildSprite(Transform parent, string name, string sprite, Vector2 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLibrary.Get(sprite);
            sr.sortingOrder = order;
            return sr;
        }

        void ApplySkinPerks(SkinDef s, bool initial)
        {
            if (s == null) return;
            skinBurnMult = s.fuelBurnMult > 0f ? s.fuelBurnMult : 1f;
            flipSpeedMult = s.flipSpeedMult > 0f ? s.flipSpeedMult : 1f;
            stuntGainMult = s.stuntGainMult > 0f ? s.stuntGainMult : 1f;
            if (initial && s.bonusFreeHits > 0) freeHits += s.bonusFreeHits;
        }

        CircleCollider2D AddWheel(Vector2 offset, PhysicsMaterial2D mat)
        {
            var c = gameObject.AddComponent<CircleCollider2D>();
            c.radius = 0.22f; c.offset = offset; c.sharedMaterial = mat;
            return c;
        }

        SpriteRenderer MakeSprite(string name, string sprite, Vector2 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLibrary.Get(sprite);
            sr.sortingOrder = order;
            return sr;
        }

        void BuildParticles()
        {
            var warm = Gradient2(PixelCanvas.C("#fff3a0"), PixelCanvas.C("#ff7a1a"), PixelCanvas.C("#5b5b6b"));
            exhaust = MakeParticles("Exhaust", new Vector2(-1.3f, 0.3f), Quaternion.Euler(0, -90, 0), 0.45f, 5f, 0.22f, warm, 200, 8f);

            var sparkGrad = Gradient2(PixelCanvas.C("#ffffff"), PixelCanvas.C("#ffd23f"), PixelCanvas.C("#ff7a1a"));
            sparks = MakeParticles("Sparks", new Vector2(0f, -0.35f), Quaternion.Euler(-90, 0, 0), 0.4f, 4.5f, 0.1f, sparkGrad, 120, 60f);

            var dustGrad = Gradient2(PixelCanvas.C("#e8dcc0"), PixelCanvas.C("#c9b891"), PixelCanvas.C("#a39373"));
            dust = MakeParticles("Dust", new Vector2(0f, -0.35f), Quaternion.Euler(-90, 0, 0), 0.5f, 2.5f, 0.28f, dustGrad, 80, 70f);

            var rainbow = Gradient2(PixelCanvas.C("#24f0ff"), PixelCanvas.C("#ff3d9a"), PixelCanvas.C("#ffd93d"));
            boostTrail = MakeParticles("BoostTrail", new Vector2(-1.05f, 0.22f), Quaternion.Euler(0, -90, 0), 0.42f, 4.75f, 0.19f, rainbow, 180, 26f);
            var btMain = boostTrail.main;
            btMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.0f);
            btMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            var btr = boostTrail.GetComponent<ParticleSystemRenderer>();
            btr.renderMode = ParticleSystemRenderMode.Stretch;
            btr.velocityScale = 0.07f;
            btr.lengthScale = 2.8f;
        }

        static Gradient Gradient2(Color a, Color b, Color c)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.45f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        ParticleSystem MakeParticles(string name, Vector2 localPos, Quaternion localRot, float life, float speed,
            float size, Gradient color, int max, float spread)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.maxParticles = max;
            main.gravityModifier = name == "Exhaust" || name == "BoostTrail" ? 0f : 0.8f;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = spread; sh.radius = 0.04f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = color;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 0.25f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SpriteLibrary.SpriteMaterial;
            r.sortingOrder = 9;
            ps.Play();
            return ps;
        }

        // ================================================================
        // Physics step
        // ================================================================
        void FixedUpdate()
        {
            if (!Alive) return;
            float dt = Time.fixedDeltaTime;

            // --- ground state from last step's collision callbacks ---
            bool touching = contactThisStep;
            onRail = onRailThisStep;
            groundNormal = normalCount > 0 ? (normalSum / normalCount).normalized : groundNormal;
            contactThisStep = false; onRailThisStep = false; normalSum = Vector2.zero; normalCount = 0;

            bool wasGrounded = grounded;
            groundedTimer = touching ? 0f : groundedTimer + dt;
            grounded = groundedTimer < 0.07f;

            // --- jump (Space): buffered press + coyote time ---
            jumpCooldownTimer -= dt;
            jumpBuffer -= dt;
            if (jumpBuffer > 0f && grounded && jumpCooldownTimer <= 0f)
            {
                var n = normalCount > 0 || groundNormal.sqrMagnitude > 0f ? groundNormal : Vector2.up;
                var v = rb.linearVelocity;
                if (v.y < 0f) v.y = 0f;                       // cancel downhill sink so every jump has the same height
                rb.linearVelocity = v;
                jumpDir = n; jumpKickLeft = cfg.jumpSpeed;    // the kick is applied over ~0.08s below (no velocity snap)
                jumpBuffer = 0f;
                jumpCooldownTimer = cfg.jumpCooldown;
                groundedTimer = 1f; grounded = false;         // leave the ground now (also ends coyote time)
                wasGrounded = true;                           // run the take-off reset below
                squashStretch = new Vector2(0.80f, 1.24f);    // stretch vertically on jump
                dust.Emit(6);
                AudioManager.Sfx("flip", 0.5f, 1.6f);
            }

            if (wasGrounded && !grounded)
            {
                // take-off
                rotAccum = 0f; announcedFlips = 0; landedThisAir = false; airTime = 0f;
            }
            if (!grounded) airTime += dt;

            // --- rotation tracking for flips ---
            float ang = rb.rotation;
            if (!grounded)
            {
                rotAccum += Mathf.DeltaAngle(lastAngle, ang);
                int flips = Mathf.FloorToInt(Mathf.Abs(rotAccum) / cfg.flipThreshold);
                if (flips > announcedFlips) { announcedFlips = flips; FlipInAir?.Invoke(flips); }
            }
            lastAngle = ang;

            // --- input (ramped so torque never snaps on/off) ---
            float pitch = GameInput.Pitch;
            float flip = GameInput.Flip;
            sPitch = Mathf.MoveTowards(sPitch, pitch, dt / cfg.inputRampTime);
            sFlip = Mathf.MoveTowards(sFlip, flip, dt / cfg.inputRampTime);
            bool wantDuck = GameInput.Down;
            if (wantDuck != IsDucking) ApplyDuck(wantDuck);

            // low-pass the measured ground slope: two wheels alternately touching made it chatter
            if (touching)
            {
                float measured = Mathf.Atan2(-groundNormal.x, groundNormal.y) * Mathf.Rad2Deg;
                smoothSlopeDeg = Mathf.LerpAngle(smoothSlopeDeg, measured, 1f - Mathf.Exp(-dt * cfg.slopeFilterRate));
            }

            // hand over smoothly between ground control and air control
            levelWeight = Mathf.MoveTowards(levelWeight, grounded ? 1f : 0f, dt * (grounded ? 12f : 7f));
            if (levelWeight > 0.001f)
            {
                // On the ground the board self-levels to the slope; Pitch Up/Down only leans the nose
                // (wheelie / nose-dive) up to a limit, so riding stays controllable on a touch screen.
                float lean = sPitch > 0f ? sPitch * cfg.maxWheelieAngle : sPitch * cfg.maxNoseDownAngle;
                float err = Mathf.DeltaAngle(rb.rotation, smoothSlopeDeg + lean);
                err = Mathf.Sign(err) * Mathf.Max(0f, Mathf.Abs(err) - cfg.levelDeadZone);   // no twitching around the target
                float tq = err * cfg.groundLevelStiffness - rb.angularVelocity * cfg.groundLevelDamping;
                rb.AddTorque(Mathf.Clamp(tq, -cfg.groundLevelMaxTorque, cfg.groundLevelMaxTorque) * levelWeight);
            }
            float airWeight = 1f - levelWeight;
            if (airWeight > 0.001f)
            {
                // Rotation in the air is pure inertia (GDD 3.2) driven by the player's torque...
                rb.AddTorque((sPitch * cfg.pitchTorque + sFlip * cfg.flipTorque * flipSpeedMult) * airWeight);

                // ...except for a weak "gyro" that levels the board when the player is NOT flipping and the
                // board is not already spinning. A plain hop therefore lands level, while a real flip
                // (flip input, or leftover spin) is never interfered with and still has to be caught by the player.
                float spin = Mathf.Abs(rb.angularVelocity) * Mathf.Deg2Rad;
                float assist = airWeight * Mathf.Clamp01(1f - Mathf.Abs(sFlip) * 4f) * Mathf.Clamp01((cfg.airAssistMaxSpin - spin) / 1f) * cfg.airAssistStrength;
                if (assist > 0.001f)
                {
                    float lean2 = sPitch > 0f ? sPitch * cfg.maxWheelieAngle : sPitch * cfg.maxNoseDownAngle;
                    float err2 = Mathf.DeltaAngle(rb.rotation, smoothSlopeDeg + lean2);
                    float tq2 = err2 * cfg.groundLevelStiffness - rb.angularVelocity * cfg.groundLevelDamping;
                    rb.AddTorque(Mathf.Clamp(tq2, -cfg.groundLevelMaxTorque, cfg.groundLevelMaxTorque) * assist);
                }
            }
            rb.angularDamping = Mathf.Lerp(cfg.airAngularDrag, 0f, levelWeight);

            // --- jump kick, spread over a few steps ---
            if (jumpKickLeft > 0f)
            {
                float chunk = Mathf.Min(jumpKickLeft, cfg.jumpSpeed * dt / cfg.jumpKickTime);
                rb.linearVelocity += jumpDir * chunk;
                jumpKickLeft -= chunk;
            }

            // --- queued speed changes (landing boost / penalty / hit) applied gradually ---
            if (Mathf.Abs(speedLogRemaining) > 0.0005f)
            {
                float part = speedLogRemaining * (1f - Mathf.Exp(-dt * cfg.speedChangeRate));
                speedLogRemaining -= part;
                rb.linearVelocity *= Mathf.Exp(part);
            }

            // --- jet thrust (Pitch Up) ---
            Thrusting = GameInput.Up && fuel > 0f;
            if (Thrusting)
            {
                rb.AddForce((Forward * cfg.thrustAccel + Up * cfg.thrustLift) * (thrustMult * rb.mass));
                fuel = Mathf.Max(0f, fuel - cfg.thrustFuelPerSecond * burnMult * skinBurnMult * dt);
            }
            var em = exhaust.emission; em.rateOverTime = SuperBoost ? (Thrusting ? 120f : 95f) : (Thrusting ? 75f : 0f);
            var emMain = exhaust.main;
            emMain.startColor = SuperBoost
                ? Color.Lerp(PixelCanvas.C("#24f0ff"), PixelCanvas.C("#ffd93d"), 0.5f + 0.5f * Mathf.Sin(Time.time * 18f))
                : Color.white;

            // --- ground slam (Pitch Down) ---
            if (GameInput.Down && !grounded) rb.AddForce(Vector2.down * (cfg.slamAccel * rb.mass));

            // --- auto-cruise & terrain momentum ---
            if (landingBoostTimer > 0f) landingBoostTimer -= dt;
            if (hitInvulnTimer > 0f) hitInvulnTimer -= dt;
            if (shieldTimer > 0f) shieldTimer -= dt;
            if (magnetTimer > 0f) magnetTimer -= dt;
            float boostFade = Mathf.Clamp01(landingBoostTimer / 1.0f);   // holds for most of the duration, then eases out
            float target = cfg.cruiseSpeed * cruiseMult * (SuperBoost ? cfg.superBoostSpeedMult : 1f) * (1f + cfg.perfectSpeedBoost * boostFade);
            if (grounded)
            {
                float sr = smoothSlopeDeg * Mathf.Deg2Rad;
                var tangent = new Vector2(Mathf.Cos(sr), Mathf.Sin(sr));
                float along = Vector2.Dot(rb.linearVelocity, tangent);
                float accel = cfg.driveAccel * traction * (SuperBoost ? 2f : 1f);
                if (along < target) rb.AddForce(tangent * (accel * rb.mass));
                else rb.AddForce(-tangent * ((along - target) * 0.3f * rb.mass)); // bleed off excess speed slowly
            }
            float maxSpeed = cfg.maxSpeed * (SuperBoost ? 1.3f : 1f);
            if (rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;

            // --- stall detection (stuck on a hill with no thrust) ---
            if (grounded && rb.linearVelocity.x < cfg.stallSpeed) stallTimer += dt; else stallTimer = 0f;
            if (stallTimer > cfg.stallTimeToGameOver) { Crash("Stalled out"); return; }

            // --- super boost timer ---
            if (SuperBoost)
            {
                superTimer -= dt;
                Stunt01 = Mathf.Clamp01(superTimer / cfg.superBoostDuration);
                if (superTimer <= 0f) EndSuperBoost();
            }
            var bt = boostTrail.emission; bt.rateOverTime = SuperBoost ? 95f : (landingBoostTimer > 0.15f ? 28f : 0f);

            // --- grind on rails ---
            if (onRail)
            {
                Grinding?.Invoke(dt);
                var se = sparks.emission; se.rateOverTime = 40f;
            }
            else { var se = sparks.emission; se.rateOverTime = 0f; }

            // --- close call / near-miss check on overhead / air hazards ---
            CheckCloseCalls();

            // --- safety: fell far below the terrain ---
            if (GameManager.Instance != null && GameManager.Instance.World != null &&
                rb.position.y < GameManager.Instance.World.HeightAt(rb.position.x) - 18f)
            {
                Crash("Fell off the world");
            }
        }

        void CheckCloseCalls()
        {
            if (hitInvulnTimer > 0f || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            var hits = Physics2D.OverlapCircleAll(rb.position + new Vector2(-0.3f, 0.7f), 2.1f);
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i];
                if (c == null || !c.TryGetComponent<Hazard>(out var hz)) continue;
                if (!hz.isAir || hz.closeCallCounted) continue;
                float dx = rb.position.x - hz.transform.position.x;
                // Trigger once the turtle safely clears the hazard's center horizontally while passing close by
                if (dx > 0.25f && dx < 1.35f)
                {
                    hz.closeCallCounted = true;
                    if (!SuperBoost)
                    {
                        Stunt01 = Mathf.Clamp01(Stunt01 + 0.16f * stuntGainMult);
                        if (Stunt01 >= 1f) StartSuperBoost();
                    }
                    CloseCall?.Invoke(hz);
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateGhosts(dt);
            UpdateTurboRings(dt);
            UpdateWheelSpin(dt);
            if (!Alive) return;
            if (GameInput.JumpPressed) { jumpBuffer = cfg.jumpBufferTime; GameInput.TouchJump = false; GameInput.OverrideJump = false; }

            // keep active turtle skin synced when changed in Skins/Shop menu
            string currentSkinId = SaveData.Current.selectedSkin;
            if (skin == null || skin.id != currentSkinId)
            {
                skin = SkinDatabase.Get(currentSkinId);
                ApplySkinPerks(skin, false);
                if (turtleSr != null) turtleSr.sprite = SpriteLibrary.GetTurtle(skin.id);
            }

            // duck pose + squash & stretch + dynamic riding bob/lean
            squashStretch = Vector2.Lerp(squashStretch, Vector2.one, 1f - Mathf.Exp(-14f * dt));
            float targetY = IsDucking ? 0.6f : 1f;
            duckScaleY = Mathf.MoveTowards(duckScaleY, targetY, dt * 8f);

            turtleBobPhase += dt * (grounded ? (4.8f + Mathf.Clamp(Speed, 0f, 22f) * 0.45f) : 3.2f);
            float bobAmp = grounded ? (onRail ? 0.045f : 0.032f) : 0.018f;
            float bobWave = Mathf.Sin(turtleBobPhase);
            float tuck = (!grounded && Mathf.Abs(sFlip) > 0.1f) ? 0.94f : 1f;

            float sx = squashStretch.x * (IsDucking ? 1.12f : 1f) * (1f - bobWave * (bobAmp * 0.7f)) * tuck;
            float sy = duckScaleY * squashStretch.y * (1f + bobWave * bobAmp) * tuck;
            turtleT.localScale = new Vector3(sx, sy, 1f);

            float targetLeanDeg = grounded ? Mathf.Sin(turtleBobPhase * 0.5f) * 3.0f : 0f;
            float targetShiftX = 0f;
            if (IsDucking)
            {
                targetLeanDeg = -10f;
                targetShiftX = 0.04f;
            }
            else if (SuperBoost)
            {
                targetLeanDeg = -13f + Mathf.Sin(Time.time * 34f) * 1.6f;
                targetShiftX = 0.07f;
            }
            else if (Thrusting)
            {
                targetLeanDeg = -7.5f;
                targetShiftX = 0.04f;
            }
            else if (!grounded)
            {
                targetLeanDeg = Mathf.Abs(sFlip) > 0.1f
                    ? -sFlip * 11f
                    : Mathf.Clamp(rb.linearVelocity.y * 0.9f, -7f, 8f);
            }
            if (onRail) targetLeanDeg += Mathf.Sin(Time.time * 48f) * 2.2f;

            float smoothK = 1f - Mathf.Exp(-14f * dt);
            turtleLeanDeg = Mathf.LerpAngle(turtleLeanDeg, targetLeanDeg, smoothK);
            turtleShiftX = Mathf.Lerp(turtleShiftX, targetShiftX, smoothK);

            turtleT.localRotation = Quaternion.Euler(0f, 0f, turtleLeanDeg);
            // legs sit on the deck top (y = 0.18); sprite centre is 0.38 units above its feet when standing
            turtleT.localPosition = new Vector3(-0.02f + turtleShiftX, 0.18f + 0.38f * sy, 0f);

            // ghost trail afterimages during SuperBoost or Perfect Landing speed boost
            ghostTimer -= dt;
            if ((SuperBoost || landingBoostTimer > 0.15f) && ghostTimer <= 0f)
            {
                ghostTimer = 0.045f;
                SpawnGhost();
            }

            UpdateTurboVisuals(dt);

            // super boost flicker / hit flash
            if (hitFlash > 0f) hitFlash -= dt;
            Color c = Color.white;
            if (skin != null && skin.rainbow) c = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.35f, 1f), 0.35f, 1f);
            if (SuperBoost) c = Color.Lerp(Color.white, EvaluateTurboPalette(Time.time * 1.2f), 0.45f);
            if (hitFlash > 0f && ((int)(Time.time * 20f) & 1) == 0) c = new Color(1f, 0.4f, 0.4f);
            if (shieldTimer > 0f && ((int)(Time.time * 12f) & 1) == 0) c.a = 0.35f;   // blink while shielded
            turtleSr.color = c;
            boardSr.color = c;

            // visual aura for shield / safety shell / magnet / SuperBoost
            if (auraSr != null)
            {
                bool showAura = shieldTimer > 0f || freeHits > 0 || magnetTimer > 0f || SuperBoost;
                auraSr.enabled = showAura;
                if (showAura)
                {
                    auraSr.transform.localPosition = new Vector3(turtleT.localPosition.x - 0.02f, turtleT.localPosition.y + 0.02f, 0f);
                    auraSr.transform.localRotation = Quaternion.Euler(0f, 0f, turtleLeanDeg * 0.45f);

                    float wave = Mathf.Sin(Time.time * 6f) * 0.08f;
                    float duckFactor = Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(0.6f, 1f, duckScaleY));
                    float sxAura = (SuperBoost ? 2.65f : (shieldTimer > 0f ? 2.55f : 2.45f)) + wave;
                    float syAura = ((SuperBoost ? 2.25f : (shieldTimer > 0f ? 2.15f : 2.05f)) + wave) * duckFactor;
                    auraSr.transform.localScale = new Vector3(sxAura, syAura, 1f);

                    Color ac;
                    if (shieldTimer > 0f)
                        ac = new Color(0.35f, 0.92f, 1f, 0.72f);
                    else if (freeHits > 0)
                        ac = new Color(0.35f, 0.92f, 1f, 0.46f);
                    else if (magnetTimer > 0f)
                        ac = new Color(1f, 0.45f, 0.78f, 0.52f);
                    else
                    {
                        ac = EvaluateTurboPalette(Time.time * 1.6f);
                        ac.a = 0.45f;
                    }
                    auraSr.color = ac;
                }
            }

            AudioManager.Thrust(Thrusting);
        }

        void UpdateWheelSpin(float dt)
        {
            if (wheelRearT == null || wheelFrontT == null || rb == null) return;
            float forwardSpeed = Vector2.Dot(rb.linearVelocity, Forward);
            if (grounded || onRail || !Alive)
            {
                // Cap rotation below ~2200 deg/s so 60Hz rendering doesn't strobe backwards.
                float targetRate = Mathf.Clamp(-(forwardSpeed / 0.38f) * Mathf.Rad2Deg, -2200f, 2200f);
                wheelSpinRate = Mathf.Lerp(wheelSpinRate, targetRate, 1f - Mathf.Exp(-20f * dt));
            }
            else
            {
                wheelSpinRate *= Mathf.Exp(-0.85f * dt);
                if (Thrusting || SuperBoost)
                    wheelSpinRate = Mathf.Lerp(wheelSpinRate, -1800f, 1f - Mathf.Exp(-2.5f * dt));
            }
            wheelSpinDeg = Mathf.Repeat(wheelSpinDeg + wheelSpinRate * dt, 360f);
            wheelRearT.localRotation = Quaternion.Euler(0f, 0f, wheelSpinDeg);
            wheelFrontT.localRotation = Quaternion.Euler(0f, 0f, wheelSpinDeg + 47f);
        }

        void UpdateTurboVisuals(float dt)
        {
            float targetFlame = (Thrusting || SuperBoost) ? 1f : 0f;
            thrustFlame01 = Mathf.MoveTowards(thrustFlame01, targetFlame, dt * (targetFlame > thrustFlame01 ? 24f : 14f));
            superVisual01 = Mathf.MoveTowards(superVisual01, SuperBoost ? 1f : 0f, dt * 10f);

            if (thrusterT != null)
            {
                if (thrustFlame01 > 0.02f)
                {
                    float vib = thrustFlame01 * (1f + superVisual01 * 0.6f);
                    thrusterT.localPosition = thrusterBasePos + new Vector2(
                        -0.04f * thrustFlame01 + Mathf.Sin(Time.time * 75f) * 0.014f * vib,
                        Mathf.Cos(Time.time * 88f) * 0.016f * vib);
                }
                else
                {
                    thrusterT.localPosition = Vector2.Lerp(thrusterT.localPosition, thrusterBasePos, 1f - Mathf.Exp(-18f * dt));
                }
            }

            bool showFlame = thrustFlame01 > 0.01f;
            if (flameOuterSr != null)
            {
                flameOuterSr.enabled = showFlame;
                flameCoreSr.enabled = showFlame;
                flameGlowSr.enabled = showFlame;
                if (showFlame)
                {
                    float flicker = 0.86f + 0.14f * Mathf.Sin(Time.time * 58f) + 0.08f * Mathf.Cos(Time.time * 91f);
                    float tierScale = 1f + thrusterTier * 0.14f;
                    float activeBoost = SuperBoost ? (Thrusting ? 1.85f : 1.50f) : 1f;
                    float lenScale = tierScale * Mathf.Lerp(1f, activeBoost, superVisual01) * thrustFlame01 * flicker;
                    float widScale = (1f + thrusterTier * 0.08f) * Mathf.Lerp(1f, SuperBoost && Thrusting ? 1.45f : 1.25f, superVisual01) * Mathf.Lerp(0.55f, 1f, thrustFlame01);

                    flameOuterSr.transform.localScale = new Vector3(lenScale, widScale, 1f);
                    flameCoreSr.transform.localScale = new Vector3(lenScale * 0.92f, widScale * 0.82f, 1f);
                    float glowScale = (1.1f + 0.65f * superVisual01) * thrustFlame01 * (0.9f + 0.12f * flicker);
                    flameGlowSr.transform.localScale = new Vector3(glowScale * 1.15f, glowScale, 1f);

                    Color turboCol = EvaluateTurboPalette(Time.time * 1.4f);
                    Color outerCol = Color.Lerp(PixelCanvas.C("#ff6b1a"), turboCol, superVisual01);
                    outerCol.a = Mathf.Clamp01(thrustFlame01 * 0.95f);
                    flameOuterSr.color = outerCol;

                    Color coreCol = Color.Lerp(PixelCanvas.C("#fff6c2"), PixelCanvas.C("#ffffff"), superVisual01);
                    coreCol.a = Mathf.Clamp01(thrustFlame01);
                    flameCoreSr.color = coreCol;

                    Color glowCol = Color.Lerp(PixelCanvas.C("#ff9f1c"), turboCol, superVisual01);
                    glowCol.a = Mathf.Clamp01(thrustFlame01 * (0.55f + 0.3f * superVisual01));
                    flameGlowSr.color = glowCol;
                }
            }

            if (turboTrail != null)
            {
                int wantMode = SuperBoost ? 2 : (landingBoostTimer > 0.15f ? 1 : (Thrusting ? 0 : -1));
                turboTrail.emitting = wantMode >= 0;
                if (wantMode >= 0 && wantMode != trailMode)
                {
                    trailMode = wantMode;
                    if (wantMode == 2)
                    {
                        turboTrail.time = 0.28f;
                        turboTrail.widthMultiplier = 0.50f;
                        turboTrail.colorGradient = superTrailGrad;
                    }
                    else if (wantMode == 1)
                    {
                        turboTrail.time = 0.18f;
                        turboTrail.widthMultiplier = 0.32f;
                        turboTrail.colorGradient = boostTrailGrad;
                    }
                    else
                    {
                        turboTrail.time = 0.13f;
                        turboTrail.widthMultiplier = 0.24f;
                        turboTrail.colorGradient = thrustTrailGrad;
                    }
                }
            }

            if (SuperBoost)
            {
                ringTimer -= dt;
                if (ringTimer <= 0f)
                {
                    ringTimer = 0.22f;
                    SpawnTurboRing(1.85f);
                }
            }
        }

        static Color EvaluateTurboPalette(float t)
        {
            float p = Mathf.Repeat(t, 4f);
            int idx = Mathf.FloorToInt(p);
            float f = p - idx;
            Color c0 = PixelCanvas.C("#24f0ff");
            Color c1 = PixelCanvas.C("#4d8bff");
            Color c2 = PixelCanvas.C("#ff3d9a");
            Color c3 = PixelCanvas.C("#ffd93d");
            switch (idx)
            {
                case 0: return Color.Lerp(c0, c1, f);
                case 1: return Color.Lerp(c1, c2, f);
                case 2: return Color.Lerp(c2, c3, f);
                default: return Color.Lerp(c3, c0, f);
            }
        }

        void SpawnTurboRing(float maxScale)
        {
            if (thrusterT == null) return;
            var go = new GameObject("TurboRing");
            go.transform.SetPositionAndRotation(thrusterT.TransformPoint(new Vector3(-0.08f, 0f, 0f)), transform.rotation);
            go.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLibrary.Get("turbo_ring");
            sr.sortingOrder = 8;
            Color rc = EvaluateTurboPalette(Time.time * 1.4f);
            rc.a = 0.85f;
            sr.color = rc;
            turboRings.Add((sr, 0f, maxScale));
        }

        void UpdateTurboRings(float dt)
        {
            const float life = 0.26f;
            for (int i = turboRings.Count - 1; i >= 0; i--)
            {
                var (sr, age, maxScale) = turboRings[i];
                if (sr == null) { turboRings.RemoveAt(i); continue; }
                age += dt;
                if (age >= life)
                {
                    Destroy(sr.gameObject);
                    turboRings.RemoveAt(i);
                }
                else
                {
                    float u = age / life;
                    float sc = Mathf.Lerp(0.55f, maxScale, 1f - (1f - u) * (1f - u));
                    sr.transform.localScale = new Vector3(sc * 0.72f, sc, 1f);
                    var rc = sr.color;
                    rc.a = 0.85f * (1f - u);
                    sr.color = rc;
                    turboRings[i] = (sr, age, maxScale);
                }
            }
        }

        void SpawnGhost()
        {
            if (turtleSr == null) return;
            var go = new GameObject("TurtleGhost");
            go.transform.SetPositionAndRotation(turtleT.position, turtleT.rotation);
            go.transform.localScale = turtleT.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = turtleSr.sprite;
            sr.sortingOrder = 7;
            Color gc = SuperBoost
                ? EvaluateTurboPalette(Time.time * 1.5f)
                : new Color(0.4f, 0.9f, 1f, 1f);
            gc.a = 0.48f;
            sr.color = gc;
            ghosts.Add((sr, 0f));
        }

        void UpdateGhosts(float dt)
        {
            const float life = 0.22f;
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var (sr, age) = ghosts[i];
                if (sr == null) { ghosts.RemoveAt(i); continue; }
                age += dt;
                if (age >= life)
                {
                    Destroy(sr.gameObject);
                    ghosts.RemoveAt(i);
                }
                else
                {
                    var c = sr.color;
                    c.a = 0.48f * (1f - age / life);
                    sr.color = c;
                    ghosts[i] = (sr, age);
                }
            }
        }

        void ClearEffects()
        {
            for (int i = 0; i < ghosts.Count; i++)
                if (ghosts[i].sr != null) Destroy(ghosts[i].sr.gameObject);
            ghosts.Clear();
            for (int i = 0; i < turboRings.Count; i++)
                if (turboRings[i].sr != null) Destroy(turboRings[i].sr.gameObject);
            turboRings.Clear();
        }

        void ApplyDuck(bool duck)
        {
            IsDucking = duck;
            if (duck) { hurtBox.size = new Vector2(0.9f, 0.35f); hurtBox.offset = new Vector2(0.05f, 0.42f); }
            else { hurtBox.size = new Vector2(0.9f, 0.8f); hurtBox.offset = new Vector2(0.05f, 0.72f); }
        }

        // ================================================================
        // Collisions
        // ================================================================
        static bool Is(Collider2D a, Collider2D b) => a == b;

        void OnCollisionEnter2D(Collision2D col)
        {
            if (!Alive) return;

            if (col.collider.TryGetComponent<Hazard>(out var hz)) { HitHazard(hz); return; }

            for (int i = 0; i < col.contactCount; i++)
            {
                var cp = col.GetContact(i);
                var mine = Is(cp.otherCollider, deck) || Is(cp.otherCollider, shell) || Is(cp.otherCollider, wheelFront) || Is(cp.otherCollider, wheelRear)
                    ? cp.otherCollider : cp.collider;

                // Head / shell / deck smashing into the ground is always fatal (GDD 3.3)
                if ((Is(mine, shell) || Is(mine, deck)) && col.relativeVelocity.magnitude > cfg.bodyCrashSpeed)
                {
                    Crash(Is(mine, shell) ? "Shell smashed into the ground" : "Board slammed flat");
                    return;
                }

                // Wheel touchdown after being airborne -> judge the landing
                if (!landedThisAir && airTime >= cfg.minAirTimeForLanding)
                {
                    var n = OrientNormal(cp);
                    landedThisAir = true;
                    EvaluateLanding(n);
                    return;
                }
            }
        }

        void OnCollisionStay2D(Collision2D col)
        {
            if (!Alive) return;
            if (col.collider.TryGetComponent<Hazard>(out _)) return;
            bool rail = col.collider.TryGetComponent<Rail>(out _);
            for (int i = 0; i < col.contactCount; i++)
            {
                var cp = col.GetContact(i);
                var mine = (Is(cp.otherCollider, wheelFront) || Is(cp.otherCollider, wheelRear)) ? cp.otherCollider
                         : (Is(cp.collider, wheelFront) || Is(cp.collider, wheelRear)) ? cp.collider : null;
                if (mine == null) continue;
                contactThisStep = true;
                if (rail) onRailThisStep = true;
                normalSum += OrientNormal(cp); normalCount++;
            }
        }

        Vector2 OrientNormal(ContactPoint2D cp)
        {
            var n = cp.normal;
            if (Vector2.Dot(n, rb.position - cp.point) < 0f) n = -n;   // always point away from the ground
            return n.normalized;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!Alive) return;
            if (other.TryGetComponent<Pickup>(out var p))
            {
                if (p.collected) return;
                p.collected = true;
                PickedUp?.Invoke(p.type);
                p.gameObject.SetActive(false);
                Destroy(p.gameObject);
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (!Alive) return;
            if (!other.TryGetComponent<Hazard>(out var hz)) return;
            if (!(hurtBox.IsTouching(other) || feetBox.IsTouching(other))) return;
            HitHazard(hz);
        }

        // ================================================================
        // Gameplay rules
        // ================================================================
        void EvaluateLanding(Vector2 normal)
        {
            float angle = Vector2.Angle(Up, normal);
            bool downhill = normal.x > 0.04f;
            LandingGrade grade;
            if (angle > cfg.roughAngle) grade = LandingGrade.Fatal;
            else if (angle <= cfg.perfectAngle) grade = downhill ? LandingGrade.Perfect : LandingGrade.Clean;
            else grade = LandingGrade.Rough;

            if (grade == LandingGrade.Fatal && shieldTimer > 0f) grade = LandingGrade.Rough;

            if (grade == LandingGrade.Fatal)
            {
                Landed?.Invoke(grade, angle);
                Crash("Crashed the landing");
                return;
            }

            squashStretch = grade == LandingGrade.Rough ? new Vector2(1.32f, 0.68f) : new Vector2(1.22f, 0.78f);

            // bank flips (no accident = they count)
            int flips = Mathf.FloorToInt(Mathf.Abs(rotAccum) / cfg.flipThreshold);
            if (flips > 0)
            {
                FlipsCommitted?.Invoke(flips);
                if (!SuperBoost)
                {
                    Stunt01 = Mathf.Clamp01(Stunt01 + flips * cfg.stuntPerFlip * stuntGainMult);
                    if (Stunt01 >= 1f) StartSuperBoost();
                }
            }
            rotAccum = 0f; announcedFlips = 0;

            var v = rb.linearVelocity;
            switch (grade)
            {
                case LandingGrade.Perfect:
                    landingBoostTimer = cfg.perfectBoostDuration;
                    QueueSpeedFactor(Mathf.Min(1f + cfg.perfectSpeedBoost, cfg.maxSpeed * 1.1f / Mathf.Max(0.1f, v.magnitude)));
                    fuel = Mathf.Min(fuelCapacity, fuel + fuelCapacity * cfg.perfectFuelRefund);
                    dust.Emit(8);
                    break;
                case LandingGrade.Rough:
                    QueueSpeedFactor(1f - cfg.roughSpeedPenalty);
                    dust.Emit(18);
                    hitFlash = 0.35f;
                    break;
                default:
                    dust.Emit(5);
                    break;
            }
            Landed?.Invoke(grade, angle);
        }

        void HitHazard(Hazard hz)
        {
            if (!Alive || hz == null) return;
            if (shieldTimer > 0f) return;      // revive shield: nothing can hurt us

            if (hz.heavy) { Crash("Crushed by a boulder"); return; }

            if (hitInvulnTimer > 0f) return;   // just got hit: ignore light obstacles for a moment

            var col = hz.GetComponent<Collider2D>();

            if (SuperBoost)
            {
                // smash through small obstacles
                sparks.Emit(14);
                HazardHandled?.Invoke(hz, true);
                Destroy(hz.gameObject);
                return;
            }

            if (freeHits > 0)
            {
                freeHits--;
                if (col != null) col.enabled = false;
                QueueSpeedFactor(1f - cfg.lightHitSpeedLoss * hitLossMult);
                hitFlash = 0.6f;
                hitInvulnTimer = cfg.hitInvulnTime;
                dust.Emit(10);
                HazardHandled?.Invoke(hz, false);
                return;
            }

            Crash("Hit a " + hz.DisplayName.ToLower());
        }

        /// <summary>Schedules a speed multiplier that is blended in over a few tenths of a second.</summary>
        void QueueSpeedFactor(float factor)
        {
            speedLogRemaining += Mathf.Log(Mathf.Max(0.05f, factor));
        }

        void StartSuperBoost()
        {
            SuperBoost = true;
            superTimer = cfg.superBoostDuration;
            Stunt01 = 1f;
            squashStretch = new Vector2(1.26f, 0.78f);
            if (boostTrail != null) boostTrail.Emit(24);
            if (sparks != null) sparks.Emit(16);
            SpawnTurboRing(2.35f);
            ringTimer = 0.20f;
            SuperBoostChanged?.Invoke(true);
        }

        void EndSuperBoost()
        {
            SuperBoost = false;
            Stunt01 = 0f;
            SuperBoostChanged?.Invoke(false);
        }

        /// <summary>Full invulnerability for a while (used after a revive). Obstacles, bad landings and body hits are ignored.</summary>
        public void GrantShield(float seconds)
        {
            shieldTimer = Mathf.Max(shieldTimer, seconds);
        }

        public void GrantMagnet(float seconds)
        {
            magnetTimer = Mathf.Max(magnetTimer, seconds);
        }

        public void RefillFuel(float fraction)
        {
            fuel = Mathf.Min(fuelCapacity, fuel + fuelCapacity * Mathf.Clamp01(fraction));
        }

        public void AddFreeHit(int count = 1)
        {
            freeHits = Mathf.Min(5, freeHits + count);
        }

        public void SetFuel01(float f) => fuel = Mathf.Clamp01(f) * fuelCapacity;

        public void Crash(string cause)
        {
            if (!Alive) return;
            if (shieldTimer > 0f && cause != "Fell off the world") return;
            Alive = false;
            Thrusting = false;
            AudioManager.Thrust(false);
            var em = exhaust.emission; em.rateOverTime = 0f;
            var bt = boostTrail.emission; bt.rateOverTime = 0f;
            var se = sparks.emission; se.rateOverTime = 0f;
            if (flameOuterSr != null) flameOuterSr.enabled = false;
            if (flameCoreSr != null) flameCoreSr.enabled = false;
            if (flameGlowSr != null) flameGlowSr.enabled = false;
            if (turboTrail != null) turboTrail.emitting = false;
            if (auraSr != null) auraSr.enabled = false;
            if (SuperBoost) { SuperBoost = false; SuperBoostChanged?.Invoke(false); }

            // Turtle gets flung off the board and tumbles as a ragdoll (GDD 3.3 / 6)
            var v = rb.linearVelocity;
            turtleSr.enabled = false;
            ragdoll = new GameObject("TurtleRagdoll");
            ragdoll.transform.position = turtleT.position;
            ragdoll.transform.rotation = transform.rotation;
            var sr = ragdoll.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLibrary.GetTurtle(SkinId);
            sr.sortingOrder = 16;
            var rrb = ragdoll.AddComponent<Rigidbody2D>();
            rrb.gravityScale = cfg.gravityScale;
            rrb.mass = 0.6f;
            rrb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rrb.linearVelocity = v + new Vector2(1.5f, 5f);
            rrb.angularVelocity = (UnityEngine.Random.value < 0.5f ? -1f : 1f) * UnityEngine.Random.Range(500f, 900f);
            var cc = ragdoll.AddComponent<CircleCollider2D>();
            cc.radius = 0.5f;
            cc.sharedMaterial = new PhysicsMaterial2D("Ragdoll") { friction = 0.5f, bounciness = 0.45f };

            // the abandoned board keeps sliding then stops
            rb.linearDamping = 0.6f;
            rb.angularDamping = 0.5f;

            sparks.Emit(16);
            Crashed?.Invoke(cause);
        }

        void OnDestroy() => ClearEffects();

        public void Dispose()
        {
            ClearEffects();
            if (ragdoll != null) Destroy(ragdoll);
            Destroy(gameObject);
        }
    }
}
