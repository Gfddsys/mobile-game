using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Tiny software canvas used to draw the placeholder pixel-art at runtime.
    /// Coordinates start at the bottom-left corner. Artists can replace any
    /// generated sprite by dropping a PNG with the same name into
    /// Resources/Sprites (see <see cref="SpriteLibrary"/>).
    /// </summary>
    public class PixelCanvas
    {
        public readonly int width, height;
        readonly Color32[] px;

        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public PixelCanvas(int w, int h)
        {
            width = w; height = h;
            px = new Color32[w * h];
        }

        public static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            px[y * width + x] = c;
        }

        public Color32 Get(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return Clear;
            return px[y * width + x];
        }

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int j = y0; j <= y1; j++)
                for (int i = x0; i <= x1; i++)
                {
                    float dx = (i + 0.5f - cx) / rx, dy = (j + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(i, j, c);
                }
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Filled triangle (simple scanline over bounding box).</summary>
        public void Triangle(Vector2 a, Vector2 b, Vector2 cpt, Color32 c)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, cpt.x)));
            int maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, cpt.x)));
            int minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, cpt.y)));
            int maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, cpt.y)));
            for (int j = minY; j <= maxY; j++)
                for (int i = minX; i <= maxX; i++)
                {
                    var p = new Vector2(i + 0.5f, j + 0.5f);
                    float d1 = Sign(p, a, b), d2 = Sign(p, b, cpt), d3 = Sign(p, cpt, a);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0;
                    bool pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Set(i, j, c);
                }
        }

        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        /// <summary>Clears every pixel below the given row (used to cut domes).</summary>
        public void ClearBelow(int row)
        {
            for (int j = 0; j < row; j++)
                for (int i = 0; i < width; i++) Set(i, j, Clear);
        }

        /// <summary>Adds a 1px outline around all opaque pixels.</summary>
        public void Outline(Color32 c)
        {
            var copy = (Color32[])px.Clone();
            for (int j = 0; j < height; j++)
                for (int i = 0; i < width; i++)
                {
                    if (copy[j * width + i].a != 0) continue;
                    bool near = false;
                    for (int dj = -1; dj <= 1 && !near; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            if (di != 0 && dj != 0) continue; // 4-neighbourhood
                            int x = i + di, y = j + dj;
                            if (x < 0 || y < 0 || x >= width || y >= height) continue;
                            if (copy[y * width + x].a != 0) { near = true; break; }
                        }
                    if (near) px[j * width + i] = c;
                }
        }

        public Texture2D ToTexture()
        {
            var t = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        public Sprite ToSprite(float pixelsPerUnit = 16f, Vector2? pivot = null, Vector4? border = null)
        {
            var tex = ToTexture();
            return Sprite.Create(tex, new Rect(0, 0, width, height), pivot ?? new Vector2(0.5f, 0.5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect, border ?? Vector4.zero);
        }
    }
}
