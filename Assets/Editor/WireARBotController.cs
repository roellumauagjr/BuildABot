using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.AR;

/// <summary>
/// One-shot editor utility to wire ARBotController Inspector slots and confirm scene wiring.
/// Run via: BuildABot > Wire ARBotController
/// </summary>
public static class WireARBotController
{
    [MenuItem("BuildABot/Wire ARBotController Slots")]
    public static void Execute()
    {
        // ── Find ARBotController ─────────────────────────────────────────────
        var controller = Object.FindObjectOfType<ARBotController>();
        if (controller == null)
        {
            // It's not in the scene yet. Create a GameManager object to host it.
            var go = new GameObject("GameManager");
            controller = go.AddComponent<ARBotController>();
            Debug.Log("[WireAR] Created GameManager with ARBotController.");
        }
        else
        {
            Debug.Log($"[WireAR] Found ARBotController on '{controller.gameObject.name}'.");
        }

        Undo.RecordObject(controller, "Wire ARBotController");

        // ── Wire ObjectSpawner ───────────────────────────────────────────────
        var spawner = Object.FindObjectOfType<ObjectSpawner>();
        if (spawner != null)
        {
            var spawnerField = typeof(ARBotController).GetField("objectSpawner",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            spawnerField?.SetValue(controller, spawner);
            Debug.Log($"[WireAR] Wired ObjectSpawner: {spawner.gameObject.name}");
        }
        else Debug.LogError("[WireAR] ObjectSpawner not found!");

        // ── Wire ARTemplateMenuManager ───────────────────────────────────────
        var menuMgr = Object.FindObjectOfType<ARTemplateMenuManager>();
        if (menuMgr != null)
        {
            var menuField = typeof(ARBotController).GetField("templateMenuManager",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            menuField?.SetValue(controller, menuMgr);
            Debug.Log($"[WireAR] Wired ARTemplateMenuManager: {menuMgr.gameObject.name}");
        }
        else Debug.Log("[WireAR] ARTemplateMenuManager not found (OK if not in scene).");

        EditorUtility.SetDirty(controller);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[WireAR] Done. Scene saved.");
    }

    [MenuItem("BuildABot/Check Scene Wiring")]
    public static void CheckWiring()
    {
        var ctrl    = Object.FindObjectOfType<ARBotController>();
        var spawner = Object.FindObjectOfType<ObjectSpawner>();
        var menu    = Object.FindObjectOfType<ARTemplateMenuManager>();
        var arPMgr  = Object.FindObjectOfType<UnityEngine.XR.ARFoundation.ARPlaneManager>();

        Debug.Log("==== BuildABot Scene Wiring Check ====");
        Debug.Log($"ARBotController : {(ctrl    != null ? ctrl.gameObject.name    : "MISSING")}");
        Debug.Log($"ObjectSpawner   : {(spawner != null ? spawner.gameObject.name : "MISSING")}");
        if (spawner != null)
        {
            Debug.Log($"  objectPrefabs count = {spawner.objectPrefabs.Count}");
            for (int i = 0; i < spawner.objectPrefabs.Count; i++)
                Debug.Log($"  [{i}] = {(spawner.objectPrefabs[i] != null ? spawner.objectPrefabs[i].name : "NULL")}");
        }
        Debug.Log($"ARTemplateMenu  : {(menu    != null ? menu.gameObject.name    : "MISSING")}");
        Debug.Log($"ARPlaneManager  : {(arPMgr  != null ? arPMgr.gameObject.name  : "MISSING")}");
        if (arPMgr != null)
            Debug.Log($"  planePrefab = {(arPMgr.planePrefab != null ? arPMgr.planePrefab.name : "NULL")}");
    }
}
