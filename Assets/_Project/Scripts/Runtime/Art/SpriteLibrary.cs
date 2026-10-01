using System;
using System.Collections.Generic;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Central place to get a sprite by name.
    /// 1) If Resources/Sprites/{name}.png exists it is used (artists: just drop in
    ///    a PNG with the same name, Point filter, 16 pixels per unit).
    /// 2) Otherwise a placeholder pixel-art sprite is generated in code.
    /// Use the menu "Turtle Blaster / Export Placeholder Sprites" to dump all
    /// generated sprites as PNG files as a starting point for artists.
    /// </summary>
    public static class SpriteLibrary
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Func<Sprite>> generators = BuildGenerators();

        public static IEnumerable<string> Names => generators.Keys;

        static Material spriteMaterial;

        /// <summary>
        /// Unity's built-in sprite material (always included in builds). Works with
        /// vertex colours, so it is also used for the terrain mesh and particles.
        /// </summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    var tmp = new GameObject("tmpMat");
                    spriteMaterial = tmp.AddComponent<SpriteRenderer>().sharedMaterial;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(tmp); else UnityEngine.Object.DestroyImmediate(tmp);
                }
                return spriteMaterial;
            }
        }

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s) && s != null) return s;

            s = Resources.Load<Sprite>("Sprites/" + name);
            if (s == null && generators.TryGetValue(name, out var gen)) s = gen();
            if (s == null) { Debug.LogWarning("Missing sprite: " + name); s = Get("square"); }
            cache[name] = s;
            return s;
        }

        // ----- palette -----
        static Color32 K => PixelCanvas.C("#1b1b2f");        // outline
        static Color32 TurtleGreen => PixelCanvas.C("#6ad24f");
        static Color32 ShellGreen => PixelCanvas.C("#2f9e44");
        static Color32 ShellLight => PixelCanvas.C("#7bdc5a");
        static Color32 White => PixelCanvas.C("#ffffff");
        static Color32 Red => PixelCanvas.C("#e63946");
        static Color32 Orange => PixelCanvas.C("#ff8c1a");
        static Color32 Yellow => PixelCanvas.C("#ffd23f");
        static Color32 Brown => PixelCanvas.C("#8d5a2b");
        static Color32 Gray => PixelCanvas.C("#8d99ae");
        static Color32 DarkGray => PixelCanvas.C("#4a4e69");
        static Color32 Blue => PixelCanvas.C("#3a86ff");

        static Dictionary<string, Func<Sprite>> BuildGenerators()
        {
            var g = new Dictionary<string, Func<Sprite>>();

            g["square"] = () =>
            {
                var c = new PixelCanvas(4, 4);
                c.Rect(0, 0, 4, 4, White);
                return c.ToSprite(4f);
            };

            g["turtle"] = () =>
            {
                var c = new PixelCanvas(26, 20);
                // legs
                c.Rect(5, 2, 4, 4, TurtleGreen);
                c.Rect(14, 2, 4, 4, TurtleGreen);
                // shell dome
                c.Ellipse(11, 7, 9.5f, 8f, ShellGreen);
                c.ClearBelow(5);
                // shell highlights
                c.Rect(7, 11, 3, 2, ShellLight);
                c.Rect(12, 12, 3, 2, ShellLight);
                c.Rect(10, 8, 2, 2, ShellLight);
                c.Rect(15, 8, 2, 2, ShellLight);
                // head
                c.Ellipse(21, 9, 4f, 3.5f, TurtleGreen);
                // helmet
                c.Ellipse(21, 11, 4.2f, 2.8f, Red);
                c.Rect(18, 10, 8, 1, Red);
                // eye
                c.Rect(22, 8, 3, 3, White);
                c.Set(24, 9, K);
                c.Set(24, 8, K);
                // smile
                c.Rect(21, 6, 3, 1, K);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["board"] = () =>
            {
                var c = new PixelCanvas(32, 7);
                c.Rect(2, 2, 28, 3, Brown);
                c.Rect(2, 3, 28, 1, Orange);
                c.Rect(0, 3, 2, 3, Brown);   // tail kick
                c.Rect(30, 3, 2, 3, Brown);  // nose kick
                c.Rect(8, 2, 2, 3, Yellow);
                c.Rect(22, 2, 2, 3, Yellow);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["wheel"] = () =>
            {
                var c = new PixelCanvas(8, 8);
                c.Ellipse(4, 4, 3.8f, 3.8f, DarkGray);
                c.Ellipse(4, 4, 1.6f, 1.6f, Gray);
                c.Set(4, 4, K);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            // Jet thruster tiers: spray can -> fire extinguisher -> rocket tube
            g["thruster_0"] = () =>
            {
                var c = new PixelCanvas(12, 6);
                c.Rect(2, 1, 9, 4, Blue);
                c.Rect(2, 1, 9, 1, PixelCanvas.C("#2a5db0"));
                c.Rect(0, 2, 2, 2, Gray); // nozzle
                c.Rect(5, 2, 2, 2, White);
                c.Outline(K);
                return c.ToSprite(16f, new Vector2(0f, 0.5f));
            };
            g["thruster_1"] = () =>
            {
                var c = new PixelCanvas(16, 8);
                c.Rect(3, 1, 12, 6, Red);
                c.Rect(3, 1, 12, 1, PixelCanvas.C("#a42431"));
                c.Rect(1, 3, 3, 2, K);        // hose nozzle
                c.Rect(7, 2, 3, 4, White);
                c.Rect(13, 6, 2, 1, Yellow);
                c.Outline(K);
                return c.ToSprite(16f, new Vector2(0f, 0.5f));
            };
            g["thruster_2"] = () =>
            {
                var c = new PixelCanvas(22, 8);
                c.Rect(3, 2, 15, 4, White);
                c.Rect(5, 2, 2, 4, Red);
                c.Rect(10, 2, 2, 4, Red);
                c.Triangle(new Vector2(18, 2), new Vector2(18, 6), new Vector2(22, 4), Red); // nose cone
                c.Rect(1, 1, 3, 6, DarkGray); // fins/nozzle
                c.Outline(K);
                return c.ToSprite(16f, new Vector2(0f, 0.5f));
            };

            g["cone"] = () =>
            {
                var c = new PixelCanvas(12, 16);
                c.Rect(0, 0, 12, 2, DarkGray);
                c.Triangle(new Vector2(1.5f, 2), new Vector2(10.5f, 2), new Vector2(6, 15), Orange);
                c.Rect(3, 6, 6, 2, White);
                c.Outline(K);
                return c.ToSprite(16f, new Vector2(0.5f, 0f));
            };

            g["pothole"] = () =>
            {
                var c = new PixelCanvas(40, 6);
                c.Ellipse(20, 3, 19.5f, 2.8f, PixelCanvas.C("#2b2118"));
                c.Ellipse(20, 3.4f, 15f, 1.8f, PixelCanvas.C("#120d09"));
                c.Rect(1, 4, 3, 2, Yellow);
                c.Rect(36, 4, 3, 2, Yellow);
                return c.ToSprite(16f, new Vector2(0.5f, 0.5f));
            };

            g["boulder"] = () =>
            {
                var c = new PixelCanvas(26, 26);
                c.Ellipse(13, 13, 12.5f, 12.5f, Gray);
                c.Ellipse(10, 16, 5f, 4f, PixelCanvas.C("#b5bfd0"));
                c.Line(14, 5, 18, 11, DarkGray);
                c.Line(18, 11, 16, 15, DarkGray);
                c.Line(6, 8, 10, 10, DarkGray);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["rail"] = () =>
            {
                var c = new PixelCanvas(64, 10);
                c.Rect(0, 7, 64, 2, PixelCanvas.C("#c9d1de"));
                c.Rect(0, 6, 64, 1, DarkGray);
                c.Rect(4, 0, 3, 7, DarkGray);
                c.Rect(32, 0, 3, 7, DarkGray);
                c.Rect(57, 0, 3, 7, DarkGray);
                c.Outline(K);
                return c.ToSprite(16f, new Vector2(0.5f, 0f));
            };

            g["sign"] = () =>
            {
                var c = new PixelCanvas(48, 40);
                c.Rect(2, 0, 3, 38, DarkGray);          // pole
                c.Rect(2, 36, 44, 3, DarkGray);         // beam
                c.Rect(12, 26, 2, 10, Gray);            // chains
                c.Rect(34, 26, 2, 10, Gray);
                c.Rect(10, 17, 28, 10, Yellow);         // plate
                c.Rect(10, 17, 28, 1, Orange);
                c.Rect(23, 19, 2, 5, K);                // "!"
                c.Rect(23, 18, 2, 1, K);
                c.Outline(K);
                return c.ToSprite(16f, new Vector2(0.5f, 0f));
            };

            g["cable"] = () =>
            {
                var c = new PixelCanvas(96, 36);
                c.Rect(1, 0, 3, 28, Brown);
                c.Rect(92, 0, 3, 28, Brown);
                c.Rect(0, 24, 6, 2, DarkGray);
                c.Rect(90, 24, 6, 2, DarkGray);
                for (int x = 4; x < 92; x++)
                {
                    float t = (x - 4) / 87f;
                    int y = 21 - Mathf.RoundToInt(Mathf.Sin(t * Mathf.PI) * 3f);
                    c.Set(x, y, K);
                    c.Set(x, y + 1, DarkGray);
                }
                c.Outline(K);
                return c.ToSprite(16f, new Vector2(0.5f, 0f));
            };

            g["bird_0"] = () => Bird(false);
            g["bird_1"] = () => Bird(true);

            g["coin"] = () =>
            {
                var c = new PixelCanvas(10, 10);
                c.Ellipse(5, 5, 4.5f, 4.5f, Yellow);
                c.Rect(4, 3, 2, 4, Orange);
                c.Set(2, 7, White);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["part"] = () =>
            {
                var c = new PixelCanvas(12, 12);
                c.Ellipse(6, 6, 4.5f, 4.5f, Gray);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    c.Rect(Mathf.RoundToInt(6 + Mathf.Cos(a) * 5f) - 1, Mathf.RoundToInt(6 + Mathf.Sin(a) * 5f) - 1, 2, 2, Gray);
                }
                c.Ellipse(6, 6, 1.8f, 1.8f, PixelCanvas.C("#2b2d42"));
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["cloud"] = () =>
            {
                var c = new PixelCanvas(40, 14);
                var w = PixelCanvas.C("#ffffff");
                c.Ellipse(10, 5, 8, 4.5f, w);
                c.Ellipse(20, 8, 9, 5.5f, w);
                c.Ellipse(30, 5, 8, 4.5f, w);
                c.Rect(8, 0, 24, 5, w);
                return c.ToSprite(8f);
            };

            g["hills_far"] = () => Hills(256, 64, PixelCanvas.C("#7fb7d6"), 20f);
            g["hills_near"] = () => Hills(256, 48, PixelCanvas.C("#5f9f6e"), 14f);

            g["sky"] = () =>
            {
                var c = new PixelCanvas(2, 48);
                for (int y = 0; y < 48; y++)
                {
                    var col = Color.Lerp(PixelCanvas.C("#ffd9a0"), PixelCanvas.C("#5ec2ff"), Mathf.Pow(y / 47f, 0.8f));
                    c.Rect(0, y, 2, 1, col);
                }
                var s = c.ToSprite(2f);
                return s;
            };

            // ----- UI -----
            g["ui_panel"] = () =>
            {
                var c = new PixelCanvas(12, 12);
                c.Rect(0, 0, 12, 12, K);
                c.Rect(2, 2, 8, 8, White);
                c.Rect(1, 1, 10, 10, PixelCanvas.C("#cfd8e8"));
                c.Rect(2, 2, 8, 8, White);
                // cut corners
                c.Set(0, 0, PixelCanvas.Clear); c.Set(11, 0, PixelCanvas.Clear);
                c.Set(0, 11, PixelCanvas.Clear); c.Set(11, 11, PixelCanvas.Clear);
                return c.ToSprite(100f, null, new Vector4(4, 4, 4, 4));
            };

            g["ui_arrow"] = () =>
            {
                var c = new PixelCanvas(20, 20);
                c.Triangle(new Vector2(10, 18), new Vector2(1, 6), new Vector2(19, 6), White);
                c.Rect(6, 1, 8, 6, White);
                c.Outline(K);
                return c.ToSprite(100f);
            };

            g["ui_flip"] = () =>
            {
                var c = new PixelCanvas(24, 24);
                for (int j = 0; j < 24; j++)
                    for (int i = 0; i < 24; i++)
                    {
                        float dx = i + 0.5f - 12f, dy = j + 0.5f - 12f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (r > 6f && r < 9.5f && !(ang > 20f && ang < 75f)) c.Set(i, j, White);
                    }
                // arrow head at the end of the arc (angle ~20 deg)
                c.Triangle(new Vector2(20, 17), new Vector2(22.5f, 7), new Vector2(11.5f, 11), White);
                c.Outline(K);
                return c.ToSprite(100f);
            };

            g["ui_circle"] = () =>
            {
                var c = new PixelCanvas(32, 32);
                c.Ellipse(16, 16, 15.5f, 15.5f, White);
                c.Outline(K);
                return c.ToSprite(100f);
            };

            return g;
        }

        static Sprite Bird(bool wingsUp)
        {
            var c = new PixelCanvas(18, 12);
            c.Ellipse(8, 5, 6, 3.5f, PixelCanvas.C("#6c5ce7"));
            c.Ellipse(3, 6, 2.5f, 2.2f, PixelCanvas.C("#6c5ce7"));   // head (flying left)
            c.Triangle(new Vector2(0, 6), new Vector2(2, 7), new Vector2(2, 4), Orange); // beak
            c.Set(3, 7, White);
            c.Set(3, 6, K);
            if (wingsUp) c.Triangle(new Vector2(6, 7), new Vector2(12, 7), new Vector2(10, 12), PixelCanvas.C("#a29bfe"));
            else c.Triangle(new Vector2(6, 4), new Vector2(12, 4), new Vector2(10, 0), PixelCanvas.C("#a29bfe"));
            c.Triangle(new Vector2(13, 5), new Vector2(17, 8), new Vector2(17, 3), PixelCanvas.C("#4834d4"));
            c.Outline(K);
            return c.ToSprite(16f);
        }

        static Sprite Hills(int w, int h, Color32 col, float amp)
        {
            var c = new PixelCanvas(w, h);
            for (int x = 0; x < w; x++)
            {
                // Tileable: use frequencies that are integer multiples of 2*PI/w.
                float t = x / (float)w * Mathf.PI * 2f;
                int top = Mathf.RoundToInt(h * 0.45f + Mathf.Sin(t * 2f) * amp * 0.5f + Mathf.Sin(t * 5f + 1f) * amp * 0.25f);
                for (int y = 0; y < top && y < h; y++) c.Set(x, y, col);
            }
            return c.ToSprite(8f, new Vector2(0.5f, 0f));
        }
    }
}
