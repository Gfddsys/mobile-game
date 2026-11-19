using UnityEngine;

namespace TurtleBlaster
{
    public struct BiomeDef
    {
        public int id;
        public string name;
        public string skySprite, farSprite, nearSprite;
        public string coneSprite, boulderSprite;
        public string[] decorSprites;
        public Color32 topA, topB, dirtTop, dirtBot;
        public Color cloudTint, bannerColor, decorTint;
        public float extraStarAlpha;
    }

    /// <summary>
    /// 3 repeating level variants (Sunny Meadow -> Canyon Desert -> Frost Peaks),
    /// each lasting ZoneLength metres with a smooth crossfade across TransitionLength.
    /// </summary>
    public static class LevelBiomes
    {
        public const float ZoneLength = 250f;
        public const float TransitionLength = 30f;
        public const int Count = 3;

        static readonly BiomeDef[] Variants =
        {
            new BiomeDef
            {
                id = 0,
                name = "Sunny Meadow",
                skySprite = "sky",
                farSprite = "hills_far",
                nearSprite = "hills_near",
                coneSprite = "cone",
                boulderSprite = "boulder",
                decorSprites = new[] { "prop_meadow_tree", "prop_meadow_bush" },
                topA = PixelCanvas.C("#52c84a"),
                topB = PixelCanvas.C("#43b03d"),
                dirtTop = PixelCanvas.C("#8d5a2b"),
                dirtBot = PixelCanvas.C("#4a2f17"),
                cloudTint = Color.white,
                bannerColor = PixelCanvas.C("#4cd964"),
                decorTint = new Color(0.82f, 0.90f, 0.88f, 0.44f),
                extraStarAlpha = 0f,
            },
            new BiomeDef
            {
                id = 1,
                name = "Canyon Desert",
                skySprite = "sky_desert",
                farSprite = "hills_far_desert",
                nearSprite = "hills_near_desert",
                coneSprite = "cone_desert",
                boulderSprite = "boulder_desert",
                decorSprites = new[] { "prop_desert_cactus", "prop_desert_rock" },
                topA = PixelCanvas.C("#e8b85a"),
                topB = PixelCanvas.C("#d9a243"),
                dirtTop = PixelCanvas.C("#b55d34"),
                dirtBot = PixelCanvas.C("#632a17"),
                cloudTint = new Color(1f, 0.90f, 0.78f, 1f),
                bannerColor = PixelCanvas.C("#ffb703"),
                decorTint = new Color(0.95f, 0.86f, 0.76f, 0.42f),
                extraStarAlpha = 0.22f,
            },
            new BiomeDef
            {
                id = 2,
                name = "Frost Peaks",
                skySprite = "sky_snow",
                farSprite = "hills_far_snow",
                nearSprite = "hills_near_snow",
                coneSprite = "cone_snow",
                boulderSprite = "boulder_snow",
                decorSprites = new[] { "prop_snow_pine", "prop_snow_snowman" },
                topA = PixelCanvas.C("#eef7ff"),
                topB = PixelCanvas.C("#cbe6ff"),
                dirtTop = PixelCanvas.C("#5c708a"),
                dirtBot = PixelCanvas.C("#273447"),
                cloudTint = new Color(0.85f, 0.94f, 1f, 1f),
                bannerColor = PixelCanvas.C("#6ee7ff"),
                decorTint = new Color(0.84f, 0.91f, 0.98f, 0.45f),
                extraStarAlpha = 0.70f,
            },
        };

        public static int GetIndex(float worldX) =>
            Mathf.FloorToInt(Mathf.Max(0f, worldX) / ZoneLength) % Count;

        public static BiomeDef Get(int index)
        {
            int idx = ((index % Count) + Count) % Count;
            return Variants[idx];
        }

        public static void Evaluate(float worldX, out int fromIndex, out int toIndex, out float blend)
        {
            float x = Mathf.Max(0f, worldX);
            int idx = Mathf.FloorToInt(x / ZoneLength);
            float local = x - idx * ZoneLength;
            fromIndex = idx % Count;
            if (local > ZoneLength - TransitionLength)
            {
                toIndex = (idx + 1) % Count;
                blend = Mathf.SmoothStep(0f, 1f, (local - (ZoneLength - TransitionLength)) / TransitionLength);
            }
            else
            {
                toIndex = fromIndex;
                blend = 0f;
            }
        }

        public static void EvaluateGroundColors(float worldX, out Color32 topA, out Color32 topB, out Color32 dirtTop, out Color32 dirtBot)
        {
            Evaluate(worldX, out int fromIdx, out int toIdx, out float blend);
            var a = Variants[fromIdx];
            if (blend <= 0.001f || fromIdx == toIdx)
            {
                topA = a.topA;
                topB = a.topB;
                dirtTop = a.dirtTop;
                dirtBot = a.dirtBot;
                return;
            }
            var b = Variants[toIdx];
            topA = Color32.Lerp(a.topA, b.topA, blend);
            topB = Color32.Lerp(a.topB, b.topB, blend);
            dirtTop = Color32.Lerp(a.dirtTop, b.dirtTop, blend);
            dirtBot = Color32.Lerp(a.dirtBot, b.dirtBot, blend);
        }
    }
}
