using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TurtleBlaster
{
    /// <summary>
    /// Skins, Shop, Leaderboard screens, the Revive offer, the x2 coins button and the
    /// test-mode ad / purchase dialogs. Split from UIRoot.cs to keep each file readable.
    /// </summary>
    public partial class UIRoot
    {
        RectTransform skinsScreen, shopScreen, ranksScreen, reviveOverlay, energyOverlay, dailyRewardOverlay, adOverlay, purchaseOverlay;
        Text skinsWallet, shopWallet, shopNote, ranksRank, ranksName, ranksDist, ranksScore, ranksDate;
        Text reviveTimerText, energyOverlayTimerText, dailyStatusText, adTitle, adSub, adCountdown, purchaseText;
        Image reviveBar;
        Button adClaimBtn, doubleCoinsBtn, energyOverlayAdBtn, dailyClaimBtn;

        class SkinCard { public SkinDef skin; public Image preview; public Text price; public Button button; }
        class DailyCard { public LoginRewardDef reward; public Image bg; public Text status; }
        readonly List<SkinCard> skinCards = new List<SkinCard>();
        readonly List<DailyCard> dailyCards = new List<DailyCard>();
        readonly List<Button> productButtons = new List<Button>();

        Action<bool> reviveCallback, adCallback, purchaseCallback;
        float reviveDuration, reviveLeft, adLeft;
        int overlayClosedFrame = -1;

        /// <summary>A modal dialog (ad / purchase / revive / energy / daily) is open, or was closed this frame.</summary>
        public bool OverlayOpen =>
            adOverlay.gameObject.activeSelf || purchaseOverlay.gameObject.activeSelf ||
            reviveOverlay.gameObject.activeSelf || energyOverlay.gameObject.activeSelf ||
            (dailyRewardOverlay != null && dailyRewardOverlay.gameObject.activeSelf) ||
            overlayClosedFrame == Time.frameCount;

        void BuildExtras(Transform parent)
        {
            BuildSkins(parent);
            BuildShop(parent);
            BuildRanks(parent);
            // overlays last so they draw on top of everything
            BuildRevive(parent);
            BuildEnergyOverlay(parent);
            BuildDailyRewardOverlay(parent);
            BuildAdOverlay(parent);
            BuildPurchaseOverlay(parent);
        }

        void HideExtras()
        {
            skinsScreen.gameObject.SetActive(false);
            shopScreen.gameObject.SetActive(false);
            ranksScreen.gameObject.SetActive(false);
            if (energyOverlay != null) energyOverlay.gameObject.SetActive(false);
            if (dailyRewardOverlay != null) dailyRewardOverlay.gameObject.SetActive(false);
        }

        static RectTransform FullScreen(Transform parent, string name, Color dim)
        {
            var rt = UIFactory.Stretch(name, parent);
            var bg = UIFactory.Image(rt, "Bg", SpriteLibrary.Get("square"), dim);
            var brt = (RectTransform)bg.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            bg.raycastTarget = true;   // block clicks to whatever is behind
            return rt;
        }

        static Text Title(Transform parent, string text, int size = 96)
        {
            var t = UIFactory.Label(parent, text, size, UIFactory.Accent, TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0, 1), new Vector2(50, -30), new Vector2(1200, 110));
            return t;
        }

        Text Wallet(Transform parent)
        {
            var t = UIFactory.Label(parent, "", 48, Color.white, TextAnchor.UpperRight);
            UIFactory.Place((RectTransform)t.transform, new Vector2(1, 1), new Vector2(-50, -50), new Vector2(900, 60));
            return t;
        }

        void BackButton(Transform parent)
        {
            var back = UIFactory.MakeButton(parent, "BACK", new Color(0.8f, 0.3f, 0.3f), () => gm.ShowMenu(), new Vector2(320, 100), 52);
            UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(320, 100));
        }

        // =====================================================================
        // Skins
        // =====================================================================
        void BuildSkins(Transform parent)
        {
            skinsScreen = FullScreen(parent, "Skins", new Color(0.08f, 0.09f, 0.16f, 0.95f));
            Title(skinsScreen, "SKINS");
            skinsWallet = Wallet(skinsScreen);

            var pos = new[]
            {
                new Vector2(-660, 175), new Vector2(-220, 175), new Vector2(220, 175), new Vector2(660, 175),
                new Vector2(-440, -195), new Vector2(0, -195), new Vector2(440, -195),
            };
            var skins = SkinDatabase.All;
            for (int i = 0; i < skins.Count && i < pos.Length; i++)
            {
                var skin = skins[i];
                var card = UIFactory.PanelBox(skinsScreen, "Skin_" + skin.id, skin.IsPremium ? new Color(0.35f, 0.22f, 0.45f) : new Color(0.17f, 0.18f, 0.3f));
                UIFactory.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), pos[i], new Vector2(400, 360));

                var preview = UIFactory.Image(card.transform, "Preview", SpriteLibrary.GetTurtle(skin.id), Color.white);
                UIFactory.Place((RectTransform)preview.transform, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(166, 128), new Vector2(0.5f, 1));

                var name = UIFactory.Label(card.transform, skin.name, 34, Color.white);
                UIFactory.Place((RectTransform)name.transform, new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(380, 40));

                var perk = UIFactory.Label(card.transform, skin.perkDesc ?? "", 24, new Color(0.55f, 0.95f, 0.7f));
                UIFactory.Place((RectTransform)perk.transform, new Vector2(0.5f, 0), new Vector2(0, 134), new Vector2(380, 32));

                var price = UIFactory.Label(card.transform, "", 28, UIFactory.Accent);
                UIFactory.Place((RectTransform)price.transform, new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(380, 36));

                int index = i;
                var btn = UIFactory.MakeButton(card.transform, "BUY", UIFactory.Blue, () => SkinAction(index), new Vector2(340, 76), 32);
                UIFactory.Place((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(340, 76));

                skinCards.Add(new SkinCard { skin = skin, preview = preview, price = price, button = btn });
            }

            BackButton(skinsScreen);
        }

        public void ShowSkins()
        {
            HideAll();
            skinsScreen.gameObject.SetActive(true);
            RefreshSkins();
        }

        void RefreshSkins()
        {
            var save = SaveData.Current;
            foreach (var c in skinCards)
            {
                bool owned = save.OwnsSkin(c.skin.id);
                bool equipped = save.selectedSkin == c.skin.id;
                var img = (Image)c.button.targetGraphic;
                c.button.interactable = !equipped;
                if (equipped)
                {
                    c.price.text = "OWNED";
                    UIFactory.SetButtonLabel(c.button, "EQUIPPED");
                    img.color = UIFactory.Good;
                }
                else if (owned)
                {
                    c.price.text = "OWNED";
                    UIFactory.SetButtonLabel(c.button, "EQUIP");
                    img.color = UIFactory.Blue;
                }
                else if (c.skin.IsPremium)
                {
                    string p = Services.Purchases.GetPriceString(c.skin.productId);
                    c.price.text = "PREMIUM  " + p;
                    UIFactory.SetButtonLabel(c.button, Services.Purchases.IsAvailable ? "BUY " + p : "UNAVAILABLE");
                    c.button.interactable = Services.Purchases.IsAvailable;
                    img.color = new Color(0.85f, 0.55f, 0.1f);
                }
                else
                {
                    c.price.text = c.skin.coinPrice + " coins";
                    UIFactory.SetButtonLabel(c.button, "BUY " + c.skin.coinPrice + "c");
                    img.color = save.coins >= c.skin.coinPrice ? UIFactory.Blue : new Color(0.35f, 0.35f, 0.45f);
                }
            }
        }

        /// <summary>Buy (coins or real money) or equip the skin at this card index.</summary>
        public void SkinAction(int index)
        {
            if (index < 0 || index >= skinCards.Count || OverlayOpen) return;
            var skin = skinCards[index].skin;
            var save = SaveData.Current;
            if (save.OwnsSkin(skin.id))
            {
                save.EquipSkin(skin.id);
                AudioManager.Sfx("click");
            }
            else if (skin.IsPremium)
            {
                Store.Buy(skin.productId, ok =>
                {
                    if (ok) { save.EquipSkin(skin.id); AudioManager.Sfx("buy"); Popup("UNLOCKED " + skin.name.ToUpper() + "!", UIFactory.Accent); }
                    else AudioManager.Sfx("deny");
                    RefreshSkins();
                });
                return;
            }
            else AudioManager.Sfx(save.TryBuySkin(skin.id) ? "buy" : "deny");
            RefreshSkins();
        }

        // =====================================================================
        // Shop (real-money coin packs + premium skin)
        // =====================================================================
        void BuildShop(Transform parent)
        {
            shopScreen = FullScreen(parent, "Shop", new Color(0.08f, 0.09f, 0.16f, 0.95f));
            Title(shopScreen, "SHOP");
            shopWallet = Wallet(shopScreen);

            var products = ProductCatalog.All;
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                float x = -660 + i * 440;
                var card = UIFactory.PanelBox(shopScreen, "Product_" + p.id, p.kind == ProductKind.Skin ? new Color(0.35f, 0.22f, 0.45f) : new Color(0.17f, 0.18f, 0.3f));
                UIFactory.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(x, 40), new Vector2(400, 440));

                var icon = UIFactory.Image(card.transform, "Icon", p.kind == ProductKind.Skin ? SpriteLibrary.GetTurtle(p.skinId) : SpriteLibrary.Get("coin"), Color.white);
                UIFactory.Place((RectTransform)icon.transform, new Vector2(0.5f, 1), new Vector2(0, -30), p.kind == ProductKind.Skin ? new Vector2(208, 160) : new Vector2(150, 150), new Vector2(0.5f, 1));

                var title = UIFactory.Label(card.transform, p.title, 36, Color.white);
                UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(380, 44));
                var what = UIFactory.Label(card.transform, p.kind == ProductKind.Coins ? "+" + p.coins.ToString("N0") + " coins" : "Exclusive animated skin", 32, UIFactory.Accent);
                UIFactory.Place((RectTransform)what.transform, new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(380, 40));

                int index = i;
                var btn = UIFactory.MakeButton(card.transform, "BUY", new Color(0.2f, 0.7f, 0.3f), () => ShopAction(index), new Vector2(340, 90), 40);
                UIFactory.Place((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(340, 90));
                productButtons.Add(btn);
            }

            shopNote = UIFactory.Label(shopScreen, "", 28, new Color(1f, 0.8f, 0.5f));
            UIFactory.Place((RectTransform)shopNote.transform, new Vector2(0.5f, 0), new Vector2(0, 205), new Vector2(1700, 40));

            var restore = UIFactory.MakeButton(shopScreen, "RESTORE PURCHASES", UIFactory.Panel, RestorePurchases, new Vector2(420, 70), 28);
            UIFactory.Place((RectTransform)restore.transform, new Vector2(1, 0), new Vector2(-50, 55), new Vector2(420, 70));

            BackButton(shopScreen);
        }

        public void ShowShop()
        {
            HideAll();
            shopScreen.gameObject.SetActive(true);
            RefreshShop();
        }

        void RefreshShop()
        {
            var products = ProductCatalog.All;
            bool available = Services.Purchases.IsAvailable;
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                var b = productButtons[i];
                bool ownedSkin = p.kind == ProductKind.Skin && SaveData.Current.OwnsSkin(p.skinId);
                b.interactable = available && !ownedSkin;
                UIFactory.SetButtonLabel(b, ownedSkin ? "OWNED" : available ? Services.Purchases.GetPriceString(p.id) : "UNAVAILABLE");
            }
            shopNote.text = !available
                ? "Store is not configured in this build."
                : Services.Purchases is MockPurchaseService
                    ? "TEST STORE: purchases here are simulated - no real money is charged."
                    : "";
        }

        public void ShopAction(int index)
        {
            var products = ProductCatalog.All;
            if (index < 0 || index >= products.Count || OverlayOpen) return;
            var p = products[index];
            Store.Buy(p.id, ok =>
            {
                if (ok)
                {
                    AudioManager.Sfx("buy");
                    Popup(p.kind == ProductKind.Coins ? "+" + p.coins.ToString("N0") + " COINS!" : "SKIN UNLOCKED!", UIFactory.Accent);
                }
                else AudioManager.Sfx("deny");
                RefreshShop();
            });
        }

        void RestorePurchases()
        {
            Services.Purchases.RestorePurchases(ids =>
            {
                foreach (var id in ids)
                {
                    var p = ProductCatalog.Get(id);
                    if (p != null && p.kind == ProductKind.Skin) SaveData.Current.GrantSkin(p.skinId);
                }
                SaveData.Current.Save();
                Popup(ids.Count > 0 ? "PURCHASES RESTORED" : "NOTHING TO RESTORE", Color.white, 52);
                RefreshShop();
            });
        }

        // =====================================================================
        // Leaderboard (best distance)
        // =====================================================================
        void BuildRanks(Transform parent)
        {
            ranksScreen = FullScreen(parent, "Leaderboard", new Color(0.08f, 0.09f, 0.16f, 0.95f));
            Title(ranksScreen, "LEADERBOARD");
            var sub = UIFactory.Label(ranksScreen, "Best distance - Global Leaderboard", 34, new Color(0.8f, 0.85f, 1f), TextAnchor.UpperLeft);
            UIFactory.Place((RectTransform)sub.transform, new Vector2(0, 1), new Vector2(54, -140), new Vector2(1200, 44));

            var panel = UIFactory.PanelBox(ranksScreen, "Table", new Color(0.17f, 0.18f, 0.3f));
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1500, 700));

            Text Col(string name, string header, float x, float w, TextAnchor a)
            {
                var h = UIFactory.Label(panel.transform, header, 34, UIFactory.Accent, a, FontStyle.Bold, name + "Header");
                UIFactory.Place((RectTransform)h.transform, new Vector2(0, 1), new Vector2(x, -35), new Vector2(w, 44), new Vector2(0, 1));
                var t = UIFactory.Label(panel.transform, "", 40, Color.white, a, FontStyle.Bold, name);
                t.lineSpacing = 1.15f;
                UIFactory.Place((RectTransform)t.transform, new Vector2(0, 1), new Vector2(x, -95), new Vector2(w, 590), new Vector2(0, 1));
                return t;
            }
            ranksRank = Col("Rank", "RANK", 60, 120, TextAnchor.UpperLeft);
            ranksName = Col("Name", "NAME", 230, 480, TextAnchor.UpperLeft);
            ranksDist = Col("Dist", "DISTANCE", 640, 260, TextAnchor.UpperRight);
            ranksScore = Col("Score", "SCORE", 930, 220, TextAnchor.UpperRight);
            ranksDate = Col("Date", "DATE", 1190, 260, TextAnchor.UpperLeft);
            BackButton(ranksScreen);
        }

        public void ShowLeaderboard()
        {
            HideAll();
            ranksScreen.gameObject.SetActive(true);
            Services.Leaderboard.GetTop(10, entries =>
            {
                string me = SaveData.Current.playerName;
                var r = new System.Text.StringBuilder(); var n = new System.Text.StringBuilder();
                var d = new System.Text.StringBuilder(); var s = new System.Text.StringBuilder(); var t = new System.Text.StringBuilder();
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    string open = e.name == me ? "<color=#ffd23f>" : "", close = e.name == me ? "</color>" : "";
                    r.AppendLine(open + "#" + (i + 1) + close);
                    n.AppendLine(open + e.name + close);
                    d.AppendLine(open + e.distance + " m" + close);
                    s.AppendLine(open + e.score + close);
                    t.AppendLine(open + e.date + close);
                }
                if (entries.Count == 0) n.AppendLine("No runs yet - go ride!");
                ranksRank.text = r.ToString(); ranksName.text = n.ToString(); ranksDist.text = d.ToString();
                ranksScore.text = s.ToString(); ranksDate.text = t.ToString();
            });
        }

        // =====================================================================
        // Summary extras: x2 coins for watching an ad
        // =====================================================================
        void BuildSummaryExtras(RectTransform panel)
        {
            doubleCoinsBtn = UIFactory.MakeButton(panel, "WATCH AD: x2 COINS", new Color(0.85f, 0.55f, 0.1f), () => gm.WatchAdForDoubleCoins(), new Vector2(560, 84), 34);
            UIFactory.Place((RectTransform)doubleCoinsBtn.transform, new Vector2(0, 0), new Vector2(740, 172), new Vector2(560, 84), new Vector2(0, 0));
        }

        void RefreshSummaryExtras(RunStats s)
        {
            bool can = gm.CanDoubleCoins;
            doubleCoinsBtn.gameObject.SetActive(can || s.coinsDoubled);
            doubleCoinsBtn.interactable = can;
            UIFactory.SetButtonLabel(doubleCoinsBtn, s.coinsDoubled ? "COINS DOUBLED!" : "WATCH AD: x2 COINS (+" + s.coins + ")");
            RefreshEnergyUI();
        }

        // =====================================================================
        // Energy overlay & live timers
        // =====================================================================
        void BuildEnergyOverlay(Transform parent)
        {
            energyOverlay = FullScreen(parent, "OutOfEnergy", new Color(0, 0, 0, 0.65f));
            var panel = UIFactory.PanelBox(energyOverlay, "Panel", new Color(0.17f, 0.18f, 0.3f));
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020, 560));

            var icon = UIFactory.Image(panel.transform, "Icon", SpriteLibrary.Get("energy"), Color.white);
            UIFactory.Place((RectTransform)icon.transform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(60, 78), new Vector2(0.5f, 1));

            var t = UIFactory.Label(panel.transform, "OUT OF ENERGY!", 84, UIFactory.Accent);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(960, 100), new Vector2(0.5f, 1));

            var sub = UIFactory.Label(panel.transform, "Each run costs 1 energy.\nRefills 1 every 15 minutes (max 5).", 32, Color.white);
            UIFactory.Place((RectTransform)sub.transform, new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(940, 86), new Vector2(0.5f, 1));

            energyOverlayTimerText = UIFactory.Label(panel.transform, "", 30, new Color(0.75f, 0.9f, 1f));
            UIFactory.Place((RectTransform)energyOverlayTimerText.transform, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(940, 44), new Vector2(0.5f, 1));

            energyOverlayAdBtn = UIFactory.MakeButton(panel.transform, "WATCH AD (+1 ENERGY)", new Color(0.85f, 0.55f, 0.1f), () =>
            {
                gm.WatchAdForEnergy(ok =>
                {
                    if (ok && SaveData.Current.energy > 0) HideOutOfEnergy();
                });
            }, new Vector2(520, 108), 36);
            UIFactory.Place((RectTransform)energyOverlayAdBtn.transform, new Vector2(0.5f, 0), new Vector2(-175, 42), new Vector2(520, 108));

            var close = UIFactory.MakeButton(panel.transform, "CLOSE", UIFactory.Panel, HideOutOfEnergy, new Vector2(290, 108), 40);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(265, 42), new Vector2(290, 108));

            energyOverlay.gameObject.SetActive(false);
        }

        public void ShowOutOfEnergy()
        {
            if (energyOverlay == null) return;
            RefreshEnergyUI();
            energyOverlay.SetAsLastSibling();
            energyOverlay.gameObject.SetActive(true);
        }

        public void HideOutOfEnergy()
        {
            if (energyOverlay == null || !energyOverlay.gameObject.activeSelf) return;
            energyOverlay.gameObject.SetActive(false);
            overlayClosedFrame = Time.frameCount;
        }

        static string FormatCountdown(TimeSpan ts)
        {
            if (ts <= TimeSpan.Zero) return "00:00";
            int totalSec = Mathf.CeilToInt((float)ts.TotalSeconds);
            int mins = totalSec / 60;
            int secs = totalSec % 60;
            return mins.ToString("00") + ":" + secs.ToString("00");
        }

        public void RefreshEnergyUI()
        {
            var save = SaveData.Current;
            int e = save.energy;
            bool full = e >= SaveData.MaxEnergy;
            bool canAd = gm != null && gm.CanWatchEnergyAd;
            string energyStr = "ENERGY " + e + "/" + SaveData.MaxEnergy;
            string nextStr = full ? "FULL" : "Next +1 in " + FormatCountdown(save.GetTimeUntilNextEnergy());
            string fullStr = full ? "FULL" : "Full in " + FormatCountdown(save.GetTimeUntilFullEnergy());
            Color timerCol = e == 0 ? UIFactory.Bad : (full ? UIFactory.Good : new Color(0.8f, 0.9f, 1f));
            string adLabel = full ? "ENERGY FULL" : (Services.Ads.IsRewardedReady ? "+1 ENERGY (AD)" : "AD LOADING...");
            Color adCol = canAd ? new Color(0.85f, 0.55f, 0.1f) : new Color(0.35f, 0.35f, 0.45f);

            if (menuEnergyText != null) menuEnergyText.text = energyStr;
            if (menuEnergyTimerText != null) { menuEnergyTimerText.text = nextStr; menuEnergyTimerText.color = timerCol; }
            if (menuEnergyAdBtn != null)
            {
                menuEnergyAdBtn.interactable = canAd;
                menuEnergyAdBtn.targetGraphic.color = adCol;
                UIFactory.SetButtonLabel(menuEnergyAdBtn, adLabel);
            }

            if (summaryEnergyText != null) summaryEnergyText.text = energyStr;
            if (summaryEnergyTimerText != null) { summaryEnergyTimerText.text = nextStr; summaryEnergyTimerText.color = timerCol; }
            if (summaryEnergyAdBtn != null)
            {
                summaryEnergyAdBtn.interactable = canAd;
                summaryEnergyAdBtn.targetGraphic.color = adCol;
                UIFactory.SetButtonLabel(summaryEnergyAdBtn, adLabel);
            }

            if (pauseEnergyText != null)
            {
                pauseEnergyText.text = energyStr + (full ? "  (FULL)" : "  -  " + nextStr);
                pauseEnergyText.color = e == 0 ? UIFactory.Bad : UIFactory.Accent;
            }

            if (energyOverlayTimerText != null)
                energyOverlayTimerText.text = full ? "ENERGY FULL!" : (nextStr + "   |   " + fullStr);
            if (energyOverlayAdBtn != null)
            {
                energyOverlayAdBtn.interactable = canAd;
                energyOverlayAdBtn.targetGraphic.color = adCol;
                UIFactory.SetButtonLabel(energyOverlayAdBtn, Services.Ads.IsRewardedReady ? "WATCH AD (+1 ENERGY)" : "AD NOT READY YET");
            }
        }

        // =====================================================================
        // 7-Day Daily Login Rewards
        // =====================================================================
        void BuildDailyRewardOverlay(Transform parent)
        {
            dailyRewardOverlay = FullScreen(parent, "DailyRewards", new Color(0, 0, 0, 0.72f));
            var panel = UIFactory.PanelBox(dailyRewardOverlay, "Panel", new Color(0.14f, 0.16f, 0.28f));
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 760));

            var title = UIFactory.Label(panel.transform, "7-DAY LOGIN REWARDS", 60, UIFactory.Accent);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1400, 72), new Vector2(0.5f, 1));
            dailyStatusText = UIFactory.Label(panel.transform, "", 28, new Color(0.82f, 0.9f, 1f));
            UIFactory.Place((RectTransform)dailyStatusText.transform, new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(1400, 38), new Vector2(0.5f, 1));

            var pos = new[]
            {
                new Vector2(-540, 105), new Vector2(-180, 105), new Vector2(180, 105), new Vector2(540, 105),
                new Vector2(-430, -110), new Vector2(-70, -110), new Vector2(365, -110),
            };
            var rewards = SaveData.DailyRewards;
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];
                float cardW = r.day == 7 ? 480f : 330f;
                var card = UIFactory.PanelBox(panel.transform, "DayCard_" + r.day, new Color(0.20f, 0.22f, 0.36f));
                UIFactory.Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), pos[i], new Vector2(cardW, 195));

                var dayTitle = UIFactory.Label(card.transform, r.day == 7 ? "DAY 7 - GRAND PRIZE" : "DAY " + r.day, 26, UIFactory.Accent);
                UIFactory.Place((RectTransform)dayTitle.transform, new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(cardW - 20, 32), new Vector2(0.5f, 1));

                var icon = UIFactory.Image(card.transform, "Icon", SpriteLibrary.Get(r.iconSprite), Color.white);
                UIFactory.Place((RectTransform)icon.transform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(44, 44), new Vector2(0.5f, 1));

                var rewardLbl = UIFactory.Label(card.transform, r.label, r.day == 7 ? 21 : 23, Color.white, TextAnchor.MiddleCenter);
                UIFactory.Place((RectTransform)rewardLbl.transform, new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(cardW - 24, 62), new Vector2(0.5f, 0));

                var status = UIFactory.Label(card.transform, "", 20, UIFactory.Good);
                UIFactory.Place((RectTransform)status.transform, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(cardW - 24, 28), new Vector2(0.5f, 0));

                dailyCards.Add(new DailyCard { reward = r, bg = card, status = status });
            }

            dailyClaimBtn = UIFactory.MakeButton(panel.transform, "CLAIM", new Color(0.2f, 0.7f, 0.3f), () => gm.ClaimDailyLoginReward(out _), new Vector2(400, 96), 42);
            UIFactory.Place((RectTransform)dailyClaimBtn.transform, new Vector2(0.5f, 0), new Vector2(-190, 36), new Vector2(400, 96));
            var close = UIFactory.MakeButton(panel.transform, "CLOSE", UIFactory.Panel, HideDailyRewards, new Vector2(300, 96), 40);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(250, 36), new Vector2(300, 96));

            dailyRewardOverlay.gameObject.SetActive(false);
        }

        public void ShowDailyRewards()
        {
            if (dailyRewardOverlay == null) return;
            RefreshDailyRewardUI();
            dailyRewardOverlay.SetAsLastSibling();
            dailyRewardOverlay.gameObject.SetActive(true);
        }

        public void HideDailyRewards()
        {
            if (dailyRewardOverlay == null || !dailyRewardOverlay.gameObject.activeSelf) return;
            dailyRewardOverlay.gameObject.SetActive(false);
            overlayClosedFrame = Time.frameCount;
        }

        public void RefreshDailyRewardUI()
        {
            SaveData.Current.GetDailyLoginState(out int claimedCount, out int nextDayIndex, out bool canClaimToday);

            if (dailyRewardBtn != null)
            {
                var img = (Image)dailyRewardBtn.targetGraphic;
                if (canClaimToday)
                {
                    UIFactory.SetButtonLabel(dailyRewardBtn, "DAILY REWARD (DAY " + (nextDayIndex + 1) + "!)");
                    if (img != null) img.color = new Color(0.2f, 0.72f, 0.32f);
                }
                else
                {
                    UIFactory.SetButtonLabel(dailyRewardBtn, "DAILY REWARDS (DONE)");
                    if (img != null) img.color = UIFactory.Panel;
                }
            }

            if (dailyStatusText != null)
            {
                dailyStatusText.text = canClaimToday
                    ? "Day " + (nextDayIndex + 1) + " reward ready to claim!"
                    : "Day " + claimedCount + "/7 claimed today. Come back tomorrow!";
                dailyStatusText.color = canClaimToday ? UIFactory.Good : new Color(0.82f, 0.9f, 1f);
            }

            if (dailyClaimBtn != null)
            {
                dailyClaimBtn.interactable = canClaimToday;
                dailyClaimBtn.targetGraphic.color = canClaimToday ? new Color(0.2f, 0.72f, 0.32f) : new Color(0.35f, 0.35f, 0.45f);
                UIFactory.SetButtonLabel(dailyClaimBtn, canClaimToday ? "CLAIM DAY " + (nextDayIndex + 1) : "CLAIMED TODAY");
            }

            for (int i = 0; i < dailyCards.Count; i++)
            {
                var c = dailyCards[i];
                bool claimed = i < claimedCount;
                bool isToday = canClaimToday && i == nextDayIndex;
                if (claimed)
                {
                    c.bg.color = new Color(0.16f, 0.36f, 0.24f);
                    c.status.text = "CLAIMED";
                    c.status.color = UIFactory.Good;
                }
                else if (isToday)
                {
                    c.bg.color = new Color(0.32f, 0.30f, 0.56f);
                    c.status.text = "TODAY!";
                    c.status.color = UIFactory.Accent;
                }
                else
                {
                    c.bg.color = c.reward.day == 7 ? new Color(0.30f, 0.20f, 0.40f) : new Color(0.19f, 0.21f, 0.34f);
                    c.status.text = "DAY " + c.reward.day;
                    c.status.color = new Color(0.7f, 0.78f, 0.92f);
                }
            }
        }

        // =====================================================================
        // Revive offer
        // =====================================================================
        void BuildRevive(Transform parent)
        {
            reviveOverlay = FullScreen(parent, "Revive", new Color(0, 0, 0, 0.55f));
            var panel = UIFactory.PanelBox(reviveOverlay, "Panel", new Color(0.17f, 0.18f, 0.3f));
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 560));
            var t = UIFactory.Label(panel.transform, "REVIVE?", 110, UIFactory.Accent);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(900, 130), new Vector2(0.5f, 1));
            var sub = UIFactory.Label(panel.transform, "Watch a short ad and keep riding from here", 36, Color.white);
            UIFactory.Place((RectTransform)sub.transform, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(900, 50), new Vector2(0.5f, 1));
            reviveBar = UIFactory.Bar(panel.transform, "Timer", UIFactory.Accent, new Vector2(760, 34), out var barRoot);
            UIFactory.Place(barRoot, new Vector2(0.5f, 1), new Vector2(0, -260), new Vector2(760, 34), new Vector2(0.5f, 1));
            reviveTimerText = UIFactory.Label(panel.transform, "", 30, new Color(0.8f, 0.85f, 1f));
            UIFactory.Place((RectTransform)reviveTimerText.transform, new Vector2(0.5f, 1), new Vector2(0, -305), new Vector2(900, 40), new Vector2(0.5f, 1));

            var yes = UIFactory.MakeButton(panel.transform, "WATCH AD", new Color(0.2f, 0.7f, 0.3f), () => ReviveChoose(true), new Vector2(400, 110), 50);
            UIFactory.Place((RectTransform)yes.transform, new Vector2(0.5f, 0), new Vector2(-215, 40), new Vector2(400, 110));
            var no = UIFactory.MakeButton(panel.transform, "NO THANKS", UIFactory.Panel, () => ReviveChoose(false), new Vector2(360, 110), 44);
            UIFactory.Place((RectTransform)no.transform, new Vector2(0.5f, 0), new Vector2(215, 40), new Vector2(360, 110));
            reviveOverlay.gameObject.SetActive(false);
        }

        /// <summary>Offers a revive for a few seconds. choose(true) = player wants to watch the ad.</summary>
        public void ShowRevive(float seconds, Action<bool> choose)
        {
            reviveCallback = choose;
            reviveDuration = reviveLeft = seconds;
            reviveOverlay.SetAsLastSibling();
            reviveOverlay.gameObject.SetActive(true);
        }

        public void ReviveChoose(bool accept)
        {
            if (!reviveOverlay.gameObject.activeSelf) return;
            reviveOverlay.gameObject.SetActive(false);
            overlayClosedFrame = Time.frameCount;
            var cb = reviveCallback; reviveCallback = null;
            cb?.Invoke(accept);
        }

        // =====================================================================
        // Test-mode rewarded ad & purchase dialogs (Mock services)
        // =====================================================================
        void BuildAdOverlay(Transform parent)
        {
            adOverlay = FullScreen(parent, "MockAd", new Color(0.02f, 0.02f, 0.05f, 0.96f));
            adTitle = UIFactory.Label(adOverlay, "TEST REWARDED AD", 120, Color.white);
            UIFactory.Place((RectTransform)adTitle.transform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(1500, 170));
            adSub = UIFactory.Label(adOverlay, "Rewarded ad placeholder - replace MockAdService with a real ad SDK", 34, new Color(0.8f, 0.85f, 1f));
            UIFactory.Place((RectTransform)adSub.transform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(1600, 50));
            adCountdown = UIFactory.Label(adOverlay, "", 56, UIFactory.Accent);
            UIFactory.Place((RectTransform)adCountdown.transform, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(1200, 80));

            adClaimBtn = UIFactory.MakeButton(adOverlay, "CLAIM REWARD", new Color(0.2f, 0.7f, 0.3f), () => CloseAd(true), new Vector2(540, 120), 48);
            UIFactory.Place((RectTransform)adClaimBtn.transform, new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(540, 120));
            var skip = UIFactory.MakeButton(adOverlay, "SKIP (no reward)", UIFactory.Panel, () => CloseAd(false), new Vector2(360, 70), 28);
            UIFactory.Place((RectTransform)skip.transform, new Vector2(1, 1), new Vector2(-40, -40), new Vector2(360, 70));
            adOverlay.gameObject.SetActive(false);
        }

        public void ShowMockAd(string placement, Action<bool> onComplete)
        {
            adCallback = onComplete;
            adLeft = 3f;
            adClaimBtn.interactable = false;
            if (adTitle != null) adTitle.text = "TEST REWARDED AD";
            if (adSub != null) adSub.text = "Rewarded ad placeholder - replace MockAdService with a real ad SDK";
            UIFactory.SetButtonLabel(adClaimBtn, placement == "energy_refill" ? "CLAIM (+1 ENERGY)" : "CLAIM REWARD");
            adOverlay.SetAsLastSibling();
            adOverlay.gameObject.SetActive(true);
        }

        void CloseAd(bool rewarded)
        {
            if (!adOverlay.gameObject.activeSelf) return;
            if (rewarded && adLeft > 0f) return;
            adOverlay.gameObject.SetActive(false);
            overlayClosedFrame = Time.frameCount;
            var cb = adCallback; adCallback = null;
            cb?.Invoke(rewarded);
        }

        void BuildPurchaseOverlay(Transform parent)
        {
            purchaseOverlay = FullScreen(parent, "MockPurchase", new Color(0, 0, 0, 0.7f));
            var panel = UIFactory.PanelBox(purchaseOverlay, "Panel", new Color(0.95f, 0.95f, 0.97f));
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 520));
            var t = UIFactory.Label(panel.transform, "TEST PURCHASE", 64, new Color(0.15f, 0.15f, 0.25f));
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(900, 90), new Vector2(0.5f, 1));
            purchaseText = UIFactory.Label(panel.transform, "", 36, new Color(0.15f, 0.15f, 0.25f), TextAnchor.MiddleCenter, FontStyle.Normal);
            purchaseText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place((RectTransform)purchaseText.transform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(880, 180));
            purchaseText.GetComponent<Outline>().enabled = false;

            var buy = UIFactory.MakeButton(panel.transform, "BUY (TEST)", new Color(0.2f, 0.7f, 0.3f), () => ClosePurchase(true), new Vector2(380, 100), 44);
            UIFactory.Place((RectTransform)buy.transform, new Vector2(0.5f, 0), new Vector2(-210, 40), new Vector2(380, 100));
            var cancel = UIFactory.MakeButton(panel.transform, "CANCEL", new Color(0.8f, 0.3f, 0.3f), () => ClosePurchase(false), new Vector2(340, 100), 44);
            UIFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0), new Vector2(210, 40), new Vector2(340, 100));
            purchaseOverlay.gameObject.SetActive(false);
        }

        public void ShowMockPurchase(string title, string price, Action<bool> onResult)
        {
            purchaseCallback = onResult;
            purchaseText.text = "Buy " + title + " for " + price + "?\nThis is a simulated store: no money is charged.";
            purchaseOverlay.SetAsLastSibling();
            purchaseOverlay.gameObject.SetActive(true);
        }

        void ClosePurchase(bool confirmed)
        {
            if (!purchaseOverlay.gameObject.activeSelf) return;
            purchaseOverlay.gameObject.SetActive(false);
            overlayClosedFrame = Time.frameCount;
            var cb = purchaseCallback; purchaseCallback = null;
            cb?.Invoke(confirmed);
        }

        // =====================================================================
        // Per-frame: timers, animated previews
        // =====================================================================
        void UpdateExtras()
        {
            float dt = Time.unscaledDeltaTime;

            if (adOverlay.gameObject.activeSelf)
            {
                adLeft -= dt;
                adClaimBtn.interactable = adLeft <= 0f;
                adCountdown.text = adLeft > 0f ? "Reward in " + Mathf.CeilToInt(adLeft) + "..." : "Reward ready!";
            }
            else if (reviveOverlay.gameObject.activeSelf)
            {
                reviveLeft -= dt;
                reviveBar.fillAmount = Mathf.Clamp01(reviveLeft / reviveDuration);
                reviveTimerText.text = "Offer ends in " + Mathf.CeilToInt(Mathf.Max(0f, reviveLeft)) + "s";
                if (reviveLeft <= 0f) ReviveChoose(false);
            }

            if (skinsScreen.gameObject.activeSelf)
            {
                foreach (var c in skinCards)
                    if (c.skin.rainbow) c.preview.color = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.35f, 1f), 0.35f, 1f);
            }
        }

        void RefreshExtrasWallet(string wallet)
        {
            if (skinsWallet != null) skinsWallet.text = wallet;
            if (shopWallet != null) shopWallet.text = wallet;
            if (skinsScreen != null && skinsScreen.gameObject.activeSelf) RefreshSkins();
        }
    }
}
