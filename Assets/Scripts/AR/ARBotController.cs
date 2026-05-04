using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.AR;

/// <summary>
/// Lightweight AR bridge for BuildABot.
///
/// This controller does NOT manage ARSession, ARPlaneManager, or ARRaycastManager.
/// Those are owned by the "XR Origin (AR Rig)" GameObject, which follows the
/// standard Unity AR Mobile Template configuration. They run continuously once
/// the app starts.
///
/// Responsibilities of this class:
///   1. Toggle camera transparency (transparent = AR feed visible, white = solid).
///   2. Control which robot the ObjectSpawner will place next.
///   3. Hide the startup cover canvas once the WebView loads.
///   4. Bridge React UI events (INITIATE_AR, SET_PAGE) to the ObjectSpawner.
///
/// Inspector Assignment Checklist:
///   objectSpawner   → "Object Spawner" under XR Origin (AR Rig)
///   coverCanvas     → startup white-cover Canvas (if used)
/// </summary>
public class ARBotController : MonoBehaviour
{
    public static ARBotController Instance { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────

    [Header("AR Template References")]
    [Tooltip("ObjectSpawner on the XR Origin. objectPrefabs[0]=Robot1, [1]=Robot2.")]
    [SerializeField] private ObjectSpawner objectSpawner;

    [Tooltip("ARTemplateMenuManager on the UI canvas. Its native UI panels are hidden.")]
    [SerializeField] private ARTemplateMenuManager templateMenuManager;

    [Header("Startup Cover")]
    [Tooltip("Canvas with a white Image. Hidden once the WebView fires OnPageLoaded.")]
    [SerializeField] private Canvas coverCanvas;

    // ─── Private ──────────────────────────────────────────────────────────

    private ARCameraBackground arCameraBackground;
    private string pendingBotId = "robot1";

    // ─── Unity Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Cache ARCameraBackground from the Main Camera
        if (Camera.main != null)
            arCameraBackground = Camera.main.GetComponent<ARCameraBackground>();

        // Suppress the AR Template's native UI panels.
        // We keep the component enabled so internal AR tracking logic continues.
        HideTemplateUI();

        // Show cover canvas while WebView loads (if assigned)
        if (coverCanvas != null)
            coverCanvas.gameObject.SetActive(true);

        // Start with transparent camera so AR shows through WebView.
        // StopAR() (white camera) is called by WebViewManager for Hub/Forge pages.
        SetCameraTransparent();

        Application.targetFrameRate = 30;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Camera Transparency ──────────────────────────────────────────────

    /// <summary>
    /// Makes the camera background transparent so the AR feed renders
    /// through the WebView overlay. Called for Scan and Battle pages.
    /// </summary>
    public void StartAR()
    {
        Debug.Log("[ARBotController] StartAR — camera transparent, AR feed visible.");
        SetCameraTransparent();

        if (arCameraBackground != null)
            arCameraBackground.enabled = true;
    }

    /// <summary>
    /// Makes the camera background solid white, hiding the AR feed.
    /// Called for Hub and Forge pages to conserve resources.
    /// </summary>
    public void StopAR()
    {
        Debug.Log("[ARBotController] StopAR — camera solid white (Hub/Forge page).");
        SetCameraWhite();

        if (arCameraBackground != null)
            arCameraBackground.enabled = false;
    }

    private void SetCameraTransparent()
    {
        if (Camera.main == null) return;
        Camera.main.clearFlags      = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }

    private void SetCameraWhite()
    {
        if (Camera.main == null) return;
        Camera.main.clearFlags      = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = Color.white;
    }

    // ─── Startup Cover ────────────────────────────────────────────────────

    /// <summary>
    /// Called by WebViewManager.OnPageLoaded once the React UI is visible.
    /// </summary>
    public void HideCover()
    {
        if (coverCanvas != null)
        {
            coverCanvas.gameObject.SetActive(false);
            Debug.Log("[ARBotController] Cover canvas hidden — WebView is ready.");
        }
    }

    // ─── Template UI Suppression ──────────────────────────────────────────

    private void HideTemplateUI()
    {
        if (templateMenuManager == null)
        {
            // Try to find it automatically
            templateMenuManager = FindObjectOfType<ARTemplateMenuManager>();
            if (templateMenuManager == null)
            {
                Debug.Log("[ARBotController] ARTemplateMenuManager not found — nothing to hide.");
                return;
            }
        }

        // Hide individual UI panels — keep component enabled so plane tracking runs
        if (templateMenuManager.objectMenu   != null) templateMenuManager.objectMenu.SetActive(false);
        if (templateMenuManager.createButton != null) templateMenuManager.createButton.gameObject.SetActive(false);
        if (templateMenuManager.deleteButton != null) templateMenuManager.deleteButton.gameObject.SetActive(false);
        if (templateMenuManager.cancelButton != null) templateMenuManager.cancelButton.gameObject.SetActive(false);
        if (templateMenuManager.modalMenu    != null) templateMenuManager.modalMenu.SetActive(false);

        Debug.Log("[ARBotController] Template UI panels hidden — React WebView is the UI layer.");
    }

    // ─── Bot Selection ────────────────────────────────────────────────────

    /// <summary>
    /// Called by WebViewManager on INITIATE_AR.
    /// Configures the ObjectSpawner to place the correct robot on the next tap.
    /// The template's ARInteractorSpawnTrigger handles the actual tap → spawn.
    /// </summary>
    public void InitiateARSequence(string payloadJson)
    {
        Debug.Log("[ARBotController] InitiateARSequence — configuring bot from React.");

        string botId = "robot1";
        if (!string.IsNullOrEmpty(payloadJson))
        {
            try
            {
                var p = JsonUtility.FromJson<UnityBridge.InitiateARPayload>(payloadJson);
                if (p != null && !string.IsNullOrEmpty(p.botId))
                    botId = p.botId;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ARBotController] Payload parse error: " + e.Message);
            }
        }

        SetPendingBot(botId);
    }

    /// <summary>
    /// Sets which robot prefab the ObjectSpawner will place on the next tap.
    ///   "robot1" / "bot_lvl1" / "0" → index 0 (Robot1.prefab)
    ///   anything else               → index 1 (Robot2.prefab)
    /// </summary>
    public void SetPendingBot(string botId)
    {
        if (string.IsNullOrEmpty(botId)) botId = "robot1";
        pendingBotId = botId;

        int index = (botId == "robot1" || botId == "bot_lvl1" || botId == "0") ? 0 : 1;

        if (objectSpawner == null)
        {
            Debug.LogError("[ARBotController] objectSpawner is null — assign it in the Inspector.");
            return;
        }

        int safe = Mathf.Clamp(index, 0, Mathf.Max(0, objectSpawner.objectPrefabs.Count - 1));
        objectSpawner.spawnOptionIndex = safe;

        string prefabName = (objectSpawner.objectPrefabs.Count > safe &&
                             objectSpawner.objectPrefabs[safe] != null)
            ? objectSpawner.objectPrefabs[safe].name : "NOT ASSIGNED";

        Debug.Log($"[ARBotController] Bot='{botId}' → spawner index {safe} ({prefabName})");
    }

    // ─── Despawn ──────────────────────────────────────────────────────────

    public void Despawn()
    {
        // Destroy all spawned children of the ObjectSpawner
        if (objectSpawner != null)
        {
            foreach (Transform child in objectSpawner.transform)
                Destroy(child.gameObject);
        }
        Debug.Log("[ARBotController] All spawned AR objects removed.");
    }
}
