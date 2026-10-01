using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace TurtleBlaster
{
    /// <summary>
    /// Builds and drives every screen: HUD + touch controls, main menu, garage,
    /// pause and run summary. Pure uGUI created in code at a 1920x1080 reference.
    /// </summary>
    public class UIRoot : MonoBehaviour
    {
        GameManager gm;
        Canvas canvas;
        CanvasGroup canvasGroup;

        // screens
        RectTransform hud, menu, garage, summary, pause, popups;

        // hud widgets
        Text distanceText, scoreText, comboText, coinText, partText, boostBanner, shieldText;
        Image fuelFill, stuntFill;
        RectTransform touchRoot;

        // menu widgets
        Text bestText, menuWallet;
        Button sfxBtn, musicBtn;

        // garage widgets
        Text garageWallet;
        readonly Text[] cardTier = new Text[4], cardInfo = new Text[4], cardPips = new Text[4];
        readonly Button[] cardBtn = new Button[4];

        // summary widgets
        Text summaryTitle, summaryBody;
        Image snapshotImg;
        Text snapshotCaption;
        Sprite snapshotSprite;

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
            HideAll();

            SaveData.Current.Changed += RefreshWallets;
        }

        void OnDestroy() { SaveData.Current.Changed -= RefreshWallets; }

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
            hud.gameObject.SetActive(true);
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
            if (on) GameInput.ClearTouch();
        }

        public void SetVisible(bool visible) { canvasGroup.alpha = visible ? 1f : 0f; }

        public void ShowSummary(RunStats s, Texture2D snapshot)
        {
            HideAll();
            summary.gameObject.SetActive(true);

            summaryTitle.text = "WIPEOUT!\n<size=40><color=#ffd23f>" + s.causeOfDeath + "</color></size>";
            var sb = new StringBuilder();
            sb.AppendLine("Distance   <color=#ffd23f>" + Mathf.RoundToInt(s.distance) + " m</color>" + (s.newBestDistance ? "  <color=#4cd964>NEW BEST!</color>" : ""));
            sb.AppendLine("Score      <color=#ffd23f>" + s.score + "</color>" + (s.newBestScore ? "  <color=#4cd964>NEW BEST!</color>" : ""));
            sb.AppendLine("Flips      " + s.flips + "     Perfect landings  " + s.perfectLandings);
            sb.AppendLine("Top speed  " + Mathf.RoundToInt(s.topSpeed * 3.6f) + " km/h     Best combo  x" + s.maxCombo);
            sb.AppendLine();
            sb.AppendLine("Earned     <color=#ffd23f>+" + s.coins + " coins</color>   <color=#9ad0ff>+" + s.parts + " parts</color>");
            summaryBody.text = sb.ToString();

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
                snapshotImg.sprite = SpriteLibrary.Get("turtle");
                snapshotImg.color = Color.white;
                snapshotCaption.text = "";
            }
        }

        public void RefreshWallets()
        {
            var s = SaveData.Current;
            string w = "<color=#ffd23f>" + s.coins + " coins</color>   <color=#9ad0ff>" + s.parts + " parts</color>";
            if (menuWallet != null) menuWallet.text = w;
            if (garageWallet != null) garageWallet.text = w;
            if (bestText != null) bestText.text = "BEST  " + s.bestDistance + " m";
            if (sfxBtn != null) UIFactory.SetButtonLabel(sfxBtn, "SFX " + (s.sfxOn ? "ON" : "OFF"));
            if (musicBtn != null) UIFactory.SetButtonLabel(musicBtn, "MUSIC " + (s.musicOn ? "ON" : "OFF"));
            if (garage != null && garage.gameObject.activeSelf) RefreshGarage();
        }

        public void Popup(string text, Color color, int size = 64)
        {
            var t = UIFactory.Label(popups, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold, "Popup");
            var rt = (RectTransform)t.transform;
            UIFactory.Place(rt, new Vector2(0.5f, 0.62f), new Vector2(Random.Range(-60f, 60f), 0), new Vector2(900, 120));
            t.gameObject.AddComponent<FloatingText>();
        }

        // =====================================================================
        // HUD
        // =====================================================================
        void BuildHud(Transform parent)
        {
            hud = UIFactory.Stretch("HUD", parent);

            distanceText = UIFactory.Label(hud, "0 m", 76, Color.white, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)distanceText.transform, new Vector2(0, 1), new Vector2(40, -24), new Vector2(520, 90));
            scoreText = UIFactory.Label(hud, "SCORE 0", 40, UIFactory.Accent, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)scoreText.transform, new Vector2(0, 1), new Vector2(44, -112), new Vector2(520, 50));
            comboText = UIFactory.Label(hud, "", 40, UIFactory.Good, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)comboText.transform, new Vector2(0, 1), new Vector2(44, -158), new Vector2(520, 50));

            // fuel + stunt bars (top centre)
            var fuelLabel = UIFactory.Label(hud, "FUEL", 30, Color.white, TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)fuelLabel.transform, new Vector2(0.5f, 1), new Vector2(-270, -50), new Vector2(120, 40));
            fuelFill = UIFactory.Bar(hud, "FuelBar", UIFactory.Good, new Vector2(460, 40), out var fuelRoot);
            UIFactory.Place(fuelRoot, new Vector2(0.5f, 1), new Vector2(30, -50), new Vector2(460, 40));
            var stuntLabel = UIFactory.Label(hud, "STUNT", 30, Color.white, TextAnchor.MiddleRight);
            UIFactory.Place((RectTransform)stuntLabel.transform, new Vector2(0.5f, 1), new Vector2(-270, -100), new Vector2(120, 40));
            stuntFill = UIFactory.Bar(hud, "StuntBar", UIFactory.Blue, new Vector2(460, 32), out var stuntRoot);
            UIFactory.Place(stuntRoot, new Vector2(0.5f, 1), new Vector2(30, -100), new Vector2(460, 32));

            boostBanner = UIFactory.Label(hud, "SUPER BOOST!", 72, UIFactory.Accent);
            UIFactory.Place((RectTransform)boostBanner.transform, new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(900, 100));
            shieldText = UIFactory.Label(hud, "", 36, new Color(0.6f, 0.85f, 1f));
            UIFactory.Place((RectTransform)shieldText.transform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(600, 40));

            // pickups counters (top right)
            var coinIcon = UIFactory.Image(hud, "CoinIcon", SpriteLibrary.Get("coin"), Color.white);
            UIFactory.Place((RectTransform)coinIcon.transform, new Vector2(1, 1), new Vector2(-300, -36), new Vector2(48, 48));
            coinText = UIFactory.Label(hud, "0", 44, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)coinText.transform, new Vector2(1, 1), new Vector2(-244, -34), new Vector2(120, 52));
            var partIcon = UIFactory.Image(hud, "PartIcon", SpriteLibrary.Get("part"), Color.white);
            UIFactory.Place((RectTransform)partIcon.transform, new Vector2(1, 1), new Vector2(-300, -92), new Vector2(48, 48));
            partText = UIFactory.Label(hud, "0", 44, Color.white, TextAnchor.MiddleLeft);
            UIFactory.Place((RectTransform)partText.transform, new Vector2(1, 1), new Vector2(-244, -90), new Vector2(120, 52));

            var pauseBtn = UIFactory.MakeButton(hud, "II", UIFactory.Panel, () => gm.Pause(), new Vector2(96, 96), 44);
            UIFactory.Place((RectTransform)pauseBtn.transform, new Vector2(1, 1), new Vector2(-28, -28), new Vector2(96, 96));

            BuildTouchControls(hud);
        }

        void BuildTouchControls(Transform parent)
        {
            touchRoot = UIFactory.Stretch("TouchControls", parent);
            var blue = new Color(0.23f, 0.52f, 1f, 0.45f);
            var orange = new Color(1f, 0.55f, 0.1f, 0.45f);

            // Left thumb: vertical axis (Pitch Up / Pitch Down)
            TouchButton("Up", "THRUST  [W/Up]", "ui_arrow", false, false, blue, new Vector2(0, 0), new Vector2(50, 290), v => GameInput.TouchUp = v);
            TouchButton("Down", "SLAM  [S/Down]", "ui_arrow", true, false, blue, new Vector2(0, 0), new Vector2(50, 50), v => GameInput.TouchDown = v);
            // Right thumb: horizontal axis (Backflip / Frontflip)
            TouchButton("Jump", "JUMP [Space]", "ui_arrow", false, false, new Color(0.3f, 0.8f, 0.4f, 0.45f), new Vector2(1, 0), new Vector2(-50, 290), v => { if (v) GameInput.TouchJump = true; });
            TouchButton("Back", "BACKFLIP [A/Left]", "ui_flip", false, false, orange, new Vector2(1, 0), new Vector2(-300, 50), v => GameInput.TouchLeft = v);
            TouchButton("Front", "FRONTFLIP [D/Right]", "ui_flip", false, true, orange, new Vector2(1, 0), new Vector2(-50, 50), v => GameInput.TouchRight = v);
        }

        void TouchButton(string name, string caption, string icon, bool flipY, bool flipX, Color color,
            Vector2 anchor, Vector2 pos, System.Action<bool> onHold)
        {
            var img = UIFactory.PanelBox(touchRoot, "Touch_" + name, color);
            img.raycastTarget = true;
            var rt = (RectTransform)img.transform;
            UIFactory.Place(rt, anchor, new Vector2(pos.x, pos.y), new Vector2(220, 220), new Vector2(anchor.x, 0));
            var hb = img.gameObject.AddComponent<HoldButton>();
            hb.OnHold = onHold;

            var ic = UIFactory.Image(rt, "Icon", SpriteLibrary.Get(icon), new Color(1, 1, 1, 0.9f));
            var irt = (RectTransform)ic.transform;
            UIFactory.Place(irt, new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(110, 110));
            irt.localScale = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);

            var cap = UIFactory.Label(rt, caption, 24, Color.white);
            UIFactory.Place((RectTransform)cap.transform, new Vector2(0.5f, 0.1f), Vector2.zero, new Vector2(220, 40));
        }

        void Update()
        {
            if (gm == null || !hud.gameObject.activeSelf) return;
            var p = gm.Player;
            var s = gm.Stats;
            if (s == null) return;

            distanceText.text = Mathf.FloorToInt(s.distance) + " m";
            scoreText.text = "SCORE " + s.score;
            comboText.text = gm.Combo > 0 ? "COMBO x" + gm.ComboMultiplier.ToString("0.00") : "";
            coinText.text = s.coins.ToString();
            partText.text = s.parts.ToString();

            if (p != null)
            {
                fuelFill.fillAmount = p.Fuel01;
                fuelFill.color = p.Fuel01 < 0.25f ? UIFactory.Bad : UIFactory.Good;
                stuntFill.fillAmount = p.Stunt01;
                stuntFill.color = p.SuperBoost ? Color.HSVToRGB(Mathf.Repeat(Time.time * 2f, 1f), 0.6f, 1f) : UIFactory.Blue;
                boostBanner.enabled = p.SuperBoost;
                if (p.SuperBoost) boostBanner.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 14f) * 0.06f);
                shieldText.text = p.FreeHitsLeft > 0 ? "SHELL x" + p.FreeHitsLeft : "";
            }
        }

        // =====================================================================
        // Menu
        // =====================================================================
        void BuildMenu(Transform parent)
        {
            menu = UIFactory.Stretch("Menu", parent);
            var dim = UIFactory.Image(menu, "Dim", SpriteLibrary.Get("square"), new Color(0, 0, 0, 0.18f));
            var drt = (RectTransform)dim.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;

            var t1 = UIFactory.Label(menu, "TURTLE", 200, UIFactory.Accent);
            UIFactory.Place((RectTransform)t1.transform, new Vector2(0.5f, 0.84f), Vector2.zero, new Vector2(1400, 200));
            var t2 = UIFactory.Label(menu, "BLASTER", 200, new Color(1f, 0.35f, 0.3f));
            UIFactory.Place((RectTransform)t2.transform, new Vector2(0.5f, 0.66f), Vector2.zero, new Vector2(1400, 200));
            var sub = UIFactory.Label(menu, "Tao Khee Skateboard", 44, Color.white);
            UIFactory.Place((RectTransform)sub.transform, new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(900, 60));

            var play = UIFactory.MakeButton(menu, "PLAY", new Color(0.2f, 0.7f, 0.3f), () => gm.StartRun(), new Vector2(520, 140), 72);
            UIFactory.Place((RectTransform)play.transform, new Vector2(0.74f, 0.36f), Vector2.zero, new Vector2(520, 140));
            var gar = UIFactory.MakeButton(menu, "GARAGE", UIFactory.Blue, () => gm.OpenGarage(), new Vector2(520, 110), 56);
            UIFactory.Place((RectTransform)gar.transform, new Vector2(0.74f, 0.2f), Vector2.zero, new Vector2(520, 110));

            bestText = UIFactory.Label(menu, "BEST 0 m", 44, Color.white, TextAnchor.LowerLeft);
            UIFactory.Place((RectTransform)bestText.transform, new Vector2(0, 0), new Vector2(40, 36), new Vector2(600, 60));
            menuWallet = UIFactory.Label(menu, "", 44, Color.white, TextAnchor.UpperRight);
            UIFactory.Place((RectTransform)menuWallet.transform, new Vector2(1, 1), new Vector2(-40, -30), new Vector2(800, 60));

            var hint = UIFactory.Label(menu, "Keyboard:  SPACE = Play    G = Garage    W/S = Thrust/Slam    A/D = Backflip/Frontflip    Esc = Pause", 28, Color.white);
            UIFactory.Place((RectTransform)hint.transform, new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(1700, 40));

            sfxBtn = UIFactory.MakeButton(menu, "SFX ON", UIFactory.Panel, () => { var s = SaveData.Current; s.sfxOn = !s.sfxOn; s.Save(); }, new Vector2(230, 80), 32);
            UIFactory.Place((RectTransform)sfxBtn.transform, new Vector2(1, 0), new Vector2(-280, 36), new Vector2(230, 80));
            musicBtn = UIFactory.MakeButton(menu, "MUSIC ON", UIFactory.Panel, () => { var s = SaveData.Current; s.musicOn = !s.musicOn; s.Save(); }, new Vector2(230, 80), 32);
            UIFactory.Place((RectTransform)musicBtn.transform, new Vector2(1, 0), new Vector2(-30, 36), new Vector2(230, 80));
        }

        // =====================================================================
        // Garage
        // =====================================================================
        void BuildGarage(Transform parent)
        {
            garage = UIFactory.Stretch("Garage", parent);
            var bg = UIFactory.Image(garage, "Bg", SpriteLibrary.Get("square"), new Color(0.08f, 0.09f, 0.16f, 0.93f));
            var brt = (RectTransform)bg.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.raycastTarget = true;

            var title = UIFactory.Label(garage, "GARAGE", 96, UIFactory.Accent, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0, 1), new Vector2(50, -30), new Vector2(800, 110));
            garageWallet = UIFactory.Label(garage, "", 48, Color.white, TextAnchor.UpperRight);
            UIFactory.Place((RectTransform)garageWallet.transform, new Vector2(1, 1), new Vector2(-50, -50), new Vector2(900, 60));

            var positions = new[] { new Vector2(-440, 120), new Vector2(440, 120), new Vector2(-440, -200), new Vector2(440, -200) };
            int i = 0;
            foreach (var def in UpgradeDatabase.All)
            {
                int idx = (int)def.id;
                var card = UIFactory.PanelBox(garage, "Card_" + def.id, new Color(0.17f, 0.18f, 0.3f, 1f));
                UIFactory.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), positions[i], new Vector2(840, 300));

                var name = UIFactory.Label(card.transform, def.title, 40, Color.white, TextAnchor.UpperLeft);
                UIFactory.Place((RectTransform)name.transform, new Vector2(0, 1), new Vector2(34, -22), new Vector2(560, 50));
                cardTier[idx] = UIFactory.Label(card.transform, "", 46, UIFactory.Accent, TextAnchor.UpperLeft);
                UIFactory.Place((RectTransform)cardTier[idx].transform, new Vector2(0, 1), new Vector2(34, -76), new Vector2(560, 56));
                cardInfo[idx] = UIFactory.Label(card.transform, "", 30, new Color(0.8f, 0.85f, 1f), TextAnchor.UpperLeft, FontStyle.Normal);
                UIFactory.Place((RectTransform)cardInfo[idx].transform, new Vector2(0, 1), new Vector2(34, -136), new Vector2(780, 40));
                cardPips[idx] = UIFactory.Label(card.transform, "", 40, UIFactory.Good, TextAnchor.LowerLeft);
                UIFactory.Place((RectTransform)cardPips[idx].transform, new Vector2(0, 0), new Vector2(34, 24), new Vector2(300, 50));

                var captured = def.id;
                cardBtn[idx] = UIFactory.MakeButton(card.transform, "UPGRADE", UIFactory.Blue, () => TryBuy(captured), new Vector2(380, 90), 34);
                UIFactory.Place((RectTransform)cardBtn[idx].transform, new Vector2(1, 0), new Vector2(-26, 26), new Vector2(380, 90));
                i++;
            }

            var gh = UIFactory.Label(garage, "Keyboard:  1-4 = Buy upgrade    SPACE = Play    Esc = Back", 30, Color.white);
            UIFactory.Place((RectTransform)gh.transform, new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(1400, 40));
            var back = UIFactory.MakeButton(garage, "BACK", new Color(0.8f, 0.3f, 0.3f), () => gm.ShowMenu(), new Vector2(320, 100), 52);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(320, 100));
        }

        public void BuyUpgrade(UpgradeId id) => TryBuy(id);

        void TryBuy(UpgradeId id)
        {
            if (SaveData.Current.TryUpgrade(id)) { AudioManager.Sfx("buy"); }
            else AudioManager.Sfx("deny");
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
                var pips = new StringBuilder();
                for (int k = 0; k <= def.MaxLevel; k++) pips.Append(k <= lv ? "■ " : "□ ");
                cardPips[idx].text = pips.ToString();

                var btn = cardBtn[idx];
                if (lv >= def.MaxLevel)
                {
                    cardInfo[idx].text = Describe(def.id, cur) + "   (MAX)";
                    UIFactory.SetButtonLabel(btn, "MAXED");
                    btn.interactable = false;
                }
                else
                {
                    var next = def.tiers[lv + 1];
                    cardInfo[idx].text = "Next: " + next.name + " - " + Describe(def.id, next);
                    UIFactory.SetButtonLabel(btn, "UPGRADE  " + next.coinCost + "c + " + next.partCost + "p");
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
            var bg = UIFactory.Image(summary, "Bg", SpriteLibrary.Get("square"), new Color(0.05f, 0.06f, 0.12f, 0.82f));
            var brt = (RectTransform)bg.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.raycastTarget = true;

            var panel = UIFactory.PanelBox(summary, "Panel", new Color(0.17f, 0.18f, 0.3f, 1f));
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(1640, 860));

            summaryTitle = UIFactory.Label(panel.transform, "WIPEOUT!", 90, UIFactory.Bad, TextAnchor.UpperCenter);
            UIFactory.Place((RectTransform)summaryTitle.transform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1400, 190));

            // polaroid style snapshot of the crash (GDD 2: snapshot of the funniest fall)
            var frame = UIFactory.PanelBox(panel.transform, "Polaroid", Color.white);
            UIFactory.Place((RectTransform)frame.transform, new Vector2(0, 0.5f), new Vector2(60, -45), new Vector2(640, 440), new Vector2(0, 0.5f));
            snapshotImg = UIFactory.Image(frame.transform, "Snapshot", SpriteLibrary.Get("turtle"), Color.white);
            UIFactory.Place((RectTransform)snapshotImg.transform, new Vector2(0.5f, 0.57f), Vector2.zero, new Vector2(588, 358));
            snapshotCaption = UIFactory.Label(frame.transform, "", 32, new Color(0.2f, 0.2f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UIFactory.Place((RectTransform)snapshotCaption.transform, new Vector2(0.5f, 0.07f), Vector2.zero, new Vector2(600, 50));
            ((Outline)snapshotCaption.GetComponent<Outline>()).enabled = false;

            summaryBody = UIFactory.Label(panel.transform, "", 44, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            summaryBody.lineSpacing = 1.25f;
            UIFactory.Place((RectTransform)summaryBody.transform, new Vector2(0, 0.5f), new Vector2(740, -45), new Vector2(840, 400), new Vector2(0, 0.5f));

            var sh = UIFactory.Label(panel.transform, "SPACE = Retry    G = Garage    Esc = Menu", 28, new Color(0.8f, 0.85f, 1f));
            UIFactory.Place((RectTransform)sh.transform, new Vector2(0.5f, 0), new Vector2(0, 175), new Vector2(1000, 36));
            var retry = UIFactory.MakeButton(panel.transform, "RETRY", new Color(0.2f, 0.7f, 0.3f), () => gm.StartRun(), new Vector2(460, 120), 60);
            UIFactory.Place((RectTransform)retry.transform, new Vector2(0.5f, 0), new Vector2(-500, 36), new Vector2(460, 120));
            var gar = UIFactory.MakeButton(panel.transform, "GARAGE", UIFactory.Blue, () => gm.OpenGarage(), new Vector2(460, 120), 60);
            UIFactory.Place((RectTransform)gar.transform, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(460, 120));
            var menuBtn = UIFactory.MakeButton(panel.transform, "MENU", UIFactory.Panel, () => gm.ShowMenu(), new Vector2(360, 120), 52);
            UIFactory.Place((RectTransform)menuBtn.transform, new Vector2(0.5f, 0), new Vector2(480, 36), new Vector2(360, 120));
        }

        // =====================================================================
        // Pause
        // =====================================================================
        void BuildPause(Transform parent)
        {
            pause = UIFactory.Stretch("Pause", parent);
            var bg = UIFactory.Image(pause, "Bg", SpriteLibrary.Get("square"), new Color(0, 0, 0, 0.6f));
            var brt = (RectTransform)bg.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.raycastTarget = true;
            var t = UIFactory.Label(pause, "PAUSED", 120, Color.white);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(900, 150));
            var resume = UIFactory.MakeButton(pause, "RESUME", new Color(0.2f, 0.7f, 0.3f), () => gm.Resume(), new Vector2(500, 130), 64);
            UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 130));
            var quit = UIFactory.MakeButton(pause, "QUIT RUN", new Color(0.8f, 0.3f, 0.3f), () => gm.QuitRun(), new Vector2(500, 110), 54);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(500, 110));
        }
    }
}
