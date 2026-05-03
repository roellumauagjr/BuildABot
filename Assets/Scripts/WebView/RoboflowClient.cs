using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP client for the Roboflow Serverless API.
/// </summary>
public class RoboflowClient : MonoBehaviour
{
    // ─── API Configuration ────────────────────────────────────────────────

    private const string WorkspaceId = "roels-workspace-hpy2g";
    private const string WorkflowId  = "yolo-world-large-demo";
    private const string CloudApiUrl = "https://serverless.roboflow.com";

    [Header("Authentication")]
    [SerializeField] private string apiKey = "oUkW3KckJB1t2FZlqp37";

    // ─── Inspector Fields ─────────────────────────────────────────────────

    [Tooltip("Confidence threshold — detections below this are ignored.")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float minConfidence = 0.50f;

    // ─── Target Classes ───────────────────────────────────────────────────
    // Very descriptive phrases are crucial — YOLO-World is an open-vocabulary
    // model and responds to natural language. More specific = less ambiguity.

    private static readonly string[] TargetClasses =
    {
        // ── Plastic Bottles ─────────────────────────────────────────────
        "transparent plastic water bottle",
        "plastic soda bottle with label",
        "crushed plastic bottle",

        // ── Aluminum / Metal Cans ────────────────────────────────────────
        "aluminum soda can",
        "crushed metal beverage can",
        "tin food can",
        "cylindrical metal can",

        // ── Cups / Mugs ──────────────────────────────────────────────────
        "disposable paper coffee cup",
        "white styrofoam cup",
        "ceramic coffee mug",

        // ── Paper ────────────────────────────────────────────────────────

        // ── Straws ───────────────────────────────────────────────────────
        "thin plastic drinking straw",
        "paper straw tube",

        // ── Context / Non-recyclable ─────────────────────────────────────
        "person", "hand", "table", "chair", "phone", "laptop",
        "potted plant", "wall", "floor", "ceiling", "window",
        "shoe", "clothing", "face", "glasses", "carpet", "door"
    };

    // ─── Singleton ────────────────────────────────────────────────────────

    public static RoboflowClient Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Submit a camera frame to the YOLO-World workflow and invoke onResult
    /// on the main thread when a result arrives (or on error).
    /// The caller owns <paramref name="texture"/> and should Destroy() it after.
    /// </summary>
    public void ScanTexture(Texture2D texture, Action<ScanDetection> onResult)
    {
        StartCoroutine(RunWorkflow(texture, onResult));
    }

    // ─── Internals ────────────────────────────────────────────────────────

    private Texture2D DownscaleTexture(Texture2D source, int maxSize)
    {
        int width = source.width;
        int height = source.height;
        if (Mathf.Max(width, height) <= maxSize) return source;

        float ratio = (float)maxSize / Mathf.Max(width, height);
        int newWidth = Mathf.RoundToInt(width * ratio);
        int newHeight = Mathf.RoundToInt(height * ratio);

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Bilinear;
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D scaled = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        scaled.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        scaled.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return scaled;
    }

    private IEnumerator RunWorkflow(Texture2D texture, Action<ScanDetection> onResult)
    {
        // 1. Downscale to max 640px to prevent main thread freeze during EncodeToJPG
        Texture2D scaledTex = DownscaleTexture(texture, 640);
        
        // 2. Encode texture → JPEG (quality 90 for better accuracy) → base64
        byte[] imageBytes = scaledTex.EncodeToJPG(90);
        string base64     = Convert.ToBase64String(imageBytes);

        if (scaledTex != texture) Destroy(scaledTex);

        // 2. Build JSON body
        string classesJson = BuildClassesJson(TargetClasses);
        string body = $@"{{
  ""api_key"": ""{apiKey}"",
  ""inputs"": {{
    ""image"": {{""type"": ""base64"", ""value"": ""{base64}""}}
  }},
  ""parameters"": {{
    ""classes"": {classesJson}
  }},
  ""use_cache"": true
}}";

        // 3. Build request
        string endpoint  = $"{CloudApiUrl}/infer/workflows/{WorkspaceId}/{WorkflowId}";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

        Debug.Log($"[RoboflowClient] POST → cloud  |  image: {imageBytes.Length / 1024}KB");

        using var request = new UnityWebRequest(endpoint, "POST");
        request.uploadHandler   = new UploadHandlerRaw(bodyBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 25;

        // 4. Send and wait
        float t0 = Time.realtimeSinceStartup;
        yield return request.SendWebRequest();
        float elapsed = Time.realtimeSinceStartup - t0;

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[RoboflowClient] ✗ {request.responseCode}: {request.error} ({elapsed:F2}s) [cloud]");
            onResult?.Invoke(null);
            yield break;
        }

        Debug.Log($"[RoboflowClient] ✓ {elapsed:F2}s  ({request.downloadedBytes} bytes)  [cloud]");

