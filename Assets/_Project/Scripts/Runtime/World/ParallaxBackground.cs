using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Sky gradient, 3-level biome crossfade (Meadow / Desert / Frost Peaks),
    /// Day/Sunset/Night/Dawn cycle, twinkling stars, drifting clouds, parallax
    /// hill layers, and high-speed wind streak lines.
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        class Layer
        {
            public SpriteRenderer[] tiles;
            public SpriteRenderer[] nextTiles;
            public Sprite[] variantSprites;
            public float tileWidth;
            public float parallax;   // 0 = glued to camera, 1 = fixed in the world
            public float yAnchor;    // fraction of view height from bottom
            public float yFollow;    // how much it follows camera Y (0..1)
            public Color baseTint;
        }

        CameraFollow cam;
        SpriteRenderer sky, skyNext;
        Sprite[] skySprites;
        readonly List<Layer> layers = new List<Layer>();
        readonly List<Transform> clouds = new List<Transform>();
        readonly List<SpriteRenderer> cloudRenderers = new List<SpriteRenderer>();
        readonly List<float> cloudSpeed = new List<float>();
        readonly List<SpriteRenderer> stars = new List<SpriteRenderer>();
        readonly List<Vector2> starOffsets = new List<Vector2>();
        readonly List<SpriteRenderer> speedLines = new List<SpriteRenderer>();
        readonly List<Vector2> speedLineOffsets = new List<Vector2>();

        public static Color EvaluateBiomeTint(float worldX, out float nightWeight)
        {
            float cycle = Mathf.Repeat(Mathf.Max(0f, worldX) / 350f, 4f);
            Color day = Color.white;
            Color sunset = new Color(1f, 0.68f, 0.52f, 1f);
            Color night = new Color(0.30f, 0.35f, 0.62f, 1f);
            Color dawn = new Color(0.96f, 0.74f, 0.86f, 1f);

            int seg = Mathf.FloorToInt(cycle);
            float t = Mathf.SmoothStep(0f, 1f, cycle - seg);
            switch (seg)
            {
                case 0:
                    nightWeight = Mathf.SmoothStep(0f, 0.35f, t);
                    return Color.Lerp(day, sunset, t);
                case 1:
                    nightWeight = Mathf.Lerp(0.35f, 1f, t);
                    return Color.Lerp(sunset, night, t);
                case 2:
                    nightWeight = Mathf.Lerp(1f, 0.25f, t);
                    return Color.Lerp(night, dawn, t);
                default:
                    nightWeight = Mathf.Lerp(0.25f, 0f, t);
                    return Color.Lerp(dawn, day, t);
            }
        }

        public void Init(CameraFollow camera)
        {
            cam = camera;

            skySprites = new Sprite[LevelBiomes.Count];
            var farSprites = new string[LevelBiomes.Count];
            var nearSprites = new string[LevelBiomes.Count];
            for (int i = 0; i < LevelBiomes.Count; i++)
            {
                var b = LevelBiomes.Get(i);
                skySprites[i] = SpriteLibrary.Get(b.skySprite);
                farSprites[i] = b.farSprite;
                nearSprites[i] = b.nearSprite;
            }

            var skyGo = new GameObject("Sky");
            skyGo.transform.SetParent(transform, false);
            sky = skyGo.AddComponent<SpriteRenderer>();
            sky.sprite = skySprites[0];
            sky.sortingOrder = -100;

            var skyNextGo = new GameObject("SkyNext");
            skyNextGo.transform.SetParent(transform, false);
            skyNext = skyNextGo.AddComponent<SpriteRenderer>();
            skyNext.sprite = skySprites[0];
            skyNext.sortingOrder = -99;
            skyNext.enabled = false;

            // twinkling stars for sunset / night / snowy biomes
            var starSpr = SpriteLibrary.Get("star");
            for (int i = 0; i < 18; i++)
            {
                var sgo = new GameObject("Star" + i);
                sgo.transform.SetParent(transform, false);
                var ssr = sgo.AddComponent<SpriteRenderer>();
                ssr.sprite = starSpr;
                ssr.sortingOrder = -97;
                float sc = Random.Range(0.7f, 1.35f);
                sgo.transform.localScale = new Vector3(sc, sc, 1f);
                ssr.color = new Color(1f, 0.98f, 0.85f, 0f);
                stars.Add(ssr);
                starOffsets.Add(new Vector2(Random.Range(-0.48f, 0.48f), Random.Range(0.05f, 0.46f)));
            }

            AddLayer("HillsFar", farSprites, 0.82f, 0.30f, -90, new Color(1, 1, 1, 0.95f));
            AddLayer("HillsNear", nearSprites, 0.6f, 0.05f, -80, Color.white);

            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject("Cloud" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteLibrary.Get("cloud");
                sr.sortingOrder = -95;
                float s = Random.Range(1.2f, 2.4f);
                go.transform.localScale = new Vector3(s, s, 1);
                sr.color = new Color(1, 1, 1, 0.9f);
                go.transform.position = new Vector3(Random.Range(-20f, 40f), Random.Range(3f, 9f), 0);
                clouds.Add(go.transform);
                cloudRenderers.Add(sr);
                cloudSpeed.Add(Random.Range(0.3f, 0.9f));
            }

            // high-speed wind streak lines (kept to top/bottom framing bands so sightlines stay clear)
            var sq = SpriteLibrary.Get("square");
            for (int i = 0; i < 16; i++)
            {
                var lgo = new GameObject("SpeedLine" + i);
                lgo.transform.SetParent(transform, false);
                var lsr = lgo.AddComponent<SpriteRenderer>();
                lsr.sprite = sq;
                lsr.sortingOrder = 25;
                lgo.transform.localScale = new Vector3(Random.Range(1.8f, 3.4f), Random.Range(0.035f, 0.055f), 1f);
                lsr.color = new Color(1f, 1f, 1f, 0f);
                speedLines.Add(lsr);
                float side = (i & 1) == 0 ? 1f : -1f;
                speedLineOffsets.Add(new Vector2(Random.Range(-0.5f, 0.5f), side * Random.Range(0.18f, 0.46f)));
            }
        }

        void AddLayer(string name, string[] spriteNames, float parallax, float yAnchor, int order, Color tint)
        {
            var sprites = new Sprite[spriteNames.Length];
            for (int i = 0; i < spriteNames.Length; i++) sprites[i] = SpriteLibrary.Get(spriteNames[i]);

            var layer = new Layer
            {
                parallax = parallax,
                yAnchor = yAnchor,
                yFollow = 0.25f,
                tileWidth = sprites[0].bounds.size.x,
                tiles = new SpriteRenderer[4],
                nextTiles = new SpriteRenderer[4],
                variantSprites = sprites,
                baseTint = tint
            };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(name + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[0]; sr.sortingOrder = order; sr.color = tint;
                layer.tiles[i] = sr;

                var ngo = new GameObject(name + "Next" + i);
                ngo.transform.SetParent(transform, false);
                var nsr = ngo.AddComponent<SpriteRenderer>();
                nsr.sprite = sprites[0]; nsr.sortingOrder = order + 1; nsr.color = tint;
                nsr.enabled = false;
                layer.nextTiles[i] = nsr;
            }
            layers.Add(layer);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            var cp = cam.transform.position;
            float vh = cam.ViewHeight, vw = cam.ViewWidth;

            LevelBiomes.Evaluate(cp.x, out int fromIdx, out int toIdx, out float blend);
            Color atmosTint = EvaluateBiomeTint(cp.x, out float nightWeight);
            var biomeA = LevelBiomes.Get(fromIdx);
            var biomeB = LevelBiomes.Get(toIdx);

            // sky fills the view and crossfades cleanly between level variants + time of day
            var activeSky = skySprites[fromIdx];
            var sp = activeSky.bounds.size;
            Vector3 skyPos = new Vector3(cp.x, cp.y, 5f);
            Vector3 skyScale = new Vector3(vw / sp.x * 1.05f, vh / sp.y * 1.05f, 1f);
            sky.sprite = activeSky;
            sky.transform.position = skyPos;
            sky.transform.localScale = skyScale;
            sky.color = atmosTint;

            if (blend > 0.001f && toIdx != fromIdx)
            {
                skyNext.enabled = true;
                skyNext.sprite = skySprites[toIdx];
                skyNext.transform.position = skyPos;
                skyNext.transform.localScale = skyScale;
                Color nextSkyCol = atmosTint;
                nextSkyCol.a = blend;
                skyNext.color = nextSkyCol;
            }
            else
            {
                skyNext.enabled = false;
            }

            // twinkling stars
            float starAlpha = Mathf.Clamp01(Mathf.Max(nightWeight, Mathf.Lerp(biomeA.extraStarAlpha, biomeB.extraStarAlpha, blend)));
            for (int i = 0; i < stars.Count; i++)
            {
                var ssr = stars[i];
                var off = starOffsets[i];
                float sx = cp.x + off.x * vw;
                float sy = cp.y + off.y * vh;
                ssr.transform.position = new Vector3(sx, sy, 4f);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(Time.time * 3.8f + i * 1.9f);
                ssr.color = new Color(1f, 0.97f, 0.82f, starAlpha * twinkle * 0.9f);
            }

            bool blendingHills = blend > 0.001f && toIdx != fromIdx;
            foreach (var l in layers)
            {
                // scale hills with the view height so they always read as backdrop
                float sc = vh / 12.4f;
                float tw = l.tileWidth * sc;
                float position = cp.x * (1f - l.parallax);
                float rel = cp.x * l.parallax;
                float baseX = Mathf.Floor(rel / tw) * tw;
                float y = cp.y - vh * 0.5f + vh * l.yAnchor;

                Color hillBase = l.baseTint * Color.Lerp(Color.white, atmosTint, 0.55f);
                Color fromCol = hillBase;
                fromCol.a = l.baseTint.a * (blendingHills ? Mathf.Clamp01(2f * (1f - blend)) : 1f);
                Color toCol = hillBase;
                toCol.a = l.baseTint.a * blend;

                var fromSpr = l.variantSprites[fromIdx];
                var toSpr = l.variantSprites[toIdx];

                for (int i = 0; i < l.tiles.Length; i++)
                {
                    Vector3 tilePos = new Vector3(position + baseX + (i - 1) * tw, y, 0f);
                    Vector3 tileScale = new Vector3(sc, sc, 1f);

                    var sr = l.tiles[i];
                    sr.sprite = fromSpr;
                    sr.color = fromCol;
                    sr.transform.localScale = tileScale;
                    sr.transform.position = tilePos;

                    var nsr = l.nextTiles[i];
                    if (blendingHills)
                    {
                        nsr.enabled = true;
                        nsr.sprite = toSpr;
                        nsr.color = toCol;
                        nsr.transform.localScale = tileScale;
                        nsr.transform.position = tilePos;
                    }
                    else
                    {
                        nsr.enabled = false;
                    }
                }
            }

            Color cloudColor = Color.Lerp(biomeA.cloudTint, biomeB.cloudTint, blend) * Color.Lerp(Color.white, atmosTint, 0.45f);
            cloudColor.a = 0.9f;
            for (int i = 0; i < clouds.Count; i++)
            {
                cloudRenderers[i].color = cloudColor;
                var c = clouds[i];
                var p = c.position;
                p.x += cloudSpeed[i] * Time.deltaTime;
                float left = cp.x - vw, right = cp.x + vw;
                if (p.x < left) p.x += vw * 2f + 10f;
                if (p.x > right + 6f) p.x -= vw * 2f + 10f;
                p.y = Mathf.Clamp(p.y, cp.y + vh * 0.05f, cp.y + vh * 0.48f);
                c.position = p;
            }

            // high-speed wind streak lines
            float speedAlpha = 0f;
            bool super = false;
            bool thrusting = false;
            if (GameManager.Instance != null && GameManager.Instance.Player != null && GameManager.Instance.Player.Alive)
            {
                var pl = GameManager.Instance.Player;
                super = pl.SuperBoost;
                thrusting = pl.Thrusting;
                float spdFactor = Mathf.Clamp01((pl.Speed - 13f) / 6f) * 0.35f;
                float thrustFactor = thrusting && pl.Speed > 10f ? 0.38f : 0f;
                speedAlpha = super ? 0.65f : Mathf.Max(spdFactor, thrustFactor);
            }
            float sweepSpeed = super ? 4.2f : (thrusting ? 3.2f : 2.6f);
            for (int i = 0; i < speedLines.Count; i++)
            {
                var lsr = speedLines[i];
                if (speedAlpha <= 0.005f)
                {
                    lsr.enabled = false;
                    continue;
                }
                lsr.enabled = true;
                var off = speedLineOffsets[i];
                off.x -= Time.deltaTime * (sweepSpeed + (i % 3) * 0.65f);
                if (off.x < -0.55f)
                {
                    off.x += 1.1f;
                    float side = (i & 1) == 0 ? 1f : -1f;
                    off.y = side * Random.Range(0.18f, 0.46f);
                }
                speedLineOffsets[i] = off;
                lsr.transform.position = new Vector3(cp.x + off.x * vw, cp.y + off.y * vh, 0f);
                float lenMult = super ? 1.55f : (thrusting ? 1.15f : 1f);
                lsr.transform.localScale = new Vector3((1.8f + (i % 4) * 0.5f) * lenMult, super ? 0.055f : 0.042f, 1f);

                Color lc;
                if (super)
                {
                    int cIdx = (i + Mathf.FloorToInt(Time.time * 6f)) % 3;
                    lc = cIdx == 0 ? PixelCanvas.C("#24f0ff") : cIdx == 1 ? PixelCanvas.C("#ffd93d") : Color.white;
                }
                else if (thrusting)
                {
                    lc = Color.Lerp(Color.white, PixelCanvas.C("#ffb347"), 0.35f);
                }
                else
                {
                    lc = Color.white;
                }
                lc.a = speedAlpha * (0.5f + 0.5f * Mathf.Sin(Time.time * 12f + i));
                lsr.color = lc;
            }
        }
    }
}
