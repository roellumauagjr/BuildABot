using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

// The WebViewObject type is provided by the Gree unity-webview package
// (net.gree.unity-webview).  If the package has not yet resolved in the
// Package Manager, this file will show compiler errors — that is expected.
// Open Window > Package Manager and wait for resolution before entering Play Mode.

/// <summary>
/// Central controller for the Gree OS-level WebView overlay.
///
/// Responsibilities:
///   1. Instantiate and configure the Gree WebViewObject on Awake.
///   2. Load the React static build from StreamingAssets/WebUI/index.html.
///   3. Inject the JS bridge helpers (window._unityBridge, window.dispatchUnityEvent).
///   4. Route incoming messages (React → Unity) to registered handlers.
///   5. Expose SendToReact() so any Unity system can push events into the UI.
///
/// Attach this to a persistent root GameObject named "WebViewManager".
/// </summary>
public class WebViewManager : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────

    [Header("Layout (screen-edge margins in pixels)")]
    [SerializeField] private int marginLeft   = 0;
    [SerializeField] private int marginTop    = 0;
    [SerializeField] private int marginRight  = 0;
    [SerializeField] private int marginBottom = 0;

    [Header("Behaviour")]
    [Tooltip("Automatically reveal the WebView as soon as the index page finishes loading.")]
    [SerializeField] private bool autoShowOnLoad = true;

    [Tooltip("Mirror JavaScript console messages to the Unity log.")]
    [SerializeField] private bool enableJSLogging = true;

    [Tooltip("Subfolder inside StreamingAssets that holds the React build output (index.html etc.).")]
    [SerializeField] private string webUIFolder = "WebUI";


    // ─── Runtime ──────────────────────────────────────────────────────────

    private WebViewObject _webView;
    private bool _isPageReady = false;

    // ─── Singleton ────────────────────────────────────────────────────────

    public static WebViewManager Instance { get; private set; }

    // ─── Unity Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitialiseWebView();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private string _currentPage = "hub";

    private void Update()
    {
        // WebView is now always visible because both 'scan' and 'battle' 
        // use it for UI overlays. Transparency is handled by CSS.
    }

    public void SetCurrentPage(string page)
    {
        _currentPage = page;
        Debug.Log($"[WebViewManager] Page set to: {page}");
    }

    // ─── Initialisation ───────────────────────────────────────────────────────

    private void InitialiseWebView()
    {
        _webView = gameObject.AddComponent<WebViewObject>();

        _webView.Init(
            cb:      OnMessageFromReact,
            ld:      OnPageLoaded,
            err:     (msg) => Debug.LogError($"[WebViewManager] Load error: {msg}"),
            httpErr: (msg) => Debug.LogError($"[WebViewManager] HTTP error: {msg}"),
            transparent: true,         // AR overlay — background must be clear
            enableWKWebView: true      // Use WKWebView on iOS (ignored on Android)
        );

        // Full-screen overlay — no margins unless you need a UI notch offset.
        _webView.SetMargins(marginLeft, marginTop, marginRight, marginBottom);

        // Hidden until the page finishes loading to prevent FOUC.
        _webView.SetVisibility(false);

        LoadReactUI();
    }


    // ─── Page loading ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds the correct URL for the React index.html on each platform.
    ///
    ///   Android : file:///android_asset/WebUI/index.html  (APK asset bundle)
    ///   iOS     : file:///…/Data/Raw/WebUI/index.html
    ///   Editor  : file:///…/Assets/StreamingAssets/WebUI/index.html
    /// </summary>
    private void LoadReactUI()
    {
        string url;

#if UNITY_ANDROID && !UNITY_EDITOR
        url = $"file:///android_asset/{webUIFolder}/index.html";
#elif UNITY_IOS && !UNITY_EDITOR
        url = $"file://{Application.streamingAssetsPath}/{webUIFolder}/index.html";
#else
        string abs = Path.Combine(Application.streamingAssetsPath, webUIFolder, "index.html")
                         .Replace("\\", "/");
        url = $"file://{abs}";
#endif

        Debug.Log($"[WebViewManager] Loading URL: {url}");
        _webView.LoadURL(url);
    }

    private void OnPageLoaded(string url)
    {
        Debug.Log($"[WebViewManager] Page ready: {url}");
        _isPageReady = true;

        InjectBridgeHelpers();

        if (autoShowOnLoad)
        {
            ShowWebView();
            Debug.Log("[WebViewManager] WebView is now visible.");
        }

        // The startup cover is no longer used as per user request.
        // The React UI is now the definitive signal that the screen is ready.
        Debug.Log("[WebViewManager] Page loaded. Ready.");
    }

    // ─── JS Bridge injection ──────────────────────────────────────────────

    /// <summary>
    /// Injects two globals into the React page after it loads:
    ///
    ///   window._unityBridge.send(action, payload)
    ///     — React → Unity typed helper (wraps window.Unity.call).
    ///
    ///   window.dispatchUnityEvent(action, payloadJson)
    ///     — Called by Unity; fires a CustomEvent('unityEvent') that React listens to.
    /// </summary>
    private void InjectBridgeHelpers()
    {
        string js = @"
(function() {
    if (window._unityBridge) { return; }

    // React → Unity
    window._unityBridge = {
        send: function(action, payload) {
            var envelope = JSON.stringify({ action: action, payload: JSON.stringify(payload || {}) });
            if (window.Unity) {
                window.Unity.call(envelope);
            } else {
                console.warn('[UnityBridge] window.Unity not available.');
            }
        }
    };

    // Unity → React
    window.dispatchUnityEvent = function(action, payloadJson) {
        window.dispatchEvent(new CustomEvent('unityEvent', {
            detail: { action: action, payload: payloadJson }
        }));
    };

    console.log('[UnityBridge] Helpers injected.');
})();
";
        _webView.EvaluateJS(js);
    }

    // ─── Visibility ───────────────────────────────────────────────────────

    public void ShowWebView() => _webView?.SetVisibility(true);
    public void HideWebView() => _webView?.SetVisibility(false);
    public bool IsPageReady  => _isPageReady;

    /// <summary>
    /// Returns the player to the Battle landing/entry page in React.
    /// Shows the WebView and dispatches a NAVIGATE_TO_BATTLE event so React
    /// can do an internal page transition — without triggering HandleSetPage's
    /// "battle" branch (which would re-hide the WebView and re-enter AR).
    /// </summary>
    public void NavigateToBattleLanding()
    {
        ShowWebView();

        if (!_isPageReady) return;

        // Dispatch a lightweight JS event that React listens to for an internal
        // navigation back to the battle entry screen.
        _webView.EvaluateJS(
            "window.dispatchEvent(new CustomEvent('unityNavigate', { detail: { page: 'battle' } }));"
        );

        SetCurrentPage("battle_landing");
        Debug.Log("[WebViewManager] NavigateToBattleLanding — WebView shown, React notified.");
    }


    // ─── React → Unity (incoming) ─────────────────────────────────────────

    /// <summary>
    /// Invoked by Gree on the main thread whenever React calls:
    ///   window.Unity.call(jsonString)
    /// </summary>
    private void OnMessageFromReact(string json)
    {
        if (enableJSLogging)
            Debug.Log($"[WebViewManager] ← React: {json}");

        UnityBridge.Message msg;
        try
        {
            msg = JsonUtility.FromJson<UnityBridge.Message>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebViewManager] JSON parse error: {ex.Message}  raw={json}");
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.action))
        {
            Debug.LogWarning($"[WebViewManager] Message missing 'action': {json}");
            return;
        }

        DispatchIncoming(msg);
    }

