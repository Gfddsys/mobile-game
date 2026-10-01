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
            float bottom = minY - 8f;

            // ---- mesh: grass strip (vertices 0..3n) + dirt block (second vertex set) ----
            var verts = new Vector3[n * 3];
            var cols = new Color32[n * 3];
            var grassA = PixelCanvas.C("#52c84a");
            var grassB = PixelCanvas.C("#43b03d");
            var dirtTop = PixelCanvas.C("#8d5a2b");
            var dirtBot = PixelCanvas.C("#4a2f17");
            for (int i = 0; i < n; i++)
            {
                float lx = i * cfg.sampleSpacing;
                float y = heights[i];
                verts[i * 3 + 0] = new Vector3(lx, y, 0);
                verts[i * 3 + 1] = new Vector3(lx, y - 0.4f, 0);
                verts[i * 3 + 2] = new Vector3(lx, bottom, 0);
                bool alt = (((index * (n - 1)) + i) / 2) % 2 == 0;
                cols[i * 3 + 0] = alt ? grassA : grassB;
                cols[i * 3 + 1] = alt ? grassA : grassB;
                cols[i * 3 + 2] = dirtBot;
            }
            // dirt top colour sits on the grass bottom vertices of a *second* set so the grass band keeps a hard edge
            var verts2 = new Vector3[n * 2];
            var cols2 = new Color32[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts2[i * 2 + 0] = verts[i * 3 + 1];
                verts2[i * 2 + 1] = verts[i * 3 + 2];
                cols2[i * 2 + 0] = dirtTop;
                cols2[i * 2 + 1] = dirtBot;
            }

            var allV = new Vector3[n * 5];
            var allC = new Color32[n * 5];
            System.Array.Copy(verts, 0, allV, 0, n * 3);
            System.Array.Copy(cols, 0, allC, 0, n * 3);
            System.Array.Copy(verts2, 0, allV, n * 3, n * 2);
            System.Array.Copy(cols2, 0, allC, n * 3, n * 2);

            var tris = new List<int>((n - 1) * 12);
            for (int i = 0; i < n - 1; i++)
            {
                // grass quad: top(i), top(i+1), mid(i+1), mid(i)
                int t0 = i * 3, t1 = (i + 1) * 3;
                tris.Add(t0); tris.Add(t1); tris.Add(t1 + 1);
                tris.Add(t0); tris.Add(t1 + 1); tris.Add(t0 + 1);
                // dirt quad using the second vertex set
                int d0 = n * 3 + i * 2, d1 = n * 3 + (i + 1) * 2;
                tris.Add(d0); tris.Add(d1); tris.Add(d1 + 1);
                tris.Add(d0); tris.Add(d1 + 1); tris.Add(d0 + 1);
            }

            var mesh = new Mesh { name = "TerrainChunk" };
            mesh.vertices = allV;
            mesh.colors32 = allC;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SpriteLibrary.SpriteMaterial;
            mr.sortingOrder = OrderTerrain;

            // ---- collider: overlap one sample on each side so chunk seams are watertight ----
            var pts = new Vector2[n + 2];
            pts[0] = new Vector2(-cfg.sampleSpacing, HeightAt(x0 - cfg.sampleSpacing));
            for (int i = 0; i < n; i++) pts[i + 1] = new Vector2(i * cfg.sampleSpacing, heights[i]);
            pts[n + 1] = new Vector2(w + cfg.sampleSpacing, HeightAt(x0 + w + cfg.sampleSpacing));
            var edge = go.AddComponent<EdgeCollider2D>();
            edge.points = pts;
            edge.sharedMaterial = groundMat;

            Populate(index, x0, x0 + w);
            return go;
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
                if (cursor < 40f) { cursor = 40f; continue; } // safe start zone
                float d = Difficulty(cursor);
                float len = PlacePattern(rng, cursor, d);
                float gap = Mathf.Lerp(12f, 5f, d) * (0.7f + (float)rng.NextDouble() * 0.6f);
                cursor += len + gap;
            }
        }

        float PlacePattern(System.Random rng, float x, float d)
        {
            float hazardChance = Mathf.Lerp(0.30f, 0.75f, d);
            bool hazard = rng.NextDouble() < hazardChance;
            float slope = SlopeDegAt(x);

            if (hazard)
            {
                // weighted pick among eligible hazards
                var options = new List<(int id, int w)>();
                bool flat = IsFlat(x, 5f, 10f);
                if (flat) { options.Add((0, 4)); options.Add((1, 2)); }                       // cone, pothole
                if (flat && d > 0.08f) options.Add((2, 2));                                    // low sign
                if (IsFlat(x, 6f, 9f) && d > 0.18f) options.Add((3, 2));                       // cable
                if (d > 0.12f) options.Add((4, 2));                                            // bird
                if (d > 0.22f && slope > 8f && slope < 28f) options.Add((5, 1)); // boulder
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
                case 0: // cones
                {
                    int count = 1 + rng.Next(1 + Mathf.RoundToInt(Mathf.Lerp(1, 3, d)));
                    for (int i = 0; i < count; i++) SpawnCone(x + i * 0.95f);
                    return count * 0.95f + 1f;
                }
                case 1: SpawnPothole(x + 1.2f); return 3f;
                case 2: // low sign with a coin trail under it (rewards ducking)
                {
                    SpawnSign(x + 2f);
                    for (int i = -1; i <= 1; i++) SpawnCoin(x + 2f + i * 1.0f, 0.55f);
                    return 4f;
                }
                case 3: SpawnCable(x + 3f); return 7f;
                case 4:
                {
                    float gy = HeightAt(x + 6f);
                    SpawnBird(x + 6f, gy + 1.5f + (float)rng.NextDouble() * 0.7f);
                    return 4f;
                }
                default: SpawnBoulder(x); return 4f;
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
            var go = MakeSprite("Cone", "cone", new Vector2(x, HeightAt(x) - 0.03f), 0f, OrderProps);
            SetupTrigger(go, new Vector2(0.4f, 0.65f), new Vector2(0, 0.42f));
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Cone;
        }

        void SpawnPothole(float x)
        {
            var go = MakeSprite("Pothole", "pothole", new Vector2(x, HeightAt(x) + 0.02f), SlopeDegAt(x), OrderProps);
            SetupTrigger(go, new Vector2(1.7f, 0.25f), Vector2.zero);
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Pothole;
        }

        void SpawnSign(float x)
        {
            var go = MakeSprite("LowSign", "sign", new Vector2(x, HeightAt(x) - 0.03f), 0f, OrderProps);
            SetupTrigger(go, new Vector2(1.5f, 1.1f), new Vector2(0, 1.69f));
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Sign; h.isAir = true;
        }

        void SpawnCable(float x)
        {
            var go = MakeSprite("Cable", "cable", new Vector2(x, HeightAt(x) - 0.03f), 0f, OrderProps);
            SetupTrigger(go, new Vector2(5.0f, 0.3f), new Vector2(0, 1.2f));
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Cable; h.isAir = true;
        }

        void SpawnBird(float x, float y)
        {
            var go = MakeSprite("Bird", "bird_0", new Vector2(x, y), 0f, OrderProps + 1);
            var c = go.AddComponent<CircleCollider2D>(); c.isTrigger = true; c.radius = 0.3f;
            var h = go.AddComponent<Hazard>(); h.type = HazardType.Bird; h.isAir = true;
            go.AddComponent<BirdFlyer>();
        }

        void SpawnBoulder(float x)
        {
            var go = MakeSprite("Boulder", "boulder", new Vector2(x, HeightAt(x) + 0.9f), 0f, OrderProps + 1);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 6f; rb.gravityScale = cfg.gravityScale;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var c = go.AddComponent<CircleCollider2D>(); c.radius = 0.8f; c.sharedMaterial = boulderMat;
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
            return count * spacing;
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
