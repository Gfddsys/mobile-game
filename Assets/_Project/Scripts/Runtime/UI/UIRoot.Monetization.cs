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
        RectTransform skinsScreen, shopScreen, ranksScreen, reviveOverlay, energyOverlay, dailyRewardOverlay, adOverlay, purchaseOverlay, quitOverlay;
        UIFactory.WalletDisplay skinsWallet, shopWallet;
        Text shopNote, ranksRank, ranksName, ranksDist, ranksScore, ranksDate;
        Text reviveTimerText, energyOverlayTimerText, dailyStatusText, adTitle, adSub, adCountdown, purchaseText;
        Image reviveBar;
        Button adClaimBtn, doubleCoinsBtn, energyOverlayAdBtn, dailyClaimBtn;

        class SkinCard { public SkinDef skin; public RectTransform cardRt; public Image accent; public Image preview; public Image priceIcon; public Text price; public Button button; }
        class DailyCard { public LoginRewardDef reward; public RectTransform cardRt; public Image accent; public Image bg; public Text status; }
        readonly List<SkinCard> skinCards = new List<SkinCard>();
        readonly List<DailyCard> dailyCards = new List<DailyCard>();
        readonly List<RectTransform> productCards = new List<RectTransform>();
        readonly List<Button> productButtons = new List<Button>();

        Action<bool> reviveCallback, adCallback, purchaseCallback;
        float reviveDuration, reviveLeft, adLeft;
        int overlayClosedFrame = -1;

        /// <summary>A modal dialog (ad / purchase / revive / energy / daily / quit) is open, or was closed this frame.</summary>
        public bool OverlayOpen =>
            adOverlay.gameObject.activeSelf || purchaseOverlay.gameObject.activeSelf ||
            reviveOverlay.gameObject.activeSelf || energyOverlay.gameObject.activeSelf ||
            (dailyRewardOverlay != null && dailyRewardOverlay.gameObject.activeSelf) ||
            (quitOverlay != null && quitOverlay.gameObject.activeSelf) ||
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
            BuildQuitConfirmOverlay(parent);
        }

        void HideExtras()
        {
            skinsScreen.gameObject.SetActive(false);
            shopScreen.gameObject.SetActive(false);
            ranksScreen.gameObject.SetActive(false);
            if (energyOverlay != null) energyOverlay.gameObject.SetActive(false);
            if (dailyRewardOverlay != null) dailyRewardOverlay.gameObject.SetActive(false);
            if (quitOverlay != null) quitOverlay.gameObject.SetActive(false);
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

        // =====================================================================
        // Skins
        // =====================================================================
        void BuildSkins(Transform parent)
        {
            skinsScreen = FullScreen(parent, "Skins", new Color(0.06f, 0.08f, 0.16f, 0.45f));
            UIFactory.HeaderBar(skinsScreen, "SKINS", () => gm.ShowMenu(), out skinsWallet);

            var skinsMotion = UIPanelMotion.Attach(skinsScreen, null, 0.16f);
            var pos = new[]
            {
                new Vector2(-675, 135), new Vector2(-225, 135), new Vector2(225, 135), new Vector2(675, 135),
                new Vector2(-450, -265), new Vector2(0, -265), new Vector2(450, -265),
            };
            var skins = SkinDatabase.All;
            for (int i = 0; i < skins.Count && i < pos.Length; i++)
            {
                var skin = skins[i];
                var card = UIFactory.PanelBox(skinsScreen, "Skin_" + skin.id, skin.IsPremium ? new Color(0.24f, 0.16f, 0.38f) : UIFactory.CardBg);
                var cardRt = (RectTransform)card.transform;
                UIFactory.Place(cardRt, new Vector2(0.5f, 0.5f), pos[i], new Vector2(410, 375));
                var acc = UIFactory.AddTopAccent(card.transform, skin.IsPremium ? UIFactory.Accent : UIFactory.Panel, 6f, 18f);
                skinsMotion.AddStagger(cardRt);

                var spot = UIFactory.Image(card.transform, "Spotlight", SpriteLibrary.Get("ui_glow"),
                    skin.IsPremium ? new Color(0.75f, 0.45f, 1f, 0.32f) : new Color(0.35f, 0.65f, 1f, 0.24f));
                UIFactory.Place((RectTransform)spot.transform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(140, 140), new Vector2(0.5f, 1));

                var preview = UIFactory.Image(card.transform, "Preview", SpriteLibrary.GetTurtle(skin.id), Color.white);
                preview.preserveAspect = true;
                var previewRt = (RectTransform)preview.transform;
                UIFactory.Place(previewRt, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(160, 122), new Vector2(0.5f, 1));
                UIFloatBob.Attach(previewRt, 4f, 0f, 2.2f, i * 0.7f);

                var name = UIFactory.Label(card.transform, skin.name, 34, Color.white);
                UIFactory.Place((RectTransform)name.transform, new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(380, 40));

                var perkPill = UIFactory.GlassPill(card.transform, "PerkPill", UIFactory.SlotDark);
                UIFactory.Place((RectTransform)perkPill.transform, new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(368, 44));
                var perk = UIFactory.Label(perkPill.transform, skin.perkDesc ?? "", 26, new Color(0.48f, 0.95f, 0.68f));
                var prt = (RectTransform)perk.transform; prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = new Vector2(12, 0); prt.offsetMax = new Vector2(-12, 0);

                var priceIcon = UIFactory.Image(card.transform, "PriceCoin", SpriteLibrary.Get("coin"), Color.white);
                priceIcon.preserveAspect = true;
                UIFactory.Place((RectTransform)priceIcon.transform, new Vector2(0.5f, 0), new Vector2(-56, 98), new Vector2(26, 26), new Vector2(0.5f, 0));

                var price = UIFactory.Label(card.transform, "", 28, UIFactory.Accent);
                UIFactory.Place((RectTransform)price.transform, new Vector2(0.5f, 0), new Vector2(0, 95), new Vector2(380, 34), new Vector2(0.5f, 0));

                int index = i;
                var btn = UIFactory.MakeButton(card.transform, "BUY", UIFactory.Blue, () => SkinAction(index), new Vector2(350, 74), 34);
                UIFactory.Place((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(350, 74));

                skinCards.Add(new SkinCard { skin = skin, cardRt = cardRt, accent = acc, preview = preview, priceIcon = priceIcon, price = price, button = btn });
            }
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
                bool showCoin = !owned && !c.skin.IsPremium;
                var img = (Image)c.button.targetGraphic;
                c.button.interactable = !equipped;
                if (c.accent != null)
                    c.accent.color = equipped ? UIFactory.Good : (c.skin.IsPremium ? UIFactory.Accent : new Color(0.28f, 0.35f, 0.56f));
                if (c.priceIcon != null) c.priceIcon.gameObject.SetActive(showCoin);
                if (showCoin)
                {
                    UIFactory.Place((RectTransform)c.price.transform, new Vector2(0.5f, 0), new Vector2(-32, 96), new Vector2(180, 30), new Vector2(0, 0));
                    c.price.alignment = TextAnchor.MiddleLeft;
                }
                else
                {
                    UIFactory.Place((RectTransform)c.price.transform, new Vector2(0.5f, 0), new Vector2(0, 96), new Vector2(380, 30), new Vector2(0.5f, 0));
                    c.price.alignment = TextAnchor.MiddleCenter;
                }

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
                    c.price.text = c.skin.coinPrice.ToString("N0");
                    UIFactory.SetButtonLabel(c.button, "BUY");
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
                UIPunch.Trigger(skinCards[index].cardRt, 0.12f);
            }
            else if (skin.IsPremium)
            {
                Store.Buy(skin.productId, ok =>
                {
                    if (ok)
                    {
                        save.EquipSkin(skin.id);
                        AudioManager.Sfx("buy");
                        UIPunch.Trigger(skinCards[index].cardRt, 0.14f);
                        Popup("UNLOCKED " + skin.name.ToUpper() + "!", UIFactory.Accent);
                    }
                    else
                    {
                        AudioManager.Sfx("deny");
                        skinCards[index].button.GetComponent<UIButtonFeel>()?.Shake();
                    }
                    RefreshSkins();
                });
                return;
            }
            else if (save.TryBuySkin(skin.id))
            {
                AudioManager.Sfx("buy");
                UIPunch.Trigger(skinCards[index].cardRt, 0.14f);
            }
            else
            {
                AudioManager.Sfx("deny");
                skinCards[index].button.GetComponent<UIButtonFeel>()?.Shake();
            }
            RefreshSkins();
        }

        // =====================================================================
        // Shop (real-money coin packs + premium skin)
        // =====================================================================
        void BuildShop(Transform parent)
        {
            shopScreen = FullScreen(parent, "Shop", new Color(0.06f, 0.08f, 0.16f, 0.45f));
            UIFactory.HeaderBar(shopScreen, "SHOP", () => gm.ShowMenu(), out shopWallet);

            var shopMotion = UIPanelMotion.Attach(shopScreen, null, 0.16f);
            var products = ProductCatalog.All;
            for (int i = 0; i < products.Count; i++)
            {
                var p = products[i];
                float x = -675 + i * 450;
                bool isSkin = p.kind == ProductKind.Skin;
                var card = UIFactory.PanelBox(shopScreen, "Product_" + p.id, isSkin ? new Color(0.24f, 0.16f, 0.38f) : UIFactory.CardBg);
                var cardRt = (RectTransform)card.transform;
                UIFactory.Place(cardRt, new Vector2(0.5f, 0.5f), new Vector2(x, 22), new Vector2(410, 470));
                UIFactory.AddTopAccent(card.transform, isSkin ? UIFactory.Purple : UIFactory.Amber, 7f, 18f);
                shopMotion.AddStagger(cardRt);
                productCards.Add(cardRt);

                var spot = UIFactory.Image(card.transform, "Spotlight", SpriteLibrary.Get("ui_glow"),
                    isSkin ? new Color(0.72f, 0.42f, 1f, 0.34f) : new Color(1f, 0.78f, 0.25f, 0.28f));
                UIFactory.Place((RectTransform)spot.transform, new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(156, 156), new Vector2(0.5f, 1));

                var icon = UIFactory.Image(card.transform, "Icon", isSkin ? SpriteLibrary.GetTurtle(p.skinId) : SpriteLibrary.Get("coin"), Color.white);
                icon.preserveAspect = true;
                var iconRt = (RectTransform)icon.transform;
                UIFactory.Place(iconRt, new Vector2(0.5f, 1), new Vector2(0, -60), isSkin ? new Vector2(190, 142) : new Vector2(138, 138), new Vector2(0.5f, 1));
                UIFloatBob.Attach(iconRt, 4f, 0f, 2.2f, i * 0.8f);

                string tag = p.id == ProductCatalog.CoinsMedium ? "POPULAR" : p.id == ProductCatalog.CoinsLarge ? "BEST VALUE" : isSkin ? "EXCLUSIVE" : null;
                if (tag != null)
                {
                    Color tagCol = p.id == ProductCatalog.CoinsLarge ? UIFactory.Good : isSkin ? UIFactory.Purple : UIFactory.Blue;
                    var tagPill = UIFactory.GlassPill(card.transform, "Tag", tagCol);
                    UIFactory.Place((RectTransform)tagPill.transform, new Vector2(1, 1), new Vector2(-16, -24), new Vector2(176, 42), new Vector2(1, 1));
                    var tagTxt = UIFactory.Label(tagPill.transform, tag, 24, Color.white);
                    var trt = (RectTransform)tagTxt.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                }

                var title = UIFactory.Label(card.transform, p.title, 34, Color.white);
                UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(380, 42));

                var whatPill = UIFactory.GlassPill(card.transform, "WhatPill", UIFactory.SlotDark);
                UIFactory.Place((RectTransform)whatPill.transform, new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(364, 54));
                if (isSkin)
                {
                    var what = UIFactory.Label(whatPill.transform, "Exclusive animated skin", 28, UIFactory.Accent);
                    var wrt = (RectTransform)what.transform; wrt.anchorMin = Vector2.zero; wrt.anchorMax = Vector2.one;
                    wrt.offsetMin = Vector2.zero; wrt.offsetMax = Vector2.zero;
                }
                else
                {
                    string amtStr = "+" + p.coins.ToString("N0");
                    float txtW = amtStr.Length * 18f + 4f;
                    float totalW = 32f + 10f + txtW;
                    float startX = -totalW * 0.5f;
                    var whatCoin = UIFactory.Image(whatPill.transform, "WhatCoin", SpriteLibrary.Get("coin"), Color.white);
                    whatCoin.preserveAspect = true;
                    UIFactory.Place((RectTransform)whatCoin.transform, new Vector2(0.5f, 0.5f), new Vector2(startX + 16f, 0), new Vector2(32, 32), new Vector2(0.5f, 0.5f));
                    var what = UIFactory.Label(whatPill.transform, amtStr, 32, UIFactory.Accent, TextAnchor.MiddleLeft);
                    UIFactory.Place((RectTransform)what.transform, new Vector2(0.5f, 0.5f), new Vector2(startX + 42f, 0), new Vector2(txtW + 24f, 44f), new Vector2(0f, 0.5f));
                }

                int index = i;
                var btn = UIFactory.MakeButton(card.transform, "BUY", UIFactory.Good, () => ShopAction(index), new Vector2(350, 88), 38);
                UIFactory.Place((RectTransform)btn.transform, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(350, 88));
                productButtons.Add(btn);
            }

            shopNote = UIFactory.Label(shopScreen, "", 28, UIFactory.TextMuted);
            UIFactory.Place((RectTransform)shopNote.transform, new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(1700, 38));

            var restore = UIFactory.MakeButton(shopScreen, "RESTORE PURCHASES", UIFactory.Panel, RestorePurchases, new Vector2(416, 66), 30);
            UIFactory.Place((RectTransform)restore.transform, new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(416, 66));
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
                    UIPunch.Trigger(productCards[index], 0.14f);
                    Popup(p.kind == ProductKind.Coins ? "+" + p.coins.ToString("N0") + " COINS!" : "SKIN UNLOCKED!", UIFactory.Accent);
                }
                else
                {
                    AudioManager.Sfx("deny");
                    productButtons[index].GetComponent<UIButtonFeel>()?.Shake();
                }
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
            ranksScreen = FullScreen(parent, "Leaderboard", new Color(0.06f, 0.08f, 0.16f, 0.45f));
            UIFactory.HeaderBar(ranksScreen, "LEADERBOARD", () => gm.ShowMenu(), out _, "TOP RUNNERS  -  BEST DISTANCE");

            var panel = UIFactory.PanelBox(ranksScreen, "Table", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(1560, 760));
            UIFactory.AddTopAccent(panel.transform, UIFactory.Accent, 7f, 22f);

            var hdrBand = UIFactory.GlassPill(panel.transform, "HeaderBand", UIFactory.SlotDark);
            var hbrt = (RectTransform)hdrBand.transform;
            hbrt.anchorMin = new Vector2(0, 1); hbrt.anchorMax = new Vector2(1, 1);
            hbrt.pivot = new Vector2(0.5f, 1);
            hbrt.offsetMin = new Vector2(24, -96); hbrt.offsetMax = new Vector2(-24, -28);

            Text Col(string name, string header, float x, float w, TextAnchor a)
            {
                var h = UIFactory.Label(panel.transform, header, 30, UIFactory.Accent, a, FontStyle.Bold, name + "Header");
                UIFactory.Place((RectTransform)h.transform, new Vector2(0, 1), new Vector2(x, -40), new Vector2(w, 44), new Vector2(0, 1));
                var t = UIFactory.Label(panel.transform, "", 38, Color.white, a, FontStyle.Bold, name);
                t.lineSpacing = 1.18f;
                UIFactory.Place((RectTransform)t.transform, new Vector2(0, 1), new Vector2(x, -108), new Vector2(w, 620), new Vector2(0, 1));
                return t;
            }
            ranksRank = Col("Rank", "RANK", 60, 120, TextAnchor.UpperLeft);
            ranksName = Col("Name", "NAME", 230, 500, TextAnchor.UpperLeft);
            ranksDist = Col("Dist", "DISTANCE", 660, 260, TextAnchor.UpperRight);
            ranksScore = Col("Score", "SCORE", 960, 220, TextAnchor.UpperRight);
            ranksDate = Col("Date", "DATE", 1230, 260, TextAnchor.UpperLeft);
            UIPanelMotion.Attach(ranksScreen, (RectTransform)panel.transform, 0.16f);
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
            doubleCoinsBtn = UIFactory.MakeButton(panel, "WATCH AD: x2 COINS", new Color(0.85f, 0.55f, 0.1f), () => gm.WatchAdForDoubleCoins(), new Vector2(868, 84), 34);
            UIFactory.Place((RectTransform)doubleCoinsBtn.transform, new Vector2(0, 0), new Vector2(736, 180), new Vector2(868, 84), new Vector2(0, 0));
        }

        void RefreshSummaryExtras(RunStats s)
        {
            bool can = gm.CanDoubleCoins;
            doubleCoinsBtn.gameObject.SetActive(can || s.coinsDoubled);
            doubleCoinsBtn.interactable = can;
            UIFactory.SetButtonLabel(doubleCoinsBtn, s.coinsDoubled ? "COINS DOUBLED!" : "WATCH AD: x2 COINS (+" + s.coins + ")");
            if (s.coinsDoubled && doubleCoinsBtn.gameObject.activeInHierarchy)
                doubleCoinsBtn.GetComponent<UIButtonFeel>()?.Punch();
            RefreshEnergyUI();
        }

        // =====================================================================
        // Energy overlay & live timers
        // =====================================================================
        void BuildEnergyOverlay(Transform parent)
        {
            energyOverlay = FullScreen(parent, "OutOfEnergy", new Color(0.03f, 0.04f, 0.10f, 0.76f));
            var panel = UIFactory.PanelBox(energyOverlay, "Panel", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020, 560));

            var glow = UIFactory.Image(panel.transform, "IconGlow", SpriteLibrary.Get("ui_glow"), new Color(1f, 0.72f, 0.18f, 0.36f));
            UIFactory.Place((RectTransform)glow.transform, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(128, 128), new Vector2(0.5f, 1));
            var icon = UIFactory.Image(panel.transform, "Icon", SpriteLibrary.Get("energy"), Color.white);
            icon.preserveAspect = true;
            UIFactory.Place((RectTransform)icon.transform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(60, 78), new Vector2(0.5f, 1));

            var t = UIFactory.Label(panel.transform, "OUT OF ENERGY!", 80, UIFactory.Accent);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(960, 96), new Vector2(0.5f, 1));

            var sub = UIFactory.Label(panel.transform, "Each run costs 1 energy.\nRefills 1 every 15 minutes (max 5).", 30, UIFactory.TextMuted);
            UIFactory.Place((RectTransform)sub.transform, new Vector2(0.5f, 1), new Vector2(0, -226), new Vector2(940, 82), new Vector2(0.5f, 1));

            var tPill = UIFactory.GlassPill(panel.transform, "TimerPill", UIFactory.SlotDark);
            UIFactory.Place((RectTransform)tPill.transform, new Vector2(0.5f, 1), new Vector2(0, -324), new Vector2(780, 62), new Vector2(0.5f, 1));
            energyOverlayTimerText = UIFactory.Label(tPill.transform, "", 32, new Color(0.78f, 0.92f, 1f));
            var trt = (RectTransform)energyOverlayTimerText.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;

            energyOverlayAdBtn = UIFactory.MakeButton(panel.transform, "WATCH AD (+1 ENERGY)", UIFactory.Amber, () =>
            {
                gm.WatchAdForEnergy(ok =>
                {
                    if (ok && SaveData.Current.energy > 0) HideOutOfEnergy();
                });
            }, new Vector2(520, 108), 34);
            UIFactory.Place((RectTransform)energyOverlayAdBtn.transform, new Vector2(0.5f, 0), new Vector2(-175, 42), new Vector2(520, 108));

            var close = UIFactory.MakeButton(panel.transform, "CLOSE", UIFactory.Panel, HideOutOfEnergy, new Vector2(290, 108), 38);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(265, 42), new Vector2(290, 108));

            UIPanelMotion.Attach(energyOverlay, (RectTransform)panel.transform, 0.15f);
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
            Color pipEmpty = UIFactory.SlotDark;
            Color pipFilled = e <= 1 ? UIFactory.Bad : UIFactory.Accent;
            for (int i = 0; i < menuEnergyPips.Length; i++)
                if (menuEnergyPips[i] != null) menuEnergyPips[i].color = i < e ? pipFilled : pipEmpty;
            if (menuEnergyAdBtn != null)
            {
                menuEnergyAdBtn.interactable = canAd;
                menuEnergyAdBtn.targetGraphic.color = adCol;
                UIFactory.SetButtonLabel(menuEnergyAdBtn, adLabel);
            }

            if (summaryEnergyText != null) summaryEnergyText.text = energyStr;
            if (summaryEnergyTimerText != null) { summaryEnergyTimerText.text = nextStr; summaryEnergyTimerText.color = timerCol; }
            for (int i = 0; i < summaryEnergyPips.Length; i++)
                if (summaryEnergyPips[i] != null) summaryEnergyPips[i].color = i < e ? pipFilled : pipEmpty;
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
            dailyRewardOverlay = FullScreen(parent, "DailyRewards", new Color(0.03f, 0.04f, 0.10f, 0.78f));
            var panel = UIFactory.PanelBox(dailyRewardOverlay, "Panel", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1560, 760));
            UIFactory.AddTopAccent(panel.transform, UIFactory.Accent, 7f, 22f);

            var title = UIFactory.Label(panel.transform, "7-DAY LOGIN REWARDS", 58, UIFactory.Accent);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(1400, 70), new Vector2(0.5f, 1));
            dailyStatusText = UIFactory.Label(panel.transform, "", 30, UIFactory.TextMuted);
            UIFactory.Place((RectTransform)dailyStatusText.transform, new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(1400, 42), new Vector2(0.5f, 1));

            var dailyMotion = UIPanelMotion.Attach(dailyRewardOverlay, (RectTransform)panel.transform, 0.15f);
            var pos = new[]
            {
                new Vector2(-540, 105), new Vector2(-180, 105), new Vector2(180, 105), new Vector2(540, 105),
                new Vector2(-430, -110), new Vector2(-70, -110), new Vector2(365, -110),
            };
            var rewards = SaveData.DailyRewards;
            var entries = new List<(string sprite, string text, Color color)>(3);
            for (int i = 0; i < rewards.Length; i++)
            {
                var r = rewards[i];
                float cardW = r.day == 7 ? 480f : 330f;
                var card = UIFactory.PanelBox(panel.transform, "DayCard_" + r.day, new Color(0.23f, 0.17f, 0.14f));
                var cardRt = (RectTransform)card.transform;
                UIFactory.Place(cardRt, new Vector2(0.5f, 0.5f), pos[i], new Vector2(cardW, 202));
                var acc = UIFactory.AddTopAccent(card.transform, r.day == 7 ? UIFactory.Purple : UIFactory.Panel, 8f, 14f);
                dailyMotion.AddStagger(cardRt);

                var dayTitle = UIFactory.Label(card.transform, r.day == 7 ? "DAY 7 - GRAND PRIZE" : "DAY " + r.day, 28, UIFactory.Accent);
                UIFactory.Place((RectTransform)dayTitle.transform, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(cardW - 20, 34), new Vector2(0.5f, 1));

                entries.Clear();
                if (r.coins > 0) entries.Add(("coin", "+" + r.coins, UIFactory.Accent));
                if (r.parts > 0) entries.Add(("part", "+" + r.parts, new Color(0.60f, 0.85f, 1f)));
                if (r.fullEnergy) entries.Add(("energy", "FULL", UIFactory.Good));

                bool single = entries.Count == 1;
                float iconW = single ? 36f : 30f;
                float iconTextGap = 8f;
                float charW = single ? 18f : 16f;
                float itemGap = 20f;
                int fontSize = single ? 32 : 28;

                float totalRowW = 0f;
                for (int k = 0; k < entries.Count; k++)
                {
                    float textW = entries[k].text.Length * charW + 4f;
                    totalRowW += iconW + iconTextGap + textW;
                    if (k > 0) totalRowW += itemGap;
                }

                float curX = -totalRowW * 0.5f;
                for (int k = 0; k < entries.Count; k++)
                {
                    var e = entries[k];
                    float iconH = e.sprite == "energy" ? (single ? 42f : 36f) : iconW;
                    float textW = e.text.Length * charW + 4f;

                    var ic = UIFactory.Image(card.transform, "RewardIcon_" + k, SpriteLibrary.Get(e.sprite), Color.white);
                    ic.preserveAspect = true;
                    UIFactory.Place((RectTransform)ic.transform, new Vector2(0.5f, 0.5f), new Vector2(curX + iconW * 0.5f, -2f), new Vector2(iconW, iconH), new Vector2(0.5f, 0.5f));

                    var lbl = UIFactory.Label(card.transform, e.text, fontSize, e.color, TextAnchor.MiddleLeft);
                    UIFactory.Place((RectTransform)lbl.transform, new Vector2(0.5f, 0.5f), new Vector2(curX + iconW + iconTextGap, -2f), new Vector2(textW + 24f, 44f), new Vector2(0f, 0.5f));

                    curX += iconW + iconTextGap + textW + itemGap;
                }

                var status = UIFactory.Label(card.transform, "", 26, UIFactory.Good);
                UIFactory.Place((RectTransform)status.transform, new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(cardW - 24, 32), new Vector2(0.5f, 0));

                dailyCards.Add(new DailyCard { reward = r, cardRt = cardRt, accent = acc, bg = card, status = status });
            }

            dailyClaimBtn = UIFactory.MakeButton(panel.transform, "CLAIM", UIFactory.Good, () => gm.ClaimDailyLoginReward(out _), new Vector2(400, 94), 40);
            UIFactory.Place((RectTransform)dailyClaimBtn.transform, new Vector2(0.5f, 0), new Vector2(-190, 34), new Vector2(400, 94));
            var close = UIFactory.MakeButton(panel.transform, "CLOSE", UIFactory.Panel, HideDailyRewards, new Vector2(300, 94), 38);
            UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(250, 34), new Vector2(300, 94));

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
                var feel = dailyRewardBtn.GetComponent<UIButtonFeel>();
                if (feel != null) feel.idlePulse = canClaimToday;
                var img = (Image)dailyRewardBtn.targetGraphic;
                var lbl = dailyRewardBtn.GetComponentInChildren<Text>();
                if (dailyDoneCheck != null) dailyDoneCheck.gameObject.SetActive(!canClaimToday);
                if (lbl != null)
                {
                    var lrt = (RectTransform)lbl.transform;
                    lrt.offsetMin = new Vector2(12, 21);
                    lrt.offsetMax = canClaimToday ? new Vector2(-12, 0) : new Vector2(-44, 0);
                }
                if (canClaimToday)
                {
                    UIFactory.SetButtonLabel(dailyRewardBtn, "DAILY REWARD (DAY " + (nextDayIndex + 1) + "!)");
                    if (img != null) img.color = new Color(0.2f, 0.72f, 0.32f);
                }
                else
                {
                    UIFactory.SetButtonLabel(dailyRewardBtn, "DAILY REWARDS");
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
                var feel = dailyClaimBtn.GetComponent<UIButtonFeel>();
                if (feel != null) feel.idlePulse = canClaimToday;
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
                    c.bg.color = new Color(0.16f, 0.34f, 0.21f);
                    if (c.accent != null) c.accent.color = UIFactory.Good;
                    c.status.text = "CLAIMED";
                    c.status.color = UIFactory.Good;
                }
                else if (isToday)
                {
                    c.bg.color = new Color(0.38f, 0.28f, 0.12f);
                    if (c.accent != null) c.accent.color = UIFactory.Accent;
                    c.status.text = "TODAY!";
                    c.status.color = UIFactory.Accent;
                }
                else
                {
                    c.bg.color = c.reward.day == 7 ? new Color(0.32f, 0.18f, 0.36f) : new Color(0.23f, 0.17f, 0.14f);
                    if (c.accent != null) c.accent.color = c.reward.day == 7 ? UIFactory.Purple : new Color(0.50f, 0.38f, 0.28f);
                    c.status.text = "DAY " + c.reward.day;
                    c.status.color = UIFactory.TextMuted;
                }
            }
        }

        // =====================================================================
        // Revive offer
        // =====================================================================
        void BuildRevive(Transform parent)
        {
            reviveOverlay = FullScreen(parent, "Revive", new Color(0.03f, 0.04f, 0.10f, 0.66f));
            var panel = UIFactory.PanelBox(reviveOverlay, "Panel", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 560));
            UIFactory.AddTopAccent(panel.transform, UIFactory.Good, 7f, 22f);
            var t = UIFactory.Label(panel.transform, "REVIVE?", 104, UIFactory.Accent);
            UIFactory.Place((RectTransform)t.transform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(900, 124), new Vector2(0.5f, 1));
            var sub = UIFactory.Label(panel.transform, "Watch a short ad and keep riding from here", 34, UIFactory.TextMuted);
            UIFactory.Place((RectTransform)sub.transform, new Vector2(0.5f, 1), new Vector2(0, -174), new Vector2(900, 48), new Vector2(0.5f, 1));
            reviveBar = UIFactory.Bar(panel.transform, "Timer", UIFactory.Accent, new Vector2(760, 34), out var barRoot);
            UIFactory.Place(barRoot, new Vector2(0.5f, 1), new Vector2(0, -252), new Vector2(760, 34), new Vector2(0.5f, 1));
            reviveTimerText = UIFactory.Label(panel.transform, "", 32, new Color(0.8f, 0.88f, 1f));
            UIFactory.Place((RectTransform)reviveTimerText.transform, new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(900, 40), new Vector2(0.5f, 1));

            var yes = UIFactory.MakeButton(panel.transform, "WATCH AD", UIFactory.Good, () => ReviveChoose(true), new Vector2(400, 110), 48);
            UIFactory.Place((RectTransform)yes.transform, new Vector2(0.5f, 0), new Vector2(-215, 40), new Vector2(400, 110));
            var yesFeel = yes.GetComponent<UIButtonFeel>();
            if (yesFeel != null) yesFeel.idlePulse = true;
            var no = UIFactory.MakeButton(panel.transform, "NO THANKS", UIFactory.Panel, () => ReviveChoose(false), new Vector2(360, 110), 42);
            UIFactory.Place((RectTransform)no.transform, new Vector2(0.5f, 0), new Vector2(215, 40), new Vector2(360, 110));
            UIPanelMotion.Attach(reviveOverlay, (RectTransform)panel.transform, 0.15f);
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
            UIPanelMotion.Attach(adOverlay, null, 0.14f);
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
            purchaseOverlay = FullScreen(parent, "MockPurchase", new Color(0.03f, 0.04f, 0.10f, 0.74f));
            var panel = UIFactory.PanelBox(purchaseOverlay, "Panel", new Color(0.95f, 0.96f, 0.99f));
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 520));
            UIFactory.AddTopAccent(panel.transform, UIFactory.Good, 7f, 22f);
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
            UIPanelMotion.Attach(purchaseOverlay, (RectTransform)panel.transform, 0.15f);
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
        // Exit Game confirmation & Back navigation (Swipe Back / Esc / Backspace)
        // =====================================================================
        void BuildQuitConfirmOverlay(Transform parent)
        {
            quitOverlay = FullScreen(parent, "QuitConfirm", new Color(0.03f, 0.04f, 0.10f, 0.78f));
            var panel = UIFactory.PanelBox(quitOverlay, "Panel", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 440));
            UIFactory.AddTopAccent(panel.transform, UIFactory.Bad, 7f, 22f);

            var title = UIFactory.Label(panel.transform, "EXIT GAME?", 76, UIFactory.Accent);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(820, 92), new Vector2(0.5f, 1));

            var sub = UIFactory.Label(panel.transform, "Are you sure you want to quit Turtle Blaster?", 32, UIFactory.TextMuted);
            UIFactory.Place((RectTransform)sub.transform, new Vector2(0.5f, 1), new Vector2(0, -168), new Vector2(800, 56), new Vector2(0.5f, 1));

            var exitBtn = UIFactory.MakeButton(panel.transform, "EXIT", UIFactory.Bad, () => gm.QuitApplication(), new Vector2(360, 102), 44);
            UIFactory.Place((RectTransform)exitBtn.transform, new Vector2(0.5f, 0), new Vector2(-195, 42), new Vector2(360, 102));

            var stayBtn = UIFactory.MakeButton(panel.transform, "STAY", UIFactory.Good, HideQuitConfirm, new Vector2(360, 102), 44);
            UIFactory.Place((RectTransform)stayBtn.transform, new Vector2(0.5f, 0), new Vector2(195, 42), new Vector2(360, 102));

            UIPanelMotion.Attach(quitOverlay, (RectTransform)panel.transform, 0.14f);
            quitOverlay.gameObject.SetActive(false);
        }

        public void ShowQuitConfirm()
        {
            if (quitOverlay == null) return;
            quitOverlay.SetAsLastSibling();
            quitOverlay.gameObject.SetActive(true);
        }

        public void HideQuitConfirm()
        {
            if (quitOverlay == null || !quitOverlay.gameObject.activeSelf) return;
            quitOverlay.gameObject.SetActive(false);
            overlayClosedFrame = Time.frameCount;
        }

        /// <summary>
        /// Handles Back / Escape for active modal overlays first.
        /// Returns true if an overlay consumed the Back action.
        /// </summary>
        public bool TryHandleOverlayBack()
        {
            if (quitOverlay != null && quitOverlay.gameObject.activeSelf)
            {
                AudioManager.Sfx("click");
                HideQuitConfirm();
                return true;
            }
            if (purchaseOverlay != null && purchaseOverlay.gameObject.activeSelf)
            {
                AudioManager.Sfx("click");
                ClosePurchase(false);
                return true;
            }
            if (adOverlay != null && adOverlay.gameObject.activeSelf)
            {
                AudioManager.Sfx("click");
                CloseAd(false);
                return true;
            }
            if (dailyRewardOverlay != null && dailyRewardOverlay.gameObject.activeSelf)
            {
                AudioManager.Sfx("click");
                HideDailyRewards();
                return true;
            }
            if (energyOverlay != null && energyOverlay.gameObject.activeSelf)
            {
                AudioManager.Sfx("click");
                HideOutOfEnergy();
                return true;
            }
            if (reviveOverlay != null && reviveOverlay.gameObject.activeSelf)
            {
                AudioManager.Sfx("click");
                ReviveChoose(false);
                return true;
            }
            return false;
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

        void RefreshExtrasWallet(int coins, int parts)
        {
            skinsWallet?.Set(coins, parts);
            shopWallet?.Set(coins, parts);
            if (skinsScreen != null && skinsScreen.gameObject.activeSelf) RefreshSkins();
        }
    }
}
