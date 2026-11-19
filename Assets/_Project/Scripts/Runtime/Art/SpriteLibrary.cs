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

        /// <summary>Every sprite name an artist can replace (Resources/Sprites/{name}.png), including skins.</summary>
        public static IEnumerable<string> OverridableNames
        {
            get
            {
                foreach (var n in generators.Keys) yield return n;
                foreach (var s in SkinDatabase.All)
                    if (s.id != SkinDatabase.DefaultId) yield return "turtle_" + s.id;
            }
        }

        /// <summary>
        /// The built-in placeholder for a name, ignoring any artist override. Used by the editor
        /// to export templates and to give imported art the same pivot / world size. Null if unknown.
        /// </summary>
        public static Sprite CreatePlaceholder(string name)
        {
            if (generators.TryGetValue(name, out var gen)) return gen();
            if (name.StartsWith("turtle_"))
            {
                var id = name.Substring("turtle_".Length);
                var skin = SkinDatabase.Get(id);
                if (skin.id == id) return Turtle(skin);
            }
            return null;
        }

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s) && s != null) return s;

            s = LoadOverrideSprite(name);
            if (s == null && generators.TryGetValue(name, out var gen)) s = gen();
            if (s == null) { Debug.LogWarning("Missing sprite: " + name); s = Get("square"); }
            cache[name] = s;
            return s;
        }

        static Sprite LoadOverrideSprite(string name)
        {
            var s = Resources.Load<Sprite>("Sprites/" + name);
            if (s != null && s.texture != null &&
                Mathf.Approximately(s.rect.width, s.texture.width) &&
                Mathf.Approximately(s.rect.height, s.texture.height))
            {
                return s;
            }
            var tex = s != null ? s.texture : Resources.Load<Texture2D>("Sprites/" + name);
            if (tex != null)
            {
                var refSprite = CreatePlaceholder(name);
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                float ppu = 16f;
                Vector4 border = Vector4.zero;
                if (refSprite != null)
                {
                    float scale = tex.width / refSprite.rect.width;
                    pivot = refSprite.pivot / refSprite.rect.size;
                    ppu = refSprite.pixelsPerUnit * scale;
                    border = refSprite.border * scale;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(refSprite.texture);
                    else UnityEngine.Object.DestroyImmediate(refSprite.texture);
                    if (Application.isPlaying) UnityEngine.Object.Destroy(refSprite);
                    else UnityEngine.Object.DestroyImmediate(refSprite);
                }
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect, border);
            }
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

            g["turtle"] = () => Turtle(SkinDatabase.Get(SkinDatabase.DefaultId));

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
                c.Set(3, 3, K);
                c.Set(5, 5, K);
                c.Set(3, 5, White);
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

            g["cone_desert"] = ConeDesert;
            g["cone_snow"] = ConeSnow;

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

            g["boulder_desert"] = BoulderDesert;
            g["boulder_snow"] = BoulderSnow;

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

            g["magnet"] = () =>
            {
                var c = new PixelCanvas(12, 12);
                c.Rect(2, 2, 8, 3, Red);
                c.Rect(2, 4, 3, 5, Red);
                c.Rect(7, 4, 3, 5, Blue);
                c.Rect(2, 9, 3, 2, White);
                c.Rect(7, 9, 3, 2, White);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["fuel"] = () =>
            {
                var c = new PixelCanvas(12, 14);
                c.Rect(2, 1, 8, 9, Red);
                c.Rect(3, 10, 5, 2, Red);
                c.Rect(8, 10, 2, 3, Yellow); // spout
                c.Rect(4, 4, 4, 4, Yellow);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["shield_orb"] = () =>
            {
                var c = new PixelCanvas(12, 12);
                c.Ellipse(6, 6, 5f, 5f, PixelCanvas.C("#4dd2ff"));
                c.Ellipse(6, 6, 3.2f, 3.2f, PixelCanvas.C("#a8ecff"));
                c.Set(4, 8, White);
                c.Set(5, 8, White);
                c.Outline(K);
                return c.ToSprite(16f);
            };

            g["star"] = () =>
            {
                var c = new PixelCanvas(7, 7);
                c.Rect(3, 0, 1, 7, White);
                c.Rect(0, 3, 7, 1, White);
                c.Rect(2, 2, 3, 3, Yellow);
                c.Set(3, 3, White);
                return c.ToSprite(16f);
            };

            g["energy"] = EnergyIcon;

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
            g["hills_far_desert"] = HillsFarDesert;
            g["hills_near_desert"] = HillsNearDesert;
            g["hills_far_snow"] = HillsFarSnow;
            g["hills_near_snow"] = HillsNearSnow;

            g["sky"] = () => SkyGradient("#ffd9a0", "#5ec2ff");
            g["sky_desert"] = () => SkyGradient("#ffbe76", "#4bcffa");
            g["sky_snow"] = () => SkyGradient("#b8e1ff", "#243b6b");

            // scenic roadside props per biome
            g["prop_meadow_tree"] = PropMeadowTree;
            g["prop_meadow_bush"] = PropMeadowBush;
            g["prop_desert_cactus"] = PropDesertCactus;
            g["prop_desert_rock"] = PropDesertRock;
            g["prop_snow_pine"] = PropSnowPine;
            g["prop_snow_snowman"] = PropSnowSnowman;

            // turbo & thruster VFX
            g["turbo_flame"] = TurboFlame;
            g["turbo_core"] = TurboCore;
            g["turbo_glow"] = TurboGlow;
            g["turbo_ring"] = TurboRing;

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

        // Custom pixel-art sprites for all turtle skins (turtle.png & turtle_{id}.png)
        static readonly Dictionary<string, string> TurtlePngBase64 = new Dictionary<string, string>
        {
            [SkinDatabase.DefaultId] = "iVBORw0KGgoAAAANSUhEUgAAABoAAAAUCAYAAACTQC2+AAAAAXNSR0IArs4c6QAAAT5JREFUSIntkjFKA0EUhr+BrBCF3cVEYpCEEEHSaGOKdF5AIQcI2NiJiHewT2W3jZ29OUYOkCrIguyCJDADWQuLZxEybHQ3BiuRfNXMvJn/mzcMbNjwb2g22nIS3Eqz0RYAtc6h+s2FzAqSWZv0B98yDu97dvPOfgmAwk8CAKfiUXeK2ZvuziVLKMEIddViFk/m41USp+LhOkV0GKOqHgCuU8R8vCORRlXndYAwjpj0ByrdjQQjAMYvQ5UpKvXOxG/VliSLwLR0IVtcIIwj/JJva+OHZ5uf+3SrJBJpgCWJDmPYykvLEJW7HakdHy1JdBjb4DyJ1ga/VbPdfEWlBQDuwV7+tVagtcHzXDtPP5vtqNztSFpgXt8YXl8CsO3u2vXETO1aYqa2lpgpp49PuZJMyt2OpH/OOvzmzIa/xyfYkIZ41W801gAAAABJRU5ErkJggg==",
            ["blue"] = "iVBORw0KGgoAAAANSUhEUgAAABoAAAAUCAYAAACTQC2+AAAAAXNSR0IArs4c6QAAAURJREFUSIntkj1LA0EQhp8L5i4e5BuMwSKVEAJ26eRa6yu0s7W00MrC0kIQjI2d/yAI+RPprP0BAckRMJoElnwUa3HeZmP2QrASyVvt7sy8z8ywsNFG/0a5Wl2Wrp5krlaXANY6Rdnjczmztowx0WwseeTPbmR0tou7AJirNQBAIl8iYyfNSScX0gSU7RbWoc/0PQjPqyCJfAnHTjIJOlAoA4T36Qz6XSiUcb4bGPYCRLNh6dPIdguAz9cXywjaPjqVqUp1ARIZ6tAIFjUw7AU4uaKKfTw/Kv/Y1a2E9Lthkj5l0IGEHWe3DHI9X2b2DxYgk6AzN46BjEcDUpXqfNIfsnQAgLOzF9vVKo1HA1LprLrra1MTuZ4vdcCk98bd5UPYgOuqdyGEehNCqJgQgvvba/ppM8Qo1/Ol/nPW0W9qNvp7+gLwmIAkOJOgFgAAAABJRU5ErkJggg==",
            ["sunset"] = "iVBORw0KGgoAAAANSUhEUgAAABoAAAAUCAYAAACTQC2+AAAAAXNSR0IArs4c6QAAAXdJREFUSIntkz1LQlEYx3+3PIJoWV1FEdMhGmq1FsGtXXBrEKLBpaUPkC66NNfUEC5Bk9BnEPwCBS613BySqwVxK/QQp6G8vl0lIqLB/3Se5/zP8zvPeYGZZvpraQAul+vXCyc3guooHaN0ZVCrm9q3QOlIQc2/C8e5SjOvjebOcpuqN46ueACYSkhHCgpAd0cRwu3oyVBUTsBy1WQvFaTx+Ea5ak4+unSkoHqAh9dbdBEDQAg3UnZpSwNdxOwNmM/3VJp5bbCbctUEoFY3tTmnXe4ED8YgQrjtGEAXMdrSQMouUnYJLq6SCRVtiNHqUKubWq1ualOPbhQC2HFbGjZs0LuVugA8jvXGQEk9q9aWtocgo104QV66T4S+Lt5odcZA9h1lE34FEA94ub7Zn9ToREXjpyyHdTsuVe6GHocLIJvwq3jAaycXwyecF3Y/A5+v77asfs6y+nOWxeFx3zYKwenDZhN+lVpfIHfZGDNP0k/WzPT/9AHAi4szMVNJRQAAAABJRU5ErkJggg==",
            ["bubblegum"] = "iVBORw0KGgoAAAANSUhEUgAAABoAAAAUCAYAAACTQC2+AAAAAXNSR0IArs4c6QAAAVRJREFUSIntkjFIAmEUx38HYXAkN0goHIKI4GDgcuMtjYWDQzTUcqOrc7QXRLMQiKtNDW5RRAUStGjHTbddYIcpSGDj1yB3nnqnrYX/6Xvfe+//e+/jg7XW+jfKZjSRPxEim9EEgPSbpqZqiM3v8Fx52FjwSBmm8M6KWmAlqKkaAqCwlUZW4qE1XdtaAKYMU9CqQKkGgPxgRIOaqiE8QLtnUZTTkyYlznj0RWfsUJSnA3Rti/KwIR2ZugC4v6hBqwLAx+ezFAo6k/dEKa3NQDzDINSDeQN0bYvrRxsAtzPi7vjN99+I2mgZpDN2AGYg7Z5Fvf5CkkSo3wKoGtPF4c7uDKTds3zjKIgzcEnmEv4285KCAAAtmYtacqmcgcvt1bsfB5/N36ga00UQ8OraXN48TYLtQHWf6V0/kOvD+cF+JCRU1ZguzPypWFk41+P9trX+rn4AuGiGP5x9Ac8AAAAASUVORK5CYII=",
            ["golden"] = "iVBORw0KGgoAAAANSUhEUgAAABoAAAAUCAYAAACTQC2+AAAAAXNSR0IArs4c6QAAAY5JREFUSIntlL9LAmEYx78n51nSEHVhp05NDaUE7oaDSxRCQyA5ODkZNDjVFEFBg63OOfcHuLk0OaQILkUgdOeFCAq+4g94G+p9U+9OKwwa/C73Ps97z/fzPO97HDDXX4qWgnRS/B3Zphmz9fhzJiDBlxdoKUiZqZZwcChbzwTExEyVTJfnlEwXgi8vTDMO+N1Ue9ilAb+bAsDEAjbRyfUzRFvf9J10Vjd4VHJhfrzLSz1MBSWjXgoArlUJdsn8yGp6wwCs5MI0lirj7mYLABBLla1ByaiXMkBVbUORRQCAXXKg3+tCqw+gyCJvoKY3kM7qwvA0sVQZAFAoqoJoBjneWzNAmCGLFVn8hH3UrLtWcBpf5JCnlzcUiiofxBTEOreCaPUBAIxMWVXbkBasvy0DKBKS6famPAKpqm1ubAVptjo4i+/wacYlDAMAwONyWnY1Sc1WB0cHHh7vJx5H7l9kkGHAq05we3EIAHA6v/KEEJ4jhPA9QgjOr+4tIaaKhGR6mdz40W/mNzVz/T+9A6Asr2VFwz0WAAAAAElFTkSuQmCC",
            ["ninja"] = "iVBORw0KGgoAAAANSUhEUgAAABoAAAAUCAYAAACTQC2+AAAAAXNSR0IArs4c6QAAAWpJREFUSIntkjFLw0AYhp/YRBIaYmmXSqUIHRyLiHTU6iIo1KX0H/jnhGI3lwrtJlI7dytZ7BJJbUljosSh5kgaqygiDn2n7+7e7557Pw5WWumvJQHIsvzrF+dyxSCfP2A06mBZpiRA5v5RAFC8u5EWmwqFw6CpTwCojTOxs9GonfCXSrUgrFXVSCYKYSEwug718DQW9aaxQdQfBdl2n0xmFwDb7n88utudvQCg8byFoqRwHAtFmSdRlBS+/4rv2zT1aayvMuhJF/enQbsuCwAQH91XEEVJAcSgvm/HHjCZjEmndY4vX7g+n2KaHZFybTFJZdCTzh7lTyG+bycgjmORmtto12VOrvRY2kSibLYc5HKlGMRxrPcL55CwjkIAPM/FMPLCE00kQNlsOQDQtPiv+o48z0XT1AQEQA5TRAGzmU212gBAVdfFvut6Ys91PXHmuh7dbmspZOnoDGOb4bCVMC/TT3pW+n96A6STo45m/xD9AAAAAElFTkSuQmCC",
            ["rainbow"] = "iVBORw0KGgoAAAANSUhEUgAAABoAAAAUCAYAAACTQC2+AAAAAXNSR0IArs4c6QAAAdtJREFUSIntkb1v01AUxX9OYyeNihucRIlePmiXqgNSRFn42Ji64YKECmJlADLwR+QP6JQsHbsgxMDEBEggBja6oAjBYCG7wYWQOnWI7IjHYBGUphF0Qyhnujr3vXPuuRdmmOG/QbFYldv5vCwWqxJA+ZtPtUJBxpUjT8MQsutstXYmNO7VDSncqC48VAGI/8kAoKBpqLHY70buGtgH4MGDrCnJLowZ/uA7zccr3N14TzuE5nx5eqKaYchCIoGqKFhBgKjcBEBt9wnlECf8iqgso85FE+/1XLZaO4pRvyo3eQXAk2YZANvePbqPCJcyt2Xp1Crn01+wBm1EKhcJ2gdYwWdEZRkAp7+PSOUAaPklnl7ZI1H8BMBl5xmPGt2R/tTVfeypdIcB6fhZPBlNradshLGE40UHEMkcBJJWWMb1LUCDwTl6yTfE0Mb0JhKV5k2ZT13kMLRIxgTxOZXD0OKMDum4oDt0gKgG0BIqrm/xru2RWVhlvwbDzgt8QvzGS2XCaNFciw7//MK0kFMx8EHRPFYyOm/XI87dboyFUH6ZaOL0iAycb+zevwNASjdGfN/rjLi+1xn1+l6HW5uv+bChH2tyLBbNNSnq1+VJEiXNqryxVD/Rnxn+PfwE4JWgyajsTN8AAAAASUVORK5CYII=",
        };

        /// <summary>
        /// Turtle sprite for a skin. Default skin = "turtle"; others = "turtle_{id}".
        /// A PNG with that name in Resources/Sprites replaces the generated one.
        /// </summary>
        public static Sprite GetTurtle(string skinId)
        {
            var skin = SkinDatabase.Get(skinId);
            string name = skin.id == SkinDatabase.DefaultId ? "turtle" : "turtle_" + skin.id;
            if (cache.TryGetValue(name, out var s) && s != null) return s;
            s = LoadOverrideSprite(name);
            if (s == null) s = Turtle(skin);
            cache[name] = s;
            return s;
        }

        static Sprite Turtle(SkinDef skin)
        {
            string id = skin != null && !string.IsNullOrEmpty(skin.id) ? skin.id : SkinDatabase.DefaultId;
            if (!TurtlePngBase64.TryGetValue(id, out var b64)) b64 = TurtlePngBase64[SkinDatabase.DefaultId];
            var tex = new Texture2D(26, 20, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.LoadImage(Convert.FromBase64String(b64));
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
        }

        static Sprite EnergyIcon()
        {
            var c = new PixelCanvas(12, 16);
            c.Triangle(new Vector2(7, 14), new Vector2(2, 7), new Vector2(7, 7), Yellow);
            c.Triangle(new Vector2(5, 9), new Vector2(10, 9), new Vector2(4, 1), Orange);
            c.Rect(4, 7, 4, 2, White);
            c.Outline(K);
            return c.ToSprite(16f);
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

        static Sprite SkyGradient(string botHex, string topHex)
        {
            var c = new PixelCanvas(2, 48);
            var bot = PixelCanvas.C(botHex);
            var top = PixelCanvas.C(topHex);
            for (int y = 0; y < 48; y++)
            {
                var col = Color.Lerp(bot, top, Mathf.Pow(y / 47f, 0.8f));
                c.Rect(0, y, 2, 1, col);
            }
            return c.ToSprite(2f);
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

        static Sprite HillsFarDesert()
        {
            const int w = 256, h = 64;
            var c = new PixelCanvas(w, h);
            var terracotta = PixelCanvas.C("#c97b53");
            var band = PixelCanvas.C("#9c5333");
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                float mesaWave = Mathf.Sin(t * 2f) * 0.85f + Mathf.Sin(t * 4f + 0.6f) * 0.35f;
                float clamped = Mathf.Clamp(mesaWave, -0.55f, 0.65f); // flat-topped mesas
                int top = Mathf.RoundToInt(h * 0.44f + clamped * 22f);
                for (int y = 0; y < top && y < h; y++)
                    c.Set(x, y, (y == top - 6 || y == top - 7 || y == 14) ? band : terracotta);
            }
            return c.ToSprite(8f, new Vector2(0.5f, 0f));
        }

        static Sprite HillsNearDesert()
        {
            const int w = 256, h = 48;
            var c = new PixelCanvas(w, h);
            var dune = PixelCanvas.C("#d9964e");
            var highlight = PixelCanvas.C("#eab26c");
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                int top = Mathf.RoundToInt(h * 0.42f + Mathf.Sin(t * 3f) * 9f + Mathf.Sin(t * 6f + 1.2f) * 3.5f);
                for (int y = 0; y < top && y < h; y++)
                    c.Set(x, y, y >= top - 2 ? highlight : dune);
            }
            return c.ToSprite(8f, new Vector2(0.5f, 0f));
        }

        static Sprite HillsFarSnow()
        {
            const int w = 256, h = 64;
            var c = new PixelCanvas(w, h);
            var slate = PixelCanvas.C("#5c7a99");
            var snow = PixelCanvas.C("#eef7ff");
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                // jagged triangle-wave alpine peaks
                float p1 = Mathf.Abs(((t * 3f / (Mathf.PI * 2f)) % 1f) * 2f - 1f);
                float p2 = Mathf.Abs(((t * 5f / (Mathf.PI * 2f) + 0.3f) % 1f) * 2f - 1f);
                int top = Mathf.RoundToInt(18f + p1 * 28f + p2 * 12f);
                int snowLine = Mathf.Max(34, top - 9);
                for (int y = 0; y < top && y < h; y++)
                    c.Set(x, y, y >= snowLine ? snow : slate);
            }
            return c.ToSprite(8f, new Vector2(0.5f, 0f));
        }

        static Sprite HillsNearSnow()
        {
            const int w = 256, h = 48;
            var c = new PixelCanvas(w, h);
            var slope = PixelCanvas.C("#8cb6d9");
            var snowTop = PixelCanvas.C("#e6f4ff");
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)w * Mathf.PI * 2f;
                int top = Mathf.RoundToInt(h * 0.46f + Mathf.Sin(t * 2f + 0.5f) * 8f + Mathf.Cos(t * 5f) * 4f);
                for (int y = 0; y < top && y < h; y++)
                    c.Set(x, y, y >= top - 4 ? snowTop : slope);
            }
            return c.ToSprite(8f, new Vector2(0.5f, 0f));
        }

        static Sprite ConeDesert()
        {
            var c = new PixelCanvas(12, 16);
            var cactus = PixelCanvas.C("#3ca35a");
            var darkCactus = PixelCanvas.C("#27753e");
            c.Rect(2, 1, 8, 2, Brown);
            c.Ellipse(6, 7, 4f, 5.5f, cactus);
            c.Rect(5, 3, 2, 9, darkCactus);
            c.Rect(4, 12, 4, 2, Yellow); // desert prickly top bloom
            c.Set(3, 6, White); c.Set(8, 8, White); c.Set(3, 10, White);
            c.Outline(K);
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite ConeSnow()
        {
            var c = new PixelCanvas(12, 16);
            var ice = PixelCanvas.C("#6ee7ff");
            var deepIce = PixelCanvas.C("#2d9cdb");
            c.Rect(1, 0, 10, 2, PixelCanvas.C("#d8efff"));
            c.Triangle(new Vector2(1.5f, 2), new Vector2(10.5f, 2), new Vector2(6f, 15f), deepIce);
            c.Triangle(new Vector2(2.5f, 2), new Vector2(6.5f, 2), new Vector2(6f, 15f), ice);
            c.Rect(5, 8, 1, 5, White);
            c.Outline(K);
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite BoulderDesert()
        {
            var c = new PixelCanvas(26, 26);
            var rock = PixelCanvas.C("#c86432");
            var hi = PixelCanvas.C("#eb8c4c");
            var crack = PixelCanvas.C("#7a3216");
            c.Ellipse(13, 13, 12.5f, 12.5f, rock);
            c.Ellipse(10, 17, 5f, 3.8f, hi);
            c.Line(14, 5, 18, 11, crack);
            c.Line(18, 11, 15, 16, crack);
            c.Line(6, 9, 10, 11, crack);
            c.Outline(K);
            return c.ToSprite(16f);
        }

        static Sprite BoulderSnow()
        {
            var c = new PixelCanvas(26, 26);
            var snow = PixelCanvas.C("#eef7ff");
            var shade = PixelCanvas.C("#99c8f0");
            c.Ellipse(13, 13, 12.5f, 12.5f, shade);
            c.Ellipse(12, 15, 10.5f, 9.5f, snow);
            c.Ellipse(9, 18, 4.5f, 3f, White);
            c.Outline(K);
            return c.ToSprite(16f);
        }

        static Sprite PropMeadowTree()
        {
            var c = new PixelCanvas(24, 34);
            var bark = PixelCanvas.C("#7f4f24");
            var leafDark = PixelCanvas.C("#2f8f46");
            var leaf = PixelCanvas.C("#52c84a");
            var leafHi = PixelCanvas.C("#8ce86b");
            c.Rect(10, 0, 4, 13, bark);
            c.Ellipse(12, 19, 9f, 8f, leafDark);
            c.Ellipse(11, 22, 8f, 7f, leaf);
            c.Ellipse(9, 25, 4.5f, 3.5f, leafHi);
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite PropMeadowBush()
        {
            var c = new PixelCanvas(20, 12);
            var leafDark = PixelCanvas.C("#2f8f46");
            var leaf = PixelCanvas.C("#52c84a");
            c.Ellipse(6, 4, 4.5f, 3.5f, leafDark);
            c.Ellipse(14, 4, 4.5f, 3.5f, leafDark);
            c.Ellipse(10, 6, 5.5f, 4.2f, leaf);
            c.Set(6, 5, Yellow); c.Set(11, 7, PixelCanvas.C("#ff7eb6")); c.Set(15, 5, Yellow);
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite PropDesertCactus()
        {
            var c = new PixelCanvas(18, 30);
            var green = PixelCanvas.C("#3ca35a");
            var hi = PixelCanvas.C("#5cc976");
            c.Rect(7, 0, 4, 24, green);
            c.Rect(8, 2, 1, 20, hi);
            // left arm
            c.Rect(2, 10, 5, 3, green);
            c.Rect(2, 12, 3, 7, green);
            // right arm
            c.Rect(11, 13, 5, 3, green);
            c.Rect(13, 15, 3, 7, green);
            // desert bloom on top
            c.Rect(7, 24, 4, 2, PixelCanvas.C("#ff5c8a"));
            c.Set(8, 26, Yellow);
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite PropDesertRock()
        {
            var c = new PixelCanvas(26, 18);
            var terra = PixelCanvas.C("#c46233");
            var sand = PixelCanvas.C("#df8949");
            var dark = PixelCanvas.C("#8a391b");
            c.Rect(3, 0, 12, 14, terra);
            c.Rect(5, 14, 8, 2, sand);
            c.Rect(3, 6, 12, 2, dark);
            c.Rect(14, 0, 9, 9, sand);
            c.Rect(14, 4, 9, 1, dark);
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite PropSnowPine()
        {
            var c = new PixelCanvas(22, 36);
            var trunk = PixelCanvas.C("#5c3a21");
            var pine = PixelCanvas.C("#2b5c4f");
            var snow = PixelCanvas.C("#eef7ff");
            c.Rect(9, 0, 4, 7, trunk);
            c.Triangle(new Vector2(2, 7), new Vector2(20, 7), new Vector2(11, 20), pine);
            c.Triangle(new Vector2(4, 15), new Vector2(18, 15), new Vector2(11, 27), pine);
            c.Triangle(new Vector2(6, 22), new Vector2(16, 22), new Vector2(11, 33), pine);
            // snow caps on bough tips
            c.Triangle(new Vector2(7, 26), new Vector2(15, 26), new Vector2(11, 33), snow);
            c.Rect(4, 15, 3, 2, snow); c.Rect(15, 15, 3, 2, snow);
            c.Rect(2, 7, 4, 2, snow); c.Rect(16, 7, 4, 2, snow);
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite PropSnowSnowman()
        {
            var c = new PixelCanvas(18, 24);
            var snow = PixelCanvas.C("#eef7ff");
            c.Ellipse(9, 5, 5.5f, 4.5f, snow);
            c.Ellipse(9, 12, 4.2f, 3.6f, snow);
            c.Ellipse(9, 18, 3.2f, 3f, snow);
            c.Rect(5, 14, 8, 2, Red);        // scarf
            c.Rect(11, 12, 2, 3, Red);
            c.Set(8, 19, DarkGray); c.Set(10, 19, DarkGray); // eyes
            c.Rect(9, 17, 3, 1, Orange);     // carrot nose
            c.Rect(5, 21, 8, 2, DarkGray);   // hat
            return c.ToSprite(16f, new Vector2(0.5f, 0f));
        }

        static Sprite TurboFlame()
        {
            const int w = 28, h = 14;
            var c = new PixelCanvas(w, h);
            for (int x = 0; x < w; x++)
            {
                float t = (x + 0.5f) / w; // 0 = left tip, 1 = right nozzle base
                float swell = x >= 24 ? Mathf.Lerp(5.4f, 4.0f, (x - 24f) / 3f) : 5.8f * Mathf.Pow(t, 0.58f);
                float jag = x < 16 ? (Mathf.Sin(x * 1.4f) * 0.55f) : 0f;
                float r = Mathf.Max(0.4f, swell + jag);
                for (int y = 0; y < h; y++)
                {
                    float dy = Mathf.Abs(y + 0.5f - h * 0.5f);
                    if (dy <= r)
                    {
                        byte a = dy <= r * 0.62f ? (byte)245 : (byte)150;
                        c.Set(x, y, new Color32(255, 255, 255, a));
                    }
                }
            }
            return c.ToSprite(16f, new Vector2(1f, 0.5f));
        }

        static Sprite TurboCore()
        {
            const int w = 18, h = 8;
            var c = new PixelCanvas(w, h);
            for (int x = 0; x < w; x++)
            {
                float t = (x + 0.5f) / w;
                float r = (x >= 15 ? 2.6f : 3.2f * Mathf.Pow(t, 0.7f));
                for (int y = 0; y < h; y++)
                {
                    float dy = Mathf.Abs(y + 0.5f - h * 0.5f);
                    if (dy <= r)
                    {
                        byte a = dy <= r * 0.6f ? (byte)255 : (byte)185;
                        c.Set(x, y, new Color32(255, 255, 255, a));
                    }
                }
            }
            return c.ToSprite(16f, new Vector2(1f, 0.5f));
        }

        static Sprite TurboGlow()
        {
            const int s = 24;
            var c = new PixelCanvas(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f - s * 0.5f) / (s * 0.5f);
                    float dy = (y + 0.5f - s * 0.5f) / (s * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = d < 0.32f ? (byte)185 : d < 0.58f ? (byte)120 : d < 0.82f ? (byte)58 : d < 1f ? (byte)24 : (byte)0;
                    if (a > 0) c.Set(x, y, new Color32(255, 255, 255, a));
                }
            return c.ToSprite(16f, new Vector2(0.5f, 0.5f));
        }

        static Sprite TurboRing()
        {
            const int w = 14, h = 28;
            var c = new PixelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f - w * 0.5f) / (w * 0.45f);
                    float ny = (y + 0.5f - h * 0.5f) / (h * 0.46f);
                    float rn = Mathf.Sqrt(nx * nx + ny * ny);
                    if (rn >= 0.70f && rn <= 1.0f)
                    {
                        byte a = (rn >= 0.80f && rn <= 0.92f) ? (byte)235 : (byte)120;
                        c.Set(x, y, new Color32(255, 255, 255, a));
                    }
                }
            return c.ToSprite(16f, new Vector2(0.5f, 0.5f));
        }
    }
}
