using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using RTG;

public class CoroutineManager : MonoBehaviour
{
    public int currentStyle;
    public static CoroutineManager _instance;
    public bool _isInitialized = false;
    public float nextCrumpetTime = 0f;
    public Camera cam;
    public Texture2D pointerTexture;
    public Texture2D paintBrushTexture;
    public InputAction mouseLeftClick;
    public int heightTool;
	public int landMask = 1 << 10; // "Land" layer
	public int waterMask = 1 << 4; // "Land" layer
	public RaycastHit hit;
	private float prefabPlacementTimer = 0f;
	private float prefabPlacementInterval = 0.05f;
    private float undoTimer = 0f; // New timer for Ctrl+Z
    private float redoTimer = 0f; // New timer for Ctrl+Y
    private bool wasCtrlZPressedLastFrame = false; // Track Ctrl+Z state
    private bool wasCtrlYPressedLastFrame = false; // Track Ctrl+Y state
	private float paintBrushTimer = 0f;
    private float paintBrushInterval = 0.1f;
	
	private bool mouseDownOverUI = false;	
	private float uiHoverTimer = 0f; // New timer for UI hover
    private float uiHoverThreshold = 1f; // 1-second threshold for tooltip
	
    public static CoroutineManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("CoroutineManager");
                _instance = go.AddComponent<CoroutineManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogError("Destroying duplicate CoroutineManager");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _isInitialized = true;
        cam = CameraManager.Instance.cam;
    }

    private void Start()
    {
        mouseLeftClick = new InputAction("MouseLeftClick", InputActionType.Button, "<Mouse>/leftButton");
        mouseLeftClick.Enable();
        mouseLeftClick.AddBinding("<Mouse>/leftButton");
        ChangeStylus(1);
    }

    public bool isInitialized => _isInitialized;

    public void ResetStylus()
    {
        ChangeStylus(currentStyle);
    }

    public void SetHeightTool(int tool)
    {
        heightTool = tool;
    }

    public void ChangeStylus(int style)
    {
        currentStyle = style;
		//Debug.LogError(style + " " + currentStyle);
    }

	private void OnDestroy()
	{
		if (_instance == this)
		{
			_instance = null;
		}
		CleanUp();
	}

	public static void CleanUp()
	{
		if (_instance != null)
		{
			_instance.StopAllCoroutines();
			GameObject.Destroy(_instance.gameObject);
			_instance = null;
		}
	}

    public bool OverUI()   //hover (tooltip) timer + short circuiting clicks over UI
    {
        // Reset timer if not over UI
        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
        {
            uiHoverTimer = 0f;
			TooltipManager.Instance.HideAllTooltips();
            return false;
        }

        // Increment timer
        uiHoverTimer += Time.deltaTime;

        // Check if timer exceeds threshold (1 second)
        if (uiHoverTimer >= uiHoverThreshold)
        {
			TooltipManager.Instance.HideAllTooltips();
			uiHoverTimer = 0f;
			
            // Get the UI object under the mouse
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            // Find the first valid GameObject on the UI layer (layer 5)
            foreach (var result in results)
            {
                if (result.gameObject.layer == 5) // UI layer
                {
                    // Call TooltipManager to show/move tooltip
                    //TooltipManager.Instance.ShowTooltipAtPosition(result.gameObject, pointerData.position);

					TooltipManager.Instance.ShowTooltipAtPosition(result.gameObject.transform.parent.gameObject, pointerData.position);
                }
            }
			return true;
        }

        return true;
    }

	public void DisableBrushFromUI(){
		
		// Check if mouse button was just pressed and set the mouseDownOverUI flag
		if (BindManager.WasPressedThisFrame("paintBrush"))
		{
			mouseDownOverUI = OverUI();
		}
		
		// Reset the flag when the mouse button is released
		if (BindManager.WasReleasedThisFrame("paintBrush"))
		{
			mouseDownOverUI = false;
		}
		
	}

    public void Update()
    {
		DisableBrushFromUI();
		
		HandleUndoRedo();
		
        if (!OverUI()){
				
				switch (currentStyle)
				{
					case 0: // disabled
						break;
					case 1:
						ItemStylusMode();
						break;
					case 2:
							PaintBrushMode();
						break;
					case 3:
						PathStylusMode();
						break;
				}

			}

    }
	
	
	private void HandleUndoRedo()
    {
    
        // Update undo/redo timers when keys are held down
        if (BindManager.IsPressed("undo"))
        {
            undoTimer += Time.deltaTime;
        }
        else
        {
            undoTimer = 0f;
        }

        if (BindManager.IsPressed("redo"))
        {
            redoTimer += Time.deltaTime;
        }
        else
        {
            redoTimer = 0f;
        }

        // Check for undo/redo
        bool shouldUndo = BindManager.WasPressedThisFrame("undo") ||
                         (BindManager.IsPressed("undo") && undoTimer >= MainScript.Instance.delay);
        bool shouldRedo = BindManager.WasPressedThisFrame("redo") ||
                         (BindManager.IsPressed("redo") && redoTimer >= MainScript.Instance.delay);

        if (shouldUndo)
        {
            UndoManager.Undo();
            undoTimer = 0f; // Reset timer after undo
        }
        else if (shouldRedo)
        {
            UndoManager.Redo();
            redoTimer = 0f; // Reset timer after redo
        }
    }

