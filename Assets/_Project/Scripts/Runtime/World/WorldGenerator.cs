using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Endless procedural world (GDD section 4).
    /// Terrain is a chain of cosine-smoothed hills (control points), meshed and
    /// collided in fixed-width chunks around the camera. Each chunk is then
    /// populated with obstacles, pickups and rails by <see cref="Populate"/>.
    /// Generation is deterministic per seed so a run can be replayed/tested.
    /// </summary>
    public class WorldGenerator : MonoBehaviour
    {
        public const int OrderTerrain = 0;
        public const int OrderDecor = -1;
        public const int OrderProps = 2;
        public const int OrderPickups = 5;

        GameConfig cfg;
        int seed;
        System.Random hillRng;
        bool lastWasUp;

        readonly List<Vector2> cps = new List<Vector2>();
        readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
        readonly List<int> removeBuffer = new List<int>();

        Transform chunkRoot, spawnedRoot;
        PhysicsMaterial2D groundMat, railMat, boulderMat;

        void Awake()
        {
            cfg = GameConfig.Instance;
            chunkRoot = new GameObject("Chunks").transform;
            chunkRoot.SetParent(transform, false);
            spawnedRoot = new GameObject("Spawned").transform;
            spawnedRoot.SetParent(transform, false);

            groundMat = new PhysicsMaterial2D("Ground") { friction = 0.6f, bounciness = 0f };
            railMat = new PhysicsMaterial2D("Rail") { friction = 0.02f, bounciness = 0f };
            boulderMat = new PhysicsMaterial2D("Boulder") { friction = 0.4f, bounciness = 0.1f };
        }

        // -------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------
        public float Difficulty(float x) => Mathf.Clamp01(x / cfg.difficultyRampMeters);

        public void Reset(int newSeed)
        {
            seed = newSeed;
            hillRng = new System.Random(seed);
            foreach (var kv in chunks) if (kv.Value != null) Destroy(kv.Value);
            chunks.Clear();
            for (int i = spawnedRoot.childCount - 1; i >= 0; i--) Destroy(spawnedRoot.GetChild(i).gameObject);

            cps.Clear();
            // Flat start runway, then the first gentle downhill to build momentum.
            cps.Add(new Vector2(-80f, 0f));
            cps.Add(new Vector2(14f, 0f));
            cps.Add(new Vector2(44f, -3.5f));
            lastWasUp = false;
            Despawner.CutoffX = float.NegativeInfinity;
        }

        /// <summary>Removes obstacles between x0 and x1 (used to give a revived player a safe restart).</summary>
        public void ClearHazards(float x0, float x1)
        {
            foreach (var h in spawnedRoot.GetComponentsInChildren<Hazard>())
            {
                float x = h.transform.position.x;
                if (x >= x0 && x <= x1) Destroy(h.gameObject);
            }
        }

        public float HeightAt(float x)
        {
            EnsureControlPoints(x + 60f);
            if (x <= cps[0].x) return cps[0].y;
            int lo = 0, hi = cps.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (cps[mid].x <= x) lo = mid; else hi = mid;
            }
            var a = cps[lo]; var b = cps[hi];
            float t = Mathf.InverseLerp(a.x, b.x, x);
            float s = (1f - Mathf.Cos(t * Mathf.PI)) * 0.5f;
            return Mathf.Lerp(a.y, b.y, s);
        }

        /// <summary>Signed slope angle in degrees. Positive = uphill (in +x direction).</summary>
        public float SlopeDegAt(float x)
        {
            float dy = HeightAt(x + 0.25f) - HeightAt(x - 0.25f);
            return Mathf.Atan2(dy, 0.5f) * Mathf.Rad2Deg;
        }

        /// <summary>Call every frame with the camera X. Streams chunks in/out.</summary>
        public void UpdateAround(float camX)
        {
            Despawner.CutoffX = camX - 50f;
            int first = Mathf.FloorToInt((camX - 35f) / cfg.chunkWidth);
            int last = Mathf.FloorToInt((camX + 95f) / cfg.chunkWidth);
            for (int i = first; i <= last; i++)
                if (!chunks.ContainsKey(i)) chunks[i] = BuildChunk(i);

            removeBuffer.Clear();
            foreach (var kv in chunks) if (kv.Key < first - 1) removeBuffer.Add(kv.Key);
            foreach (var k in removeBuffer) { Destroy(chunks[k]); chunks.Remove(k); }
        }

        // -------------------------------------------------------------
        // Terrain
        // -------------------------------------------------------------
        void EnsureControlPoints(float untilX)
        {
            while (cps[cps.Count - 1].x < untilX)
            {
                var last = cps[cps.Count - 1];
                float d = Difficulty(last.x);
                float delta = Mathf.Lerp(cfg.minHillDelta, Mathf.Lerp(3f, cfg.maxHillDelta, d), (float)hillRng.NextDouble());
                float len = Mathf.Lerp(cfg.minHillLength, cfg.maxHillLength, (float)hillRng.NextDouble()) * Mathf.Lerp(1f, 0.8f, d);
                len = Mathf.Max(len, delta * 2.6f);  // keeps max slope ~30 degrees

                bool up = hillRng.NextDouble() < 0.82 ? !lastWasUp : lastWasUp;
                if (last.y > 9f) up = false;
                if (last.y < -4f) up = true;
                lastWasUp = up;
                cps.Add(new Vector2(last.x + len, last.y + (up ? delta : -delta)));
            }
        }

        GameObject BuildChunk(int index)
        {
            float w = cfg.chunkWidth;
            float x0 = index * w;
            int n = Mathf.RoundToInt(w / cfg.sampleSpacing) + 1;

            var go = new GameObject("Chunk_" + index);
            go.transform.SetParent(chunkRoot, false);
            go.transform.position = new Vector3(x0, 0f, 0f);
            go.AddComponent<GroundSurface>();

            var heights = new float[n];
            float minY = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                heights[i] = HeightAt(x0 + i * cfg.sampleSpacing);
                if (heights[i] < minY) minY = heights[i];
            }
            float bottom = Mathf.Floor((minY - 8f) * 16f) / 16f;

            // ---- simple pixel-style visual mesh: 16-PPU stepped columns with clean flat turf & dirt bands ----
            const float pixSize = 1f / 16f; // 16 pixels per world unit
            const int turfPx = 7;           // uniform 7px (~0.44m) turf strip
            int colsCount = Mathf.RoundToInt(w / pixSize);
            int basePixX = Mathf.RoundToInt(x0 * 16f);

            var allV = new Vector3[colsCount * 8];
            var allUV = new Vector2[colsCount * 8];
            var allC = new Color32[colsCount * 8];
            var tris = new int[colsCount * 12];

            const float texW = SpriteLibrary.TerrainTexWidth;
            const float texH = SpriteLibrary.TerrainTexHeight;
            const float vTurfTop = 1f;
            const float vTurfBot = 152f / texH;
            const float vDirtTopRow = 151.5f;

            for (int c = 0; c < colsCount; c++)
            {
                float lx0 = c * pixSize;
                float lx1 = (c + 1) * pixSize;
                float wx = x0 + lx0 + pixSize * 0.5f;
                int worldPixX = basePixX + c;

                float rawY = HeightAt(wx);
                float yTop = Mathf.Round(rawY * 16f) / 16f;
                float yMid = yTop - turfPx * pixSize;

                Color biomeTint = Color.Lerp(Color.white, ParallaxBackground.EvaluateBiomeTint(wx, out _), 0.32f);
                LevelBiomes.EvaluateGroundColors(wx, out var gA, out var gB, out var dTop, out var dBot);
                Color32 grassA = (Color)gA * biomeTint;
                Color32 grassB = (Color)gB * biomeTint;
                Color32 dirtTop = (Color)dTop * biomeTint;
                Color32 dirtBot = (Color)dBot * biomeTint;

                bool alt = (((worldPixX >> 3) & 1) == 0); // simple 0.5m alternating turf stripes
                Color32 turfCol = alt ? grassA : grassB;

                float u0 = worldPixX / texW;
                float u1 = (worldPixX + 1) / texW;

                // Top quad (clean turf strip with 1px dark top & bottom outline)
                int vi = c * 8;
                allV[vi + 0] = new Vector3(lx0, yTop, 0f);
                allV[vi + 1] = new Vector3(lx1, yTop, 0f);
                allV[vi + 2] = new Vector3(lx1, yMid, 0f);
                allV[vi + 3] = new Vector3(lx0, yMid, 0f);

                allUV[vi + 0] = new Vector2(u0, vTurfTop);
                allUV[vi + 1] = new Vector2(u1, vTurfTop);
                allUV[vi + 2] = new Vector2(u1, vTurfBot);
                allUV[vi + 3] = new Vector2(u0, vTurfBot);

                allC[vi + 0] = turfCol;
                allC[vi + 1] = turfCol;
                allC[vi + 2] = turfCol;
                allC[vi + 3] = turfCol;

                // Bottom quad (clean flat-shaded dirt block)
                float dirtDepthPixels = (yMid - bottom) * 16f;
                float vDirt0 = vDirtTopRow / texH;
                float vDirt1 = (vDirtTopRow - dirtDepthPixels) / texH;

                allV[vi + 4] = new Vector3(lx0, yMid, 0f);
                allV[vi + 5] = new Vector3(lx1, yMid, 0f);
                allV[vi + 6] = new Vector3(lx1, bottom, 0f);
                allV[vi + 7] = new Vector3(lx0, bottom, 0f);

                allUV[vi + 4] = new Vector2(u0, vDirt0);
                allUV[vi + 5] = new Vector2(u1, vDirt0);
                allUV[vi + 6] = new Vector2(u1, vDirt1);
                allUV[vi + 7] = new Vector2(u0, vDirt1);

                allC[vi + 4] = dirtTop;
                allC[vi + 5] = dirtTop;
                allC[vi + 6] = dirtBot;
                allC[vi + 7] = dirtBot;

                int ti = c * 12;
                tris[ti + 0] = vi + 0; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
                tris[ti + 3] = vi + 0; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
                tris[ti + 6] = vi + 4; tris[ti + 7] = vi + 5; tris[ti + 8] = vi + 6;
                tris[ti + 9] = vi + 4; tris[ti + 10] = vi + 6; tris[ti + 11] = vi + 7;
            }

            var mesh = new Mesh { name = "TerrainChunk" };
            mesh.vertices = allV;
            mesh.uv = allUV;
            mesh.colors32 = allC;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SpriteLibrary.TerrainMaterial;
            mr.sortingOrder = OrderTerrain;

            // ---- collider: exact same smooth EdgeCollider2D so riding physics stay identical ----
            var pts = new Vector2[n + 2];
            pts[0] = new Vector2(-cfg.sampleSpacing, HeightAt(x0 - cfg.sampleSpacing));
            for (int i = 0; i < n; i++) pts[i + 1] = new Vector2(i * cfg.sampleSpacing, heights[i]);
            pts[n + 1] = new Vector2(w + cfg.sampleSpacing, HeightAt(x0 + w + cfg.sampleSpacing));
            var edge = go.AddComponent<EdgeCollider2D>();
            edge.points = pts;
            edge.sharedMaterial = groundMat;

            Populate(index, x0, x0 + w);
            SpawnDecorProps(index, x0, x0 + w);
            return go;
        }

        void SpawnDecorProps(int chunkIndex, float x0, float x1)
        {
            var decorRng = new System.Random(seed * 3571 + chunkIndex * 9187);
            float cursor = x0 + 1.5f + (float)decorRng.NextDouble() * 4.5f;
            while (cursor < x1 - 1.5f)
            {
                if (cursor >= 8f)
                {
                    var biome = LevelBiomes.Get(LevelBiomes.GetIndex(cursor));
                    var pool = biome.decorSprites;
                    if (pool != null && pool.Length > 0)
                    {
                        string sprName = pool[decorRng.Next(pool.Length)];
                        float slopeSink = 0.12f + Mathf.Abs(Mathf.Tan(SlopeDegAt(cursor) * Mathf.Deg2Rad)) * 0.45f;
                        var go = MakeSprite("Decor_" + sprName, sprName, new Vector2(cursor, HeightAt(cursor) - slopeSink), 0f, OrderDecor);
                        float sc = 0.78f + (float)decorRng.NextDouble() * 0.22f;
                        float flip = decorRng.Next(2) == 0 ? -1f : 1f;
                        go.transform.localScale = new Vector3(sc * flip, sc, 1f);

                        var sr = go.GetComponent<SpriteRenderer>();
                        Color atmos = Color.Lerp(Color.white, ParallaxBackground.EvaluateBiomeTint(cursor, out _), 0.45f);
                        Color c = biome.decorTint * atmos;
                        c.a = biome.decorTint.a;
                        sr.color = c;
                    }
                }
                cursor += 9.0f + (float)decorRng.NextDouble() * 7.5f;
            }
        }

        // -------------------------------------------------------------
        // Obstacle / pickup population (GDD section 4 + pickups)
        // -------------------------------------------------------------
        bool IsFlat(float x, float length, float maxDeg)
        {
            for (float s = 0; s <= length; s += 1.5f)
                if (Mathf.Abs(SlopeDegAt(x + s)) > maxDeg) return false;
            return true;
        }

        void Populate(int chunkIndex, float x0, float x1)
        {
            var rng = new System.Random(seed * 7919 + chunkIndex * 104729);
            float cursor = x0 + (float)rng.NextDouble() * 4f;

            while (cursor < x1 - 3f)
            {
                if (cursor < 44f) { cursor = 44f; continue; } // safe start zone
                float d = Difficulty(cursor);
                float len = PlacePattern(rng, cursor, d);
                float gap = Mathf.Lerp(16f, 9f, d) * (0.75f + (float)rng.NextDouble() * 0.55f);
                cursor += len + gap;
            }
        }

        float PlacePattern(System.Random rng, float x, float d)
        {
            float hazardChance = Mathf.Lerp(0.24f, 0.56f, d);
            bool hazard = rng.NextDouble() < hazardChance;
            float slope = SlopeDegAt(x);

            if (hazard)
            {
                // weighted pick among eligible hazards
                var options = new List<(int id, int w)>();
                bool flat = IsFlat(x, 5f, 9f);
                if (flat) { options.Add((0, 4)); options.Add((1, 2)); }                       // cone, pothole
                if (flat && d > 0.10f) options.Add((2, 2));                                    // low sign
                if (IsFlat(x, 6f, 7f) && d > 0.20f) options.Add((3, 2));                       // cable
                if (d > 0.15f) options.Add((4, 2));                                            // bird
                if (d > 0.28f && slope > 8f && slope < 20f) options.Add((5, 1));               // boulder
                if (options.Count > 0)
                {
                    int total = 0; foreach (var o in options) total += o.w;
                    int roll = rng.Next(total);
                    foreach (var o in options)
                    {
                        if (roll < o.w) return SpawnHazardPattern(o.id, rng, x, d);
                        roll -= o.w;
                    }
                }
            }

            // reward patterns
            double r = rng.NextDouble();
            if (r < 0.28 && IsFlat(x, 9f, 7f)) return SpawnRail(rng, x);
            if (r < 0.62) return SpawnCoinLine(x, 5 + rng.Next(4), 1.0f);
            if (r < 0.90) return SpawnCoinArc(x, 7, 1.1f, 2.4f + (float)rng.NextDouble() * 1.4f);
            return SpawnPart(x);
        }

        float SpawnHazardPattern(int id, System.Random rng, float x, float d)
        {
            switch (id)
            {
                case 0: // cones (1 to 2 max so a single jump always clears them without needing fuel)
                {
                    int count = (d > 0.35f && rng.NextDouble() < 0.45) ? 2 : 1;
                    for (int i = 0; i < count; i++) SpawnCone(x + i * 0.78f);
                    return count * 0.78f + 1.5f;
                }
                case 1: SpawnPothole(x + 1.2f); return 3.2f;
                case 2: // low sign with a coin trail under it (rewards ducking)
                {
                    SpawnSign(x + 2f);
                    for (int i = -1; i <= 1; i++) SpawnCoin(x + 2f + i * 1.0f, 0.55f);
                    return 4.5f;
                }
                case 3:
                {
                    SpawnCable(x + 3f);
                    for (int i = -1; i <= 1; i++) SpawnCoin(x + 3f + i * 1.2f, 0.55f);
                    return 7f;
                }
                case 4:
                {
                    float gy = HeightAt(x + 6f);
                    SpawnBird(x + 6f, gy + 1.75f + (float)rng.NextDouble() * 0.45f);
                    return 4.5f;
                }
                default:
                {
                    SpawnBoulder(x);
                    SpawnCoinArc(x - 2.5f, 5, 1.1f, 2.5f);
                    return 5f;
                }
            }
        }

        // ----- spawners -----
        GameObject MakeSprite(string name, string sprite, Vector2 pos, float rotDeg, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(spawnedRoot, false);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y, 0), Quaternion.Euler(0, 0, rotDeg));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteLibrary.Get(sprite);
            sr.sortingOrder = order;
            go.AddComponent<Despawner>();
            return go;
        }

        static void SetupTrigger(GameObject go, Vector2 size, Vector2 offset)
        {
            var c = go.AddComponent<BoxCollider2D>();
            c.isTrigger = true; c.size = size; c.offset = offset;
        }

        void SpawnCone(float x)
        {
            string spr = LevelBiomes.Get(LevelBiomes.GetIndex(x)).coneSprite;
            var go = MakeSprite("Cone", spr, new Vector2(x, HeightAt(x) - 0.03f), 0f, OrderProps);
            SetupTrigger(go, new Vector2(0.32f, 0.54f), new Vector2(0, 0.35f));
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Cone;
        }

        void SpawnPothole(float x)
        {
            var go = MakeSprite("Pothole", "pothole", new Vector2(x, HeightAt(x) + 0.02f), SlopeDegAt(x), OrderProps);
            SetupTrigger(go, new Vector2(1.25f, 0.20f), new Vector2(0f, -0.02f));
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Pothole;
        }

        void SpawnSign(float x)
        {
            var go = MakeSprite("LowSign", "sign", new Vector2(x, HeightAt(x) - 0.03f), SlopeDegAt(x), OrderProps);
            SetupTrigger(go, new Vector2(1.25f, 0.80f), new Vector2(0, 1.65f));
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Sign; h.isAir = true;
        }

        void SpawnCable(float x)
        {
            var go = MakeSprite("Cable", "cable", new Vector2(x, HeightAt(x) - 0.03f), SlopeDegAt(x), OrderProps);
            SetupTrigger(go, new Vector2(4.2f, 0.24f), new Vector2(0, 1.45f));
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Cable; h.isAir = true;
        }

        void SpawnBird(float x, float y)
        {
            var go = MakeSprite("Bird", "bird_0", new Vector2(x, y), 0f, OrderProps + 1);
            var c = go.AddComponent<CircleCollider2D>(); c.isTrigger = true; c.radius = 0.24f;
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Bird; h.isAir = true;
            go.AddComponent<BirdFlyer>();
        }

        void SpawnBoulder(float x)
        {
            string spr = LevelBiomes.Get(LevelBiomes.GetIndex(x)).boulderSprite;
            var go = MakeSprite("Boulder", spr, new Vector2(x, HeightAt(x) + 0.85f), 0f, OrderProps + 1);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 6f; rb.gravityScale = cfg.gravityScale;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var c = go.AddComponent<CircleCollider2D>(); c.radius = 0.64f; c.sharedMaterial = boulderMat;
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Boulder; h.heavy = true;
            go.AddComponent<Boulder>();
        }

        float SpawnRail(System.Random rng, float x)
        {
            int segments = 1 + rng.Next(3);
            float cx = x + 2f;
            for (int i = 0; i < segments; i++)
            {
                float px = cx + i * 4f;
                var go = MakeSprite("Rail", "rail", new Vector2(px, HeightAt(px) - 0.03f), SlopeDegAt(px), OrderProps);
                var c = go.AddComponent<BoxCollider2D>();
                c.size = new Vector2(3.9f, 0.1f); c.offset = new Vector2(0, 0.52f);
                go.layer = Layers.Rail;   // deck & shell ignore rails; only the wheels grind on them
                c.usedByEffector = true; c.sharedMaterial = railMat;
                var eff = go.AddComponent<PlatformEffector2D>();
                eff.useOneWay = true; eff.surfaceArc = 160f; eff.useSideFriction = false; eff.useSideBounce = false;
                go.AddComponent<Rail>();
            }
            SpawnCoinLine(cx - 1f, segments * 4 + 1, 1.0f, 1.45f);
            return segments * 4f + 3f;
        }

        float SpawnCoinLine(float x, int count, float spacing, float height = 1.0f)
        {
            for (int i = 0; i < count; i++)
            {
                float cx = x + i * spacing;
                SpawnCoin(cx, height);
            }
            return count * spacing;
        }

        float SpawnCoinArc(float x, int count, float spacing, float arcHeight)
        {
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float cx = x + i * spacing;
                var go = MakeSprite("Coin", "coin", new Vector2(cx, HeightAt(cx) + 1.1f + Mathf.Sin(t * Mathf.PI) * arcHeight), 0f, OrderPickups);
                AddPickup(go, PickupType.Coin, 0.32f);
            }

            // Deterministically place an in-run power-up above the peak of some coin arcs (without extra RNG calls)
            int hash = Mathf.Abs(Mathf.RoundToInt(x * 13f));
            if (x > 55f && hash % 3 == 0)
            {
                float midX = x + (count - 1) * 0.5f * spacing;
                float py = HeightAt(midX) + 1.1f + arcHeight + 0.95f;
                int kind = (hash / 3) % 3;
                if (kind == 0) SpawnPowerup("Magnet", "magnet", midX, py, PickupType.Magnet);
                else if (kind == 1) SpawnPowerup("FuelCan", "fuel", midX, py, PickupType.Fuel);
                else SpawnPowerup("ShieldOrb", "shield_orb", midX, py, PickupType.Shield);
            }

            return count * spacing;
        }

        void SpawnPowerup(string name, string sprite, float x, float y, PickupType type)
        {
            var go = MakeSprite(name, sprite, new Vector2(x, y), 0f, OrderPickups + 1);
            AddPickup(go, type, 0.48f);
        }

        float SpawnPart(float x)
        {
            float px = x + 1f;
            var go = MakeSprite("Part", "part", new Vector2(px, HeightAt(px) + 3.2f), 0f, OrderPickups);
            AddPickup(go, PickupType.Part, 0.42f);
            // a short coin trail leading up to it
            for (int i = 0; i < 3; i++) SpawnCoinAbs(px - 3f + i * 1.0f, HeightAt(px - 3f + i) + 1.6f + i * 0.5f);
            return 5f;
        }

        void SpawnCoin(float x, float heightAboveGround)
        {
            SpawnCoinAbs(x, HeightAt(x) + heightAboveGround);
        }

        void SpawnCoinAbs(float x, float y)
        {
            var go = MakeSprite("Coin", "coin", new Vector2(x, y), 0f, OrderPickups);
            AddPickup(go, PickupType.Coin, 0.32f);
        }

        static void AddPickup(GameObject go, PickupType type, float radius)
        {
            var c = go.AddComponent<CircleCollider2D>(); c.isTrigger = true; c.radius = radius;
            var p = go.AddComponent<Pickup>(); p.type = type;
        }
    }
}
