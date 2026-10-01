using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurtleBlaster
{
    /// <summary>Small helpers to build uGUI hierarchies in code (reference resolution 1920x1080).</summary>
    public static class UIFactory
    {
        public static readonly Color Ink = PixelCanvas.C("#1b1b2f");
        public static readonly Color Panel = PixelCanvas.C("#2b2d42");
        public static readonly Color Accent = PixelCanvas.C("#ffd23f");
        public static readonly Color Good = PixelCanvas.C("#4cd964");
        public static readonly Color Bad = PixelCanvas.C("#ff5a5f");
        public static readonly Color Blue = PixelCanvas.C("#3a86ff");

        static Font font;

        public static Font DefaultFont
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                }
                return font;
            }
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

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public static Image PanelBox(Transform parent, string name, Color color)
        {
            var img = Image(parent, name, SpriteLibrary.Get("ui_panel"), color, true);
            img.pixelsPerUnitMultiplier = 0.3f;   // chunky 4px borders become ~13 canvas units
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold, string name = "Label")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont; t.text = text; t.fontSize = size; t.color = color;
            t.alignment = align; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(Mathf.Max(2, size / 14f), -Mathf.Max(2, size / 14f));
            return t;
        }

        public static Button MakeButton(Transform parent, string label, Color color, Action onClick,
            Vector2 size, int fontSize = 44)
        {
            var img = PanelBox(parent, "Button_" + label, color);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            btn.colors = cb;
            ((RectTransform)img.transform).sizeDelta = size;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Sfx("click");
                onClick?.Invoke();
            });
            var t = Label(img.transform, label, fontSize, Color.white);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return btn;
        }

        public static void SetButtonLabel(Button b, string text)
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
        public static Image Bar(Transform parent, string name, Color fill, Vector2 size, out RectTransform root)
        {
            var bg = PanelBox(parent, name, new Color(0.1f, 0.1f, 0.18f, 0.85f));
            root = (RectTransform)bg.transform;
            root.sizeDelta = size;
            var f = Image(bg.transform, "Fill", SpriteLibrary.Get("square"), fill);
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
        Image img;
        Color idle;

        void Awake() { img = GetComponent<Image>(); if (img != null) idle = img.color; }

        void Set(bool v)
        {
            if (Held == v) return;
            Held = v;
            if (img != null) img.color = v ? new Color(idle.r, idle.g, idle.b, Mathf.Min(1f, idle.a + 0.35f)) : idle;
            OnHold?.Invoke(v);
        }

        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);
        public void OnPointerExit(PointerEventData e) => Set(false);
        void OnDisable() => Set(false);
    }

    /// <summary>Text that floats up and fades out, then destroys itself.</summary>
    public class FloatingText : MonoBehaviour
    {
        public float life = 1.1f;
        public float rise = 120f;
        float t;
        Text text;
        RectTransform rt;
        Vector2 start;

        void Awake() { text = GetComponent<Text>(); rt = (RectTransform)transform; }
        void Start() { start = rt.anchoredPosition; }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = t / life;
            rt.anchoredPosition = start + Vector2.up * (rise * k);
            float scale = k < 0.12f ? Mathf.Lerp(0.6f, 1.15f, k / 0.12f) : Mathf.Lerp(1.15f, 1f, (k - 0.12f) / 0.3f);
            rt.localScale = Vector3.one * Mathf.Clamp(scale, 0.9f, 1.2f);
            var c = text.color; c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f; text.color = c;
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
