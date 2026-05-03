using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class InternetPermissionSetter
{
    static InternetPermissionSetter()
    {
        // Force internet permission for Android to ensure Roboflow/YOLO-World API works
        if (PlayerSettings.Android.forceInternetPermission != true)
        {
            PlayerSettings.Android.forceInternetPermission = true;
            Debug.Log("[InternetPermissionSetter] Android Internet Permission forced to TRUE.");
        }
    }
    
    [MenuItem("BuildABot/Force Internet Permission")]
    public static void ForcePermission()
    {
        PlayerSettings.Android.forceInternetPermission = true;
        Debug.Log("[InternetPermissionSetter] Android Internet Permission manually forced to TRUE.");
    }
}
