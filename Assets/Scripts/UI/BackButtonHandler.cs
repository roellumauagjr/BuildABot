using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the Back Button click while in the AR Camera native UI.
/// Returns the player to the Battle landing page in React — NOT the hub.
/// The AR camera is stopped and the WebView is shown with the battle page.
/// </summary>
public class BackButtonHandler : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnBackButtonClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnBackButtonClicked);
    }

    private void OnBackButtonClicked()
    {
        Debug.Log("[BackButtonHandler] Back tapped — returning to Battle landing page.");

        // 1. Hide native AR UI overlay
        if (ARBotController.Instance != null)
            ARBotController.Instance.HideNativeUI();

        // 2. Stop AR camera (planes, tracking) to save battery
        if (ARBotController.Instance != null)
            ARBotController.Instance.StopAR();

        // 3. Clean up any placed robots and reset spawn-lock state
        //    so the next AR session starts fresh (player can place a new robot)
        var battleManager = FindObjectOfType<ARBattleManager>();
        if (battleManager != null)
            battleManager.CleanupOnExit();

        // 4. Show the WebView
        if (WebViewManager.Instance != null)
            WebViewManager.Instance.ShowWebView();

        // 5. Tell React to navigate back to the battle landing page
        if (WebViewManager.Instance != null)
            WebViewManager.Instance.NavigateToBattleLanding();
    }
}
