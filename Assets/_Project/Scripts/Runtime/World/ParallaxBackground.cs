using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>Sky gradient, drifting clouds and two parallax hill layers.</summary>
    public class ParallaxBackground : MonoBehaviour
    {
        class Layer
        {
            public SpriteRenderer[] tiles;
            public float tileWidth;
            public float parallax;   // 0 = glued to camera, 1 = fixed in the world
            public float yAnchor;    // fraction of view height from bottom
            public float yFollow;    // how much it follows camera Y (0..1)
        }

        CameraFollow cam;
        SpriteRenderer sky;
        readonly List<Layer> layers = new List<Layer>();
        readonly List<Transform> clouds = new List<Transform>();
        readonly List<float> cloudSpeed = new List<float>();

        public void Init(CameraFollow camera)
        {
            cam = camera;

            var skyGo = new GameObject("Sky");
            skyGo.transform.SetParent(transform, false);
            sky = skyGo.AddComponent<SpriteRenderer>();
            sky.sprite = SpriteLibrary.Get("sky");
            sky.sortingOrder = -100;

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
                cloudSpeed.Add(Random.Range(0.3f, 0.9f));
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
                tiles = new SpriteRenderer[4]
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

            // sky fills the view
            var sp = sky.sprite.bounds.size;
            sky.transform.position = new Vector3(cp.x, cp.y, 5f);
            sky.transform.localScale = new Vector3(vw / sp.x * 1.05f, vh / sp.y * 1.05f, 1f);

            foreach (var l in layers)
            {
                // scale hills with the view height so they always read as backdrop
                float sc = vh / 12.4f;
                float tw = l.tileWidth * sc;
                float position = cp.x * (1f - l.parallax);
                float rel = cp.x * l.parallax;
                float baseX = Mathf.Floor(rel / tw) * tw;
                float y = cp.y - vh * 0.5f + vh * l.yAnchor;
                for (int i = 0; i < l.tiles.Length; i++)
                {
                    var t = l.tiles[i].transform;
                    t.localScale = new Vector3(sc, sc, 1f);
                    t.position = new Vector3(position + baseX + (i - 1) * tw, y, 0f);
                }
            }

            for (int i = 0; i < clouds.Count; i++)
            {
                var c = clouds[i];
                var p = c.position;
                p.x += cloudSpeed[i] * Time.deltaTime;
                float left = cp.x - vw, right = cp.x + vw;
                if (p.x < left) p.x += vw * 2f + 10f;
                if (p.x > right + 6f) p.x -= vw * 2f + 10f;
                p.y = Mathf.Clamp(p.y, cp.y + vh * 0.05f, cp.y + vh * 0.48f);
                c.position = p;
            }
        }
    }
}
