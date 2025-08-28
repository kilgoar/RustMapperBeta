using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }
	
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Lists for tooltip targets and configurations, visible in the Unity Inspector
    [SerializeField]
    private List<GameObject> tooltipTargets = new List<GameObject>();
    [SerializeField]
    private List<TooltipConfig> tooltipConfigs = new List<TooltipConfig>();

    // Dictionary to store instantiated tooltips
    private Dictionary<GameObject, TooltipTemplate> instantiatedTooltips = new Dictionary<GameObject, TooltipTemplate>();

    // Track the last shown tooltip
    private TooltipTemplate lastShownTooltip = null;

    private TooltipTemplate tooltipPrefab = null;
	
	private Canvas canvas;
	private RectTransform canvasRect; 

    // Tooltip configuration class, serializable for Inspector visibility
    [System.Serializable]
    public class TooltipConfig
    {
        public string title;
        public string message;
        public string footer;
        public Sprite icon; // Sprite for direct Inspector assignment
    }

    public void LoadTooltipTemplatePrefab()
    {
        if (tooltipPrefab == null)
        {
            tooltipPrefab = Resources.Load<TooltipTemplate>("TooltipTemplate");
            if (tooltipPrefab == null)
            {
                Debug.LogError("TooltipManager: Failed to load TooltipTemplate prefab from Resources/TooltipTemplate.");
            }
        }
    }

    private void Start()
    {
		canvas = AppManager.Instance.uiCanvas;
		canvasRect = canvas.GetComponent<RectTransform>();
		
        // Load the tooltip prefab
        LoadTooltipTemplatePrefab();

        // Ensure AppManager is initialized
        if (AppManager.Instance == null)
        {
            Debug.LogError("TooltipManager: AppManager.Instance is not initialized.");
            return;
        }

        // Validate lists
        if (tooltipTargets.Count != tooltipConfigs.Count)
        {
            Debug.LogError("TooltipManager: The number of tooltip targets and tooltip configs must match.");
            return;
        }

        // Create all tooltips on Start
        CreateAllTooltips();
    }

    private void OnDestroy()
    {
        // Clear singleton reference if this instance is destroyed
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Create all tooltips from the lists and set them to deactivated
    private void CreateAllTooltips()
    {
        if (tooltipPrefab == null)
        {
            Debug.LogError("TooltipManager: TooltipTemplate prefab is not loaded. Cannot create tooltips.");
            return;
        }
		
		
        instantiatedTooltips.Clear();

        for (int i = 0; i < tooltipTargets.Count; i++)
        {
            GameObject target = tooltipTargets[i];
            TooltipConfig config = tooltipConfigs[i];

            if (target == null)
            {
                Debug.LogWarning($"TooltipManager: Tooltip target at index {i} is null. Skipping.");
                continue;
            }

            // Instantiate tooltip from the prefab
            TooltipTemplate tooltip = Instantiate(tooltipPrefab, gameObject.transform);

            // Verify required components
            if (tooltip.title == null || tooltip.message == null || tooltip.footer == null || tooltip.icon == null)
            {
                Debug.LogWarning($"TooltipManager: Instantiated tooltip at index {i} is missing components: " +
                    $"Title={(tooltip.title != null ? "found" : "missing")}, " +
                    $"Message={(tooltip.message != null ? "found" : "missing")}, " +
                    $"Footer={(tooltip.footer != null ? "found" : "missing")}, " +
                    $"Image={(tooltip.icon != null ? "found" : "missing")}. Skipping.");
                Destroy(tooltip.gameObject);
                continue;
            }

            // Set tooltip content
            tooltip.title.text = config.title;
            tooltip.message.text = config.message;
            tooltip.footer.text = config.footer;

            // Set icon if provided
            if (config.icon != null)
            {
                tooltip.icon.sprite = config.icon;
                tooltip.icon.color = Color.white; // Ensure full visibility
            }
            else
            {
                tooltip.icon.gameObject.SetActive(false); // Hide icon if none provided
            }

            // Apply scale from AppManager's menuPanel
            if (AppManager.Instance.menuPanel != null)
            {
                RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
                Vector3 menuScale = AppManager.Instance.menuPanel.GetComponent<RectTransform>().localScale;
                Vector3 adjustedScale = menuScale - Vector3.one;
                adjustedScale.x = Mathf.Clamp(adjustedScale.x, 0.6f, 3f);
                adjustedScale.y = Mathf.Clamp(adjustedScale.y, 0.6f, 3f);
                tooltipRect.localScale = adjustedScale;
            }

            // Set text colors to match AppManager's color scheme
            tooltip.title.color = AppManager.Instance.color1;
            tooltip.message.color = AppManager.Instance.color1;
            tooltip.footer.color = AppManager.Instance.color1;

            // Deactivate the tooltip
            tooltip.gameObject.SetActive(false);

            // Store in instantiatedTooltips dictionary
            instantiatedTooltips[target] = tooltip;
        }

        // Deactivate this GameObject to prevent it from being visible
        gameObject.SetActive(false);

        Debug.Log($"TooltipManager: Created {instantiatedTooltips.Count} tooltips.");
    }

    // Show the tooltip for the specified GameObject
    public void ShowTooltip(GameObject target)
    {
		gameObject.transform.SetAsLastSibling();
        if (target == null || !instantiatedTooltips.ContainsKey(target))
        {
            Debug.LogWarning("TooltipManager: Cannot show tooltip: Target is null or not in instantiatedTooltips.");
            return;
        }

        TooltipTemplate tooltip = instantiatedTooltips[target];
        tooltip.gameObject.SetActive(true);
        lastShownTooltip = tooltip; // Track the shown tooltip
    }

    // Hide the tooltip for the specified GameObject
    public void HideTooltip(GameObject target)
    {
        if (target == null || !instantiatedTooltips.ContainsKey(target))
        {
            Debug.LogWarning("TooltipManager: Cannot hide tooltip: Target is null or not in instantiatedTooltips.");
            return;
        }

        TooltipTemplate tooltip = instantiatedTooltips[target];
        tooltip.gameObject.SetActive(false);
        if (lastShownTooltip == tooltip)
        {
            lastShownTooltip = null; // Clear if hiding the last shown tooltip
        }
    }

    // Hide the last shown tooltip
    public void HideLastTooltip()
    {
		
        if (lastShownTooltip != null && lastShownTooltip.gameObject != null)
        {
            lastShownTooltip.gameObject.SetActive(false);
            lastShownTooltip = null; // Clear the reference after hiding
        }
		
    }
	
	public void HideAllTooltips()
	{
		foreach (var tooltipPair in instantiatedTooltips)
		{
			if (tooltipPair.Value != null && tooltipPair.Value.gameObject != null)
			{
				tooltipPair.Value.gameObject.SetActive(false);
			}
		}
		lastShownTooltip = null; // Clear the last shown tooltip reference
		//Debug.Log("TooltipManager: All tooltips hidden.");
	}

	public void ShowTooltipAtPosition(GameObject target, Vector2 cursorPosition)
	{

		// Check if the target or its parent is in instantiatedTooltips
		if (!instantiatedTooltips.ContainsKey(target))
		{
			// Check parent
			Transform parent = target.transform.parent;
			if (parent != null && !instantiatedTooltips.ContainsKey(parent.gameObject))
			{
				//Debug.LogWarning($"No tooltip found for GameObject: {target.name} or its parent");
				return;
			}
			// If parent is found, use it as the target
			if (parent != null)
			{
				target = parent.gameObject;
			}
		}
		
		gameObject.transform.SetAsLastSibling();
		gameObject.SetActive(true);
		TooltipTemplate tooltip = instantiatedTooltips[target];
		tooltip.gameObject.SetActive(true);

		RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();

		// Ensure tooltip layout is up-to-date (for dynamic content)
		LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

		// Convert cursor screen position to local position in the canvas
		Vector2 localPoint;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			canvasRect,
			cursorPosition,
			canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : CameraManager.Instance.cam,
			out localPoint
		);



		// Clamp position to canvas boundaries (adapted from ClampToCanvas)
		Vector2 tooltipSize = tooltipRect.sizeDelta * tooltipRect.localScale.x; // Scale (0.6)
		Vector2 halfTooltipSize = tooltipSize * 0.5f;
		Vector2 canvasSize = canvasRect.sizeDelta;
		Vector2 halfCanvas = canvasSize * 0.5f;
		Vector2 pivot = tooltipRect.pivot; // e.g., (0.5, 0.5)
		
		// Adjust desired position with offset (mimicking mouse follow)
		Vector2 offset = new Vector2(15f + halfTooltipSize.x, -15f- halfTooltipSize.y); // Small offset from cursor
		Vector3 desiredPosition = new Vector3(localPoint.x + offset.x, localPoint.y + offset.y, 0f);

		// Calculate pivot-adjusted bounds
		Vector2 minPos = new Vector2(tooltipSize.x * pivot.x, tooltipSize.y * pivot.y);
		Vector2 maxPos = new Vector2(canvasSize.x - tooltipSize.x * (1f - pivot.x), 
									 canvasSize.y - tooltipSize.y * (1f - pivot.y));

		// Clamp position
		float clampedX = Mathf.Clamp(desiredPosition.x , minPos.x - halfCanvas.x, maxPos.x - halfCanvas.x );
		float clampedY = Mathf.Clamp(desiredPosition.y, minPos.y - halfCanvas.y, maxPos.y - halfCanvas.y);
		Vector3 clampedPosition = new Vector3(clampedX, clampedY, 0f);

		// Set tooltip's anchored position
		tooltipRect.anchoredPosition = clampedPosition;

		// Debug logging to diagnose
		//Debug.Log($"Canvas Size: {canvasSize}, Half Canvas: {halfCanvas}");
		//Debug.Log($"Tooltip Size: {tooltipSize}, Pivot: {pivot}, Desired X: {desiredPosition.x}, Clamped X: {clampedX}");

		lastShownTooltip = tooltip;
	}
}