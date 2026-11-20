#if NATIVE_GALLERY && NATIVE_SHARE
using System;
using System.IO;
using UnityEngine;

namespace TurtleBlaster
{
    /// <summary>
    /// Screenshot service backed by NativeGallery (saves to Mobile Gallery on Android/iOS)
    /// and NativeShare (opens native OS share sheet). In Unity Editor, delegates saving to
    /// DefaultScreenshotService so screenshots appear in the PC project's Screenshots folder.
    /// </summary>
    public sealed class NativeScreenshotService : IScreenshotService
    {
        const string AlbumName = "TurtleBlaster";
        readonly DefaultScreenshotService fallback = new DefaultScreenshotService();

        public string LastSavedPath { get; private set; }
        public string LastStatusMessage { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Services.Screenshot = new NativeScreenshotService();
        }

        public void SaveScreenshot(byte[] pngBytes, string fileName, Action<ScreenshotResult> onComplete)
        {
            if (pngBytes == null || pngBytes.Length == 0 || string.IsNullOrEmpty(fileName))
            {
                LastStatusMessage = "Screenshot failed: empty image data.";
                onComplete?.Invoke(new ScreenshotResult { success = false, statusMessage = LastStatusMessage });
                return;
            }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            try
            {
                string sharePath = Path.Combine(Application.temporaryCachePath, fileName);
                File.WriteAllBytes(sharePath, pngBytes);
                LastSavedPath = sharePath;

                NativeGallery.SaveImageToGallery(pngBytes, AlbumName, fileName, (success, galleryPath) =>
                {
                    LastStatusMessage = success
                        ? $"Saved to Mobile Gallery ({AlbumName})!"
                        : "Saved locally (Gallery permission denied or failed)";
                    Debug.Log($"[Screenshot] Gallery save success={success}, galleryPath={galleryPath}, sharePath={sharePath}");

                    onComplete?.Invoke(new ScreenshotResult
                    {
                        success = true,
                        filePath = sharePath,
                        galleryPath = galleryPath ?? string.Empty,
                        statusMessage = LastStatusMessage
                    });
                });
            }
            catch (Exception e)
            {
                LastStatusMessage = "Mobile screenshot save failed: " + e.Message;
                Debug.LogWarning("[Screenshot] " + LastStatusMessage);
                onComplete?.Invoke(new ScreenshotResult { success = false, statusMessage = LastStatusMessage });
            }
#else
            fallback.SaveScreenshot(pngBytes, fileName, res =>
            {
                LastSavedPath = res.filePath;
                LastStatusMessage = res.statusMessage;
                onComplete?.Invoke(res);
            });
#endif
        }

        public void ShareScreenshot(string filePath, string subject, string text, Action<bool> onComplete = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogWarning("[Screenshot] No screenshot file available to share.");
                onComplete?.Invoke(false);
                return;
            }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            new NativeShare()
                .AddFile(filePath, "image/png")
                .SetSubject(subject ?? "Turtle Blaster")
                .SetText(text ?? "Check out my Turtle Blaster screenshot!")
                .SetCallback((result, shareTarget) =>
                {
                    Debug.Log($"[Screenshot] NativeShare result={result}, target={shareTarget}");
                    onComplete?.Invoke(result != NativeShare.ShareResult.NotShared);
                })
                .Share();
#else
            Debug.Log("[Screenshot] Native Share runs on Android/iOS device. Saved file: " + filePath);
            onComplete?.Invoke(false);
#endif
        }

        public void OpenFolder(string filePath) => DefaultScreenshotService.OpenPathInFileBrowser(filePath);
    }
}
#endif
