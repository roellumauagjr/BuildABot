// This file lives in an Editor-only assembly — it will NOT be included in builds.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility for setting up the WebView scene objects.
/// Access via:  BuildABot > Setup WebView Scene
/// </summary>
public static class WebViewSceneSetup
{
    [MenuItem("BuildABot/Setup WebView Scene")]
    public static void SetupScene()
    {
        // ── 1. WebViewManager ──────────────────────────────────────────────
        GameObject wvm = GameObject.Find("WebViewManager");
        if (wvm == null)
        {
            wvm = new GameObject("WebViewManager");
            wvm.AddComponent<WebViewManager>();
            Debug.Log("[WebViewSceneSetup] Created WebViewManager GameObject.");
        }
        else
        {
            // Ensure the component is attached even if the GO existed.
            if (wvm.GetComponent<WebViewManager>() == null)
            {
                wvm.AddComponent<WebViewManager>();
                Debug.Log("[WebViewSceneSetup] Added WebViewManager component to existing GO.");
            }
            else
            {
                Debug.Log("[WebViewSceneSetup] WebViewManager already configured.");
            }
        }

        // Mark the scene dirty so Unity knows to save it.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "WebView Scene Setup",
            "WebViewManager is ready in the scene.\n\n" +
            "Next steps:\n" +
            "• The Gree package (net.gree.unity-webview) must resolve in the Package Manager.\n" +
            "• On first open after adding the package, Unity may recompile — wait for it to finish.\n" +
            "• Place your React build output in  Assets/StreamingAssets/WebUI/\n" +
            "• Call WebViewManager.Instance.SendToReact() from your AR scripts.\n" +
            "• React calls window._unityBridge.send() to talk back to Unity.",
            "Got it");
    }

    [MenuItem("BuildABot/Open WebUI StreamingAssets Folder")]
    public static void OpenWebUIFolder()
    {
        string path = System.IO.Path.Combine(
            Application.streamingAssetsPath, "WebUI");

        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);

        EditorUtility.RevealInFinder(path);
    }
}
#endif
