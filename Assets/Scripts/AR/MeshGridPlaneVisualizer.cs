using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Attach this to an AR Plane prefab (alongside ARPlaneMeshVisualizer).
/// Draws a cyan grid + boundary ring that fades in as the plane is detected,
/// and fades out when the plane is removed.
/// </summary>
[RequireComponent(typeof(ARPlane))]
public class MeshGridPlaneVisualizer : MonoBehaviour
{
    [Header("Grid Appearance")]
    [SerializeField] private Color gridColor    = new Color(0f, 0.7f, 1f, 0.35f);
    [SerializeField] private float lineThickness = 0.003f;
    [SerializeField] private float gridSize      = 0.1f;
    [SerializeField] private float fadeDuration  = 0.5f;

    [Header("Edge Ring")]
    [SerializeField] private Color edgeColor     = new Color(0f, 0.85f, 1f, 0.65f);
    [SerializeField] private float edgeThickness = 0.006f;

    private ARPlane      _arPlane;
    private LineRenderer _gridLines;
    private LineRenderer _edgeLines;
    private float        _fadeProgress = 0f;
    private bool         _isDirty      = true;
    private Vector2      _lastSize     = Vector2.zero;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _arPlane = GetComponent<ARPlane>();
        CreateGridLineRenderer();
        CreateEdgeLineRenderer();
    }

    private void OnEnable()
    {
        _isDirty      = true;
        _fadeProgress = 0f;
    }

    private void Update()
    {
        if (_arPlane == null) return;

        // Rebuild grid if plane size changed
        Vector2 currentSize = _arPlane.size;
        if (currentSize != _lastSize)
        {
            _lastSize = currentSize;
            _isDirty  = true;
        }

        if (_isDirty)
        {
            DrawGrid();
            DrawEdge();
            _isDirty = false;
        }

        // Fade in
        if (_fadeProgress < 1f)
        {
            _fadeProgress += Time.deltaTime / Mathf.Max(fadeDuration, 0.01f);
            _fadeProgress  = Mathf.Clamp01(_fadeProgress);
            UpdateAlpha();
        }
    }

    // ─── Line renderer creation ───────────────────────────────────────────────

    private void CreateGridLineRenderer()
    {
        var go = new GameObject("GridLines");
        go.transform.SetParent(transform, false);
        _gridLines = go.AddComponent<LineRenderer>();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = gridColor;

        _gridLines.material          = mat;
        _gridLines.startWidth        = lineThickness;
        _gridLines.endWidth          = lineThickness;
        _gridLines.useWorldSpace     = false;
        _gridLines.numCapVertices    = 0;
        _gridLines.numCornerVertices = 0;
        _gridLines.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _gridLines.receiveShadows    = false;
    }

    private void CreateEdgeLineRenderer()
    {
        var go = new GameObject("EdgeLines");
        go.transform.SetParent(transform, false);
        _edgeLines = go.AddComponent<LineRenderer>();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = edgeColor;

        _edgeLines.material          = mat;
        _edgeLines.startWidth        = edgeThickness;
        _edgeLines.endWidth          = edgeThickness;
        _edgeLines.useWorldSpace     = false;
        _edgeLines.numCapVertices    = 2;
        _edgeLines.numCornerVertices = 2;
        _edgeLines.loop              = true;
        _edgeLines.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _edgeLines.receiveShadows    = false;
    }

    // ─── Drawing ─────────────────────────────────────────────────────────────

    private void DrawGrid()
    {
        if (_gridLines == null) return;

        float sizeX = _arPlane.size.x;
        float sizeZ = _arPlane.size.y;
        if (sizeX < 0.01f || sizeZ < 0.01f) return;

        var points = new System.Collections.Generic.List<Vector3>();

        // Horizontal lines (along X)
        int zSteps = Mathf.CeilToInt(sizeZ / gridSize);
        for (int i = 0; i <= zSteps; i++)
        {
            float z = Mathf.Clamp(-sizeZ / 2f + (i * gridSize), -sizeZ / 2f, sizeZ / 2f);
            points.Add(new Vector3(-sizeX / 2f, 0.001f, z));
            points.Add(new Vector3( sizeX / 2f, 0.001f, z));
        }

        // Vertical lines (along Z)
        int xSteps = Mathf.CeilToInt(sizeX / gridSize);
        for (int i = 0; i <= xSteps; i++)
        {
            float x = Mathf.Clamp(-sizeX / 2f + (i * gridSize), -sizeX / 2f, sizeX / 2f);
            points.Add(new Vector3(x, 0.001f, -sizeZ / 2f));
            points.Add(new Vector3(x, 0.001f,  sizeZ / 2f));
        }

        _gridLines.positionCount = points.Count;
        _gridLines.SetPositions(points.ToArray());
    }

    private void DrawEdge()
    {
        if (_edgeLines == null) return;

        var boundary = _arPlane.boundary;
        if (boundary == null || boundary.Length == 0) return;

        var points = new Vector3[boundary.Length];
        for (int i = 0; i < boundary.Length; i++)
            points[i] = new Vector3(boundary[i].x, 0.002f, boundary[i].y);

        _edgeLines.positionCount = points.Length;
        _edgeLines.SetPositions(points);
    }

    private void UpdateAlpha()
    {
        float alpha = Mathf.Lerp(0f, 1f, _fadeProgress);

        if (_gridLines != null && _gridLines.material != null)
        {
            Color c = gridColor;
            c.a *= alpha;
            _gridLines.material.color = c;
        }

        if (_edgeLines != null && _edgeLines.material != null)
        {
            Color c = edgeColor;
            c.a *= alpha;
            _edgeLines.material.color = c;
        }
    }
}
