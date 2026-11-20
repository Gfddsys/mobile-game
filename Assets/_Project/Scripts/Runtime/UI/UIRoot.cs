using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TurtleBlaster
{
    /// <summary>
    /// Builds and drives every screen: HUD + touch controls, main menu, garage,
    /// pause and run summary. Pure uGUI created in code at a 1920x1080 reference.
    /// </summary>
    public partial class UIRoot : MonoBehaviour
    {
        GameManager gm;
        Canvas canvas;
        CanvasGroup canvasGroup;

        // screens
        RectTransform hud, menu, garage, summary, pause, popups;

        // hud widgets
        Text distanceText, scoreText, comboText, missionHudText, zoneHudText, coinText, partText, boostBanner, magnetTimerText;
        readonly Image[] shieldIcons = new Image[5];
        Image fuelFill, stuntFill;
        RectTransform touchRoot, fuelBarRoot, stuntBarRoot, coinIconRt, partIconRt, shieldPillRt, magnetPillRt, biomeBannerRt;
        CanvasGroup biomeBannerGroup;
        float displayedScore, biomeBannerTimer;
        int lastHudCoins, lastHudParts, lastHudCombo, lastHudBiome = -1, lastHudShield = -1, lastHudMagnetSec = -1;

        // menu widgets
        Text bestText, menuMissionText, menuMissionRewardText;
        UIFactory.WalletDisplay menuWallet;
        Text menuEnergyText, menuEnergyTimerText;
        Button menuEnergyAdBtn, dailyRewardBtn;
        Image dailyDoneCheck;
        Button sfxBtn, musicBtn;
        readonly Image[] menuEnergyPips = new Image[SaveData.MaxEnergy];

        // garage widgets
        UIFactory.WalletDisplay garageWallet;
        readonly Text[] cardTier = new Text[4], cardInfo = new Text[4], cardLevelLbl = new Text[4];
        readonly Image[,] cardPipImgs = new Image[4, 4];
        readonly Button[] cardBtn = new Button[4];
        readonly GameObject[] cardCostRow = new GameObject[4];
        readonly Text[] cardCoinCost = new Text[4], cardPartCost = new Text[4];
        readonly RectTransform[] garageCards = new RectTransform[4];

        // summary widgets
        Text summaryTitle, summaryBody, summaryCoinsText, summaryPartsText;
        Text summaryEnergyText, summaryEnergyTimerText;
        Button summaryEnergyAdBtn;
        readonly Image[] summaryEnergyPips = new Image[SaveData.MaxEnergy];
        Image snapshotImg;
        Text snapshotCaption;
        Sprite snapshotSprite;

        // pause widgets
        Text pauseEnergyText;
        Button pauseRestartBtn;

        public void Init(GameManager manager)
        {
            gm = manager;
            UIFactory.EnsureEventSystem();

            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            canvasGroup = go.AddComponent<CanvasGroup>();

            BuildHud(go.transform);
            popups = UIFactory.Stretch("Popups", go.transform);
            BuildMenu(go.transform);
            BuildGarage(go.transform);
            BuildSummary(go.transform);
            BuildPause(go.transform);
            BuildExtras(go.transform);
            HideAll();

            SaveData.Current.Changed += RefreshWallets;
        }

        void OnDestroy()
        {
            SaveData.Current.Changed -= RefreshWallets;
            Services.PurchasesChanged -= OnPurchasesChanged;
        }

        // =====================================================================
        // Navigation
        // =====================================================================
        public void HideAll()
        {
            hud.gameObject.SetActive(false);
            menu.gameObject.SetActive(false);
            garage.gameObject.SetActive(false);
            summary.gameObject.SetActive(false);
            pause.gameObject.SetActive(false);
            HideExtras();
            GameInput.ClearTouch();
        }

        public void ShowMenu()
        {
            HideAll();
            RefreshWallets();
            menu.gameObject.SetActive(true);
        }

        public void ShowHud()
        {
            HideAll();
            displayedScore = 0f;
            lastHudCoins = 0;
            lastHudParts = 0;
            lastHudCombo = 0;
            lastHudBiome = -1;
            lastHudShield = -1;
            lastHudMagnetSec = -1;
            biomeBannerTimer = 0f;
            if (biomeBannerRt != null) biomeBannerRt.gameObject.SetActive(false);
            hud.gameObject.SetActive(true);
        }

        public void ShowBiomeBanner(string biomeName, Color color)
        {
            if (biomeBannerRt == null || zoneHudText == null) return;
            zoneHudText.text = biomeName.ToUpper();
            zoneHudText.color = color;
            biomeBannerTimer = 2.4f;
            biomeBannerRt.anchoredPosition = new Vector2(0f, (boostBanner != null && boostBanner.enabled) ? -260f : -196f);
            if (biomeBannerGroup != null) biomeBannerGroup.alpha = 1f;
            biomeBannerRt.gameObject.SetActive(true);
            UIPunch.Trigger(biomeBannerRt, 0.16f);
        }

        public void ShowGarage()
        {
            HideAll();
            RefreshGarage();
            garage.gameObject.SetActive(true);
        }

        public void ShowPause(bool on)
        {
            pause.gameObject.SetActive(on);
            if (on)
            {
                GameInput.ClearTouch();
                RefreshEnergyUI();
            }
        }

        public void SetVisible(bool visible) { canvasGroup.alpha = visible ? 1f : 0f; }

        public void ShowSummary(RunStats s, Texture2D snapshot)
        {
            HideAll();
            summary.gameObject.SetActive(true);
            RefreshSummary(s);

            if (snapshotSprite != null) Destroy(snapshotSprite);
            if (snapshot != null)
            {
                snapshotSprite = Sprite.Create(snapshot, new Rect(0, 0, snapshot.width, snapshot.height), new Vector2(0.5f, 0.5f), 100f);
                snapshotImg.sprite = snapshotSprite;
                snapshotImg.color = Color.white;
                snapshotImg.preserveAspect = true;
                snapshotCaption.text = "Your finest wipeout";
            }
            else
            {
                snapshotImg.sprite = SpriteLibrary.GetTurtle(SaveData.Current.selectedSkin);
                snapshotImg.color = Color.white;
                snapshotCaption.text = "";
            }
        }

        /// <summary>Updates the summary texts only (e.g. after the x2 coins ad).</summary>
        public void RefreshSummary(RunStats s)
        {
            if (this == null || summaryTitle == null || summaryBody == null) return;
            summaryTitle.text = "WIPEOUT!\n<size=" + UIFactory.ScaleFontSize(36) + "><color=#ffd23f>" + s.causeOfDeath + "</color></size>";
            var sb = new StringBuilder();
            sb.AppendLine("Distance   <color=#ffd23f>" + Mathf.RoundToInt(s.distance) + " m</color>" + (s.newBestDistance ? "  <color=#4cd964>NEW BEST!</color>" : "") +
                          (s.leaderboardRank > 0 ? "   <color=#9ad0ff>Rank #" + s.leaderboardRank + "</color>" : ""));
            sb.AppendLine("Score      <color=#ffd23f>" + s.score + "</color>" + (s.newBestScore ? "  <color=#4cd964>NEW BEST!</color>" : ""));
            sb.AppendLine("Flips  " + s.flips + "     Perfect  " + s.perfectLandings + (s.closeCalls > 0 ? "     Close  " + s.closeCalls : ""));
            sb.Append("Top speed  " + Mathf.RoundToInt(s.topSpeed * 3.6f) + " km/h     Combo  x" + s.maxCombo);
            summaryBody.text = sb.ToString();
            if (summaryCoinsText != null) summaryCoinsText.text = "+" + s.coins;
            if (summaryPartsText != null) summaryPartsText.text = "+" + s.parts;
            RefreshSummaryExtras(s);
        }

        public void RefreshWallets()
        {
            var s = SaveData.Current;
            menuWallet?.Set(s.coins, s.parts);
            garageWallet?.Set(s.coins, s.parts);
            if (bestText != null) bestText.text = "BEST  " + s.bestDistance + " m";
            if (menuMissionText != null)
            {
                var m = s.GetCurrentMission();
                menuMissionText.text = "MISSION: " + m.title;
                if (menuMissionRewardText != null) menuMissionRewardText.text = "+" + m.rewardCoins;
            }
            if (sfxBtn != null)
            {
                UIFactory.SetButtonLabel(sfxBtn, "SFX " + (s.sfxOn ? "ON" : "OFF"));
                sfxBtn.targetGraphic.color = s.sfxOn ? new Color(0.18f, 0.48f, 0.68f) : new Color(0.24f, 0.27f, 0.38f);
            }
            if (musicBtn != null)
            {
                UIFactory.SetButtonLabel(musicBtn, "MUSIC " + (s.musicOn ? "ON" : "OFF"));
                musicBtn.targetGraphic.color = s.musicOn ? new Color(0.18f, 0.48f, 0.68f) : new Color(0.24f, 0.27f, 0.38f);
            }
            if (garage != null && garage.gameObject.activeSelf) RefreshGarage();
            RefreshExtrasWallet(s.coins, s.parts);
            RefreshEnergyUI();
            RefreshDailyRewardUI();
        }

        public void Popup(string text, Color color, int size = 64)
        {
            if (popups == null) return;

            for (int i = popups.childCount - 1; i >= 0; i--)
            {
                var existing = popups.GetChild(i).GetComponent<FloatingText>();
                if (existing != null && existing.BaseText == text && existing.Age < 0.65f)
                {
                    existing.RefreshPop(color);
                    return;
                }
            }

            int scaledBase = Mathf.RoundToInt(size * 0.82f);
            int boostedSize = scaledBase + Mathf.Min(gm != null ? gm.Combo * 2 : 0, 14);
            var t = UIFactory.Label(popups, text, boostedSize, color, TextAnchor.MiddleCenter, FontStyle.Bold, "Popup");
            var rt = (RectTransform)t.transform;

            Vector2 pos = new Vector2(0f, 130f);
            if (gm != null && gm.State == GameState.Playing && gm.Player != null && gm.Cam != null && gm.Cam.Cam != null)
            {
                Vector3 vp = gm.Cam.Cam.WorldToViewportPoint(gm.Player.transform.position + new Vector3(0.25f, 2.55f, 0f));
                if (vp.z > 0f)
                {
                    float vx = Mathf.Clamp(vp.x, 0.24f, 0.76f);
                    float vy = Mathf.Clamp(vp.y, 0.44f, 0.74f);
                    pos = new Vector2((vx - 0.5f) * 1920f + Random.Range(-16f, 16f), (vy - 0.5f) * 1080f);
                }
            }

            const float minVerticalGap = 58f;
            float requiredFloorY = pos.y + minVerticalGap;
            for (int i = popups.childCount - 1; i >= 0; i--)
            {
                var child = popups.GetChild(i);
                if (child == rt) continue;
                var existing = child.GetComponent<FloatingText>();
                if (existing == null) continue;
                if (existing.CurrentY < requiredFloorY)
                {
                    existing.BumpUp(requiredFloorY - existing.CurrentY);
                }
                requiredFloorY = existing.CurrentY + minVerticalGap;
            }

            UIFactory.Place(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(960, 110), new Vector2(0.5f, 0.5f));
            var ft = t.gameObject.AddComponent<FloatingText>();
            ft.BaseText = text;
            ft.EnsureInit();
        }

        // =====================================================================
        // HUD
        // =====================================================================
        void BuildHud(Transform parent)
        {
            hud = UIFactory.Stretch("HUD", parent);

            // Top-left telemetry (no background box)
            var scoreBox = UIFactory.Rect("TelemetryBox", hud);
            UIFactory.Place(scoreBox, new Vector2(0, 1), new Vector2(28, -16), new Vector2(400, 140));
            distanceText = UIFactory.Label(scoreBox, "0 m", 64, Color.white, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)distanceText.transform, new Vector2(0, 1), new Vector2(6, -4), new Vector2(380, 82));
            scoreText = UIFactory.Label(scoreBox, "SCORE 0", 34, UIFactory.Accent, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)scoreText.transform, new Vector2(0, 1), new Vector2(8, -84), new Vector2(380, 46));

            comboText = UIFactory.Label(hud, "", 38, UIFactory.Good, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)comboText.transform, new Vector2(0, 1), new Vector2(420, -26), new Vector2(320, 62));

            var missionBox = UIFactory.Rect("MissionPill", hud);
            UIFactory.Place(missionBox, new Vector2(0, 1), new Vector2(36, -144), new Vector2(760, 48));
            missionHudText = UIFactory.Label(missionBox, "", 32, new Color(0.88f, 0.95f, 1f), TextAnchor.MiddleLeft);
            var mrt = (RectTransform)missionHudText.transform; mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.offsetMin = Vector2.zero; mrt.offsetMax = Vector2.zero;

            biomeBannerRt = UIFactory.Rect("BiomeBanner", hud);
            UIFactory.Place(biomeBannerRt, new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(560, 58), new Vector2(0.5f, 1));
            biomeBannerGroup = biomeBannerRt.gameObject.AddComponent<CanvasGroup>();
            zoneHudText = UIFactory.Label(biomeBannerRt, "", 42, UIFactory.Good, TextAnchor.MiddleCenter);
            var zrt = (RectTransform)zoneHudText.transform; zrt.anchorMin = Vector2.zero; zrt.anchorMax = Vector2.one; zrt.offsetMin = Vector2.zero; zrt.offsetMax = Vector2.zero;
            biomeBannerRt.gameObject.SetActive(false);

            // Top-center fuel + stunt gauges (no outer housing box)
            var gaugeBox = UIFactory.Rect("GaugeBox", hud);
            UIFactory.Place(gaugeBox, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(560, 104));
            var fuelIcon = UIFactory.Image(gaugeBox, "FuelIcon", SpriteLibrary.Get("fuel"), Color.white);
            fuelIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)fuelIcon.transform, new Vector2(0, 1), new Vector2(8, -9), new Vector2(28, 32));
            var fuelLabel = UIFactory.Label(gaugeBox, "FUEL", 30, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)fuelLabel.transform, new Vector2(0, 1), new Vector2(44, -6), new Vector2(96, 38));
            fuelFill = UIFactory.Bar(gaugeBox, "FuelBar", UIFactory.Good, new Vector2(404, 30), out var fuelRoot);
            fuelBarRoot = fuelRoot;
            UIFactory.Place(fuelRoot, new Vector2(0, 1), new Vector2(142, -10), new Vector2(404, 30));

            var stuntIcon = UIFactory.Image(gaugeBox, "StuntIcon", SpriteLibrary.Get("star"), Color.white);
            stuntIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)stuntIcon.transform, new Vector2(0, 1), new Vector2(8, -53), new Vector2(28, 28));
            var stuntLabel = UIFactory.Label(gaugeBox, "STUNT", 30, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)stuntLabel.transform, new Vector2(0, 1), new Vector2(44, -50), new Vector2(96, 38));
            stuntFill = UIFactory.Bar(gaugeBox, "StuntBar", UIFactory.Blue, new Vector2(404, 28), out var stuntRoot);
            stuntBarRoot = stuntRoot;
            UIFactory.Place(stuntRoot, new Vector2(0, 1), new Vector2(142, -54), new Vector2(404, 28));

            shieldPillRt = UIFactory.Rect("ShieldPill", hud);
            UIFactory.Place(shieldPillRt, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(194, 48), new Vector2(0.5f, 1));
            var shieldSprite = SpriteLibrary.Get("ui_shield");
            for (int i = 0; i < shieldIcons.Length; i++)
            {
                var ic = UIFactory.Image(shieldPillRt, "Shield_" + i, shieldSprite, Color.white);
                ic.preserveAspect = true;
                UIFactory.Place((RectTransform)ic.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 38), new Vector2(0.5f, 0.5f));
                shieldIcons[i] = ic;
            }
            shieldPillRt.gameObject.SetActive(false);

            magnetPillRt = UIFactory.Rect("MagnetPill", hud);
            UIFactory.Place(magnetPillRt, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(148, 48), new Vector2(0.5f, 1));
            var magnetIcon = UIFactory.Image(magnetPillRt, "MagnetIcon", SpriteLibrary.Get("magnet"), Color.white);
            magnetIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)magnetIcon.transform, new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(34, 34), new Vector2(0, 0.5f));
            magnetTimerText = UIFactory.Label(magnetPillRt, "0s", 34, new Color(1f, 0.51f, 0.77f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)magnetTimerText.transform, new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(86, 46), new Vector2(0, 0.5f));
            magnetPillRt.gameObject.SetActive(false);

            boostBanner = UIFactory.Label(hud, "SUPER BOOST!", 72, UIFactory.Accent);
            UIFactory.Place((RectTransform)boostBanner.transform, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(900, 100));

            // Top-right pickup counters (no background pills) + crisp pause button
            var coinPill = UIFactory.Rect("CoinPill", hud);
            UIFactory.Place(coinPill, new Vector2(1, 1), new Vector2(-134, -20), new Vector2(190, 48));
            var coinIcon = UIFactory.Image(coinPill, "CoinIcon", SpriteLibrary.Get("coin"), Color.white);
            coinIcon.preserveAspect = true;
            coinIconRt = (RectTransform)coinIcon.transform;
            UIFactory.Place(coinIconRt, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(36, 36));
            coinText = UIFactory.Label(coinPill, "0", 36, UIFactory.Accent, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)coinText.transform, new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(130, 48));

            var partPill = UIFactory.Rect("PartPill", hud);
            UIFactory.Place(partPill, new Vector2(1, 1), new Vector2(-134, -76), new Vector2(190, 48));
            var partIcon = UIFactory.Image(partPill, "PartIcon", SpriteLibrary.Get("part"), Color.white);
            partIcon.preserveAspect = true;
            partIconRt = (RectTransform)partIcon.transform;
            UIFactory.Place(partIconRt, new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(36, 36));
            partText = UIFactory.Label(partPill, "0", 36, new Color(0.6f, 0.85f, 1f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)partText.transform, new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(130, 48));

            var pauseBtn = UIFactory.MakeButton(hud, "", UIFactory.Panel, () => gm.Pause(), new Vector2(98, 98), 44);
            UIFactory.Place((RectTransform)pauseBtn.transform, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(98, 98));
            var pIcon = UIFactory.Image(pauseBtn.transform, "PauseIcon", SpriteLibrary.Get("ui_pause"), Color.white);
            UIFactory.Place((RectTransform)pIcon.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(44, 44));

            var snapBtn = UIFactory.MakeButton(hud, "SNAP", UIFactory.Blue, () => gm.CaptureAndShowScreenshot(), new Vector2(98, 66), 26);
            UIFactory.Place((RectTransform)snapBtn.transform, new Vector2(1, 1), new Vector2(-24, -128), new Vector2(98, 66));

            BuildTouchControls(hud);
            UIPanelMotion.Attach(hud, null, 0.14f);
        }

        void BuildTouchControls(Transform parent)
        {
            touchRoot = UIFactory.Stretch("TouchControls", parent);
            var blue = new Color(UIFactory.Blue.r * 0.85f, UIFactory.Blue.g * 0.85f, UIFactory.Blue.b * 0.85f, 0.82f);
            var orange = new Color(UIFactory.Amber.r * 0.90f, UIFactory.Amber.g * 0.90f, UIFactory.Amber.b * 0.90f, 0.82f);
            var green = new Color(UIFactory.Good.r * 0.85f, UIFactory.Good.g * 0.85f, UIFactory.Good.b * 0.85f, 0.82f);

            // Left thumb: vertical axis (Pitch Up / Pitch Down)
            TouchButton("Up", "THRUST", "ui_arrow", false, false, blue, new Vector2(0, 0), new Vector2(50, 280), v => GameInput.TouchUp = v);
            TouchButton("Down", "SLAM", "ui_arrow", true, false, blue, new Vector2(0, 0), new Vector2(50, 48), v => GameInput.TouchDown = v);
            // Right thumb: horizontal axis (Backflip / Frontflip)
            TouchButton("Jump", "JUMP", "ui_arrow", false, false, green, new Vector2(1, 0), new Vector2(-50, 280), v => { if (v) GameInput.TouchJump = true; });
            TouchButton("Back", "BACKFLIP", "ui_flip", false, false, orange, new Vector2(1, 0), new Vector2(-290, 48), v => GameInput.TouchLeft = v);
            TouchButton("Front", "FRONTFLIP", "ui_flip", false, true, orange, new Vector2(1, 0), new Vector2(-50, 48), v => GameInput.TouchRight = v);
        }

        void TouchButton(string name, string caption, string icon, bool flipY, bool flipX, Color color,
            Vector2 anchor, Vector2 pos, System.Action<bool> onHold)
        {
            var img = UIFactory.Image(touchRoot, "Touch_" + name, SpriteLibrary.Get("ui_touch"), color, true);
            img.pixelsPerUnitMultiplier = 0.25f;
            img.raycastTarget = true;
            var rt = (RectTransform)img.transform;
            UIFactory.Place(rt, anchor, new Vector2(pos.x, pos.y), new Vector2(210, 210), new Vector2(anchor.x, 0));
            var hb = img.gameObject.AddComponent<HoldButton>();
            hb.OnHold = onHold;

            var ic = UIFactory.Image(rt, "Icon", SpriteLibrary.Get(icon), new Color(1, 1, 1, 0.96f));
            var irt = (RectTransform)ic.transform;
            UIFactory.Place(irt, new Vector2(0.5f, 0.60f), Vector2.zero, new Vector2(88, 88));
            irt.localScale = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);

            var cap = UIFactory.Label(rt, caption, 32, Color.white);
            UIFactory.Place((RectTransform)cap.transform, new Vector2(0.5f, 0.23f), Vector2.zero, new Vector2(196, 44));
        }

        void Update()
        {
            if (gm == null) return;
            UpdateExtras();
            if (menu.gameObject.activeSelf || summary.gameObject.activeSelf || pause.gameObject.activeSelf ||
                (energyOverlay != null && energyOverlay.gameObject.activeSelf))
            {
                RefreshEnergyUI();
            }
            if (!hud.gameObject.activeSelf) return;
            var p = gm.Player;
            var s = gm.Stats;
            if (s == null) return;

            float udt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            distanceText.text = Mathf.FloorToInt(s.distance) + " m";
            if (Mathf.Abs(s.score - displayedScore) <= 1f) displayedScore = s.score;
            else displayedScore = Mathf.Lerp(displayedScore, s.score, 1f - Mathf.Exp(-16f * udt));
            scoreText.text = "SCORE " + Mathf.RoundToInt(displayedScore);
            comboText.text = gm.Combo > 0 ? "COMBO x" + gm.ComboMultiplier.ToString("0.00") : "";
            if (gm.Combo > lastHudCombo) UIPunch.Trigger(comboText.transform, 0.24f);
            lastHudCombo = gm.Combo;
            coinText.text = s.coins.ToString();
            if (s.coins != lastHudCoins)
            {
                UIPunch.Trigger(coinText.transform, 0.24f);
                UIPunch.Trigger(coinIconRt, 0.28f);
                lastHudCoins = s.coins;
            }
            partText.text = s.parts.ToString();
            if (s.parts != lastHudParts)
            {
                UIPunch.Trigger(partText.transform, 0.24f);
                UIPunch.Trigger(partIconRt, 0.28f);
                lastHudParts = s.parts;
            }

            var m = SaveData.Current.GetCurrentMission();
            int prog = Mathf.Min(m.GetProgress(s), m.target);
            missionHudText.text = "MISSION: " + m.title + " (" + prog + "/" + m.target + ")";

            if (p != null)
            {
                int biomeIdx = LevelBiomes.GetIndex(p.transform.position.x);
                if (biomeIdx != lastHudBiome)
                {
                    lastHudBiome = biomeIdx;
                    var biome = LevelBiomes.Get(biomeIdx);
                    ShowBiomeBanner(biome.name, biome.bannerColor);
                }
                if (biomeBannerTimer > 0f && biomeBannerRt != null)
                {
                    biomeBannerTimer -= udt;
                    if (biomeBannerTimer <= 0f)
                    {
                        biomeBannerTimer = 0f;
                        biomeBannerRt.gameObject.SetActive(false);
                    }
                    else if (biomeBannerGroup != null)
                    {
                        biomeBannerGroup.alpha = biomeBannerTimer < 0.45f ? Mathf.Clamp01(biomeBannerTimer / 0.45f) : 1f;
                        float targetY = (boostBanner != null && boostBanner.enabled) ? -260f : -196f;
                        var bpos = biomeBannerRt.anchoredPosition;
                        bpos.y = Mathf.Lerp(bpos.y, targetY, 1f - Mathf.Exp(-14f * udt));
                        biomeBannerRt.anchoredPosition = bpos;
                    }
                }

                float fillK = 1f - Mathf.Exp(-14f * udt);
                fuelFill.fillAmount = Mathf.Lerp(fuelFill.fillAmount, p.Fuel01, fillK);
                Color wantFuelCol = p.Fuel01 < 0.25f ? UIFactory.Bad : UIFactory.Good;
                fuelFill.color = Color.Lerp(fuelFill.color, wantFuelCol, 1f - Mathf.Exp(-12f * udt));
                if (fuelBarRoot != null)
                {
                    Vector3 wantFuelScale = p.Fuel01 < 0.25f
                        ? Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 14f) * 0.035f)
                        : Vector3.one;
                    fuelBarRoot.localScale = Vector3.Lerp(fuelBarRoot.localScale, wantFuelScale, fillK);
                }

                stuntFill.fillAmount = Mathf.Lerp(stuntFill.fillAmount, p.Stunt01, fillK);
                stuntFill.color = p.SuperBoost ? Color.HSVToRGB(Mathf.Repeat(Time.time * 2f, 1f), 0.6f, 1f) : UIFactory.Blue;
                if (stuntBarRoot != null)
                {
                    Vector3 wantStuntScale = p.SuperBoost
                        ? Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 12f) * 0.03f)
                        : Vector3.one;
                    stuntBarRoot.localScale = Vector3.Lerp(stuntBarRoot.localScale, wantStuntScale, fillK);
                }
                boostBanner.enabled = p.SuperBoost;
                if (p.SuperBoost) boostBanner.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 14f) * 0.06f);

                bool hasShield = p.FreeHitsLeft > 0;
                bool hasMagnet = p.MagnetActive;
                float shieldW = 34f;
                if (shieldPillRt != null)
                {
                    shieldPillRt.gameObject.SetActive(hasShield);
                    if (hasShield)
                    {
                        int count = Mathf.Clamp(p.FreeHitsLeft, 1, shieldIcons.Length);
                        const float iconW = 34f, gap = 6f, step = iconW + gap;
                        shieldW = count * iconW + (count - 1) * gap;
                        shieldPillRt.sizeDelta = new Vector2(shieldW, 48f);
                        float startX = -shieldW * 0.5f + iconW * 0.5f;
                        for (int i = 0; i < shieldIcons.Length; i++)
                        {
                            bool on = i < count;
                            shieldIcons[i].gameObject.SetActive(on);
                            if (on)
                                ((RectTransform)shieldIcons[i].transform).anchoredPosition = new Vector2(startX + i * step, 0f);
                        }
                        if (lastHudShield >= 0 && p.FreeHitsLeft != lastHudShield) UIPunch.Trigger(shieldPillRt, 0.24f);
                        lastHudShield = p.FreeHitsLeft;
                    }
                    else lastHudShield = 0;
                }
                if (magnetPillRt != null)
                {
                    magnetPillRt.gameObject.SetActive(hasMagnet);
                    if (hasMagnet)
                    {
                        int sec = Mathf.CeilToInt(p.MagnetTimeLeft);
                        magnetTimerText.text = sec + "s";
                        if (lastHudMagnetSec <= 0) UIPunch.Trigger(magnetPillRt, 0.24f);
                        lastHudMagnetSec = sec;
                    }
                    else lastHudMagnetSec = 0;
                }
                if (hasShield && hasMagnet)
                {
                    const float magW = 136f, groupGap = 20f;
                    float totalW = shieldW + groupGap + magW;
                    shieldPillRt.anchoredPosition = new Vector2(-totalW * 0.5f + shieldW * 0.5f, -118f);
                    magnetPillRt.anchoredPosition = new Vector2(totalW * 0.5f - magW * 0.5f, -118f);
                }
                else if (hasShield) shieldPillRt.anchoredPosition = new Vector2(0f, -118f);
                else if (hasMagnet) magnetPillRt.anchoredPosition = new Vector2(0f, -118f);
            }
        }

        static void BuildEnergyPips(Transform parent, Image[] target, float yOffset, float totalWidth)
        {
            const float gap = 6f;
            float w = (totalWidth - gap * (target.Length - 1)) / target.Length;
            float startX = -totalWidth * 0.5f + w * 0.5f;
            for (int i = 0; i < target.Length; i++)
            {
                var pip = UIFactory.PixelPip(parent, "Pip_" + i, UIFactory.Accent);
                UIFactory.Place((RectTransform)pip.transform, new Vector2(0.5f, 1), new Vector2(startX + i * (w + gap), yOffset), new Vector2(w, 12), new Vector2(0.5f, 1));
                target[i] = pip;
            }
        }

        // =====================================================================
        // Menu
        // =====================================================================
        void BuildMenu(Transform parent)
        {
            menu = UIFactory.Stretch("Menu", parent);
            var dim = UIFactory.Image(menu, "Dim", SpriteLibrary.Get("square"), new Color(0, 0, 0, 0.10f));
            var drt = (RectTransform)dim.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;

            var titleGroup = UIFactory.Rect("TitleGroup", menu);
            UIFactory.Place(titleGroup, new Vector2(0.5f, 0.77f), Vector2.zero, new Vector2(920, 320), new Vector2(0.5f, 0.5f));

            var t1 = UIFactory.Label(titleGroup, "TURTLE", 158, UIFactory.Accent);
            var t1Rt = (RectTransform)t1.transform;
            UIFactory.Place(t1Rt, new Vector2(0.5f, 0.5f), new Vector2(-78, 78), new Vector2(900, 170), new Vector2(0.5f, 0.5f));
            t1Rt.localRotation = Quaternion.Euler(0f, 0f, 2f);
            UIFloatBob.Attach(t1Rt, 5f, 0f, 2.0f, 0f);

            var t2 = UIFactory.Label(titleGroup, "BLASTER", 162, UIFactory.Bad);
            var t2Rt = (RectTransform)t2.transform;
            UIFactory.Place(t2Rt, new Vector2(0.5f, 0.5f), new Vector2(18, -48), new Vector2(900, 170), new Vector2(0.5f, 0.5f));
            t2Rt.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
            UIFloatBob.Attach(t2Rt, 5f, 0f, 2.0f, 0.7f);

            var sub = UIFactory.Label(titleGroup, "TAO KHEE SKATEBOARD", 38, Color.white);
            var srt = (RectTransform)sub.transform;
            UIFactory.Place(srt, new Vector2(0.5f, 0.5f), new Vector2(18, -138), new Vector2(700, 56), new Vector2(0.5f, 0.5f));
            srt.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
            UIFloatBob.Attach(srt, 5f, 0f, 2.0f, 1.1f);

            // Default turtle character riding its skateboard launched next to "TURTLE" / above "BLASTER"
            var mascotRt = UIFactory.Rect("TitleMascot", titleGroup);
            UIFactory.Place(mascotRt, new Vector2(0.5f, 0.5f), new Vector2(246, 62), new Vector2(180, 140), new Vector2(0.5f, 0.5f));
            mascotRt.localRotation = Quaternion.Euler(0f, 0f, 16f);
            UIFloatBob.Attach(mascotRt, 7f, 2.5f, 2.3f, 0.35f);

            var mFlame = UIFactory.Image(mascotRt, "Flame", SpriteLibrary.Get("turbo_flame"), new Color(1f, 0.45f, 0.12f, 0.96f));
            mFlame.preserveAspect = true;
            UIFactory.Place((RectTransform)mFlame.transform, new Vector2(0.5f, 0.5f), new Vector2(-102, -13), new Vector2(78, 39), new Vector2(0.5f, 0.5f));

            var mFlameCore = UIFactory.Image(mascotRt, "FlameCore", SpriteLibrary.Get("turbo_core"), new Color(1f, 0.92f, 0.50f, 0.98f));
            mFlameCore.preserveAspect = true;
            UIFactory.Place((RectTransform)mFlameCore.transform, new Vector2(0.5f, 0.5f), new Vector2(-92, -13), new Vector2(50, 22), new Vector2(0.5f, 0.5f));

            var mThruster = UIFactory.Image(mascotRt, "Thruster", SpriteLibrary.Get("thruster_1"), Color.white);
            mThruster.preserveAspect = true;
            UIFactory.Place((RectTransform)mThruster.transform, new Vector2(0.5f, 0.5f), new Vector2(-50, -13), new Vector2(58, 29), new Vector2(0.5f, 0.5f));

            var mBoard = UIFactory.Image(mascotRt, "Board", SpriteLibrary.Get("board"), Color.white);
            mBoard.preserveAspect = true;
            UIFactory.Place((RectTransform)mBoard.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(144, 32), new Vector2(0.5f, 0.5f));

            var mWheelR = UIFactory.Image(mascotRt, "WheelR", SpriteLibrary.Get("wheel"), Color.white);
            mWheelR.preserveAspect = true;
            UIFactory.Place((RectTransform)mWheelR.transform, new Vector2(0.5f, 0.5f), new Vector2(-47, -33), new Vector2(36, 36), new Vector2(0.5f, 0.5f));

            var mWheelF = UIFactory.Image(mascotRt, "WheelF", SpriteLibrary.Get("wheel"), Color.white);
            mWheelF.preserveAspect = true;
            UIFactory.Place((RectTransform)mWheelF.transform, new Vector2(0.5f, 0.5f), new Vector2(47, -33), new Vector2(36, 36), new Vector2(0.5f, 0.5f));

            var mTurtle = UIFactory.Image(mascotRt, "Turtle", SpriteLibrary.Get("turtle"), Color.white);
            mTurtle.preserveAspect = true;
            UIFactory.Place((RectTransform)mTurtle.transform, new Vector2(0.5f, 0.5f), new Vector2(-1, 16), new Vector2(117, 90), new Vector2(0.5f, 0.5f));

            // Hero CTA + 2x2 action grid on right
            var ctaAnchor = new Vector2(0.76f, 0.28f);
            var ctaPivot = new Vector2(0.5f, 0.5f);
            var play = UIFactory.MakeButton(menu, "PLAY", UIFactory.Good, () => gm.StartRun(), new Vector2(520, 136), 68);
            UIFactory.Place((RectTransform)play.transform, ctaAnchor, new Vector2(0, 114), new Vector2(520, 136), ctaPivot);
            UIFactory.AddButtonIcon(play, "ui_play", new Vector2(44, 44), 42f);
            play.GetComponent<UIButtonFeel>().idlePulse = true;

            var gar = UIFactory.MakeButton(menu, "GARAGE", UIFactory.Blue, () => gm.OpenGarage(), new Vector2(252, 92), 36);
            UIFactory.Place((RectTransform)gar.transform, ctaAnchor, new Vector2(-134, -18), new Vector2(252, 92), ctaPivot);
            var skinsBtn = UIFactory.MakeButton(menu, "SKINS", UIFactory.Purple, () => gm.OpenSkins(), new Vector2(252, 92), 36);
            UIFactory.Place((RectTransform)skinsBtn.transform, ctaAnchor, new Vector2(134, -18), new Vector2(252, 92), ctaPivot);
            var shopBtn = UIFactory.MakeButton(menu, "SHOP", UIFactory.Amber, () => gm.OpenShop(), new Vector2(252, 92), 36);
            UIFactory.Place((RectTransform)shopBtn.transform, ctaAnchor, new Vector2(-134, -128), new Vector2(252, 92), ctaPivot);
            var ranksBtn = UIFactory.MakeButton(menu, "RANKS", UIFactory.Panel, () => gm.OpenLeaderboard(), new Vector2(252, 92), 36);
            UIFactory.Place((RectTransform)ranksBtn.transform, ctaAnchor, new Vector2(134, -128), new Vector2(252, 92), ctaPivot);

            // Bottom-left progress glass card (Best + Active Mission)
            var progBox = UIFactory.PanelBox(menu, "ProgressCard", UIFactory.GlassDark);
            UIFactory.Place((RectTransform)progBox.transform, new Vector2(0, 0), new Vector2(36, 28), new Vector2(740, 136));
            bestText = UIFactory.Label(progBox.transform, "BEST 0 m", 38, Color.white, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)bestText.transform, new Vector2(0, 1), new Vector2(24, -16), new Vector2(680, 48));
            menuMissionText = UIFactory.Label(progBox.transform, "", 30, new Color(0.78f, 0.87f, 1f), TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)menuMissionText.transform, new Vector2(0, 1), new Vector2(24, -74), new Vector2(560, 46));
            var mCoin = UIFactory.Image(progBox.transform, "MissionCoin", SpriteLibrary.Get("coin"), Color.white);
            mCoin.preserveAspect = true;
            UIFactory.Place((RectTransform)mCoin.transform, new Vector2(1, 0), new Vector2(-124, 24), new Vector2(28, 28), new Vector2(0, 0));
            menuMissionRewardText = UIFactory.Label(progBox.transform, "+60", 30, UIFactory.Accent, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)menuMissionRewardText.transform, new Vector2(1, 0), new Vector2(-90, 18), new Vector2(76, 40), new Vector2(0, 0));

            // Top-right wallet pill + audio toggles
            menuWallet = UIFactory.WalletPill(menu, "WalletPill", UIFactory.GlassDark,
                new Vector2(1, 1), new Vector2(-36, -24), new Vector2(468, 68));

            sfxBtn = UIFactory.MakeButton(menu, "SFX ON", UIFactory.Panel, () => { var s = SaveData.Current; s.sfxOn = !s.sfxOn; s.Save(); }, new Vector2(226, 62), 30);
            UIFactory.Place((RectTransform)sfxBtn.transform, new Vector2(1, 1), new Vector2(-278, -104), new Vector2(226, 62));
            musicBtn = UIFactory.MakeButton(menu, "MUSIC ON", UIFactory.Panel, () => { var s = SaveData.Current; s.musicOn = !s.musicOn; s.Save(); }, new Vector2(226, 62), 30);
            UIFactory.Place((RectTransform)musicBtn.transform, new Vector2(1, 1), new Vector2(-36, -104), new Vector2(226, 62));

            var shotBtn = UIFactory.MakeButton(menu, "SCREENSHOT", UIFactory.Blue, () => gm.CaptureAndShowScreenshot(), new Vector2(226, 62), 26);
            UIFactory.Place((RectTransform)shotBtn.transform, new Vector2(1, 1), new Vector2(-278, -178), new Vector2(226, 62));
            var shareShotBtn = UIFactory.MakeButton(menu, "SHARE SHOT", UIFactory.Good, () => gm.ShareLastOrCaptureScreenshot(), new Vector2(226, 62), 26);
            UIFactory.Place((RectTransform)shareShotBtn.transform, new Vector2(1, 1), new Vector2(-36, -178), new Vector2(226, 62));

            // Top-left energy card + Daily Rewards
            var eBox = UIFactory.PanelBox(menu, "EnergyBox", UIFactory.CardBg);
            UIFactory.Place((RectTransform)eBox.transform, new Vector2(0, 1), new Vector2(36, -24), new Vector2(468, 178));
            var eIcon = UIFactory.Image(eBox.transform, "EnergyIcon", SpriteLibrary.Get("energy"), Color.white);
            eIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)eIcon.transform, new Vector2(0, 1), new Vector2(18, -18), new Vector2(32, 42));
            menuEnergyText = UIFactory.Label(eBox.transform, "ENERGY 5/5", 27, UIFactory.Accent, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)menuEnergyText.transform, new Vector2(0, 1), new Vector2(56, -20), new Vector2(180, 36));
            menuEnergyTimerText = UIFactory.Label(eBox.transform, "FULL", 23, UIFactory.TextMuted, TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)menuEnergyTimerText.transform, new Vector2(1, 1), new Vector2(-18, -20), new Vector2(220, 36));
            BuildEnergyPips(eBox.transform, menuEnergyPips, -72f, 424f);
            menuEnergyAdBtn = UIFactory.MakeButton(eBox.transform, "+1 ENERGY (AD)", UIFactory.Amber, () => gm.WatchAdForEnergy(), new Vector2(430, 50), 26);
            UIFactory.Place((RectTransform)menuEnergyAdBtn.transform, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(430, 50));

            dailyRewardBtn = UIFactory.MakeButton(menu, "DAILY REWARDS", UIFactory.Good, ShowDailyRewards, new Vector2(468, 74), 30);
            UIFactory.Place((RectTransform)dailyRewardBtn.transform, new Vector2(0, 1), new Vector2(36, -214), new Vector2(468, 74));
            dailyDoneCheck = UIFactory.Image(dailyRewardBtn.transform, "DoneCheck", SpriteLibrary.Get("ui_check"), Color.white);
            dailyDoneCheck.preserveAspect = true;
            UIFactory.Place((RectTransform)dailyDoneCheck.transform, new Vector2(0.5f, 0.5f), new Vector2(126, 4), new Vector2(34, 34), new Vector2(0.5f, 0.5f));
            dailyDoneCheck.gameObject.SetActive(false);

            var menuMotion = UIPanelMotion.Attach(menu, null, 0.16f);
            menuMotion.AddStagger((RectTransform)play.transform);
            menuMotion.AddStagger((RectTransform)gar.transform);
            menuMotion.AddStagger((RectTransform)skinsBtn.transform);
            menuMotion.AddStagger((RectTransform)shopBtn.transform);
            menuMotion.AddStagger((RectTransform)ranksBtn.transform);
            menuMotion.AddStagger((RectTransform)eBox.transform);
            menuMotion.AddStagger((RectTransform)dailyRewardBtn.transform);
        }

        // =====================================================================
        // Garage
        // =====================================================================
        void BuildGarage(Transform parent)
        {
            garage = UIFactory.Stretch("Garage", parent);
            var bg = UIFactory.Image(garage, "Bg", SpriteLibrary.Get("square"), new Color(0.06f, 0.08f, 0.16f, 0.94f));
            var brt = (RectTransform)bg.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.raycastTarget = true;

            UIFactory.HeaderBar(garage, "GARAGE", () => gm.ShowMenu(), out garageWallet);

            var garageMotion = UIPanelMotion.Attach(garage, null, 0.16f);
            var positions = new[] { new Vector2(-450, 140), new Vector2(450, 140), new Vector2(-450, -245), new Vector2(450, -245) };
            int i = 0;
            foreach (var def in UpgradeDatabase.All)
            {
                int idx = (int)def.id;
                Color accent = UpgradeAccent(def.id);
                var card = UIFactory.PanelBox(garage, "Card_" + def.id, UIFactory.CardBg);
                var cardRt = (RectTransform)card.transform;
                garageCards[idx] = cardRt;
                UIFactory.Place(cardRt, new Vector2(0.5f, 0.5f), positions[i], new Vector2(860, 355));
                UIFactory.AddTopAccent(card.transform, accent, 7f, 20f);
                garageMotion.AddStagger(cardRt);

                var badge = UIFactory.GlassPill(card.transform, "Badge", UIFactory.SlotDark);
                UIFactory.Place((RectTransform)badge.transform, new Vector2(0, 1), new Vector2(30, -32), new Vector2(116, 116));
                var glow = UIFactory.Image(badge.transform, "Glow", SpriteLibrary.Get("ui_glow"), new Color(accent.r, accent.g, accent.b, 0.35f));
                UIFactory.Place((RectTransform)glow.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(106, 106));
                var icon = UIFactory.Image(badge.transform, "Icon", SpriteLibrary.Get(UpgradeIcon(def.id)), Color.white);
                icon.preserveAspect = true;
                UIFactory.Place((RectTransform)icon.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78, 78));

                var name = UIFactory.Label(card.transform, def.title, 38, Color.white, TextAnchor.UpperLeft);
                UIFactory.Place((RectTransform)name.transform, new Vector2(0, 1), new Vector2(166, -32), new Vector2(640, 48));
                cardTier[idx] = UIFactory.Label(card.transform, "", 40, UIFactory.Accent, TextAnchor.UpperLeft);
                UIFactory.Place((RectTransform)cardTier[idx].transform, new Vector2(0, 1), new Vector2(166, -80), new Vector2(640, 50));
                cardInfo[idx] = UIFactory.Label(card.transform, "", 30, UIFactory.TextMuted, TextAnchor.UpperLeft, FontStyle.Normal);
                UIFactory.Place((RectTransform)cardInfo[idx].transform, new Vector2(0, 1), new Vector2(30, -168), new Vector2(800, 44));

                int tierCount = Mathf.Min(def.tiers.Count, 4);
                cardLevelLbl[idx] = UIFactory.Label(card.transform, "", 26, UIFactory.TextMuted, TextAnchor.LowerLeft);
                UIFactory.Place((RectTransform)cardLevelLbl[idx].transform, new Vector2(0, 0), new Vector2(30, 72), new Vector2(370, 34), new Vector2(0, 0));

                const float totalW = 370f;
                const float gap = 10f;
                float pipW = (totalW - gap * (tierCount - 1)) / tierCount;
                for (int k = 0; k < 4; k++)
                {
                    if (k < tierCount)
                    {
                        var pip = UIFactory.PixelPip(card.transform, "Pip_" + k, new Color(0.42f, 0.33f, 0.27f));
                        UIFactory.Place((RectTransform)pip.transform, new Vector2(0, 0), new Vector2(30f + k * (pipW + gap), 40f), new Vector2(pipW, 20f), new Vector2(0, 0));
                        cardPipImgs[idx, k] = pip;
                    }
                    else
                    {
                        cardPipImgs[idx, k] = null;
                    }
                }

                var captured = def.id;
                cardBtn[idx] = UIFactory.MakeButton(card.transform, "UPGRADE", UIFactory.Blue, () => TryBuy(captured), new Vector2(390, 90), 32);
                UIFactory.Place((RectTransform)cardBtn[idx].transform, new Vector2(1, 0), new Vector2(-26, 26), new Vector2(390, 90));

                var costRt = UIFactory.Rect("CostRow", cardBtn[idx].transform);
                UIFactory.Place(costRt, new Vector2(1, 0.5f), new Vector2(-20, 4), new Vector2(210, 40), new Vector2(1, 0.5f));
                var ccIcon = UIFactory.Image(costRt, "CoinIcon", SpriteLibrary.Get("coin"), Color.white);
                ccIcon.preserveAspect = true;
                UIFactory.Place((RectTransform)ccIcon.transform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(26, 26), new Vector2(0, 0.5f));
                cardCoinCost[idx] = UIFactory.Label(costRt, "0", 28, UIFactory.Accent, TextAnchor.MiddleLeft);
                UIFactory.Place((RectTransform)cardCoinCost[idx].transform, new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(76, 36), new Vector2(0, 0.5f));
                var pcIcon = UIFactory.Image(costRt, "PartIcon", SpriteLibrary.Get("part"), Color.white);
                pcIcon.preserveAspect = true;
                UIFactory.Place((RectTransform)pcIcon.transform, new Vector2(0, 0.5f), new Vector2(116, 0), new Vector2(26, 26), new Vector2(0, 0.5f));
                cardPartCost[idx] = UIFactory.Label(costRt, "0", 28, new Color(0.60f, 0.85f, 1f), TextAnchor.MiddleLeft);
                UIFactory.Place((RectTransform)cardPartCost[idx].transform, new Vector2(0, 0.5f), new Vector2(148, 0), new Vector2(56, 36), new Vector2(0, 0.5f));
                cardCostRow[idx] = costRt.gameObject;
                i++;
            }
        }

        static Color UpgradeAccent(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.JetThruster: return UIFactory.Bad;
                case UpgradeId.FuelTank: return UIFactory.Amber;
                case UpgradeId.Wheels: return UIFactory.Blue;
                default: return UIFactory.Good;
            }
        }

        static string UpgradeIcon(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.JetThruster: return "thruster_2";
                case UpgradeId.FuelTank: return "fuel";
                case UpgradeId.Wheels: return "wheel";
                default: return "shield_orb";
            }
        }

        public void BuyUpgrade(UpgradeId id) => TryBuy(id);

        void TryBuy(UpgradeId id)
        {
            if (SaveData.Current.TryUpgrade(id))
            {
                AudioManager.Sfx("buy");
                UIPunch.Trigger(garageCards[(int)id], 0.12f);
            }
            else
            {
                AudioManager.Sfx("deny");
                cardBtn[(int)id].GetComponent<UIButtonFeel>()?.Shake();
            }
            RefreshGarage();
        }

        string Describe(UpgradeId id, UpgradeTier t)
        {
            switch (id)
            {
                case UpgradeId.JetThruster: return "Thrust power x" + t.value.ToString("0.00");
                case UpgradeId.FuelTank: return "Tank x" + t.value.ToString("0.0") + "   Burn x" + t.value2.ToString("0.0");
                case UpgradeId.Wheels: return "Cruise speed x" + t.value.ToString("0.00") + "   Grip x" + t.value2.ToString("0.00");
                default: return "Absorbs " + Mathf.RoundToInt(t.value) + " hit(s) per run";
            }
        }

        void RefreshGarage()
        {
            var save = SaveData.Current;
            foreach (var def in UpgradeDatabase.All)
            {
                int idx = (int)def.id;
                int lv = save.GetLevel(def.id);
                var cur = def.tiers[lv];
                cardTier[idx].text = cur.name;
                if (cardLevelLbl[idx] != null)
                    cardLevelLbl[idx].text = lv >= def.MaxLevel
                        ? "LEVEL MAX (" + def.tiers.Count + "/" + def.tiers.Count + ")"
                        : "LEVEL " + (lv + 1) + " / " + def.tiers.Count;

                Color filledCol = lv >= def.MaxLevel ? UIFactory.Accent : UIFactory.Good;
                Color emptyCol = new Color(0.42f, 0.33f, 0.27f);
                for (int k = 0; k < 4; k++)
                {
                    if (cardPipImgs[idx, k] == null) continue;
                    cardPipImgs[idx, k].color = k <= lv ? filledCol : emptyCol;
                }

                var btn = cardBtn[idx];
                var lbl = btn.transform.Find("Label")?.GetComponent<Text>();
                if (lv >= def.MaxLevel)
                {
                    cardInfo[idx].text = Describe(def.id, cur) + "   (MAX)";
                    if (cardCostRow[idx] != null) cardCostRow[idx].SetActive(false);
                    if (lbl != null)
                    {
                        var lrt = (RectTransform)lbl.transform;
                        lrt.offsetMin = new Vector2(12, 10);
                        lrt.offsetMax = new Vector2(-12, -2);
                        lbl.alignment = TextAnchor.MiddleCenter;
                        lbl.fontSize = UIFactory.ScaleFontSize(32);
                        lbl.text = "MAXED";
                    }
                    btn.interactable = false;
                }
                else
                {
                    var next = def.tiers[lv + 1];
                    cardInfo[idx].text = "Next: " + next.name + " - " + Describe(def.id, next);
                    if (cardCostRow[idx] != null) cardCostRow[idx].SetActive(true);
                    if (cardCoinCost[idx] != null) cardCoinCost[idx].text = next.coinCost.ToString();
                    if (cardPartCost[idx] != null) cardPartCost[idx].text = next.partCost.ToString();
                    if (lbl != null)
                    {
                        var lrt = (RectTransform)lbl.transform;
                        lrt.offsetMin = new Vector2(22, 10);
                        lrt.offsetMax = new Vector2(-220, -2);
                        lbl.alignment = TextAnchor.MiddleLeft;
                        lbl.fontSize = UIFactory.ScaleFontSize(30);
                        lbl.text = "UPGRADE";
                    }
                    btn.interactable = true;
                    btn.targetGraphic.color = save.CanAfford(next) ? UIFactory.Blue : new Color(0.35f, 0.35f, 0.45f);
                }
            }
        }

        // =====================================================================
        // Summary
        // =====================================================================
        void BuildSummary(Transform parent)
        {
            summary = UIFactory.Stretch("Summary", parent);
            var bg = UIFactory.Image(summary, "Bg", SpriteLibrary.Get("square"), new Color(0.04f, 0.05f, 0.11f, 0.84f));
            var brt = (RectTransform)bg.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.raycastTarget = true;

            var panel = UIFactory.PanelBox(summary, "Panel", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1660, 880));

            summaryTitle = UIFactory.Label(panel.transform, "WIPEOUT!", 84, UIFactory.Bad, TextAnchor.UpperCenter);
            summaryTitle.lineSpacing = 0.92f;
            UIFactory.Place((RectTransform)summaryTitle.transform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(900, 165));

            var sumShotBtn = UIFactory.MakeButton(panel.transform, "SCREENSHOT", UIFactory.Blue, () => gm.CaptureAndShowScreenshot(), new Vector2(216, 72), 26);
            UIFactory.Place((RectTransform)sumShotBtn.transform, new Vector2(0, 1), new Vector2(36, -30), new Vector2(216, 72));
            var sumShareBtn = UIFactory.MakeButton(panel.transform, "SHARE", UIFactory.Good, () => gm.ShareLastOrCaptureScreenshot(), new Vector2(176, 72), 26);
            UIFactory.Place((RectTransform)sumShareBtn.transform, new Vector2(0, 1), new Vector2(266, -30), new Vector2(176, 72));

            // polaroid style snapshot of the crash (GDD 2: snapshot of the funniest fall)
            var frame = UIFactory.PanelBox(panel.transform, "Polaroid", Color.white);
            UIFactory.Place((RectTransform)frame.transform, new Vector2(0, 0), new Vector2(56, 198), new Vector2(640, 444), new Vector2(0, 0));
            frame.transform.localRotation = Quaternion.Euler(0f, 0f, -2.5f);
            snapshotImg = UIFactory.Image(frame.transform, "Snapshot", SpriteLibrary.Get("turtle"), Color.white);
            UIFactory.Place((RectTransform)snapshotImg.transform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(592, 336), new Vector2(0.5f, 1));
            snapshotCaption = UIFactory.Label(frame.transform, "", 32, new Color(0.22f, 0.17f, 0.14f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.Place((RectTransform)snapshotCaption.transform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(590, 48), new Vector2(0.5f, 0));
            var capOutline = snapshotCaption.GetComponent<Outline>();
            if (capOutline != null) capOutline.enabled = false;
            var capShadow = snapshotCaption.GetComponent<Shadow>();
            if (capShadow != null) capShadow.enabled = false;

            var statsBox = UIFactory.PanelBox(panel.transform, "StatsBox", new Color(0.17f, 0.12f, 0.10f, 0.96f));
            UIFactory.Place((RectTransform)statsBox.transform, new Vector2(0, 0), new Vector2(736, 280), new Vector2(868, 378), new Vector2(0, 0));
            summaryBody = UIFactory.Label(statsBox.transform, "", 36, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            summaryBody.lineSpacing = 1.18f;
            var sbrt = (RectTransform)summaryBody.transform;
            sbrt.anchorMin = Vector2.zero; sbrt.anchorMax = Vector2.one;
            sbrt.offsetMin = new Vector2(30, 76); sbrt.offsetMax = new Vector2(-30, -22);

            var statsSep = UIFactory.Image(statsBox.transform, "Divider", SpriteLibrary.Get("square"), new Color(1f, 0.92f, 0.80f, 0.16f));
            UIFactory.Place((RectTransform)statsSep.transform, new Vector2(0, 0), new Vector2(28, 72), new Vector2(812, 2), new Vector2(0, 0));

            var earnedRow = UIFactory.Rect("EarnedRow", statsBox.transform);
            UIFactory.Place(earnedRow, new Vector2(0, 0), new Vector2(30, 18), new Vector2(808, 48), new Vector2(0, 0));
            var eLbl = UIFactory.Label(earnedRow, "Earned", 36, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)eLbl.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(170, 48), new Vector2(0, 0.5f));
            var scIcon = UIFactory.Image(earnedRow, "CoinIcon", SpriteLibrary.Get("coin"), Color.white);
            scIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)scIcon.transform, new Vector2(0, 0.5f), new Vector2(186, 0), new Vector2(32, 32), new Vector2(0, 0.5f));
            summaryCoinsText = UIFactory.Label(earnedRow, "+0", 36, UIFactory.Accent, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)summaryCoinsText.transform, new Vector2(0, 0.5f), new Vector2(228, 0), new Vector2(192, 48), new Vector2(0, 0.5f));
            var spIcon = UIFactory.Image(earnedRow, "PartIcon", SpriteLibrary.Get("part"), Color.white);
            spIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)spIcon.transform, new Vector2(0, 0.5f), new Vector2(440, 0), new Vector2(32, 32), new Vector2(0, 0.5f));
            summaryPartsText = UIFactory.Label(earnedRow, "+0", 36, new Color(0.60f, 0.85f, 1f), TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)summaryPartsText.transform, new Vector2(0, 0.5f), new Vector2(482, 0), new Vector2(192, 48), new Vector2(0, 0.5f));

            BuildSummaryExtras((RectTransform)panel.transform);

            // energy box (top-right of summary panel)
            var seBox = UIFactory.PanelBox(panel.transform, "SummaryEnergyBox", new Color(0.24f, 0.18f, 0.14f, 0.98f));
            UIFactory.Place((RectTransform)seBox.transform, new Vector2(1, 1), new Vector2(-36, -24), new Vector2(468, 170));
            var seIcon = UIFactory.Image(seBox.transform, "EnergyIcon", SpriteLibrary.Get("energy"), Color.white);
            seIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)seIcon.transform, new Vector2(0, 1), new Vector2(18, -18), new Vector2(32, 42));
            summaryEnergyText = UIFactory.Label(seBox.transform, "ENERGY 5/5", 27, UIFactory.Accent, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)summaryEnergyText.transform, new Vector2(0, 1), new Vector2(56, -20), new Vector2(180, 36));
            summaryEnergyTimerText = UIFactory.Label(seBox.transform, "FULL", 23, UIFactory.TextMuted, TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)summaryEnergyTimerText.transform, new Vector2(1, 1), new Vector2(-18, -20), new Vector2(220, 36));
            BuildEnergyPips(seBox.transform, summaryEnergyPips, -70f, 424f);
            summaryEnergyAdBtn = UIFactory.MakeButton(seBox.transform, "+1 ENERGY (AD)", UIFactory.Amber, () => gm.WatchAdForEnergy(), new Vector2(430, 50), 26);
            UIFactory.Place((RectTransform)summaryEnergyAdBtn.transform, new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(430, 50));

            var retry = UIFactory.MakeButton(panel.transform, "RETRY", UIFactory.Good, () => gm.StartRun(), new Vector2(484, 114), 54);
            UIFactory.Place((RectTransform)retry.transform, new Vector2(0.5f, 0), new Vector2(-528, 34), new Vector2(484, 114));
            UIFactory.AddButtonIcon(retry, "ui_play", new Vector2(36, 36), 38f);
            var gar = UIFactory.MakeButton(panel.transform, "GARAGE", UIFactory.Blue, () => gm.OpenGarage(), new Vector2(484, 114), 54);
            UIFactory.Place((RectTransform)gar.transform, new Vector2(0.5f, 0), new Vector2(0, 34), new Vector2(484, 114));
            var menuBtn = UIFactory.MakeButton(panel.transform, "MENU", UIFactory.Panel, () => gm.ShowMenu(), new Vector2(484, 114), 54);
            UIFactory.Place((RectTransform)menuBtn.transform, new Vector2(0.5f, 0), new Vector2(528, 34), new Vector2(484, 114));
            UIPanelMotion.Attach(summary, (RectTransform)panel.transform, 0.16f);
        }

        // =====================================================================
        // Pause
        // =====================================================================
        void BuildPause(Transform parent)
        {
            pause = UIFactory.Stretch("Pause", parent);
            var bg = UIFactory.Image(pause, "Bg", SpriteLibrary.Get("square"), new Color(0.03f, 0.04f, 0.10f, 0.72f));
            var brt = (RectTransform)bg.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.raycastTarget = true;

            var panel = UIFactory.PanelBox(pause, "Panel", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 620));
            UIFactory.AddTopAccent(panel.transform, UIFactory.Blue, 7f, 22f);

            var t = UIFactory.Label(panel.transform, "PAUSED", 86, Color.white);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(560, 100), new Vector2(0.5f, 1));

            var ePill = UIFactory.GlassPill(panel.transform, "PauseEnergyPill", UIFactory.SlotDark);
            UIFactory.Place((RectTransform)ePill.transform, new Vector2(0.5f, 1), new Vector2(0, -152), new Vector2(490, 58), new Vector2(0.5f, 1));
            var peIcon = UIFactory.Image(ePill.transform, "EnergyIcon", SpriteLibrary.Get("energy"), Color.white);
            peIcon.preserveAspect = true;
            UIFactory.Place((RectTransform)peIcon.transform, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(22, 28), new Vector2(0, 0.5f));
            pauseEnergyText = UIFactory.Label(ePill.transform, "ENERGY 5/5", 30, UIFactory.Accent);
            var pert = (RectTransform)pauseEnergyText.transform; pert.anchorMin = Vector2.zero; pert.anchorMax = Vector2.one;
            pert.offsetMin = new Vector2(50, 0); pert.offsetMax = new Vector2(-16, 0);

            var resume = UIFactory.MakeButton(panel.transform, "RESUME", UIFactory.Good, () => gm.Resume(), new Vector2(480, 104), 48);
            UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(480, 104), new Vector2(0.5f, 1));
            UIFactory.AddButtonIcon(resume, "ui_play", new Vector2(32, 32), 34f);
            pauseRestartBtn = UIFactory.MakeButton(panel.transform, "RESTART (-1 ENERGY)", UIFactory.Blue, () => gm.StartRun(), new Vector2(480, 96), 34);
            UIFactory.Place((RectTransform)pauseRestartBtn.transform, new Vector2(0.5f, 1), new Vector2(0, -354), new Vector2(480, 96), new Vector2(0.5f, 1));
            var quit = UIFactory.MakeButton(panel.transform, "QUIT RUN", UIFactory.Bad, () => gm.QuitRun(), new Vector2(480, 96), 42);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(480, 96), new Vector2(0.5f, 1));

            var pauseMotion = UIPanelMotion.Attach(pause, (RectTransform)panel.transform, 0.14f);
            pauseMotion.AddStagger((RectTransform)resume.transform);
            pauseMotion.AddStagger((RectTransform)pauseRestartBtn.transform);
            pauseMotion.AddStagger((RectTransform)quit.transform);
        }
    }
}
