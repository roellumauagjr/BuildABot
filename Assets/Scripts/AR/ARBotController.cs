using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.AR;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif


/// <summary>
/// Lightweight AR bridge for BuildABot.
/// </summary>
public class ARBotController : MonoBehaviour
{
    public static ARBotController Instance { get; private set; }

    // ─── Inspector ────────────────────────────────────────────────────────

    [Header("AR Template References")]
    [Tooltip("ObjectSpawner on the XR Origin. objectPrefabs[0]=Robot1, [1]=Robot2.")]
    [SerializeField] private ObjectSpawner objectSpawner;

    [Tooltip("Legacy Template Menu Manager (if used).")]
    [SerializeField] private ARTemplateMenuManager templateMenuManager;

    [Tooltip("New Sample Menu Manager (Unity 6 / AR Starter Assets).")]
    [SerializeField] private ARSampleMenuManager sampleMenuManager;

    // [Header("Startup Cover")]
    // Removed: StartupCover is no longer used.

    // ─── Private ──────────────────────────────────────────────────────────

    private ARSession arSession;
    private ARCameraManager arCameraManager;
    private ARCameraBackground arCameraBackground;
    private ARPlaneManager arPlaneManager;
    private ARPointCloudManager arPointCloudManager;
    private string pendingBotId = "robot1";
    private bool isInitializingAR = false;

    // ─── Unity Lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Ensure we have fresh references every time the scene starts
        FindCoreComponents();

        Application.targetFrameRate = 30;
        
        Debug.Log($"[ARBotController] Awake complete. ARSession found: {arSession != null}");
    }


    private void FindCoreComponents()
    {
        // 1. Find ARSession (the hardware controller)
        if (arSession == null) arSession = FindObjectOfType<ARSession>();

        // 3. Cache ARCameraBackground from the Main Camera
        if (Camera.main != null)
            arCameraBackground = Camera.main.GetComponent<ARCameraBackground>();

        // 4. Find ARCameraManager from the Main Camera
        if (Camera.main != null && arCameraManager == null)
            arCameraManager = Camera.main.GetComponent<ARCameraManager>();

        // 5. Find managers dynamically
        if (arPlaneManager == null) arPlaneManager = FindObjectOfType<ARPlaneManager>();
        if (arPointCloudManager == null) arPointCloudManager = FindObjectOfType<ARPointCloudManager>();
        if (objectSpawner == null) objectSpawner = FindObjectOfType<ObjectSpawner>();
        
        if (templateMenuManager == null) templateMenuManager = FindObjectOfType<ARTemplateMenuManager>();
        if (sampleMenuManager == null) sampleMenuManager = FindObjectOfType<ARSampleMenuManager>();

        Debug.Log($"[ARBotController] FindCoreComponents: ARSession={arSession != null}, ARCameraManager={arCameraManager != null}, ARCameraBackground={arCameraBackground != null}");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Granular AR Modes ────────────────────────────────────────────────

    public void SetMenuMode()
    {
        Debug.Log("[ARBotController] Hub Mode — Camera running behind Web UI.");
        Application.targetFrameRate = 30;

        // Keep hardware alive so it doesn't lock up, but hide planes
        if (arSession != null && !arSession.enabled)
        {
            arSession.enabled = true; 
        }

        ToggleARVisuals(false); 
    }

    public void SetScannerMode()
    {
        Debug.Log("[ARBotController] Entering Scanner Mode — Revealing Camera.");
        Application.targetFrameRate = -1;
        HideNativeUI();
        StartCoroutine(EnableARAndHideCover(false)); // false = no AR planes, just video
    }

    public void SetBattleMode()
    {
        Debug.Log("[ARBotController] Entering Battle Mode — Full AR Active.");
        Application.targetFrameRate = -1;
        HideNativeUI();
        StartCoroutine(EnableARAndHideCover(true)); // true = show AR planes
    }

    private IEnumerator RequestCameraPermission()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.Log("[ARBotController] Requesting Camera permission...");
            Permission.RequestUserPermission(Permission.Camera);
            
            // Wait for user response
            float waitTime = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && waitTime < 5f)
            {
                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;
            }
            
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
                Debug.Log("[ARBotController] Camera permission GRANTED.");
            else
                Debug.LogWarning("[ARBotController] Camera permission NOT granted or timed out.");
        }
