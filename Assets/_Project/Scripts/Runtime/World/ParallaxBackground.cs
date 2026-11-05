using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Sky gradient, dynamic Day/Sunset/Night/Dawn biome cycle, twinkling stars,
    /// drifting clouds, parallax hill layers, and high-speed wind streak lines.
    /// </summary>
    public class ParallaxBackground : MonoBehaviour
    {
        class Layer
        {
            public SpriteRenderer[] tiles;
            public float tileWidth;
            public float parallax;   // 0 = glued to camera, 1 = fixed in the world
            public float yAnchor;    // fraction of view height from bottom
            public float yFollow;    // how much it follows camera Y (0..1)
            public Color baseTint;
        }

        CameraFollow cam;
        SpriteRenderer sky;
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

            var skyGo = new GameObject("Sky");
            skyGo.transform.SetParent(transform, false);
            sky = skyGo.AddComponent<SpriteRenderer>();
            sky.sprite = SpriteLibrary.Get("sky");
            sky.sortingOrder = -100;

            // twinkling stars for sunset / night biomes
            var starSpr = SpriteLibrary.Get("star");
            for (int i = 0; i < 18; i++)
            {
                var sgo = new GameObject("Star" + i);
                sgo.transform.SetParent(transform, false);
                var ssr = sgo.AddComponent<SpriteRenderer>();
                ssr.sprite = starSpr;
                ssr.sortingOrder = -98;
                float sc = Random.Range(0.7f, 1.35f);
                sgo.transform.localScale = new Vector3(sc, sc, 1f);
                ssr.color = new Color(1f, 0.98f, 0.85f, 0f);
                stars.Add(ssr);
                starOffsets.Add(new Vector2(Random.Range(-0.48f, 0.48f), Random.Range(0.05f, 0.46f)));
            }

            AddLayer("HillsFar", "hills_far", 0.82f, 0.30f, -90, new Color(1, 1, 1, 0.95f));
            AddLayer("HillsNear", "hills_near", 0.6f, 0.05f, -80, Color.white);

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

            // high-speed wind streak lines
            var sq = SpriteLibrary.Get("square");
            for (int i = 0; i < 10; i++)
            {
                var lgo = new GameObject("SpeedLine" + i);
                lgo.transform.SetParent(transform, false);
                var lsr = lgo.AddComponent<SpriteRenderer>();
                lsr.sprite = sq;
                lsr.sortingOrder = 25;
                lgo.transform.localScale = new Vector3(Random.Range(1.8f, 3.4f), Random.Range(0.035f, 0.06f), 1f);
                lsr.color = new Color(1f, 1f, 1f, 0f);
                speedLines.Add(lsr);
                speedLineOffsets.Add(new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.42f, 0.42f)));
            }
        }

        void AddLayer(string name, string sprite, float parallax, float yAnchor, int order, Color tint)
        {
            var spr = SpriteLibrary.Get(sprite);
            var layer = new Layer
            {
                parallax = parallax,
                yAnchor = yAnchor,
                yFollow = 0.25f,
                tileWidth = spr.bounds.size.x,
                tiles = new SpriteRenderer[4],
                baseTint = tint
            };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(name + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr; sr.sortingOrder = order; sr.color = tint;
                layer.tiles[i] = sr;
            }
            layers.Add(layer);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            var cp = cam.transform.position;
            float vh = cam.ViewHeight, vw = cam.ViewWidth;

            Color biomeTint = EvaluateBiomeTint(cp.x, out float nightWeight);

            // sky fills the view and shifts tint across Day / Sunset / Night / Dawn
            var sp = sky.sprite.bounds.size;
            sky.transform.position = new Vector3(cp.x, cp.y, 5f);
            sky.transform.localScale = new Vector3(vw / sp.x * 1.05f, vh / sp.y * 1.05f, 1f);
            sky.color = biomeTint;

            // twinkling stars
            for (int i = 0; i < stars.Count; i++)
            {
                var ssr = stars[i];
                var off = starOffsets[i];
                float sx = cp.x + off.x * vw;
                float sy = cp.y + off.y * vh;
                ssr.transform.position = new Vector3(sx, sy, 4f);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(Time.time * 3.8f + i * 1.9f);
                ssr.color = new Color(1f, 0.97f, 0.82f, nightWeight * twinkle * 0.9f);
            }

            foreach (var l in layers)
            {
                // scale hills with the view height so they always read as backdrop
                float sc = vh / 12.4f;
                float tw = l.tileWidth * sc;
                float position = cp.x * (1f - l.parallax);
                float rel = cp.x * l.parallax;
                float baseX = Mathf.Floor(rel / tw) * tw;
                float y = cp.y - vh * 0.5f + vh * l.yAnchor;
                Color hillColor = l.baseTint * Color.Lerp(Color.white, biomeTint, 0.65f);
                hillColor.a = l.baseTint.a;
                for (int i = 0; i < l.tiles.Length; i++)
                {
                    var sr = l.tiles[i];
                    sr.color = hillColor;
                    var t = sr.transform;
                    t.localScale = new Vector3(sc, sc, 1f);
                    t.position = new Vector3(position + baseX + (i - 1) * tw, y, 0f);
                }
            }

            Color cloudColor = Color.Lerp(Color.white, biomeTint, 0.55f);
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
            if (GameManager.Instance != null && GameManager.Instance.Player != null && GameManager.Instance.Player.Alive)
            {
                var pl = GameManager.Instance.Player;
                super = pl.SuperBoost;
                speedAlpha = super ? 0.55f : Mathf.Clamp01((pl.Speed - 13.5f) / 6f) * 0.35f;
            }
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
                off.x -= Time.deltaTime * (2.6f + (i % 3) * 0.6f);
                if (off.x < -0.55f)
                {
                    off.x += 1.1f;
                    off.y = Random.Range(-0.44f, 0.44f);
                }
                speedLineOffsets[i] = off;
                lsr.transform.position = new Vector3(cp.x + off.x * vw, cp.y + off.y * vh, 0f);
                Color lc = super
                    ? Color.HSVToRGB(Mathf.Repeat(Time.time * 2f + i * 0.1f, 1f), 0.4f, 1f)
                    : Color.white;
                lc.a = speedAlpha * (0.5f + 0.5f * Mathf.Sin(Time.time * 12f + i));
                lsr.color = lc;
            }
        }
    }
}
