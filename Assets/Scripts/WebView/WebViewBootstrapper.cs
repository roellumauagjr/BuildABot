using UnityEngine;

/// <summary>
/// Bootstrapper — ensures a WebViewManager singleton exists in the scene at runtime.
///
/// Add this component to ANY GameObject in the AR scene (e.g., the XR Origin,
/// or a dedicated "Bootstrapper" root object).
///
/// On Awake, if no WebViewManager already exists in the scene, this script
/// creates a new GameObject named "WebViewManager", marks it DontDestroyOnLoad,
/// and attaches the WebViewManager component to it.
///
/// This is a safety net — you can also place the WebViewManager prefab directly
/// in the scene hierarchy if you prefer explicit placement.
/// </summary>
public class WebViewBootstrapper : MonoBehaviour
{
    private void Awake()
    {
        if (WebViewManager.Instance != null)
        {
            // Already exists (e.g., dragged into scene as prefab).
            return;
        }

        Debug.Log("[WebViewBootstrapper] Creating WebViewManager at runtime.");
        var go = new GameObject("WebViewManager");
        go.AddComponent<WebViewManager>();
        // WebViewManager.Awake() will call DontDestroyOnLoad on itself.
    }
}
