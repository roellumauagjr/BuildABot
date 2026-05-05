using System;
using UnityEngine;

/// <summary>
/// Defines all message actions that can flow between React and Unity.
///
/// React → Unity:  React calls window.Unity.call(JSON) which triggers
///                 WebViewManager.OnMessageFromReact(). That method uses
///                 these enums to dispatch to the correct handler.
///
/// Unity → React:  Unity calls WebViewManager.SendToReact() which
///                 evaluates window.dispatchUnityEvent(action, payload) on
///                 the live WebView page.
/// </summary>
public static class UnityBridge
{
    // ─── Actions: React → Unity ───────────────────────────────────────────

    /// <summary>Spawn a bot at the target AR anchor.</summary>
    public const string SPAWN_BOT = "SPAWN_BOT";

    /// <summary>Remove a bot instance by its scene ID.</summary>
    public const string DESPAWN_BOT = "DESPAWN_BOT";

    /// <summary>
    /// React scanner screen mounted — start the AR image scanner.
    /// </summary>
    public const string START_SCAN = "START_SCAN";

    /// <summary>
    /// React scanner screen unmounted — stop the AR image scanner to save
    /// CPU/GPU and battery when the scan overlay is not visible.
    /// </summary>
    public const string STOP_SCAN = "STOP_SCAN";

    /// <summary>
    /// User tapped the shutter button — capture one frame and scan it.
    /// </summary>
    public const string CAPTURE_AND_SCAN = "CAPTURE_AND_SCAN";

    /// <summary>
    /// React captured a frame from getUserMedia() and encoded it as base64 JPEG.
    /// Payload: ScanFrameB64Payload.
    /// Used when the scanner page is in native camera mode (AR is OFF).
    /// </summary>
    public const string SCAN_FRAME_B64 = "SCAN_FRAME_B64";

    /// <summary>Request the current scan state from Unity.</summary>
    public const string REQUEST_SCAN_STATE = "REQUEST_SCAN_STATE";

    /// <summary>User tapped a recyclable category in the UI.</summary>
    public const string SELECT_CATEGORY = "SELECT_CATEGORY";

    /// <summary>User confirmed deploying a bot from the floating panel.</summary>
    public const string CONFIRM_DEPLOY = "CONFIRM_DEPLOY";

    /// <summary>User confirmed deploying the assembled bot to AR.</summary>
    public const string INITIATE_AR = "INITIATE_AR";

    /// <summary>Notify Unity of the current active UI page.</summary>
    public const string SET_PAGE = "SET_PAGE";

    /// <summary>Select a robot to display in the Forge preview.</summary>
    public const string SELECT_FORGE_BOT = "SELECT_FORGE_BOT";

    /// <summary>Select a robot from the AR drawer to be placed on tap.</summary>
    public const string SELECT_BOT = "SELECT_BOT";


    // ─── Actions: Unity → React ───────────────────────────────────────────

    /// <summary>
    /// AR scanner + YOLO-World API has identified a recyclable object.
    /// Payload: ScanCompletePayload.
    /// </summary>
    public const string SCAN_COMPLETE = "SCAN_COMPLETE";

    /// <summary>
    /// The API is processing a frame — React can show a loading indicator.
    /// </summary>
    public const string SCAN_PROCESSING = "SCAN_PROCESSING";

    /// <summary>
    /// No recyclable found in the current frame.
    /// </summary>
    public const string SCAN_EMPTY = "SCAN_EMPTY";

    /// <summary>Bot successfully placed in the AR scene.</summary>
    public const string BOT_SPAWNED = "BOT_SPAWNED";

    /// <summary>Bot was removed or recalled.</summary>
    public const string BOT_DESPAWNED = "BOT_DESPAWNED";

    /// <summary>
    /// Unity has captured a frame — send the base64 image to React for "freezing".
    /// Payload: FrameCapturedPayload.
    /// </summary>
    public const string FRAME_CAPTURED = "FRAME_CAPTURED";

    /// <summary>AR tracking was lost or regained.</summary>
    public const string TRACKING_CHANGED = "TRACKING_CHANGED";

