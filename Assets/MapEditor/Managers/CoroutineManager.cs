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
	public int socketMask = 1 << 13; //sockets
	public int prefabMask = 1 << 3; //prefabs
	public int waterMask = 1 << 4; //water
	public RaycastHit hit;
	private float prefabPlacementTimer = 0f;
	private float prefabPlacementInterval = 0.05f;
    private float undoTimer = 0f; // New timer for Ctrl+Z
    private float redoTimer = 0f; // New timer for Ctrl+Y
    private bool wasCtrlZPressedLastFrame = false; // Track Ctrl+Z state
    private bool wasCtrlYPressedLastFrame = false; // Track Ctrl+Y state
	private float paintBrushTimer = 0f;
    private float paintBrushInterval = 0.1f;
	
	private bool selectPrefabDownOverUI = false; // For selectPrefab (drag-select)	
	private bool isFrustumMode = true;
	private bool mouseDownOverUI = false;	
	private float uiHoverTimer = 0f; // New timer for UI hover
    private float uiHoverThreshold = 1f; // 1-second threshold for tooltip
	
	private CubeSelector cubeSelector; // Reference to CubeSelector component
	private bool isDragging; // Flag to track if dragging is occurring
	private Vector2 dragStartScreenPos; // Screen position where drag started
	private readonly float dragThreshold = 5f; // Pixel threshold to consider a drag
	
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
		
		var go = new GameObject("CubeSelector");
		cubeSelector = go.AddComponent<CubeSelector>();
		cubeSelector.Initialize(cam, prefabMask);
		DontDestroyOnLoad(go);
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
		
		if(CameraManager.Instance._workGizmo.Gizmo.HoverInfo.IsHovered){
			return true;
		}
		
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
		
	    // Check if selectPrefab was just pressed and set the selectPrefabDownOverUI flag
		if (BindManager.WasPressedThisFrame("selectPrefab"))
		{
			selectPrefabDownOverUI = OverUI();
		}
		
		if (BindManager.WasReleasedThisFrame("selectPrefab"))
		{
			cubeSelector.EndSelection();
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
        // Update undo/redo timers only for TerrainUndoAction when keys are held
        if (BindManager.IsPressed("undo") && UndoManager.IsNextUndoTerrainAction())
        {
            undoTimer += Time.deltaTime;
        }
        else
        {
            undoTimer = 0f;
        }

        if (BindManager.IsPressed("redo") && UndoManager.IsNextRedoTerrainAction())
        {
            redoTimer += Time.deltaTime;
        }
        else
        {
            redoTimer = 0f;
        }

        // Check for undo
        bool shouldUndo = BindManager.WasPressedThisFrame("undo") ||
                         (BindManager.IsPressed("undo") && UndoManager.IsNextUndoTerrainAction() && undoTimer >= MainScript.Instance.delay);

        // Check for redo
        bool shouldRedo = BindManager.WasPressedThisFrame("redo") ||
                         (BindManager.IsPressed("redo") && UndoManager.IsNextRedoTerrainAction() && redoTimer >= MainScript.Instance.delay);

        if (shouldUndo)
        {
            UndoManager.Undo();
            if (UndoManager.IsNextUndoTerrainAction())
            {
                undoTimer = 0f; // Reset timer only for continuous TerrainUndoAction
            }
        }
        else if (shouldRedo)
        {
            UndoManager.Redo();
            if (UndoManager.IsNextRedoTerrainAction())
            {
                redoTimer = 0f; // Reset timer only for continuous TerrainUndoAction
            }
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

    // Handle placePrefabFluid (dragging while holding selectPrefab)
    if (BindManager.IsPressed("placePrefabFluid"))
    {
        if (HierarchyWindow.Instance != null && HierarchyWindow.Instance.gameObject.activeSelf)
        {
            prefabPlacementTimer += Time.deltaTime;
            if (prefabPlacementTimer >= prefabPlacementInterval)
            {
                if (Physics.Raycast(cam.ScreenPointToRay(Mouse.current.position.ReadValue()), out var hit, Mathf.Infinity, landMask))
                {
                    HierarchyWindow.Instance.PlacePrefab(hit.point);
                }
                prefabPlacementTimer = 0f;
            }
        }
        return; // Exit early to prioritize placePrefabFluid
    }
    else if (BindManager.WasReleasedThisFrame("placePrefabFluid") || BindManager.WasReleasedThisFrame("placePrefab"))
    {
        prefabPlacementTimer = 0f; // Reset timer when either placePrefabFluid or placePrefab is released
    }

    // Toggle between cube and frustum selection modes
    if (BindManager.WasPressedThisFrame("toggle3d"))
    {
        isFrustumMode = !isFrustumMode;
    }

    // Handle selection (single-click, cube, or frustum selection) with "selectPrefab"
    if (BindManager.IsPressed("selectPrefab") && RTGizmosEngine.Get.HoveredGizmo == null)
    {
        Vector2 currentScreenPos = Mouse.current.position.ReadValue();

        if (BindManager.WasPressedThisFrame("selectPrefab"))
        {
            if (selectPrefabDownOverUI)
            {
                isDragging = false;
                return;
            }

            // Store initial mouse position to detect dragging
            dragStartScreenPos = currentScreenPos;
            isDragging = false; // Reset dragging state

            // Handle legacy placePrefab action on initial press
            if (BindManager.IsPressed("placePrefab"))
            {
                if (HierarchyWindow.Instance != null && HierarchyWindow.Instance.gameObject.activeSelf)
                {
                    if (Physics.Raycast(cam.ScreenPointToRay(currentScreenPos), out var hit, Mathf.Infinity, landMask))
                    {
                        HierarchyWindow.Instance.PlacePrefab(hit.point);
                    }
                }
                return;
            }

			if (BindManager.IsPressed("socketConnect"))
			{
				Debug.LogError("doing socket connection");
				Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
				if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, socketMask))			{

					GameObject hitObject = hit.collider.gameObject;
					DungeonBaseSocket socket = hitObject.transform.parent.GetComponent<DungeonBaseSocket>();
					
					if (socket != null)				{
						SocketManager.Connect(socket);
					}
				}
				else{
					Debug.Log("unselecting socket");
					SocketManager.Unselect();
				}
				return;
			}
            if (BindManager.IsPressed("socketSelect"))
            {
                Ray ray = cam.ScreenPointToRay(currentScreenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, socketMask))
                {
                    CameraManager.Instance.Unselect();
                    GameObject hitObject = hit.collider.gameObject;
                    CameraManager.Instance.SelectPrefab(hitObject.transform.parent.gameObject);
                }
                return;
            }
            if (BindManager.IsPressed("createSocket"))
            {
                Ray ray = cam.ScreenPointToRay(currentScreenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, prefabMask))
                {
                    CameraManager.Instance.Unselect();
                    SocketManager.AddSocket(hit);
                }
                return;
            }
        }
        else
        {
            // Check if mouse has moved enough to be considered a drag
            if (Vector2.Distance(dragStartScreenPos, currentScreenPos) > dragThreshold)
            {
                if (selectPrefabDownOverUI)
                {
                    isDragging = false;
                    return;
                }
                isDragging = true;
                // Start or update selection based on mode
                if (!cubeSelector.IsSelecting)
                {
                    cubeSelector.StartSelection(currentScreenPos);
                }
                float scrollDelta = Mouse.current.scroll.y.ReadValue();
                if (isFrustumMode)
                {
                    cubeSelector.UpdateFrustumVisualization(currentScreenPos);
                    cubeSelector.UpdateObjectsInFrustum(currentScreenPos);
                }
                else
                {
                    cubeSelector.UpdateSelection(currentScreenPos, scrollDelta);
                }
            }
        }
    }
    
	if (BindManager.WasPressedThisFrame("selectPrefab") && RTGizmosEngine.Get.HoveredGizmo == null)
    {
        if (isDragging)
        {
            cubeSelector.EndSelection();
            isDragging = false;
        }
        else
        {
                SocketManager.Clear();
                CameraManager.Instance.SelectPrefab();
        }
        cubeSelector.Hide();
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
	
