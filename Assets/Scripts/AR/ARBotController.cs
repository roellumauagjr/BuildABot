using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public class ARBotController : MonoBehaviour
{
    public static ARBotController Instance { get; private set; }

    [Header("AR Configuration")]
    public GameObject botPrefabOverride;
    public Sprite placementReticleSprite;
    
    [Header("Startup Cover (hides Unity scene until WebView loads)")]
    [Tooltip("Assign a UI Canvas with a white Image child. It covers the Unity scene until the WebView fires OnPageLoaded.")]
    [SerializeField] private Canvas coverCanvas;

    private GameObject      currentBotInstance;
    private ARRaycastManager  raycastManager;
    private ARSession          arSession;
    private ARPlaneManager     arPlaneManager;
    private ARCameraBackground arCameraBackground;
    private ARTapToPlace       arTapToPlace;
    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private bool   isARActive    = false;
    private string pendingBotId  = "bot_lvl1";
    private GameObject placementIndicator;
    private ObjectSpawner _objectSpawner;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        raycastManager     = FindObjectOfType<ARRaycastManager>();
        arSession          = FindObjectOfType<ARSession>();
        arPlaneManager     = FindObjectOfType<ARPlaneManager>();
        _objectSpawner     = FindObjectOfType<ObjectSpawner>();
        arTapToPlace       = FindObjectOfType<ARTapToPlace>();

        // Cache ARCameraBackground from the main camera
        if (Camera.main != null)
            arCameraBackground = Camera.main.GetComponent<ARCameraBackground>();

        // ── Show cover canvas immediately ───────────────────────────────────
        // Belt-and-suspenders: the Canvas covers the raw Unity scene during the
        // 1–3 seconds the WebView needs to load. Set Clear Flags = Solid Color
        // and Background = White on the Main Camera in the Inspector too.
        if (coverCanvas != null)
            coverCanvas.gameObject.SetActive(true);

        // Also set camera to white as fallback (in case Canvas isn't assigned)
        if (Camera.main != null)
        {
            Camera.main.clearFlags      = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.white;
        }

        // ── Disable AR at startup ────────────────────────────────────────
        StopAR();

        Application.targetFrameRate = 20;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// Called by WebViewManager.OnPageLoaded — hides the startup cover canvas
    /// once the React UI is fully loaded and visible.
    /// </summary>
    public void HideCover()
    {
        if (coverCanvas != null)
        {
            coverCanvas.gameObject.SetActive(false);
            Debug.Log("[ARBotController] Cover canvas hidden — WebView is ready.");
        }
    }

    // ─── Public AR Lifecycle ──────────────────────────────────────────────────

    /// <summary>
    /// Enable AR session and camera passthrough.
    /// Called by WebViewManager when entering Scan or Battle pages.
    /// </summary>
    public void StartAR()
    {
        Debug.Log("[ARBotController] StartAR — enabling ARSession, plane detection, tap-to-place.");

        if (arSession != null)          arSession.enabled = true;
        if (arCameraBackground != null) arCameraBackground.enabled = true;

        // Enable plane detection so grid visualizer appears on detected surfaces
        if (arPlaneManager != null)     arPlaneManager.enabled = true;

        // Enable tap-to-place
        if (arTapToPlace != null)       arTapToPlace.enabled = true;

        // Transparent clear so Unity AR camera shows through the WebView
        if (Camera.main != null)
        {
            Camera.main.clearFlags      = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0, 0, 0, 0);
        }

        Application.targetFrameRate = 30;
    }

    /// <summary>
    /// Disable AR session and camera passthrough.
    /// Called at startup, and when returning to Hub or Forge.
    /// </summary>
    public void StopAR()
    {
        Debug.Log("[ARBotController] StopAR — disabling ARSession, planes, tap-to-place.");

        isARActive = false;

        if (arSession != null)          arSession.enabled = false;
        if (arCameraBackground != null) arCameraBackground.enabled = false;

        // Disable plane detection — hides existing plane visualizers too
        if (arPlaneManager != null)
        {
            // Hide all currently tracked planes before disabling
            foreach (var plane in arPlaneManager.trackables)
                plane.gameObject.SetActive(false);
            arPlaneManager.enabled = false;
        }

        // Disable tap-to-place
        if (arTapToPlace != null)       arTapToPlace.enabled = false;

        // Solid white clear — no camera bleed-through on non-AR pages
        if (Camera.main != null)
        {
            Camera.main.clearFlags      = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.white;
        }

        if (placementIndicator != null)
            placementIndicator.SetActive(false);

        Application.targetFrameRate = 20;
    }

    // ─── AR Bot Sequence (called after StartAR is already active) ─────────────

    public void InitiateARSequence(string payloadJson)
    {
        isARActive = true;
        Debug.Log("[ARBotController] InitiateARSequence — AR already started, configuring bot placement.");

        // Ensure AR is fully on (safe to call again if already started)
        StartAR();

        CreatePlacementIndicator();

        if (!string.IsNullOrEmpty(payloadJson))
        {
            try
            {
                var p = JsonUtility.FromJson<UnityBridge.InitiateARPayload>(payloadJson);
                if (p != null && !string.IsNullOrEmpty(p.botId))
                    SetPendingBot(p.botId);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ARBotController] Failed to parse AR payload: " + e.Message);
            }
        }
    }

    private void CreatePlacementIndicator()
    {
        if (placementIndicator != null) return;

        placementIndicator = GameObject.CreatePrimitive(PrimitiveType.Quad);
        placementIndicator.name = "AR_Placement_Reticle";
        placementIndicator.transform.rotation = Quaternion.Euler(90, 0, 0);
        placementIndicator.transform.localScale = Vector3.one * 0.2f;

        var renderer = placementIndicator.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Unlit/Transparent"));
            if (placementReticleSprite != null)
                mat.mainTexture = placementReticleSprite.texture;
            else
                Debug.Log("[ARBotController] Reticle sprite not assigned, using fallback colour.");

            mat.color = new Color(0, 0.7f, 1f, 0.5f);
            renderer.material = mat;
        }

        var col = placementIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        placementIndicator.SetActive(false);
    }

    public void SetPendingBot(string botId)
    {
        pendingBotId = botId;
        Debug.Log($"[ARBotController] Pending bot changed to: {pendingBotId}");

        int index = (botId == "robot1" || botId == "bot_lvl1" || botId == "0") ? 0 : 1;

        if (_objectSpawner != null)
            _objectSpawner.spawnOptionIndex = index;

        // Also forward to tap-to-place controller
        if (arTapToPlace != null)
            arTapToPlace.SetSpawnIndex(index);

        Debug.Log($"[ARBotController] Bot '{botId}' → spawner index {index}");
    }

    private void Update()
    {
        if (!isARActive) return;
        UpdatePlacementIndicator();
    }

    private void UpdatePlacementIndicator()
    {
        if (placementIndicator == null) return;

        Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
        if (raycastManager != null && raycastManager.Raycast(center, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;
            placementIndicator.SetActive(true);
            placementIndicator.transform.position = hitPose.position + Vector3.up * 0.01f;
            placementIndicator.transform.rotation = hitPose.rotation * Quaternion.Euler(90, 0, 0);
        }
        else if (Application.isEditor || raycastManager == null)
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                placementIndicator.SetActive(true);
                placementIndicator.transform.position = hit.point + Vector3.up * 0.01f;
                placementIndicator.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(90, 0, 0);
            }
            else
            {
                placementIndicator.SetActive(false);
            }
        }
        else
        {
            placementIndicator.SetActive(false);
        }
    }

    public void Despawn()
    {
        // Remove placed bot via tap controller
        if (arTapToPlace != null)
            arTapToPlace.DespawnCurrent();

        if (currentBotInstance != null)
        {
            Destroy(currentBotInstance);
            currentBotInstance = null;
        }

        // Return to non-AR state
        StopAR();

        Debug.Log("[ARBotController] Despawned all AR elements.");
    }
}
