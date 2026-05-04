using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// One-shot editor utility to replace the AR template's shape prefabs with our robot prefabs.
/// Run via menu: BuildABot > Wire Robot Prefabs.
/// This script is editor-only and is safe to leave in the project.
/// </summary>
public static class WireRobotPrefabs
{
    private const string ROBOT1_GUID = "3c91704e55cd7ee4f8b7e906a82ec1e5";
    private const string ROBOT2_GUID = "62fdf3eda0fc8e9428900c2ba1a31dbb";

    [MenuItem("BuildABot/Wire Robot Prefabs into ObjectSpawner")]
    public static void Execute()
    {
        var robot1 = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ROBOT1_GUID));
        var robot2 = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ROBOT2_GUID));

        if (robot1 == null) { Debug.LogError("[WireRobots] Robot1.prefab not found — check GUID."); return; }
        if (robot2 == null) { Debug.LogError("[WireRobots] Robot2.prefab not found — check GUID."); return; }

        var spawner = Object.FindObjectOfType<ObjectSpawner>();
        if (spawner == null) { Debug.LogError("[WireRobots] ObjectSpawner not found in scene."); return; }

        Undo.RecordObject(spawner, "Wire Robot Prefabs");

        // Replace the entire list with our two robots (index 0=Robot1, 1=Robot2)
        spawner.objectPrefabs = new List<GameObject> { robot1, robot2 };
        spawner.spawnOptionIndex = 0;

        EditorUtility.SetDirty(spawner);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[WireRobots] ObjectSpawner.objectPrefabs = [Robot1, Robot2]. Scene saved.");
    }
}