        // 5. Parse and return best detection
        string json         = request.downloadHandler.text;
        ScanDetection best  = ParseBestDetection(json, minConfidence);
        onResult?.Invoke(best);
    }

    // ─── JSON helpers ─────────────────────────────────────────────────────

    private static string BuildClassesJson(string[] classes)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < classes.Length; i++)
        {
            sb.Append('"');
            sb.Append(classes[i].Replace("\"", "\\\""));
            sb.Append('"');
            if (i < classes.Length - 1) sb.Append(',');
        }
        sb.Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Parse the workflow response and return the highest-scoring detection
    /// above minConfidence. Score = area × confidence (prefers large, confident objects).
    /// </summary>
    private static ScanDetection ParseBestDetection(string rawJson, float minConfidence)
    {
        // JsonUtility can't deserialise "class" (reserved keyword) so rename it first
        string json = rawJson.Replace("\"class\":", "\"class_label\":");

        WorkflowResponse response;
        try
        {
            response = JsonUtility.FromJson<WorkflowResponse>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoboflowClient] JSON parse error: {ex.Message}\nRaw: {rawJson.Substring(0, Mathf.Min(400, rawJson.Length))}");
            return null;
        }

        if (response?.outputs == null || response.outputs.Length == 0)
        {
            Debug.Log("[RoboflowClient] No outputs in response.");
            return null;
        }

        RawPrediction bestTarget = null;
        float maxTargetScore = -1f;

        // Default dimensions if none provided
        int imageW = 640;
        int imageH = 480;

        foreach (var output in response.outputs)
        {
            var predSet = output?.predictions;
            if (predSet == null) continue;

            if (predSet.image != null)
            {
                if (predSet.image.width  > 0) imageW = predSet.image.width;
                if (predSet.image.height > 0) imageH = predSet.image.height;
            }

            if (predSet.predictions == null) continue;

            foreach (var p in predSet.predictions)
            {
                // 1. Thresholding
                if (p.confidence < minConfidence) continue;

                bool isTarget = IsTargetClass(p.class_label);
                
                // 2. Log all detections for debugging
                Debug.Log($"[RoboflowClient] Found: '{p.class_label}' ({p.confidence:P0}) | Target={isTarget}");

                // 3. Scoring logic:
                if (isTarget)
                {
                    // For targets, use a mix of confidence and center-weighting
                    float centerX = p.x / imageW;
                    float centerY = p.y / imageH;
                    float distFromCenter = Vector2.Distance(new Vector2(centerX, centerY), new Vector2(0.5f, 0.5f));
                    float centerWeight = 1.0f - Mathf.Clamp01(distFromCenter);
                    
                    float score = p.confidence * (1.0f + centerWeight * 0.5f); // Boost items in the middle

                    if (score > maxTargetScore)
                    {
                        bestTarget = p;
                        maxTargetScore = score;
                    }
                }
            }
        }

        if (bestTarget == null)
        {
            Debug.Log($"[RoboflowClient] No target items found above {minConfidence:P0}.");
            return null;
        }

        Debug.Log($"[RoboflowClient] ★ Best Target: '{bestTarget.class_label}' ({bestTarget.confidence:P0})");

        return new ScanDetection
        {
            rawClass   = bestTarget.class_label,
            confidence = bestTarget.confidence,
            normX      = imageW > 0 ? (float)bestTarget.x / imageW : 0.5f,
            normY      = imageH > 0 ? (float)bestTarget.y / imageH : 0.5f,
        };
    }

    private static bool IsTargetClass(string label)
    {
        if (string.IsNullOrEmpty(label)) return false;
        label = label.ToLower();

        // Items we want to collect (Recyclables)
        if (label.Contains("bottle") || label.Contains("can") || label.Contains("cup") || label.Contains("mug") || label.Contains("straw"))
            return true;
        
        return false;
    }

    // ─── Response data models ─────────────────────────────────────────────

    [Serializable] private class WorkflowResponse { public OutputItem[]    outputs;     }
    [Serializable] private class OutputItem       { public PredictionSet   predictions; }
    [Serializable] private class PredictionSet    { public RawPrediction[] predictions; public ImageSize image; }
    [Serializable] private class ImageSize        { public int width, height; }
    [Serializable] private class RawPrediction
    {
        public string class_label;
        public int    class_id;
        public float  confidence;
        public float  x, y, width, height;
    }
}

/// <summary>
/// Parsed result from Roboflow YOLO-World.
/// Passed to ImageScanner which converts it to a UnityBridge payload.
/// </summary>
public class ScanDetection
{
    /// <summary>Raw YOLO-World class string, e.g. "aluminum soda can".</summary>
    public string rawClass;

    /// <summary>Model confidence (0–1).</summary>
    public float confidence;

    /// <summary>Detection centre in normalised image coordinates (0–1).</summary>
    public float normX, normY;
}
