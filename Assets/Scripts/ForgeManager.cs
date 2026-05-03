using UnityEngine;
using System.Collections.Generic;

public class ForgeManager : MonoBehaviour
{
    public static ForgeManager Instance { get; private set; }

    [Header("Robot Showcases")]
    public GameObject robotLvl1;
    public GameObject robotLvl2;
    
    [Header("Visual Settings")]
    public Material silhouetteMaterial;
    public Vector3 displayPosition = new Vector3(0, 0, 10);
    public Vector3 displayRotation = new Vector3(0, 180, 0);
    public float rotationSpeed = 20f;

    private Dictionary<string, GameObject> _robots = new Dictionary<string, GameObject>();
    private GameObject _activeBot;
    private bool _isShowcaseActive = false;
    private Dictionary<GameObject, Material[]> _originalMaterials = new Dictionary<GameObject, Material[]>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        if (robotLvl1 != null) _robots.Add("bot_lvl1", robotLvl1);
        if (robotLvl2 != null) _robots.Add("bot_lvl2", robotLvl2);

        // Hide all initially
        foreach (var bot in _robots.Values)
        {
            bot.SetActive(false);
            CaptureOriginalMaterials(bot);
        }
    }

    private void Update()
    {
        if (_isShowcaseActive && _activeBot != null)
        {
            _activeBot.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }

    public void ShowShowcase(bool active)
    {
        _isShowcaseActive = active;
        if (!active && _activeBot != null)
        {
            _activeBot.SetActive(false);
        }
    }

    public void SelectBot(string robotId, bool isUnlocked)
    {
        // Hide current
        if (_activeBot != null)
        {
            _activeBot.SetActive(false);
        }

        if (_robots.TryGetValue(robotId, out GameObject bot))
        {
            _activeBot = bot;
            _activeBot.SetActive(true);
            _activeBot.transform.position = displayPosition;
            _activeBot.transform.rotation = Quaternion.Euler(displayRotation);
            
            ApplyVisualMode(bot, isUnlocked);
        }
        else
        {
            Debug.LogWarning($"[ForgeManager] Robot ID '{robotId}' not found in registry.");
        }
    }

    private void CaptureOriginalMaterials(GameObject bot)
    {
        var renderers = bot.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (!_originalMaterials.ContainsKey(r.gameObject))
            {
                _originalMaterials.Add(r.gameObject, r.sharedMaterials);
            }
        }
    }

    private void ApplyVisualMode(GameObject bot, bool isUnlocked)
    {
        var renderers = bot.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (isUnlocked)
            {
                if (_originalMaterials.TryGetValue(r.gameObject, out Material[] originals))
                {
                    r.materials = originals;
                }
            }
            else if (silhouetteMaterial != null)
            {
                Material[] silhouettes = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < silhouettes.Length; i++)
                {
                    silhouettes[i] = silhouetteMaterial;
                }
                r.materials = silhouettes;
            }
        }
    }
}