#endif
        yield return null;
    }

    private IEnumerator EnableARAndHideCover(bool showVisuals)
    {
        if (isInitializingAR) yield break;
        isInitializingAR = true;

        // 1. Request Permissions
        yield return StartCoroutine(RequestCameraPermission());

        // 2. Ensure session is on
        if (arSession != null && !arSession.enabled)
        {
            arSession.enabled = true;
        }

        // (We no longer touch SetCameraTransparent() or arCameraBackground here. 
        // We let URP handle it natively so it never breaks!)

        // 3. Toggle UI and Visuals
        ToggleARVisuals(showVisuals);
        
        if (showVisuals) {
            ShowNativeUI();  // Battle Mode
        } else {
            HideNativeUI();  // Scanner Mode
        }

        EnsureDirectionalLight();
        isInitializingAR = false;
    }

    private void EnsureDirectionalLight()
    {
        Light[] lights = FindObjectsOfType<Light>();
        bool hasDirLight = false;
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional && l.enabled)
            {
                hasDirLight = true;
                l.intensity = Mathf.Max(l.intensity, 1.2f); // Ensure it's bright enough
                break;
            }
        }

        if (!hasDirLight)
        {
            GameObject lightGameObject = new GameObject("AR Emergency Light");
            Light lightPtr = lightGameObject.AddComponent<Light>();
            lightPtr.type = LightType.Directional;
            lightPtr.intensity = 1.2f;
            lightGameObject.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        // Force Trilight ambient mode to prevent pitch-black shadows in AR
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.8f, 0.8f, 0.8f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
        RenderSettings.ambientIntensity = 1.5f;
    }



    private void ToggleARVisuals(bool enabled)
    {
        if (arPlaneManager != null) arPlaneManager.enabled = enabled;
        if (arPointCloudManager != null) arPointCloudManager.enabled = enabled;

        // Hide or show existing trackables
        if (arPlaneManager != null)
        {
            foreach (var plane in arPlaneManager.trackables)
                plane.gameObject.SetActive(enabled);
        }

        if (arPointCloudManager != null)
        {
            foreach (var point in arPointCloudManager.trackables)
                point.gameObject.SetActive(enabled);
        }
    }

    // ─── Legacy Helpers (For Compatibility) ───────────────────────────────

    public void StartAR() => SetScannerMode();
    public void StopAR() => SetMenuMode();


    // ─── Startup Cover ────────────────────────────────────────────────────

    // ─── Native UI Suppression ──────────────────────────────────────────

    public void HideNativeUI()
    {
        // Bulletproof Native UI Suppression
        if (sampleMenuManager != null) 
        {
            sampleMenuManager.gameObject.SetActive(false); // Kill the whole manager object
            if (sampleMenuManager.createButton != null) sampleMenuManager.createButton.gameObject.SetActive(false);
        }
        
        if (templateMenuManager != null) 
        {
            templateMenuManager.gameObject.SetActive(false);
            if (templateMenuManager.createButton != null) templateMenuManager.createButton.gameObject.SetActive(false);
        }

        Debug.Log("[ARBotController] Native AR UI completely suppressed.");
    }

    public void ShowNativeUI()
    {
        // Bring it back for Battle Mode
        if (sampleMenuManager != null) 
        {
            sampleMenuManager.gameObject.SetActive(true);
            if (sampleMenuManager.createButton != null) sampleMenuManager.createButton.gameObject.SetActive(true);
        }
        
        if (templateMenuManager != null) 
        {
            templateMenuManager.gameObject.SetActive(true);
            if (templateMenuManager.createButton != null) templateMenuManager.createButton.gameObject.SetActive(true);
        }
        
        Debug.Log("[ARBotController] Native AR UI restored.");
    }

    // ─── Bot Selection ────────────────────────────────────────────────────

    /// <summary>
    /// Called by WebViewManager on INITIATE_AR.
    /// Configures the ObjectSpawner to place the correct robot on the next tap.
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
        if (objectSpawner != null)
        {
            foreach (Transform child in objectSpawner.transform)
                Destroy(child.gameObject);
        }
        Debug.Log("[ARBotController] All spawned AR objects removed.");
    }
}
