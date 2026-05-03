"""
BuildABot — Roboflow Local Inference Test
-----------------------------------------
Run this to verify the local server is working before deploying to the device.

Usage:
  python test_scan.py path/to/image.jpg
  python test_scan.py  # (uses a placeholder test image)
"""

import sys, json
from inference_sdk import InferenceHTTPClient

# ── Config ──────────────────────────────────────────────────────────────────
LOCAL_URL   = "http://localhost:9001"
API_KEY     = "oUkW3KckJB1t2FZlqp37"
WORKSPACE   = "roels-workspace-hpy2g"
WORKFLOW    = "yolo-world-medium-demo"

TARGET_CLASSES = [
    "plastic water bottle", "plastic soda bottle", "clear plastic bottle",
    "aluminum soda can", "aluminum beer can", "metal beverage can", "tin can",
    "paper coffee cup", "disposable paper cup", "plastic cup",
    "paper", "newspaper", "sheet of paper",
    "plastic drinking straw", "paper straw",
    "bottle", "can", "cup", "straw",
]

# ── Connect ──────────────────────────────────────────────────────────────────
client = InferenceHTTPClient(api_url=LOCAL_URL, api_key=API_KEY)

# ── Image ────────────────────────────────────────────────────────────────────
image_path = sys.argv[1] if len(sys.argv) > 1 else "YOUR_IMAGE.jpg"

print(f"\n[Test] Sending '{image_path}' to {LOCAL_URL}\n")

try:
    result = client.run_workflow(
        workspace_name=WORKSPACE,
        workflow_id=WORKFLOW,
        images={"image": image_path},
        parameters={"classes": TARGET_CLASSES},
        use_cache=True
    )
    print("[Test] ✓ Raw response:")
    print(json.dumps(result, indent=2))

    # Extract best detection
    outputs = result if isinstance(result, list) else result.get("outputs", [])
    best = None
    max_score = 0
    for output in outputs:
        preds = output.get("predictions", {})
        if isinstance(preds, dict):
            preds = preds.get("predictions", [])
        for p in (preds or []):
            score = p.get("width", 0) * p.get("height", 0) * p.get("confidence", 0)
            if score > max_score:
                max_score = score
                best = p

    if best:
        print(f"\n[Test] ★ Best detection: '{best['class']}' at {best['confidence']:.0%} confidence")
    else:
        print("\n[Test] No detections above threshold.")

except Exception as e:
    print(f"\n[Test] ✗ ERROR: {e}")
    print("   Make sure the local server is running: start_inference_server.bat")