private void DispatchIncoming(UnityBridge.Message msg)
    {
        switch (msg.action)
        {
            case UnityBridge.SPAWN_BOT:
                HandleSpawnBot(msg.payload);
                break;

            case UnityBridge.DESPAWN_BOT:
                HandleDespawnBot(msg.payload);
                break;

            case UnityBridge.START_SCAN:
                HandleStartScan();
                break;

            case UnityBridge.STOP_SCAN:
                HandleStopScan();
                break;

            case UnityBridge.CAPTURE_AND_SCAN:
                HandleCaptureAndScan();
                break;

            case UnityBridge.SCAN_FRAME_B64:
                HandleScanFrameB64(msg.payload);
                break;

            case UnityBridge.REQUEST_SCAN_STATE:
                HandleRequestScanState();
                break;

            case UnityBridge.SELECT_CATEGORY:
                HandleSelectCategory(msg.payload);
                break;

            case UnityBridge.CONFIRM_DEPLOY:
                HandleConfirmDeploy(msg.payload);
                break;

            case UnityBridge.INITIATE_AR:
                HandleInitiateAR(msg.payload);
                break;

            case UnityBridge.SET_PAGE:
                HandleSetPage(msg.payload);
                break;

            case UnityBridge.SELECT_FORGE_BOT:
                HandleSelectForgeBot(msg.payload);
                break;

            case UnityBridge.SELECT_BOT:
                HandleSelectBot(msg.payload);
                break;

            default:
                Debug.LogWarning($"[WebViewManager] Unknown React action: '{msg.action}'");
                break;
        }
    }

    // ─── Incoming handlers (stubs — wire to your AR systems) ─────────────

    private void HandleSpawnBot(string payloadJson)
    {
        var p = SafeDeserialise<UnityBridge.SpawnBotPayload>(payloadJson);
        if (p == null) return;
        
        Debug.Log($"[WebViewManager] SPAWN_BOT botId={p.botId}");
        
        if (ARBotController.Instance != null)
        {
            ARBotController.Instance.InitiateARSequence(payloadJson);
        }
    }

    private void HandleDespawnBot(string payloadJson)
    {
        Debug.Log($"[WebViewManager] DESPAWN_BOT {payloadJson}");
        if (ARBotController.Instance != null)
        {
            ARBotController.Instance.Despawn();
        }
    }

    private void HandleStartScan()
    {
        Debug.Log("[WebViewManager] START_SCAN — activating ImageScanner.");
        
        // React UI handles its own camera for scanning, so keep WebView visible.
        ShowWebView();
        
        if (ImageScanner.Instance != null)
            ImageScanner.Instance.StartScanning();
        else
            Debug.LogWarning("[WebViewManager] START_SCAN received but ImageScanner.Instance is null.");
    }

    private void HandleStopScan()
    {
        Debug.Log("[WebViewManager] STOP_SCAN — deactivating ImageScanner.");
        ImageScanner.Instance?.StopScanning();
    }

    private void HandleCaptureAndScan()
    {
        Debug.Log("[WebViewManager] CAPTURE_AND_SCAN — triggering one-shot capture.");
        ImageScanner.Instance?.CaptureAndScan();
    }

    private void HandleScanFrameB64(string payloadJson)
    {
        // React captured a frame from getUserMedia() <video> element and sent it as base64.
        // Forward to ImageScanner which will pass it to Roboflow directly.
        var p = SafeDeserialise<UnityBridge.ScanFrameB64Payload>(payloadJson);
        if (p == null || string.IsNullOrEmpty(p.base64Image))
        {
            Debug.LogWarning("[WebViewManager] SCAN_FRAME_B64 received but payload is empty.");
            return;
        }
        Debug.Log("[WebViewManager] SCAN_FRAME_B64 — forwarding frame to ImageScanner.");
        ImageScanner.Instance?.ScanBase64(p.base64Image);
    }

    private void HandleRequestScanState()
    {
        bool scanning = ImageScanner.Instance?.IsScanning ?? false;
        Debug.Log($"[WebViewManager] REQUEST_SCAN_STATE — IsScanning={scanning}");
        // If scanner is active and we have a pending detection, re-send it;
        // otherwise just confirm the idle state via SCAN_EMPTY.
        if (!scanning)
            SendToReact(UnityBridge.SCAN_EMPTY);
    }

    private void HandleSelectCategory(string payloadJson)
    {
        var p = SafeDeserialise<UnityBridge.SelectCategoryPayload>(payloadJson);
        Debug.Log($"[WebViewManager] SELECT_CATEGORY '{p?.category}'");
        // TODO: Highlight AR recyclables of that category.
    }

    private void HandleConfirmDeploy(string payloadJson)
    {
        Debug.Log($"[WebViewManager] CONFIRM_DEPLOY {payloadJson}");
        // TODO: Trigger deployment sequence.
    }


    private void HandleInitiateAR(string payloadJson)
    {
        Debug.Log($"[WebViewManager] INITIATE_AR payload: {payloadJson}");
        
        // 1. DELETE/HIDE the React Web UI instantly.
        HideWebView();

        if (ARBotController.Instance != null)
        {
            // 2. Configure the bot to spawn
            ARBotController.Instance.InitiateARSequence(payloadJson);
            
            // 3. CRITICAL FIX: Turn on the Battle Mode (Camera Hardware ON, Planes ON)
            ARBotController.Instance.SetBattleMode();
            
            // 4. Reveal the AR Mobile Native UI
            ARBotController.Instance.ShowNativeUI();
        }
        else
        {
            Debug.LogWarning("[WebViewManager] ARBotController.Instance not found! AR sequence aborted.");
        }
    }

    private void HandleSetPage(string payloadJson)
    {
        var p = SafeDeserialise<UnityBridge.SetPagePayload>(payloadJson);
        if (p == null || string.IsNullOrEmpty(p.page)) return;
        
        SetCurrentPage(p.page);
        Debug.Log($"[WebViewManager] HandleSetPage: page='{p.page}'");

        switch (p.page)
        {
            case "hub":
            case "forge":
                // Non-AR pages: disable camera, solid white background
                ARBotController.Instance?.StopAR();
                ForgeManager.Instance?.ShowShowcase(p.page == "forge");
                ShowWebView();
                Debug.Log($"[WebViewManager] {p.page} — AR stopped, solid background.");
                break;

            case "scan":
                // Scanner page uses the AR camera passthrough as its live feed.
                // We keep tracking running but hide visual planes for a clean scan.
                ForgeManager.Instance?.ShowShowcase(false);
                ARBotController.Instance?.SetScannerMode();
                ShowWebView();
                Debug.Log("[WebViewManager] scan — AR camera ON, visuals hidden.");
                break;

            case "battle":
                // Battle page: Full AR experience enabled.
                ForgeManager.Instance?.ShowShowcase(false);
                ARBotController.Instance?.SetBattleMode();
                
                // Hide WebView as per user request to use Native AR UI
                HideWebView();
                ARBotController.Instance?.ShowNativeUI();
                
                Debug.Log("[WebViewManager] battle — Full AR ON (Planes visible), WebView HIDDEN.");
                break;

            default:
                ARBotController.Instance?.StopAR();
                ShowWebView();
                break;
        }
    }


    private void HandleSelectForgeBot(string payloadJson)
    {
        var p = SafeDeserialise<UnityBridge.SelectForgeBotPayload>(payloadJson);
        if (p == null) return;
        
        Debug.Log($"[WebViewManager] SELECT_FORGE_BOT id='{p.robotId}' unlocked={p.isUnlocked}");
        ForgeManager.Instance?.SelectBot(p.robotId, p.isUnlocked);
    }

    private void HandleSelectBot(string payloadJson)
    {
        var p = SafeDeserialise<UnityBridge.SelectBotPayload>(payloadJson);
        if (p == null) return;
        
        Debug.Log($"[WebViewManager] SELECT_BOT id='{p.botId}'");
        if (ARBotController.Instance != null)
        {
            ARBotController.Instance.SetPendingBot(p.botId);
        }
    }

    // ─── Unity → React (outgoing) ─────────────────────────────────────────

    /// <summary>
    /// Sends an event into the live React page by evaluating:
    ///   window.dispatchUnityEvent(action, payloadJson)
    ///
    /// React listens via:
    ///   window.addEventListener('unityEvent', (e) => { ... e.detail ... });
    ///
    /// Usage:
    ///   var p = new UnityBridge.ScanCompletePayload { material = "Plastic", confidence = 0.95f };
    ///   WebViewManager.Instance.SendToReact(UnityBridge.SCAN_COMPLETE, p);
    /// </summary>
    public void SendToReact<T>(string action, T payload)
    {
        if (!_isPageReady)
        {
            Debug.LogWarning($"[WebViewManager] SendToReact({action}) called before page is ready — dropped.");
            return;
        }

        string payloadJson = JsonUtility.ToJson(payload);

        if (enableJSLogging)
            Debug.Log($"[WebViewManager] → React: {action}  {payloadJson}");

        // Escape single-quotes so the injected JS string literal stays valid.
        string escaped = payloadJson.Replace("\\", "\\\\").Replace("'", "\\'");
        _webView.EvaluateJS($"window.dispatchUnityEvent('{action}', '{escaped}');");
    }

    /// <summary>Overload for events that carry no payload.</summary>
    public void SendToReact(string action) => SendToReact(action, new { });

    // ─── Helpers ──────────────────────────────────────────────────────────

    private T SafeDeserialise<T>(string json) where T : class
    {
        try   { return JsonUtility.FromJson<T>(json); }
        catch { return null; }
    }
}
