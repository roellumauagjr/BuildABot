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
    
    private GameObject currentBotInstance;
    private ARRaycastManager raycastManager;
    private ARSession arSession;
    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private bool isARActive = false;
    private string pendingBotId = "bot_lvl1";
    private GameObject placementIndicator;
    private ObjectSpawner _objectSpawner;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        raycastManager = FindObjectOfType<ARRaycastManager>();
        arSession = FindObjectOfType<ARSession>();
        _objectSpawner = FindObjectOfType<ObjectSpawner>();

        // Start with a low frame rate to save power on Hub
        Application.targetFrameRate = 20;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void InitiateARSequence(string payloadJson)
    {
        isARActive = true;
        Debug.Log("[ARBotController] AR Sequence Active. Boosting performance and enabling AR Session.");

        // Thermal Governor: 30 FPS is enough for AR but saves significant heat/battery
        Application.targetFrameRate = 30;

        // Ensure AR Session is running
        if (arSession != null)
        {
            arSession.enabled = true;
            Debug.Log("[ARBotController] ARSession Resumed.");
        }

        // Ensure the AR Camera is rendering the background
        var arCameraBg = Camera.main.GetComponent<ARCameraBackground>();
        if (arCameraBg != null)
        {
            arCameraBg.enabled = true;
        }

        // Transparency setup for WebView
        // Important: Set clear flags to Depth only or Solid Color with alpha 0
        Camera.main.clearFlags = CameraClearFlags.SolidColor;
        Camera.main.backgroundColor = new Color(0, 0, 0, 0); 

        CreatePlacementIndicator();

        // Initial Bot Setup from payload
        if (!string.IsNullOrEmpty(payloadJson))
        {
            try {
                var p = JsonUtility.FromJson<UnityBridge.InitiateARPayload>(payloadJson);
                if (p != null && !string.IsNullOrEmpty(p.botId))
                {
                    SetPendingBot(p.botId);
                }
            } catch (System.Exception e) {
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

        // Use the serialized sprite if assigned
        var renderer = placementIndicator.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Unlit/Transparent"));
            if (placementReticleSprite != null) 
            {
                mat.mainTexture = placementReticleSprite.texture;
            }
            else
            {
                // Fallback to Resources just in case or procedural color
                Debug.Log("[ARBotController] Reticle sprite not assigned, using fallback.");
            }
            mat.color = new Color(0, 0.7f, 1f, 0.5f); // Cyan-ish translucent
            renderer.material = mat;
        }
        
        // Remove collider so it doesn't interfere with raycasts
        var col = placementIndicator.GetComponent<Collider>();
        if (col != null) Destroy(col);

        placementIndicator.SetActive(false);
    }

    public void SetPendingBot(string botId)
    {
        pendingBotId = botId;
        Debug.Log($"[ARBotController] Pending bot changed to: {pendingBotId}");
        
        if (_objectSpawner == null) return;

        // Map botId to ObjectSpawner index
        // Index 0 = Robot1, Index 1 = Robot2 (configured in ObjectSpawner inspector)
        int index = (botId == "robot1" || botId == "1") ? 0 : 1;
        _objectSpawner.spawnOptionIndex = index;
        
        Debug.Log($"[ARBotController] Selected bot '{botId}' -> Spawner index {index}");
    }

    private void Update()
    {
        if (!isARActive) return;

        // Note: Actual placement is now handled by ObjectPlacement script
        // ARBotController handles the HUD and state transitions

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
        if (currentBotInstance != null) Destroy(currentBotInstance);
        if (placementIndicator != null) placementIndicator.SetActive(false);
        
        isARActive = false;
        
        // Lower frame rate to save battery when not in AR
        Application.targetFrameRate = 15;

        // Pause AR Session to stop camera and sensor processing
        if (arSession != null)
        {
            arSession.enabled = false;
            Debug.Log("[ARBotController] ARSession Paused for thermal management.");
        }

        Debug.Log("[ARBotController] Despawned all AR elements.");
    }
}
