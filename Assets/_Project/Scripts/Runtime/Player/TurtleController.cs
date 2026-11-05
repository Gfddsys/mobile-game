using System;
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

        // Use the body's rotation (not the interpolated transform) for physics decisions.
        Vector2 Forward => Quaternion.Euler(0f, 0f, rb.rotation) * Vector2.right;
        Vector2 Up => Quaternion.Euler(0f, 0f, rb.rotation) * Vector2.up;

        // ---------------- internals ----------------
        GameConfig cfg;
        Rigidbody2D rb;
        CircleCollider2D wheelFront, wheelRear;
        BoxCollider2D deck, hurtBox, feetBox;
        CircleCollider2D shell;
        SpriteRenderer turtleSr, boardSr;
        Transform turtleT;
        ParticleSystem exhaust, sparks, dust, boostTrail;
        GameObject ragdoll;

        // upgrade-derived values
        float thrustMult = 1f, fuelCapacity = 100f, burnMult = 1f, cruiseMult = 1f, traction = 1f, hitLossMult = 1f;
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
        float landingBoostTimer;       // after a Perfect Landing the cruise target stays raised for a while
        Vector2 normalSum; int normalCount;
        Vector2 groundNormal = Vector2.up;
        float lastAngle, rotAccum;
        int announcedFlips;
        bool landedThisAir;
        float superTimer, stallTimer;
        float hitFlash;

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

            // ---- visuals ----
            boardSr = MakeSprite("Board", "board", new Vector2(0f, 0.02f), 10);
            MakeSprite("WheelR", "wheel", wheelRear.offset, 11);
            MakeSprite("WheelF", "wheel", wheelFront.offset, 11);
            string thrusterName = "thruster_" + Mathf.Clamp(save.GetLevel(UpgradeId.JetThruster), 0, 2);
            MakeSprite("Thruster", thrusterName, new Vector2(-1.18f, 0.3f), 9);
            var turtleGo = MakeSprite("TurtleSprite", "turtle", new Vector2(-0.02f, 0.72f), 12);
            turtleSr = turtleGo;
            turtleT = turtleGo.transform;

            BuildParticles();
            lastAngle = rb.rotation;
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

            var rainbow = Gradient2(PixelCanvas.C("#ff4d6d"), PixelCanvas.C("#ffd23f"), PixelCanvas.C("#4dd2ff"));
            boostTrail = MakeParticles("BoostTrail", new Vector2(-0.9f, 0.1f), Quaternion.Euler(0, -90, 0), 0.5f, 2.2f, 0.3f, rainbow, 120, 35f);
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
            r.sortingOrder = 8;
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
                rb.AddTorque((sPitch * cfg.pitchTorque + sFlip * cfg.flipTorque) * airWeight);

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
                fuel = Mathf.Max(0f, fuel - cfg.thrustFuelPerSecond * burnMult * dt);
            }
            var em = exhaust.emission; em.rateOverTime = Thrusting ? 70f : 0f;

            // --- ground slam (Pitch Down) ---
            if (GameInput.Down && !grounded) rb.AddForce(Vector2.down * (cfg.slamAccel * rb.mass));

            // --- auto-cruise & terrain momentum ---
            if (landingBoostTimer > 0f) landingBoostTimer -= dt;
            if (hitInvulnTimer > 0f) hitInvulnTimer -= dt;
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
            var bt = boostTrail.emission; bt.rateOverTime = SuperBoost ? 60f : 0f;

            // --- grind on rails ---
            if (onRail)
            {
                Grinding?.Invoke(dt);
                var se = sparks.emission; se.rateOverTime = 40f;
            }
            else { var se = sparks.emission; se.rateOverTime = 0f; }

            // --- safety: fell far below the terrain ---
            if (GameManager.Instance != null && GameManager.Instance.World != null &&
                rb.position.y < GameManager.Instance.World.HeightAt(rb.position.x) - 18f)
            {
                Crash("Fell off the world");
            }
        }

        void Update()
        {
            if (!Alive) return;
            if (GameInput.JumpPressed) { jumpBuffer = cfg.jumpBufferTime; GameInput.TouchJump = false; GameInput.OverrideJump = false; }

            // duck pose
            float targetY = IsDucking ? 0.6f : 1f;
            var s = turtleT.localScale;
            s.y = Mathf.MoveTowards(s.y, targetY, Time.deltaTime * 8f);
            turtleT.localScale = s;
            // legs sit on the deck top (y = 0.24); sprite centre is 0.5 units above its feet when standing
            turtleT.localPosition = new Vector3(-0.02f, 0.24f + 0.5f * s.y, 0f);

            // super boost flicker / hit flash
            if (hitFlash > 0f) hitFlash -= Time.deltaTime;
            Color c = Color.white;
            if (SuperBoost) c = Color.HSVToRGB(Mathf.Repeat(Time.time * 2f, 1f), 0.45f, 1f);
            if (hitFlash > 0f && ((int)(Time.time * 20f) & 1) == 0) c = new Color(1f, 0.4f, 0.4f);
            turtleSr.color = c;
            boardSr.color = c;

            AudioManager.Thrust(Thrusting);
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
                PickedUp?.Invoke(p.type);
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

            if (grade == LandingGrade.Fatal)
            {
                Landed?.Invoke(grade, angle);
                Crash("Crashed the landing");
                return;
            }

            // bank flips (no accident = they count)
            int flips = Mathf.FloorToInt(Mathf.Abs(rotAccum) / cfg.flipThreshold);
            if (flips > 0)
            {
                FlipsCommitted?.Invoke(flips);
                if (!SuperBoost)
                {
                    Stunt01 = Mathf.Clamp01(Stunt01 + flips * cfg.stuntPerFlip);
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
            SuperBoostChanged?.Invoke(true);
        }

        void EndSuperBoost()
        {
            SuperBoost = false;
            Stunt01 = 0f;
            SuperBoostChanged?.Invoke(false);
        }

        public void Crash(string cause)
        {
            if (!Alive) return;
            Alive = false;
            Thrusting = false;
            AudioManager.Thrust(false);
            var em = exhaust.emission; em.rateOverTime = 0f;
            var bt = boostTrail.emission; bt.rateOverTime = 0f;
            var se = sparks.emission; se.rateOverTime = 0f;
            if (SuperBoost) { SuperBoost = false; SuperBoostChanged?.Invoke(false); }

            // Turtle gets flung off the board and tumbles as a ragdoll (GDD 3.3 / 6)
            var v = rb.linearVelocity;
            turtleSr.enabled = false;
            ragdoll = new GameObject("TurtleRagdoll");
            ragdoll.transform.position = turtleT.position;
            ragdoll.transform.rotation = transform.rotation;
            var sr = ragdoll.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLibrary.Get("turtle");
            sr.sortingOrder = 13;
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

        public void Dispose()
        {
            if (ragdoll != null) Destroy(ragdoll);
            Destroy(gameObject);
        }
    }
}
