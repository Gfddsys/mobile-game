using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurtleBlaster
{
    /// <summary>
    /// Springy tactile press/hover/release scale feel and optional CTA breathing + error shake
    /// for uGUI Buttons. Uses unscaledDeltaTime so it stays smooth even while paused.
    /// </summary>
    public class UIButtonFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public bool idlePulse;
        public float pulseAmp = 0.032f;
        public float pulseSpeed = 3.2f;

        UnityEngine.UI.Button btn;
        RectTransform rt;
        UIPanelMotion parentMotion;
        bool down, hover;
        float scale = 1f, scaleVel;
        float shakeLeft, shakeAmp;
        Vector2 shakeRestPos;

        void Awake()
        {
            btn = GetComponent<UnityEngine.UI.Button>();
            rt = transform as RectTransform;
            parentMotion = GetComponentInParent<UIPanelMotion>(true);
        }

        void OnTransformParentChanged()
        {
            parentMotion = GetComponentInParent<UIPanelMotion>(true);
        }

        void OnEnable()
        {
            if (parentMotion == null) parentMotion = GetComponentInParent<UIPanelMotion>(true);
        }

        public void Punch(float impulse = 5.5f)
        {
            scaleVel += impulse;
        }

        public void Shake(float amplitude = 14f)
        {
            if (rt == null) return;
            if (shakeLeft <= 0f) shakeRestPos = rt.anchoredPosition;
            shakeAmp = amplitude;
            shakeLeft = 0.26f;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            if (shakeLeft > 0f && rt != null)
            {
                shakeLeft -= dt;
                if (shakeLeft <= 0f)
                {
                    shakeLeft = 0f;
                    rt.anchoredPosition = shakeRestPos;
                }
                else
                {
                    float k = shakeLeft / 0.26f;
                    float ox = Mathf.Sin(k * Mathf.PI * 5f) * shakeAmp * k;
                    rt.anchoredPosition = shakeRestPos + new Vector2(ox, 0f);
                }
            }

            // Let an active staggered parent panel entrance own localScale until revealed (unless pressed).
            if (parentMotion != null && parentMotion.IsAnimating && !down && Mathf.Abs(scaleVel) < 0.05f)
            {
                scale = 1f;
                scaleVel = 0f;
                return;
            }

            bool interactable = btn == null || btn.interactable;
            float target = 1f;
            if (down && interactable) target = 0.91f;
            else if (hover && interactable) target = 1.04f;
            else if (idlePulse && interactable) target = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmp;

            float stiffness = down ? 420f : 320f;
            float damping = down ? 26f : 18f;
            scaleVel += (target - scale) * stiffness * dt;
            scaleVel *= Mathf.Exp(-damping * dt);
            scale += scaleVel * dt;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (btn != null && !btn.interactable) return;
            down = true;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (down && (btn == null || btn.interactable)) scaleVel += 5.5f;
            down = false;
        }

        public void OnPointerEnter(PointerEventData e) => hover = true;

        public void OnPointerExit(PointerEventData e)
        {
            hover = false;
            down = false;
        }

        void OnDisable()
        {
            down = false;
            hover = false;
            scale = 1f;
            scaleVel = 0f;
            transform.localScale = Vector3.one;
            if (shakeLeft > 0f && rt != null)
            {
                shakeLeft = 0f;
                rt.anchoredPosition = shakeRestPos;
            }
        }
    }

    /// <summary>
    /// Smoothly fades in screens/overlays via CanvasGroup, pops in an optional modal panel,
    /// and staggers registered cards/buttons without changing GameObject activeSelf timing.
    /// </summary>
    public class UIPanelMotion : MonoBehaviour
    {
        public float fadeDuration = 0.16f;
        public RectTransform content;
        public float contentSlideY = 22f;
        public float contentStartScale = 0.90f;
        public float contentDuration = 0.21f;
        public float staggerStep = 0.016f;
        public float itemDuration = 0.16f;
        public float itemSlideY = 18f;

        struct StaggerItem
        {
            public RectTransform rt;
            public Vector2 basePos;
        }

        readonly List<StaggerItem> items = new List<StaggerItem>();
        CanvasGroup cg;
        Vector2 contentBasePos;
        bool hasContent;
        float elapsed;
        bool playing;

        public bool IsAnimating => playing;

        public static UIPanelMotion Attach(RectTransform root, RectTransform contentPanel = null, float fadeDur = 0.16f)
        {
            var pm = root.GetComponent<UIPanelMotion>();
            if (pm == null) pm = root.gameObject.AddComponent<UIPanelMotion>();
            pm.fadeDuration = fadeDur;
            if (contentPanel != null) pm.SetContent(contentPanel);
            return pm;
        }

        void Awake()
        {
            EnsureCanvasGroup();
        }

        void EnsureCanvasGroup()
        {
            if (cg != null) return;
            cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        public void SetContent(RectTransform panel)
        {
            content = panel;
            hasContent = panel != null;
            if (hasContent) contentBasePos = panel.anchoredPosition;
        }

        public void AddStagger(RectTransform item)
        {
            if (item == null) return;
            items.Add(new StaggerItem { rt = item, basePos = item.anchoredPosition });
        }

        void OnEnable()
        {
            EnsureCanvasGroup();
            elapsed = 0f;
            playing = true;
            ApplyAt(0f);
        }

        void Update()
        {
            if (!playing) return;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float total = TotalDuration();
            if (elapsed >= total)
            {
                Finish();
                return;
            }
            ApplyAt(elapsed);
        }

        float TotalDuration()
        {
            float maxT = fadeDuration;
            if (hasContent) maxT = Mathf.Max(maxT, contentDuration);
            if (items.Count > 0) maxT = Mathf.Max(maxT, (items.Count - 1) * staggerStep + itemDuration);
            return maxT;
        }

        void ApplyAt(float t)
        {
            float fa = fadeDuration > 0.001f ? EaseOutCubic(Mathf.Clamp01(t / fadeDuration)) : 1f;
            cg.alpha = fa;

            if (hasContent && content != null)
            {
                float u = contentDuration > 0.001f ? Mathf.Clamp01(t / contentDuration) : 1f;
                float s = Mathf.LerpUnclamped(contentStartScale, 1f, EaseOutBack(u));
                content.localScale = new Vector3(s, s, 1f);
                content.anchoredPosition = Vector2.LerpUnclamped(contentBasePos - new Vector2(0f, contentSlideY), contentBasePos, EaseOutCubic(u));
            }

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.rt == null) continue;
                float localT = (t - i * staggerStep) / Mathf.Max(0.001f, itemDuration);
                float u = Mathf.Clamp01(localT);
                float s = Mathf.LerpUnclamped(0.88f, 1f, EaseOutBack(u));
                item.rt.localScale = new Vector3(s, s, 1f);
                item.rt.anchoredPosition = Vector2.LerpUnclamped(item.basePos - new Vector2(0f, itemSlideY), item.basePos, EaseOutCubic(u));
            }
        }

        void Finish()
        {
            playing = false;
            if (cg != null) cg.alpha = 1f;
            if (hasContent && content != null)
            {
                content.localScale = Vector3.one;
                content.anchoredPosition = contentBasePos;
            }
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.rt == null) continue;
                item.rt.localScale = Vector3.one;
                item.rt.anchoredPosition = item.basePos;
            }
        }

        void OnDisable()
        {
            Finish();
        }

        static float EaseOutCubic(float u)
        {
            float inv = 1f - u;
            return 1f - inv * inv * inv;
        }

        static float EaseOutBack(float u)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = u - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }

    /// <summary>Gentle idle float and micro-tilt for logo titles and card previews.</summary>
    public class UIFloatBob : MonoBehaviour
    {
        public float ampY = 6f;
        public float rotDeg = 1.2f;
        public float speed = 2.0f;
        public float phase;

        RectTransform rt;
        Vector2 basePos;
        Quaternion baseRot;
        bool initialized;

        public static UIFloatBob Attach(RectTransform rt, float ampY = 6f, float rotDeg = 1.2f, float speed = 2.0f, float phase = 0f)
        {
            var bob = rt.GetComponent<UIFloatBob>();
            if (bob == null) bob = rt.gameObject.AddComponent<UIFloatBob>();
            bob.ampY = ampY;
            bob.rotDeg = rotDeg;
            bob.speed = speed;
            bob.phase = phase;
            return bob;
        }

        void EnsureBase()
        {
            if (initialized) return;
            rt = transform as RectTransform;
            if (rt != null) basePos = rt.anchoredPosition;
            baseRot = transform.localRotation;
            initialized = true;
        }

        void OnEnable() => EnsureBase();

        void Update()
        {
            EnsureBase();
            float s = Mathf.Sin(Time.unscaledTime * speed + phase);
            if (rt != null) rt.anchoredPosition = basePos + new Vector2(0f, s * ampY);
            if (Mathf.Abs(rotDeg) > 0.001f)
            {
                float r = Mathf.Cos(Time.unscaledTime * (speed * 0.8f) + phase) * rotDeg;
                transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, r);
            }
        }

        void OnDisable()
        {
            if (!initialized) return;
            if (rt != null) rt.anchoredPosition = basePos;
            transform.localRotation = baseRot;
        }
    }

    /// <summary>Lightweight spring scale punch for HUD counters, badges, and cards.</summary>
    public class UIPunch : MonoBehaviour
    {
        Vector3 baseScale = Vector3.one;
        float scale = 1f, scaleVel;
        bool hasBase;

        public static void Trigger(Transform t, float strength = 0.24f)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return;
            var p = t.GetComponent<UIPunch>();
            if (p == null) p = t.gameObject.AddComponent<UIPunch>();
            p.Impulse(strength);
        }

        void Awake() => CaptureBase();

        void CaptureBase()
        {
            if (hasBase) return;
            baseScale = transform.localScale;
            hasBase = true;
        }

        void Impulse(float strength)
        {
            CaptureBase();
            scaleVel += strength * 22f;
        }

        void Update()
        {
            if (!hasBase) return;
            if (Mathf.Abs(scale - 1f) < 0.0005f && Mathf.Abs(scaleVel) < 0.001f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            scaleVel += (1f - scale) * 320f * dt;
            scaleVel *= Mathf.Exp(-18f * dt);
            scale += scaleVel * dt;
            if (Mathf.Abs(scale - 1f) < 0.0005f && Mathf.Abs(scaleVel) < 0.001f)
            {
                scale = 1f;
                scaleVel = 0f;
            }
            transform.localScale = baseScale * scale;
        }

        void OnDisable()
        {
            scale = 1f;
            scaleVel = 0f;
            if (hasBase) transform.localScale = baseScale;
        }
    }

    /// <summary>
    /// Smooth icon flip/pop and button background color transition for toggle icon buttons (SFX / Music).
    /// </summary>
    public class UIIconToggle : MonoBehaviour
    {
        UnityEngine.UI.Image btnBg;
        UnityEngine.UI.Image iconImg;
        Sprite onSprite, offSprite;
        Color onBgColor, offBgColor;
        Color onIconColor = Color.white;
        Color offIconColor = new Color(0.82f, 0.85f, 0.92f, 0.88f);

        bool initialized;
        bool currentState;
        bool animating;
        bool swappedSprite;
        float animT;
        const float Duration = 0.22f;
        Color currentBg, targetBg;
        Color currentIconCol, targetIconCol;

        public static UIIconToggle Attach(UnityEngine.UI.Button btn, UnityEngine.UI.Image icon,
            Sprite spriteOn, Sprite spriteOff, Color bgOn, Color bgOff)
        {
            var t = btn.GetComponent<UIIconToggle>();
            if (t == null) t = btn.gameObject.AddComponent<UIIconToggle>();
            t.btnBg = btn.targetGraphic as UnityEngine.UI.Image;
            t.iconImg = icon;
            t.onSprite = spriteOn;
            t.offSprite = spriteOff;
            t.onBgColor = bgOn;
            t.offBgColor = bgOff;
            return t;
        }

        public void SetState(bool isOn)
        {
            Color wantBg = isOn ? onBgColor : offBgColor;
            Color wantIcon = isOn ? onIconColor : offIconColor;
            Sprite wantSprite = isOn ? onSprite : offSprite;

            if (!initialized || !gameObject.activeInHierarchy)
            {
                initialized = true;
                currentState = isOn;
                animating = false;
                currentBg = targetBg = wantBg;
                currentIconCol = targetIconCol = wantIcon;
                if (btnBg != null) btnBg.color = wantBg;
                if (iconImg != null)
                {
                    iconImg.sprite = wantSprite;
                    iconImg.color = wantIcon;
                    iconImg.transform.localScale = Vector3.one;
                    iconImg.transform.localRotation = Quaternion.identity;
                }
                return;
            }

            if (currentState == isOn)
            {
                if (!animating)
                {
                    if (btnBg != null) btnBg.color = wantBg;
                    if (iconImg != null) { iconImg.sprite = wantSprite; iconImg.color = wantIcon; }
                }
                return;
            }

            currentState = isOn;
            targetBg = wantBg;
            targetIconCol = wantIcon;
            animating = true;
            swappedSprite = false;
            animT = 0f;
        }

        void Update()
        {
            if (!animating) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            animT += dt;
            float u = Mathf.Clamp01(animT / Duration);

            // Smooth color blend
            float colK = 1f - Mathf.Exp(-18f * dt);
            currentBg = Color.Lerp(currentBg, targetBg, colK);
            currentIconCol = Color.Lerp(currentIconCol, targetIconCol, colK);
            if (btnBg != null) btnBg.color = currentBg;
            if (iconImg != null) iconImg.color = currentIconCol;

            if (iconImg != null)
            {
                // First half: squash horizontally & tilt out; midpoint: swap sprite; second half: springy overshoot back
                if (u < 0.42f)
                {
                    float p = u / 0.42f;
                    float sx = Mathf.Lerp(1f, 0.15f, p * p);
                    float sy = Mathf.Lerp(1f, 0.86f, p);
                    float tilt = Mathf.Lerp(0f, currentState ? 18f : -18f, p);
                    iconImg.transform.localScale = new Vector3(sx, sy, 1f);
                    iconImg.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                }
                else
                {
                    if (!swappedSprite)
                    {
                        swappedSprite = true;
                        iconImg.sprite = currentState ? onSprite : offSprite;
                    }
                    float p = (u - 0.42f) / 0.58f;
                    const float c1 = 2.1f;
                    const float c3 = c1 + 1f;
                    float x = p - 1f;
                    float back = 1f + c3 * x * x * x + c1 * x * x;
                    float sx = Mathf.LerpUnclamped(0.15f, 1f, back);
                    float sy = Mathf.LerpUnclamped(1.16f, 1f, p);
                    float tilt = Mathf.LerpUnclamped(currentState ? -14f : 14f, 0f, 1f - (1f - p) * (1f - p));
                    iconImg.transform.localScale = new Vector3(sx, sy, 1f);
                    iconImg.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                }
            }

            if (u >= 1f)
            {
                animating = false;
                currentBg = targetBg;
                currentIconCol = targetIconCol;
                if (btnBg != null) btnBg.color = targetBg;
                if (iconImg != null)
                {
                    iconImg.sprite = currentState ? onSprite : offSprite;
                    iconImg.color = targetIconCol;
                    iconImg.transform.localScale = Vector3.one;
                    iconImg.transform.localRotation = Quaternion.identity;
                }
            }
        }

        void OnDisable()
        {
            if (!initialized) return;
            animating = false;
            if (btnBg != null) btnBg.color = currentState ? onBgColor : offBgColor;
            if (iconImg != null)
            {
                iconImg.sprite = currentState ? onSprite : offSprite;
                iconImg.color = currentState ? onIconColor : offIconColor;
                iconImg.transform.localScale = Vector3.one;
                iconImg.transform.localRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// One-time black fade-in transition played only when first entering/booting the game.
    /// </summary>
    public class UIBootFade : MonoBehaviour
    {
        UnityEngine.UI.Image overlay;
        float elapsed;
        const float HoldBlack = 0.08f;
        const float FadeDuration = 0.55f;

        public static void PlayOnce(Transform canvasTransform)
        {
            var img = UIFactory.Image(canvasTransform, "BootBlackFade", SpriteLibrary.Get("square"), Color.black);
            img.raycastTarget = false;
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();
            var bf = img.gameObject.AddComponent<UIBootFade>();
            bf.overlay = img;
        }

        void Update()
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (elapsed <= HoldBlack)
            {
                if (overlay != null) overlay.color = Color.black;
                return;
            }

            float u = Mathf.Clamp01((elapsed - HoldBlack) / FadeDuration);
            if (u >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Smooth ease-out fade from solid black to transparent
            float inv = 1f - u;
            float alpha = inv * inv * (3f - 2f * inv);
            if (overlay != null) overlay.color = new Color(0f, 0f, 0f, alpha);
        }
    }
}
