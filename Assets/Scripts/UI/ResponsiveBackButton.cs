using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the Back Button responsive to different screen sizes and safe areas.
/// Handles notched phones, tablets, and different aspect ratios.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ResponsiveBackButton : MonoBehaviour
{
    [Header("Positioning")]
    [Tooltip("Distance from screen edge (in pixels)")]
    public float edgePadding = 20f;
    
    [Tooltip("Additional top padding for status bar/notch")]
    public float topSafeAreaPadding = 60f;
    
    [Header("Size Settings")]
    [Tooltip("Button size as percentage of screen width (0.1 = 10%)")]
    [Range(0.05f, 0.3f)]
    public float buttonSizePercent = 0.12f;
    
    [Tooltip("Minimum button size in pixels")]
    public float minButtonSize = 60f;
    
    [Tooltip("Maximum button size in pixels")]
    public float maxButtonSize = 100f;

    private RectTransform rectTransform;
    private Canvas canvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        UpdatePosition();
    }

    private void Update()
    {
        // In editor, update constantly for testing different resolutions
        #if UNITY_EDITOR
        UpdatePosition();
        #endif
    }

    private void UpdatePosition()
    {
        if (rectTransform == null || canvas == null) return;

        // Get screen dimensions
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Calculate button size based on screen width
        float buttonSize = Mathf.Clamp(screenWidth * buttonSizePercent, minButtonSize, maxButtonSize);
        rectTransform.sizeDelta = new Vector2(buttonSize, buttonSize);

        // Get safe area (accounts for notches, status bars, etc.)
        Rect safeArea = Screen.safeArea;
        
        // Calculate position from top-left corner
        float posX = edgePadding;
        float posY = -(edgePadding + topSafeAreaPadding);
        
        // Adjust for safe area
        posX += safeArea.x;
        posY -= (screenHeight - safeArea.y - safeArea.height);

        // Set anchors to top-left for consistent positioning
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        
        // Apply position
        rectTransform.anchoredPosition = new Vector2(posX, posY);

        // Ensure button is square
        float size = rectTransform.sizeDelta.x;
        rectTransform.sizeDelta = new Vector2(size, size);
    }

    /// <summary>
    /// Call this if screen orientation changes
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        UpdatePosition();
    }
}
