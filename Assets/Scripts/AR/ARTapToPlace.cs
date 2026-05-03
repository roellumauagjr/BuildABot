using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Listens for a single touch tap, raycasts against detected AR planes,
/// and tells ObjectSpawner to place the selected robot at the hit point.
///
/// Attach to the same GameObject as ARBotController (or any persistent object).
/// Enable/disable this component via ARBotController.StartAR() / StopAR().
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class ARTapToPlace : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The ObjectSpawner that holds the robot prefabs.")]
    [SerializeField] private ObjectSpawner objectSpawner;

    [Tooltip("Optional: particle/flash prefab that plays at spawn point.")]
    [SerializeField] private GameObject spawnFxPrefab;

    [Header("Behaviour")]
    [Tooltip("If true, removing the previous bot before placing a new one.")]
    [SerializeField] private bool replacePreviousBot = true;

    // ─── State ────────────────────────────────────────────────────────────────

    private ARRaycastManager      _raycastManager;
    private static List<ARRaycastHit> _hits = new List<ARRaycastHit>();
    private GameObject            _spawnedBot;
    private bool                  _isReady = false;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();

        if (objectSpawner == null)
            objectSpawner = FindObjectOfType<ObjectSpawner>();
    }

    private void OnEnable()
    {
        _isReady = true;
        Debug.Log("[ARTapToPlace] Ready — tap a detected plane to place the selected robot.");
    }

    private void OnDisable()
    {
        _isReady = false;
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isReady) return;

#if UNITY_EDITOR
        // Editor: left-click simulates tap
        if (Input.GetMouseButtonDown(0))
            TryPlaceAtScreenPoint(Input.mousePosition);
#else
        // Device: single-finger tap (not a multi-touch gesture)
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
                TryPlaceAtScreenPoint(touch.position);
        }
#endif
    }

    // ─── Placement ────────────────────────────────────────────────────────────

    private void TryPlaceAtScreenPoint(Vector2 screenPoint)
    {
        if (_raycastManager == null) return;

        if (_raycastManager.Raycast(screenPoint, _hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = _hits[0].pose;
            PlaceRobot(hitPose.position, hitPose.up);
        }
    }

    private void PlaceRobot(Vector3 position, Vector3 normal)
    {
        if (objectSpawner == null)
        {
            Debug.LogWarning("[ARTapToPlace] No ObjectSpawner assigned.");
            return;
        }

        // Remove previous instance if configured
        if (replacePreviousBot && _spawnedBot != null)
        {
            Destroy(_spawnedBot);
            _spawnedBot = null;
        }

        // Intercept the objectSpawned event to cache the spawned bot reference
        objectSpawner.objectSpawned -= OnBotSpawned;
        objectSpawner.objectSpawned += OnBotSpawned;

        bool spawned = objectSpawner.TrySpawnObject(position, normal);

        if (!spawned)
        {
            Debug.LogWarning("[ARTapToPlace] Spawn failed (point may be out of camera view).");
            objectSpawner.objectSpawned -= OnBotSpawned;
        }

        // Optional spawn FX
        if (spawned && spawnFxPrefab != null)
        {
            var fx = Instantiate(spawnFxPrefab, position, Quaternion.identity);
            Destroy(fx, 3f);
        }
    }

    private void OnBotSpawned(GameObject bot)
    {
        _spawnedBot = bot;
        objectSpawner.objectSpawned -= OnBotSpawned;
        Debug.Log($"[ARTapToPlace] Placed: {bot.name} at {bot.transform.position}");
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ARBotController when the user changes bot selection in the UI.
    /// </summary>
    public void SetSpawnIndex(int index)
    {
        if (objectSpawner != null)
            objectSpawner.spawnOptionIndex = index;
    }

    /// <summary>
    /// Removes the currently placed bot from the scene.
    /// </summary>
    public void DespawnCurrent()
    {
        if (_spawnedBot != null)
        {
            Destroy(_spawnedBot);
            _spawnedBot = null;
        }
    }
}
