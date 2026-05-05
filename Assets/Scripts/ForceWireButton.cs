using UnityEngine;
using UnityEngine.UI;

public class ForceWireButton : MonoBehaviour
{
    void Start()
    {
        var cancelGO = GameObject.Find("UI/Object Menu Animator/Object Menu/Cancel Button");
        if (cancelGO != null)
        {
            var button = cancelGO.GetComponent<Button>();
            if (button != null)
            {
                var gameManager = GameObject.Find("GameManager");
                if (gameManager != null)
                {
                    var battleManager = gameManager.GetComponent<ARBattleManager>();
                    if (battleManager != null)
                    {
                        button.onClick.RemoveAllListeners();
                        button.onClick.AddListener(battleManager.OnCancelButtonClicked);
                    }
                }
            }
        }
    }
}
