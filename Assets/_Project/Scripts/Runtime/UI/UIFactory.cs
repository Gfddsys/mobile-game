using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurtleBlaster
{
    /// <summary>Small helpers to build uGUI hierarchies in code (reference resolution 1920x1080).</summary>
    public static class UIFactory
    {
        public static readonly Color Ink = PixelCanvas.C("#241711");
        public static readonly Color Panel = PixelCanvas.C("#5c4438");
        public static readonly Color CardBg = PixelCanvas.C("#3b2c25");
        public static readonly Color SlotDark = PixelCanvas.C("#271d18");
        public static readonly Color GlassDark = new Color(0.18f, 0.13f, 0.10f, 0.90f);
        public static readonly Color Accent = PixelCanvas.C("#ffd147");
        public static readonly Color Good = PixelCanvas.C("#4cd964");
        public static readonly Color Bad = PixelCanvas.C("#f24c4c");
        public static readonly Color Blue = PixelCanvas.C("#3fa7f5");
        public static readonly Color Purple = PixelCanvas.C("#a362ea");
        public static readonly Color Amber = PixelCanvas.C("#f58a27");
        public static readonly Color TextMuted = PixelCanvas.C("#e2d0be");

        static Font font;
        static bool isPixelFont;
        static bool hookedFontRebuild;

        public static Font DefaultFont
        {
            get
            {
                if (font == null)
                {
                    font = Resources.Load<Font>("Fonts/VT323-Regular");
                    isPixelFont = font != null;
                    if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                    ApplyPointFilter(font);
                    if (!hookedFontRebuild)
                    {
                        hookedFontRebuild = true;
                        Font.textureRebuilt += ApplyPointFilter;
                    }
                }
                return font;
            }
        }

        static void ApplyPointFilter(Font f)
        {
            if (f != null && f == font && isPixelFont && f.material != null && f.material.mainTexture != null)
                f.material.mainTexture.filterMode = FilterMode.Point;
        }

        public static int ScaleFontSize(int size)
        {
            _ = DefaultFont;
            return isPixelFont ? Mathf.RoundToInt(size * 1.25f) : size;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static UnityEngine.UI.Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite; img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public static UnityEngine.UI.Image PanelBox(Transform parent, string name, Color color)
        {
            var img = Image(parent, name, SpriteLibrary.Get("ui_panel"), color, true);
            img.pixelsPerUnitMultiplier = 0.25f;
            if (color.r + color.g + color.b <= 2.2f)
            {
                var frame = Image(img.transform, "Frame", SpriteLibrary.Get("ui_panel_frame"),
                    new Color(1f, 1f, 1f, Mathf.Clamp01(color.a + 0.12f)), true);
                frame.pixelsPerUnitMultiplier = 0.25f;
                var frt = (RectTransform)frame.transform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = frt.offsetMax = Vector2.zero;
            }
            return img;
        }

        public static UnityEngine.UI.Image GlassPill(Transform parent, string name, Color color)
        {
            var img = Image(parent, name, SpriteLibrary.Get("ui_pill"), color, true);
            img.pixelsPerUnitMultiplier = 0.33f;
            if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) <= 0.55f)
            {
                var frame = Image(img.transform, "Frame", SpriteLibrary.Get("ui_pill_frame"),
                    new Color(1f, 1f, 1f, Mathf.Clamp01(color.a + 0.12f)), true);
                frame.pixelsPerUnitMultiplier = 0.33f;
                var frt = (RectTransform)frame.transform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = frt.offsetMax = Vector2.zero;
            }
            return img;
        }

        public static UnityEngine.UI.Image PixelPip(Transform parent, string name, Color color)
        {
            var img = Image(parent, name, SpriteLibrary.Get("ui_pip"), color, true);
            img.pixelsPerUnitMultiplier = 0.5f;
            return img;
        }

        public static UnityEngine.UI.Image AddTopAccent(Transform panel, Color color, float height = 8f, float insetX = 20f)
        {
            height = Mathf.Max(8f, height);
            float ix = Mathf.Max(20f, insetX);
            var bar = PixelPip(panel, "TopAccent", color);
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(ix, -15f - height);
            rt.offsetMax = new Vector2(-ix, -15f);
            return bar;
        }

        public sealed class WalletDisplay
        {
            public Text coins;
            public Text parts;

            public void Set(int coinCount, int partCount)
            {
                if (coins != null) coins.text = coinCount.ToString("N0");
                if (parts != null) parts.text = partCount.ToString("N0");
            }
        }

        public static WalletDisplay WalletPill(Transform parent, string name, Color bgColor, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var pill = GlassPill(parent, name, bgColor);
            Place((RectTransform)pill.transform, anchor, pos, size, pivot);

            var cIcon = Image(pill.transform, "CoinIcon", SpriteLibrary.Get("coin"), Color.white);
            cIcon.preserveAspect = true;
            Place((RectTransform)cIcon.transform, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(34, 34), new Vector2(0, 0.5f));
            var cTxt = Label(pill.transform, "0", 32, Accent, TextAnchor.MiddleLeft, FontStyle.Bold, "Coins");
            Place((RectTransform)cTxt.transform, new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(size.x * 0.5f - 74f, 48), new Vector2(0, 0.5f));

            var sep = Image(pill.transform, "Divider", SpriteLibrary.Get("square"), new Color(1f, 1f, 1f, 0.14f));
            Place((RectTransform)sep.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 34), new Vector2(0.5f, 0.5f));

            var pIcon = Image(pill.transform, "PartIcon", SpriteLibrary.Get("part"), Color.white);
            pIcon.preserveAspect = true;
            Place((RectTransform)pIcon.transform, new Vector2(0.5f, 0.5f), new Vector2(18, 0), new Vector2(34, 34), new Vector2(0, 0.5f));
            var pTxt = Label(pill.transform, "0", 32, new Color(0.60f, 0.85f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold, "Parts");
            Place((RectTransform)pTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(62, 0), new Vector2(size.x * 0.5f - 74f, 48), new Vector2(0, 0.5f));

            return new WalletDisplay { coins = cTxt, parts = pTxt };
        }

        static RectTransform HeaderBarBase(Transform parent, string titleText, Action onBack)
        {
            var bar = Image(parent, "HeaderBar", SpriteLibrary.Get("square"), new Color(0.14f, 0.10f, 0.08f, 0.98f));
            var barRt = (RectTransform)bar.transform;
            barRt.anchorMin = new Vector2(0, 1);
            barRt.anchorMax = new Vector2(1, 1);
            barRt.pivot = new Vector2(0.5f, 1);
            barRt.anchoredPosition = Vector2.zero;
            barRt.sizeDelta = new Vector2(0, 112);

            var checker = Image(barRt, "CheckerTrim", SpriteLibrary.Get("ui_checker"), Color.white);
            checker.type = UnityEngine.UI.Image.Type.Tiled;
            checker.pixelsPerUnitMultiplier = 0.5f;
            var crt = (RectTransform)checker.transform;
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(1, 0);
            crt.pivot = new Vector2(0.5f, 0);
            crt.anchoredPosition = new Vector2(0, 2);
            crt.sizeDelta = new Vector2(0, 8);

            var line = Image(barRt, "Divider", SpriteLibrary.Get("square"), Ink);
            var lrt = (RectTransform)line.transform;
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 0);
            lrt.pivot = new Vector2(0.5f, 0);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(0, 2);

            var back = MakeButton(barRt, "< BACK", new Color(0.85f, 0.30f, 0.36f), onBack, new Vector2(184, 66), 32);
            Place((RectTransform)back.transform, new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(184, 66), new Vector2(0, 0.5f));

            var title = Label(barRt, titleText, 64, Accent, TextAnchor.MiddleLeft);
            Place((RectTransform)title.transform, new Vector2(0, 0.5f), new Vector2(244, 0), new Vector2(720, 84), new Vector2(0, 0.5f));
            return barRt;
        }

        public static RectTransform HeaderBar(Transform parent, string titleText, Action onBack, out WalletDisplay wallet)
        {
            var barRt = HeaderBarBase(parent, titleText, onBack);
            wallet = WalletPill(barRt, "HeaderWalletPill", new Color(0.18f, 0.13f, 0.10f, 0.98f),
                new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(460, 62), new Vector2(1, 0.5f));
            return barRt;
        }

        public static RectTransform HeaderBar(Transform parent, string titleText, Action onBack, out Text walletText, string rightSubtext = null)
        {
            var barRt = HeaderBarBase(parent, titleText, onBack);
            var pill = GlassPill(barRt, "HeaderRightPill", new Color(0.18f, 0.13f, 0.10f, 0.98f));
            float pillW = rightSubtext != null ? 560f : 460f;
            Place((RectTransform)pill.transform, new Vector2(1, 0.5f), new Vector2(-34, 0), new Vector2(pillW, 62), new Vector2(1, 0.5f));
            walletText = Label(pill.transform, rightSubtext ?? "", rightSubtext != null ? 28 : 32, rightSubtext != null ? TextMuted : Color.white, TextAnchor.MiddleCenter);
            var wrt = (RectTransform)walletText.transform;
            wrt.anchorMin = Vector2.zero; wrt.anchorMax = Vector2.one;
            wrt.offsetMin = new Vector2(18, 0); wrt.offsetMax = new Vector2(-18, 0);
            return barRt;
        }

        public static UnityEngine.UI.Button MakeButton(Transform parent, string label, Color color, Action onClick,
            Vector2 size, int fontSize = 44)
        {
            var img = Image(parent, "Button_" + label, SpriteLibrary.Get("ui_button"), color, true);
            img.pixelsPerUnitMultiplier = 0.25f;
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.72f);
            btn.colors = cb;
            ((RectTransform)img.transform).sizeDelta = size;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Sfx("click");
                onClick?.Invoke();
            });
            var t = Label(img.transform, label, fontSize, Color.white);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12, 10); trt.offsetMax = new Vector2(-12, -2);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            img.gameObject.AddComponent<UIButtonFeel>();
            return btn;
        }

        public static UnityEngine.UI.Image AddButtonIcon(UnityEngine.UI.Button btn, string spriteName, Vector2 iconSize, float leftPadding = 28f)
        {
            var ic = Image(btn.transform, "Icon", SpriteLibrary.Get(spriteName), Color.white);
            Place((RectTransform)ic.transform, new Vector2(0, 0.5f), new Vector2(leftPadding, 3), iconSize, new Vector2(0, 0.5f));
            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null)
            {
                var trt = (RectTransform)txt.transform;
                trt.offsetMin = new Vector2(leftPadding + iconSize.x * 0.7f, 10);
            }
            return ic;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold, string name = "Label")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont; t.text = text; t.fontSize = ScaleFontSize(size); t.color = color;
            t.alignment = align; t.fontStyle = isPixelFont ? FontStyle.Normal : style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            float shDist = Mathf.Round(Mathf.Max(3f, size / 11f));
            var sh = rt.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = new Color(0.10f, 0.06f, 0.04f, 0.96f);
            sh.effectDistance = new Vector2(2f, -shDist);
            float outDist = Mathf.Round(Mathf.Max(2f, size / 14f));
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.11f, 0.07f, 0.05f, 0.96f);
            o.effectDistance = new Vector2(outDist, -outDist);
            return t;
        }

        public static void SetButtonLabel(UnityEngine.UI.Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static EventSystem EnsureEventSystem()
        {
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es = go.GetComponent<EventSystem>();
            }
            return es;
        }

        /// <summary>A horizontal fill bar. Returns the fill image.</summary>
        public static UnityEngine.UI.Image Bar(Transform parent, string name, Color fill, Vector2 size, out RectTransform root)
        {
            var bg = GlassPill(parent, name, new Color(0.11f, 0.08f, 0.06f, 0.98f));
            root = (RectTransform)bg.transform;
            root.sizeDelta = size;
            var f = Image(bg.transform, "Fill", SpriteLibrary.Get("ui_bar_fill"), fill);
            var frt = (RectTransform)f.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(6, 6); frt.offsetMax = new Vector2(-6, -6);
            f.type = UnityEngine.UI.Image.Type.Filled;
            f.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            f.fillAmount = 1f;
            return f;
        }
    }

    /// <summary>Button that is "held" while a finger / mouse is down. Supports multi-touch.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> OnHold;
        public bool Held { get; private set; }
        UnityEngine.UI.Image img;
        Color idle, targetColor;

        void Awake()
        {
            img = GetComponent<UnityEngine.UI.Image>();
            if (img != null) idle = targetColor = img.color;
        }

        void Set(bool v)
        {
            if (Held == v) return;
            Held = v;
            targetColor = v ? new Color(idle.r, idle.g, idle.b, Mathf.Min(1f, idle.a + 0.35f)) : idle;
            OnHold?.Invoke(v);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float k = 1f - Mathf.Exp(-24f * dt);
            if (img != null) img.color = Color.Lerp(img.color, targetColor, k);
            float targetScale = Held ? 0.90f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(targetScale, targetScale, 1f), k);
        }

        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);
        public void OnPointerExit(PointerEventData e) => Set(false);

        void OnDisable()
        {
            Set(false);
            transform.localScale = Vector3.one;
            if (img != null) img.color = targetColor = idle;
        }
    }

    /// <summary>Text that floats up and fades out, then destroys itself.</summary>
    public class FloatingText : MonoBehaviour
    {
        public float life = 1.1f;
        public float rise = 110f;
        public string BaseText;
        public int RepeatCount = 1;
        float t;
        float stackOffsetY;
        float targetStackOffsetY;
        Text text;
        RectTransform rt;
        Vector2 start;
        bool initialized;

        public float Age => t;

        public float CurrentY
        {
            get
            {
                EnsureInit();
                float k = Mathf.Clamp01(t / life);
                float riseK = 1f - Mathf.Pow(1f - k, 2.2f);
                return start.y + rise * riseK + targetStackOffsetY;
            }
        }

        void Awake() { text = GetComponent<Text>(); rt = (RectTransform)transform; }
        void Start() { EnsureInit(); }

        public void EnsureInit()
        {
            if (initialized) return;
            if (rt == null) rt = (RectTransform)transform;
            if (text == null) text = GetComponent<Text>();
            start = rt.anchoredPosition;
            initialized = true;
        }

        public void BumpUp(float dy)
        {
            EnsureInit();
            targetStackOffsetY += dy;
        }

        public void RefreshPop(Color color)
        {
            EnsureInit();
            RepeatCount++;
            if (text != null)
            {
                text.text = BaseText + " x" + RepeatCount;
                text.color = color;
            }
            t = Mathf.Min(t, 0.06f);
        }

        void Update()
        {
            EnsureInit();
            float dt = Time.unscaledDeltaTime;
            t += dt;
            stackOffsetY = Mathf.Lerp(stackOffsetY, targetStackOffsetY, 1f - Mathf.Exp(-22f * dt));
            float k = Mathf.Clamp01(t / life);
            float riseK = 1f - Mathf.Pow(1f - k, 2.2f);
            rt.anchoredPosition = start + Vector2.up * (rise * riseK + stackOffsetY);

            float scale;
            if (k < 0.22f)
            {
                float u = k / 0.22f;
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float x = u - 1f;
                float back = 1f + c3 * x * x * x + c1 * x * x;
                scale = Mathf.LerpUnclamped(0.45f, 1f, back);
            }
            else if (k < 0.7f)
            {
                scale = 1f;
            }
            else
            {
                scale = Mathf.Lerp(1f, 0.92f, (k - 0.7f) / 0.3f);
            }
            rt.localScale = Vector3.one * scale;
            var c = text.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; text.color = c;
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