public void PathStylusMode()
{
    if (BindManager.WasPressedThisFrame("rotateCamera")) return;
	
    if (BindManager.WasPressedThisFrame("placePath") && RTGizmosEngine.Get.HoveredGizmo == null)
    {

        CameraManager.Instance.PaintNodes();
        return;
    }

    if (BindManager.IsPressed("placePathFluid"))
    {
        CameraManager.Instance.DragNodes();
    }
	
	if (BindManager.WasPressedThisFrame("selectPrefab") && RTGizmosEngine.Get.HoveredGizmo == null)
	{
	        CameraManager.Instance.SelectPath();
	}
}

public void ItemStylusMode()
{
    if (BindManager.WasPressedThisFrame("rotateCamera")) return;

    // Check for Alt + Shift + Left Mouse Button
    if (BindManager.IsPressed("placePrefabFluid"))
    {
        if (HierarchyWindow.Instance != null && HierarchyWindow.Instance.gameObject.activeSelf)
        {
            // Update timer
            prefabPlacementTimer += Time.deltaTime;

            // Place prefab if timer exceeds interval
            if (prefabPlacementTimer >= prefabPlacementInterval)
            {
                if (Physics.Raycast(cam.ScreenPointToRay(Mouse.current.position.ReadValue()), out var hit, Mathf.Infinity, landMask))
                {
                    HierarchyWindow.Instance.PlacePrefab(hit.point);
                }
                prefabPlacementTimer = 0f; // Reset timer
            }
        }
    }
    // Reset timer when left mouse button is released
    else if (BindManager.WasReleasedThisFrame("placePrefab"))
    {
        prefabPlacementTimer = 0f;
    }

    if (BindManager.WasPressedThisFrame("selectPrefab") && RTGizmosEngine.Get.HoveredGizmo == null)
    {
        if (BindManager.IsPressed("placePrefab"))
        {
            if (HierarchyWindow.Instance != null && HierarchyWindow.Instance.gameObject.activeSelf)
            {
                if (Physics.Raycast(cam.ScreenPointToRay(Mouse.current.position.ReadValue()), out hit, Mathf.Infinity, landMask))
                {
                    HierarchyWindow.Instance.PlacePrefab(hit.point);
                }
            }
        }
        CameraManager.Instance.SelectPrefab();
    }
}

public void PaintBrushMode()
{
    if (AppManager.Instance.AnyDragging() || mouseDownOverUI)
    {
        return;
    }

    // Early exit if no raycast hit
    Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
    RaycastHit hit;

	

    // Update paint timer when mouse is held down
    if (BindManager.IsPressed("paintBrush"))
    {
        paintBrushTimer += Time.deltaTime;
    }
    else
    {
        paintBrushTimer = 0f; // Reset timer when mouse button is released
    }

 
    // Check for quick tap or continuous painting
    bool shouldPaint = BindManager.WasPressedThisFrame("paintBrush") || // Quick tap
                      (BindManager.IsPressed("paintBrush") && paintBrushTimer >= MainScript.Instance.delay);

   if (MainScript.Instance.paintMode == -5)
    {
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, waterMask) && shouldPaint)
        {
            TerrainManager.GetTerrainCoordinates(hit, MainScript.Instance.brushSize, out int numX, out int numY);
            MainScript.Instance.ModifyWater(numX, numY);
            paintBrushTimer = 0f; // Reset timer after painting
        }
        return;
    }

    if (Physics.Raycast(ray, out hit, Mathf.Infinity, landMask))
    {
        TerrainManager.GetTerrainCoordinates(hit, MainScript.Instance.brushSize, out int numX, out int numY);

        if (shouldPaint)
        {
            if (BindManager.IsPressed("sampleHeight"))
            {
                TerrainWindow.Instance.SampleHeightAtClick(hit);
                paintBrushTimer = 0f; // Reset timer after sampling
                return;
            }

            if (MainScript.Instance.paintMode == -1)
            {
                MainScript.Instance.ModifySplat(numX, numY);
            }
            else if (MainScript.Instance.paintMode == -3)
            {
                MainScript.Instance.ModifyAlpha(numX, numY);
            }
            else if (MainScript.Instance.paintMode == -2)
            {
                MainScript.Instance.ModifyTopology(numX, numY);
            }
            else if (MainScript.Instance.paintMode == -4)
            {
                MainScript.Instance.ModifyBlendMap(numX, numY);
            }
            else
            {
                MainScript.Instance.ModifyTerrain(numX, numY);
            }

            MainScript.Instance.RegenerateBrushWithRotation();
            paintBrushTimer = 0f; // Reset timer after painting
        }

        MainScript.Instance.PreviewTerrain(numX, numY);
    }
}

// Helper method to check if mouse is over UI on layer 5
private bool IsMouseOverUI()
{
    // Check if the pointer is over a UI element
    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
    {
        // Get the UI object under the mouse
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        // Check if any UI element is on layer 5
        foreach (var result in results)
        {
            if (result.gameObject.layer == 5) // Layer 5 is the UI layer
            {
                return true;
            }
        }
    }
    return false;
}
	
	public void StopRuntimeCoroutine(Coroutine coroutine)
	{
		if (coroutine != null)
		{
			StopCoroutine(coroutine);
		}
	}
		
	public Coroutine StartRuntimeCoroutine(IEnumerator coroutine)
    {
        if (coroutine == null)
        {
            Debug.LogError("Coroutine is null!");
            return null;
        }
        return StartCoroutine(coroutine);
    }
}
	
