using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// ImageScanner — drives the AR camera capture + YOLO-World detection pipeline.
///
/// Lifecycle:
///   1. React sends START_SCAN → WebViewManager calls StartScanning().
///   2. Every `scanInterval` seconds, if IsScanning and no request is in flight,
///      grab the latest AR camera frame, downsample to `captureWidth × captureHeight`,
///      send to RoboflowClient.
///   3. On result → map raw class to our material system → SendToReact SCAN_COMPLETE.
///   4. Pauses scanning after a confident detection (waits for React to navigate away,
///      which triggers STOP_SCAN, or waits `pauseAfterDetection` seconds then resumes).
///   5. React sends STOP_SCAN → WebViewManager calls StopScanning().
///
/// Attach to the same GameObject as WebViewManager (or any persistent GO).
/// Assign the ARCameraManager reference in the Inspector.
/// </summary>
[RequireComponent(typeof(RoboflowClient))]
public class ImageScanner : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────

    [Header("AR Camera")]
    [Tooltip("Drag the XR Origin's AR Camera component here.")]
    [SerializeField] private ARCameraManager cameraManager;

    [Header("Capture settings")]
    [Tooltip("Downsampled width sent to the API. Smaller = faster, lower accuracy.")]
    [SerializeField] private int captureWidth  = 480;
    [Tooltip("Downsampled height sent to the API.")]
    [SerializeField] private int captureHeight = 640;
    [Tooltip("JPEG quality (1–100). Lower = faster upload.")]
    [SerializeField] [Range(40, 95)] private int jpegQuality = 70;

    [Header("Timing")]
    [Tooltip("Seconds between scan attempts when IsScanning is true.")]
    [SerializeField] [Range(0.5f, 10f)] private float scanInterval = 2.0f;
    [Tooltip("Seconds to pause scanning after a successful detection before resuming.")]
    [SerializeField] [Range(0f, 30f)]  private float pauseAfterDetection = 8.0f;
    [Tooltip("If true, scanning resumes automatically after pauseAfterDetection. If false, React must send START_SCAN again.")]
    [SerializeField] private bool autoResumeAfterDetection = true;

    [Header("Confidence")]
    [Tooltip("Detections below this threshold are discarded.")]
    [SerializeField] [Range(0.1f, 0.95f)] private float minimumConfidence = 0.40f;

    // ─── Runtime state ────────────────────────────────────────────────────

    public bool  IsScanning         { get; private set; } = false;
    public bool  IsRequestInFlight  { get; private set; } = false;

    private RoboflowClient _client;
    private ScanDetection  _lastDetection;

    // ─── Singleton ────────────────────────────────────────────────────────

    public static ImageScanner Instance { get; private set; }

    // ─── Unity lifecycle ──────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _client  = GetComponent<RoboflowClient>();
    }

    private void Start()
    {
        // Auto-find ARCameraManager if not assigned in Inspector
        if (cameraManager == null)
        {
            cameraManager = FindObjectOfType<ARCameraManager>();
            if (cameraManager == null)
                Debug.LogWarning("[ImageScanner] No ARCameraManager found in scene. Frame capture will be unavailable.");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public control ───────────────────────────────────────────────────

    private Coroutine _scanLoop;

    /// <summary>Begin the scan loop. Safe to call if already scanning.</summary>
    public void StartScanning()
    {
        if (IsScanning) return;
        IsScanning = true;
        Debug.Log("[ImageScanner] Manual scanning mode enabled.");
    }

    /// <summary>Immediately capture one frame and scan it.</summary>
    public void CaptureAndScan()
    {
        if (IsRequestInFlight) return;
        Debug.Log("[ImageScanner] Reverting to Capture and Scan (One-Shot).");
        StopScanning(); // Ensure live loop is off
        StartCoroutine(CaptureAndSend(oneShot: true));
    }

    /// <summary>Stop scanning mode. Safe to call if not scanning.</summary>
    public void StopScanning()
    {
        if (!IsScanning) return;
        IsScanning = false;
        Debug.Log("[ImageScanner] Scanning mode disabled.");
        _lastDetection = null;
    }

    // ─── React-side frame scanning ─────────────────────────────────────────
    // Used when Scanner page is in native camera mode (AR is OFF).
    // React captures a frame from getUserMedia() <video> via canvas.toDataURL()
    // and sends it as SCAN_FRAME_B64. We decode and forward to Roboflow.

    /// <summary>
    /// Accept a base64-encoded JPEG captured by React from its getUserMedia()
    /// video stream. Decode, send to Roboflow, fire SCAN_COMPLETE or SCAN_EMPTY.
    /// </summary>
    public void ScanBase64(string base64Jpeg)
    {
        if (IsRequestInFlight)
        {
            Debug.Log("[ImageScanner] ScanBase64 skipped — request in flight.");
            return;
        }

        // Strip the data URL prefix if React included it
        const string prefix = "data:image/jpeg;base64,";
        if (base64Jpeg != null && base64Jpeg.StartsWith(prefix))
            base64Jpeg = base64Jpeg.Substring(prefix.Length);

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Jpeg);
        }
        catch (Exception e)
        {
            Debug.LogError("[ImageScanner] ScanBase64 — base64 decode failed: " + e.Message);
            WebViewManager.Instance?.SendToReact(UnityBridge.SCAN_EMPTY);
            return;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (!texture.LoadImage(imageBytes))
        {
            Debug.LogError("[ImageScanner] ScanBase64 — LoadImage failed.");
            Destroy(texture);
            WebViewManager.Instance?.SendToReact(UnityBridge.SCAN_EMPTY);
            return;
        }

        Debug.Log($"[ImageScanner] ScanBase64 — decoded {texture.width}×{texture.height}, forwarding to Roboflow.");
        WebViewManager.Instance?.SendToReact(UnityBridge.SCAN_PROCESSING);

        IsRequestInFlight = true;
        ScanDetection scanResult = null;
        bool done = false;

        _client.ScanTexture(texture, (det) => { scanResult = det; done = true; });

        StartCoroutine(WaitForB64Result(texture, () => done, () => scanResult));
    }

    private System.Collections.IEnumerator WaitForB64Result(
        Texture2D tex,
        System.Func<bool> isDone,
        System.Func<ScanDetection> getResult)
    {
        yield return new WaitUntil(isDone);

        IsRequestInFlight = false;
        Destroy(tex);

        var result = getResult();
        if (result == null || result.confidence < minimumConfidence)
        {
            WebViewManager.Instance?.SendToReact(UnityBridge.SCAN_EMPTY);
            yield break;
        }

        FireScanComplete(result);
    }

    // ─── Scan logic ───────────────────────────────────────────────────────

    private IEnumerator CaptureAndSend(bool oneShot)
    {
        // 1. Guard against missing camera
        if (cameraManager == null) yield break;

        // 2. Grab the latest CPU image from AR camera
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            Debug.LogWarning("[ImageScanner] Failed to acquire CPU image from AR camera.");
            yield break;
        }

        // Notify React that a scan is processing
        WebViewManager.Instance?.SendToReact(UnityBridge.SCAN_PROCESSING);

        // Convert and downsample the AR frame
        Texture2D texture;
        using (image)
        {
            var convParams = new XRCpuImage.ConversionParams
            {
                inputRect        = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(captureWidth, captureHeight),
                outputFormat     = TextureFormat.RGB24,
                transformation   = XRCpuImage.Transformation.MirrorY,
            };

            int bufferSize = image.GetConvertedDataSize(convParams);
            var buffer     = new NativeArray<byte>(bufferSize, Allocator.TempJob);

            image.Convert(convParams, buffer);

            texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            texture.LoadRawTextureData(buffer);
            texture.Apply();
            buffer.Dispose();

            // Rotate 90° CW to convert landscape sensor buffer → portrait display
            texture = RotateTexture90CW(texture);

            // 3. Export as Base64 for the UI freeze-frame effect
            byte[] jpgBytes = texture.EncodeToJPG(jpegQuality);
            string base64 = Convert.ToBase64String(jpgBytes);
            WebViewManager.Instance?.SendToReact(UnityBridge.FRAME_CAPTURED, new UnityBridge.FrameCapturedPayload { base64Image = base64 });
        }

        // Ship to Roboflow API
        IsRequestInFlight = true;
        ScanDetection result = null;
        bool done = false;

        _client.ScanTexture(texture, (det) =>
        {
            result = det;
            done   = true;
        });

        yield return new WaitUntil(() => done);

        IsRequestInFlight = false;
        Destroy(texture);

        if (result == null || result.confidence < minimumConfidence)
        {
            // Only notify "empty" if this was a manual one-shot, 
            // otherwise just continue loop silently
            if (oneShot) WebViewManager.Instance?.SendToReact(UnityBridge.SCAN_EMPTY);
            yield break;
        }

        // We have a confident detection — map and broadcast to React
        var mapping = MapToMaterial(result.rawClass);

        // --- NEW LOGIC: Pause/Stop scanning once identified ---
        if (mapping.isRecyclable)
        {
            _lastDetection = result;
            FireScanComplete(result);
            StopScanning();
        }
        else
        {
            // Even if not recyclable, if we're in one-shot mode, we should notify React
            // so it can show the "Not Recyclable" state in the UI
            FireScanComplete(result);
            StopScanning();
        }
    }

    private void FireScanComplete(ScanDetection det)
    {
        var mapping = MapToMaterial(det.rawClass);

        var payload = new UnityBridge.ScanCompletePayload
        {
            material      = mapping.material,
            displayName   = mapping.displayName,
            isRecyclable  = mapping.isRecyclable,
            rawClass      = det.rawClass,
            confidence    = det.confidence,
            normX         = det.normX,
            normY         = det.normY,
        };

        Debug.Log($"[ImageScanner] SCAN_COMPLETE → {mapping.material} (isRecyclable={mapping.isRecyclable}) raw=\"{det.rawClass}\"");
        WebViewManager.Instance?.SendToReact(UnityBridge.SCAN_COMPLETE, payload);
    }

    // ─── Material mapping ─────────────────────────────────────────────────

    private static readonly Dictionary<string, (string material, string displayName, bool isRecyclable)> ClassMap =
        new Dictionary<string, (string, string, bool)>(StringComparer.OrdinalIgnoreCase)
    {
        // ── Plastic Bottles ─────────────────────────────────────────────────
        // material = "plastic"  matches LootReward.tsx materialData key
        { "transparent plastic water bottle", ("plastic", "Plastic Bottle", true) },
        { "plastic soda bottle with label",   ("plastic", "Plastic Bottle", true) },
        { "crushed plastic bottle",           ("plastic", "Plastic Bottle", true) },
        { "plastic bottle",                   ("plastic", "Plastic Bottle", true) },
        { "bottle",                           ("plastic", "Plastic Bottle", true) },

        // ── Aluminum / Metal Cans ────────────────────────────────────────────
        // material = "metal"  matches LootReward.tsx materialData key
        { "aluminum soda can",                ("metal",   "Aluminum Can",   true) },
        { "crushed metal beverage can",       ("metal",   "Aluminum Can",   true) },
        { "tin food can",                     ("metal",   "Aluminum Can",   true) },
        { "cylindrical metal can",            ("metal",   "Aluminum Can",   true) },
        { "metal can",                        ("metal",   "Aluminum Can",   true) },
        { "can",                              ("metal",   "Aluminum Can",   true) },

        // ── Cups ─────────────────────────────────────────────────────────────
        // material = "paper"  matches LootReward.tsx materialData key
        { "disposable paper coffee cup",      ("paper",   "Paper Cup",      true) },
        { "white styrofoam cup",              ("paper",   "Paper Cup",      true) },
        { "ceramic coffee mug",              ("paper",   "Ceramic Mug",    false) },
        { "cup",                              ("paper",   "Cup",            true) },
        { "mug",                              ("paper",   "Mug",            true) },

        // ── Paper / Cardboard ─────────────────────────────────────────────────
        { "sheet of white paper",             ("paper",   "Paper",          true) },
        { "crumpled paper ball",              ("paper",   "Paper",          true) },
        { "cardboard box piece",              ("paper",   "Cardboard",      true) },
        { "paper",                            ("paper",   "Paper",          true) },

        // ── Non-Recyclables / Negative Context ───────────────────────────────
        { "person",      ("none", "Person",   false) },
        { "hand",        ("none", "Hand",     false) },
        { "table",       ("none", "Table",    false) },
        { "chair",       ("none", "Chair",    false) },
        { "laptop",      ("none", "Laptop",   false) },
        { "phone",       ("none", "Phone",    false) },
        { "potted plant",("none", "Plant",    false) },
        { "wall",        ("none", "Wall",     false) },
        { "floor",       ("none", "Floor",    false) },
        { "ceiling",     ("none", "Ceiling",  false) },
        { "window",      ("none", "Window",   false) },
        { "shoe",        ("none", "Shoe",     false) },
        { "clothing",    ("none", "Clothing", false) },
        { "face",        ("none", "Face",     false) },
        { "glasses",     ("none", "Glasses",  false) },
        { "carpet",      ("none", "Carpet",   false) },
        { "door",        ("none", "Door",     false) },
    };

    private static MaterialInfo MapToMaterial(string label)
    {
        label = label.ToLower().Trim();

        // 1. Try exact match in ClassMap
        if (ClassMap.TryGetValue(label, out var info))
        {
            return new MaterialInfo { displayName = info.displayName, material = info.material, isRecyclable = info.isRecyclable };
        }

        // 2. Try partial match in ClassMap
        foreach (var kvp in ClassMap)
        {
            if (label.Contains(kvp.Key))
            {
                return new MaterialInfo { displayName = kvp.Value.displayName, material = kvp.Value.material, isRecyclable = kvp.Value.isRecyclable };
            }
        }

        // 3. Fallback for other objects
        string literalName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);
        return new MaterialInfo { displayName = literalName, material = "Generic", isRecyclable = false };
    }

    private struct MaterialInfo
    {
        public string displayName;
        public string material;
        public bool isRecyclable;
    }

    // ─── Texture rotation ─────────────────────────────────────────────────
    // AR camera frames arrive in landscape (native sensor). Rotate 90° CW
    // so the freeze-frame shown in the UI matches portrait orientation.

    private static Texture2D RotateTexture90CW(Texture2D src)
    {
        int w = src.width;
        int h = src.height;
        // Output has swapped dimensions: h wide, w tall
        Texture2D dst = new Texture2D(h, w, src.format, false);
        Color32[] srcPixels = src.GetPixels32();
        Color32[] dstPixels = new Color32[h * w];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 90° CW: dst(h-1-y, x) = src(x, y)
                // In array terms (row-major, origin bottom-left):
                dstPixels[(h - 1 - y) + x * h] = srcPixels[x + y * w];
            }
        }

        dst.SetPixels32(dstPixels);
        dst.Apply();
        Destroy(src); // free the original
        return dst;
    }
}
