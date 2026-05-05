using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tool to verify ARBattleManager wiring for the simplified battle system.
/// Cancel Button is now the battle trigger - no more StartBattleButton needed!
/// </summary>
public class WireBattleManager : EditorWindow
{
    [MenuItem("Tools/Wire Battle Manager")]
    public static void ShowWindow()
    {
        GetWindow<WireBattleManager>("Wire Battle Manager");
    }

    private void OnGUI()
    {
        GUILayout.Label("AR Battle System - Simplified", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "New System:\n" +
            "• Place ANY number of robots\n" +
            "• Cancel Button = BATTLE!\n" +
            "• Win = +5-10 materials\n" +
            "• Lose = -1-5 materials\n" +
            "• Status shows on screen (no logs needed)", 
            MessageType.Info);
        GUILayout.Space(10);

        if (GUILayout.Button("Verify Wiring"))
        {
            VerifyWiring();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Wire Cancel Button (Battle Trigger)"))
        {
            WireCancelButton();
        }
    }

    private void VerifyWiring()
    {
        var gameManager = GameObject.Find("GameManager")?.GetComponent<ARBattleManager>();
        if (gameManager == null)
        {
            Debug.LogError("ARBattleManager not found on GameManager!");
            return;
        }

        bool hasSpawner = gameManager.spawner != null;
        bool hasFadeScreen = gameManager.fadeScreen != null;
        bool hasOpponentText = gameManager.opponentText != null;
        bool hasBattleLogText = gameManager.battleLogText != null;
        bool hasCancelButton = gameManager.cancelButton != null;

        Debug.Log("╔══════════════════════════════════════════════════╗");
        Debug.Log("║     ARBattleManager WIRING (Simplified)          ║");
        Debug.Log("╠══════════════════════════════════════════════════╣");
        Debug.Log($"║  Spawner:        {(hasSpawner ? "✓ OK" : "✗ MISSING"),-23} ║");
        Debug.Log($"║  FadeScreen:     {(hasFadeScreen ? "✓ OK" : "✗ MISSING"),-23} ║");
        Debug.Log($"║  OpponentText:    {(hasOpponentText ? "✓ OK" : "✗ MISSING"),-23} ║");
        Debug.Log($"║  BattleLogText:  {(hasBattleLogText ? "✓ OK" : "✗ MISSING"),-23} ║");
        Debug.Log($"║  CancelButton:   {(hasCancelButton ? "✓ OK" : "✗ MISSING"),-23} ║");
        Debug.Log("╚══════════════════════════════════════════════════╝");

        if (hasSpawner && hasFadeScreen && hasOpponentText && hasBattleLogText && hasCancelButton)
        {
            Debug.Log("<color=green>✓✓✓ ALL REFERENCES WIRED! READY FOR BATTLE! ✓✓✓</color>");
        }
        else
        {
            Debug.LogWarning("<color=orange>Some references missing. Try 'Wire Cancel Button' button.</color>");
        }
    }

    private void WireCancelButton()
    {
        var gameManager = GameObject.Find("GameManager")?.GetComponent<ARBattleManager>();
        
        if (gameManager == null)
        {
            Debug.LogError("ARBattleManager not found!");
            return;
        }
        
        // Find Cancel Button (child of Object Menu)
        Button cancelButton = null;
        var cancelGO = GameObject.Find("UI/Object Menu Animator/Object Menu/Cancel Button");
        if (cancelGO != null)
            cancelButton = cancelGO.GetComponent<Button>();

        if (cancelButton == null)
        {
            Debug.LogError("Cancel Button not found at UI/Object Menu Animator/Object Menu/Cancel Button!");
            return;
        }
        
        // Find AR Session and AR Camera
        var arSession = GameObject.Find("AR Session");
        Camera arCamera = null;
        var arCamGO = GameObject.Find("AR Camera");
        if (arCamGO != null)
            arCamera = arCamGO.GetComponent<Camera>();

        // Clear existing listeners and add battle trigger
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(gameManager.OnCancelButtonClicked);
        
        // Change text to "START BATTLE"
        foreach (Transform child in cancelButton.transform)
        {
            if (child.name == "Text")
            {
                child.gameObject.SetActive(true);
                var textComp = child.GetComponent<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = "BATTLE!";
                    EditorUtility.SetDirty(textComp);
                }
                break;
            }
        }
        
        // Find Status Text for Android debugging (try multiple paths)
        var statusTextGO = GameObject.Find("UI/StatusText");
        if (statusTextGO == null) statusTextGO = GameObject.Find("StatusText");
        TextMeshProUGUI statusText = null;
        if (statusTextGO != null)
            statusText = statusTextGO.GetComponent<TextMeshProUGUI>();
        
        // Assign fields via reflection
        var field = typeof(ARBattleManager).GetField("cancelButton", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field != null)
            field.SetValue(gameManager, cancelButton);
        
        var arSessionField = typeof(ARBattleManager).GetField("arSession", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (arSessionField != null)
            arSessionField.SetValue(gameManager, arSession);
        
        var arCameraField = typeof(ARBattleManager).GetField("arCamera", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (arCameraField != null)
            arCameraField.SetValue(gameManager, arCamera);
        
        var statusField = typeof(ARBattleManager).GetField("statusText", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (statusField != null)
            statusField.SetValue(gameManager, statusText);
        
        EditorUtility.SetDirty(gameManager);
        EditorUtility.SetDirty(cancelButton);
    }
}
