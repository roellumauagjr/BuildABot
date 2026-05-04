using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Switches the ARPlaneManager to use the XRI AR Starter Assets "AR Feathered Plane" prefab
/// which uses FeatheredPlaneShader.shader — a custom HLSL that compiles correctly for Android
/// and gives the yellow feathered grid look from the AR Mobile Template.
///
/// Also adds that shader to Project Settings → Graphics → Always Included Shaders so it
/// is never stripped from the Android build.
///
/// Run via: BuildABot > Fix AR Plane Shader
/// </summary>
public static class FixARPlaneShader
{
    private const string FEATHERED_PLANE_GUID = "a6b7ca1d53c75490595d1f0d5f43be38";
    private const string SHADER_GUID          = "a78405e91de6b4166aa290ef5fd21148";

    [MenuItem("BuildABot/Fix AR Plane Shader")]
    public static void Execute()
    {
        // ── 1. Switch ARPlaneManager to the working prefab ─────────────────
        var planePrefabPath = AssetDatabase.GUIDToAssetPath(FEATHERED_PLANE_GUID);
        var planePrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(planePrefabPath);

        if (planePrefab == null)
        {
            Debug.LogError("[FixARPlane] 'AR Feathered Plane.prefab' not found. " +
                           "Import 'AR Starter Assets' sample from XR Interaction Toolkit.");
            return;
        }

        var planeManager = Object.FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            Debug.LogError("[FixARPlane] ARPlaneManager not found in scene.");
            return;
        }

        Undo.RecordObject(planeManager, "Fix AR Plane Prefab");
        planeManager.planePrefab = planePrefab.GetComponent<ARPlane>() != null
            ? planePrefab : planePrefab;

        // ARPlaneManager.planePrefab expects a GameObject prefab
        var field = typeof(ARPlaneManager).GetField(
            "m_PlanePrefab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(planeManager, planePrefab);

        EditorUtility.SetDirty(planeManager);
        Debug.Log($"[FixARPlane] ARPlaneManager.planePrefab → '{planePrefab.name}'");

        // ── 2. Add FeatheredPlaneShader to Always Included Shaders ─────────
        var shaderPath = AssetDatabase.GUIDToAssetPath(SHADER_GUID);
        var shader     = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

        if (shader == null)
        {
            Debug.LogWarning($"[FixARPlane] FeatheredPlaneShader not found at GUID {SHADER_GUID}. Skipping.");
        }
        else
        {
            // Load GraphicsSettings.asset directly as a plain Object via SerializedObject
            var gsAsset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gsAsset == null)
            {
                Debug.LogWarning("[FixARPlane] Could not load GraphicsSettings.asset. Skipping Always Included step.");
            }
            else
            {
                var so   = new SerializedObject(gsAsset);
                var prop = so.FindProperty("m_AlwaysIncludedShaders");

                bool alreadyPresent = false;
                for (int i = 0; i < prop.arraySize; i++)
                {
                    if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    { alreadyPresent = true; break; }
                }

                if (!alreadyPresent)
                {
                    prop.arraySize++;
                    prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = shader;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[FixARPlane] Added '{shader.name}' to Always Included Shaders.");
                }
                else
                {
                    Debug.Log($"[FixARPlane] '{shader.name}' already in Always Included Shaders.");
                }
            }
        }


        // ── 3. Save scene ──────────────────────────────────────────────────
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[FixARPlane] Done. Scene saved.");
    }
}
