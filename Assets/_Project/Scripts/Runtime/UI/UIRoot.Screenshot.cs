using UnityEngine;
using UnityEngine.UI;

namespace TurtleBlaster
{
    /// <summary>
    /// Screenshot preview modal: displays the captured screen, shows where it was saved
    /// (PC folder in Editor / Mobile Gallery on Android & iOS), and triggers Native Share.
    /// </summary>
    public partial class UIRoot
    {
        RectTransform screenshotOverlay;
        RawImage screenshotPreviewImg;
        Text screenshotStatusText;
        Button screenshotOpenFolderBtn;
        string currentShotPath;

        void BuildScreenshotOverlay(Transform parent)
        {
            screenshotOverlay = FullScreen(parent, "ScreenshotOverlay", new Color(0.03f, 0.04f, 0.10f, 0.80f));
            var panel = UIFactory.PanelBox(screenshotOverlay, "Panel", UIFactory.CardBg);
            UIFactory.Place((RectTransform)panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1280, 840));
            UIFactory.AddTopAccent(panel.transform, UIFactory.Blue, 7f, 22f);

            var title = UIFactory.Label(panel.transform, "SCREENSHOT SAVED!", 56, UIFactory.Accent);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1160, 74), new Vector2(0.5f, 1));

            var frame = UIFactory.PanelBox(panel.transform, "PreviewFrame", Color.white);
            UIFactory.Place((RectTransform)frame.transform, new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(920, 518), new Vector2(0.5f, 1));

            var rawGo = new GameObject("PreviewImage", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(frame.transform, false);
            screenshotPreviewImg = rawGo.GetComponent<RawImage>();
            screenshotPreviewImg.color = Color.white;
            screenshotPreviewImg.raycastTarget = false;
            var rrt = (RectTransform)rawGo.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(10, 10);
            rrt.offsetMax = new Vector2(-10, -10);

            screenshotStatusText = UIFactory.Label(panel.transform, "", 28, UIFactory.TextMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
            screenshotStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.Place((RectTransform)screenshotStatusText.transform, new Vector2(0.5f, 0), new Vector2(0, 134), new Vector2(1160, 72), new Vector2(0.5f, 0));

            bool showOpenFolder = false;
#if UNITY_EDITOR || UNITY_STANDALONE
            showOpenFolder = true;
#endif
            float shareX = showOpenFolder ? -330f : -170f;
            float closeX = showOpenFolder ? 360f : 180f;

            var shareBtn = UIFactory.MakeButton(panel.transform, "SHARE (NATIVE)", UIFactory.Good, () =>
            {
                Services.Screenshot.ShareScreenshot(
                    currentShotPath,
                    "Turtle Blaster",
                    "Check out my Turtle Blaster screenshot!",
                    ok =>
                    {
                        if (this != null && Application.isEditor)
                            Popup("MOBILE ONLY: USE ANDROID FOR NATIVE SHARE", UIFactory.Accent, 44);
                    });
            }, new Vector2(360, 86), 34);
            UIFactory.Place((RectTransform)shareBtn.transform, new Vector2(0.5f, 0), new Vector2(shareX, 32), new Vector2(360, 86), new Vector2(0.5f, 0));

            screenshotOpenFolderBtn = UIFactory.MakeButton(panel.transform, "OPEN FOLDER", UIFactory.Blue, () =>
            {
                Services.Screenshot.OpenFolder(currentShotPath);
            }, new Vector2(320, 86), 34);
            UIFactory.Place((RectTransform)screenshotOpenFolderBtn.transform, new Vector2(0.5f, 0), new Vector2(40, 32), new Vector2(320, 86), new Vector2(0.5f, 0));
            screenshotOpenFolderBtn.gameObject.SetActive(showOpenFolder);

            var closeBtn = UIFactory.MakeButton(panel.transform, "CLOSE", UIFactory.Panel, HideScreenshotPreview, new Vector2(240, 86), 34);
            UIFactory.Place((RectTransform)closeBtn.transform, new Vector2(0.5f, 0), new Vector2(closeX, 32), new Vector2(240, 86), new Vector2(0.5f, 0));

            UIPanelMotion.Attach(screenshotOverlay, (RectTransform)panel.transform, 0.15f);
            screenshotOverlay.gameObject.SetActive(false);
        }

        public void ShowScreenshotPreview(Texture2D tex, ScreenshotResult result)
        {
            if (screenshotOverlay == null) return;
            currentShotPath = result.filePath;
            if (screenshotPreviewImg != null) screenshotPreviewImg.texture = tex;
            if (screenshotStatusText != null)
            {
                screenshotStatusText.text = string.IsNullOrEmpty(result.statusMessage)
                    ? result.filePath
                    : result.statusMessage;
            }
            screenshotOverlay.SetAsLastSibling();
            screenshotOverlay.gameObject.SetActive(true);
        }

        public void HideScreenshotPreview()
        {
            if (screenshotOverlay == null || !screenshotOverlay.gameObject.activeSelf) return;
            screenshotOverlay.gameObject.SetActive(false);
            overlayClosedFrame = Time.frameCount;
            gm?.OnScreenshotOverlayClosed();
        }
    }
}