    /// <summary>Generic error event — UI should show a toast/modal.</summary>
    public const string ERROR = "ERROR";

    /// <summary>
    /// Battle completed — send win/loss result and material rewards to React.
    /// Payload: BattleRewardPayload.
    /// </summary>
    public const string BATTLE_REWARD = "BATTLE_REWARD";

    // ─── Message envelope ─────────────────────────────────────────────────

    /// <summary>
    /// The JSON envelope used for all React → Unity messages.
    /// WebViewManager deserialises incoming JSON into this.
    /// </summary>
    [Serializable]
    public class Message
    {
        /// <summary>One of the SCREAMING_SNAKE_CASE action constants above.</summary>
        public string action;

        /// <summary>
        /// Arbitrary JSON payload string. Parse with JsonUtility inside handlers.
        /// </summary>
        public string payload;
    }

    // ─── Payload structs (React → Unity) ─────────────────────────────────

    [Serializable]
    public class SpawnBotPayload
    {
        /// <summary>Unique identifier from the React UI catalogue.</summary>
        public string botId;

        /// <summary>Material type string e.g. "PlasticBottle".</summary>
        public string type;

        /// <summary>Optional world-space hint position (if React has it).</summary>
        public float x, y, z;
    }

    [Serializable]
    public class SelectCategoryPayload
    {
        public string category; // e.g. "Plastic", "Metal", "Paper"
    }

    [Serializable]
    public class SetPagePayload
    {
        public string page; // "hub", "scan", "forge", "battle"
    }

    [Serializable]
    public class SelectForgeBotPayload
    {
        public string robotId;
        public bool isUnlocked;
    }

    [Serializable]
    public class SelectBotPayload
    {
        public string botId;
    }

    [Serializable]
    public class InitiateARPayload
    {
        public string botId;
    }

    /// <summary>
    /// Fired when YOLO-World identifies a recyclable in the camera frame.
    /// React uses `material` as the key into its MATERIAL_MAP catalogue.
    /// </summary>
    [Serializable]
    public class ScanCompletePayload
    {
        /// <summary>
        /// Normalised material key — maps to MATERIAL_MAP in ScannerScreen.jsx.
        /// e.g. "PlasticBottle", "MetalCan", "CardboardBox"
        /// </summary>
        public string material;

        /// <summary>Display-friendly name e.g. "Plastic Bottle".</summary>
        public string displayName;

        /// <summary>Raw YOLO-World class string e.g. "plastic bottle".</summary>
        public string rawClass;

        /// <summary>Confidence score from the YOLO-World model (0–1).</summary>
        public float confidence;

        /// <summary>Is the identified object recyclable?</summary>
        public bool isRecyclable;

        /// <summary>
        /// Bounding box centre in normalised image coordinates (0–1).
        /// Useful for AR hit-testing against the scan region.
        /// </summary>
        public float normX, normY;
    }

    [Serializable]
    public class BotSpawnedPayload
    {
        public int id;
        public string botType;
    }

    [Serializable]
    public class FrameCapturedPayload
    {
        public string base64Image;
    }

    [Serializable]
    public class TrackingChangedPayload
    {
        public bool isTracking;
        public string reason; // e.g. "LostTracking", "Resumed"
    }

    [Serializable]
    public class ErrorPayload
    {
        public string code;
        public string message;
    }

    /// <summary>
    /// Payload for SCAN_FRAME_B64: a base64-encoded JPEG image captured by React
    /// from the getUserMedia() video stream. Unity forwards this to Roboflow.
    /// </summary>
    [Serializable]
    public class ScanFrameB64Payload
    {
        /// <summary>Base64-encoded JPEG image (without the data:image/jpeg;base64, prefix).</summary>
        public string base64Image;
    }

    /// <summary>
    /// Fired when battle ends — sends material reward/penalty to React.
    /// Positive amount = reward (win), negative = penalty (loss).
    /// </summary>
    [Serializable]
    public class BattleRewardPayload
    {
        public bool won;           // true = player won, false = player lost
        public int plastic;        // amount to add (can be negative)
        public int metal;          // amount to add (can be negative)
        public int paper;          // amount to add (can be negative)
        public string message;     // display message for the player
    }
}
