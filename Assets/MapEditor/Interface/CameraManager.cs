using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using RustMapEditor.Variables;
using UnityEngine.EventSystems;
using UIRecycleTreeNamespace;
using RTG;
using System;
using System.Linq;
using EasyRoads3Dv3;
using static WorldSerialization;

public class CameraManager : MonoBehaviour
{
	public GameObject transformTool;
	public UIRecycleTree itemTree;
	public Camera cam;
	//public Camera camTransform;
	public GameObject xPos, yPos, zPos;
	public LayerMask worldToolLayer;
	public Terrain landTerrain;
	public List<InputField> snapFields;
	public Vector3 position;
	public bool lockCam;
	

    public Vector3 movement = new Vector3(0, 0, 0);
    public float movementSpeed = 100f;
    public float rotationSpeed = .25f;
    public InputControl<Vector2> mouseMovement;
    public Vector3 globalMove = new Vector3(0, 0, 0);
    public float pitch = 90f;
    public float yaw = 0f;
    public float sprint = 1f;
    public bool dragXarrow, dragYarrow, dragZarrow, sync;
    Quaternion dutchlessTilt;
	public Keyboard key;
	public Mouse mouse;

	public float currentTime;
	public float lastUpdateTime = 0f;
	public float updateFrequency = .3f;
	
	private bool wasMoving = false;
	private ObjectTransformGizmo _objectMoveGizmo;
    private ObjectTransformGizmo _objectRotationGizmo;
    private ObjectTransformGizmo _objectScaleGizmo;
    private ObjectTransformGizmo _objectUniversalGizmo;
	
	public List<GameObject> _selectedObjects = new List<GameObject>();
	public GameObject _selectedRoad = null;
	
	private int layerMask = 1 << 10; // "Land" layer	
	
	public GizmoId _workGizmoId;
	public ObjectTransformGizmo _workGizmo;

	private List<RaycastHit> previousHits = new List<RaycastHit>();
	private int currentSelectionIndex = 0;
	
	public delegate void SelectionChangedHandler();
    public event SelectionChangedHandler OnSelectionChanged;

    FilePreset settings;
	
	public static CameraManager Instance { get; private set; }

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

    void Start()
    {
        Configure();
		MapManager.Callbacks.MapLoaded += OnMapLoad;
		InitializeGizmos();
		SetupSnapListeners();
    }
	
public void InitializeGizmos()
{
    // Create gizmos
    _objectMoveGizmo = RTGizmosEngine.Get.CreateObjectMoveGizmo();
    _objectRotationGizmo = RTGizmosEngine.Get.CreateObjectRotationGizmo();
    _objectScaleGizmo = RTGizmosEngine.Get.CreateObjectScaleGizmo();
    _objectUniversalGizmo = RTGizmosEngine.Get.CreateObjectUniversalGizmo();

    // Disable gizmos by default (visual enablement), but enable snapping
    _objectMoveGizmo.Gizmo.SetEnabled(false);
    MoveGizmo moveGizmo = _objectMoveGizmo.Gizmo.GetFirstBehaviourOfType<MoveGizmo>();
    if (moveGizmo != null)
    {
        moveGizmo.SetSnapEnabled(true); // Enable snap by default
    }

    _objectRotationGizmo.Gizmo.SetEnabled(false);
    RotationGizmo rotationGizmo = _objectRotationGizmo.Gizmo.GetFirstBehaviourOfType<RotationGizmo>();
    if (rotationGizmo != null)
    {
        rotationGizmo.SetSnapEnabled(true); // Enable snap by default
    }

    _objectScaleGizmo.Gizmo.SetEnabled(false);
    ScaleGizmo scaleGizmo = _objectScaleGizmo.Gizmo.GetFirstBehaviourOfType<ScaleGizmo>();
    if (scaleGizmo != null)
    {
        scaleGizmo.SetSnapEnabled(true); // Enable snap by default
    }

    _objectUniversalGizmo.Gizmo.SetEnabled(false);
    UniversalGizmo universalGizmo = _objectUniversalGizmo.Gizmo.GetFirstBehaviourOfType<UniversalGizmo>();
    if (universalGizmo != null)
    {
        universalGizmo.SetSnapEnabled(true); // Enable snap by default
    }

    // Set target objects for gizmos
    _objectMoveGizmo.SetTargetObjects(_selectedObjects);
    _objectRotationGizmo.SetTargetObjects(_selectedObjects);
    _objectScaleGizmo.SetTargetObjects(_selectedObjects);
    _objectUniversalGizmo.SetTargetObjects(_selectedObjects);

    // Default to Move gizmo for selection
    _workGizmo = _objectMoveGizmo;
    _workGizmoId = GizmoId.Move;
}

	public PrefabDataHolder[] SelectedDataHolders()
	{
		if (_selectedObjects == null || _selectedObjects.Count == 0)
		{
			return new PrefabDataHolder[0]; // Return empty array if no objects are selected
		}

		// Use LINQ to filter and collect all PrefabDataHolder components
		var prefabDataHolders = _selectedObjects
			.Where(obj => obj != null) // Ensure the GameObject exists
			.SelectMany(obj => obj.GetComponents<PrefabDataHolder>()) // Get all PrefabDataHolder components from each GameObject
			.ToArray();

		return prefabDataHolders;
	}

    public enum GizmoId    {
            Move = 1,
            Rotate,
            Scale,
            Universal
    }

	public void SetCameraPosition(Vector3 position)
	{

		cam.transform.position = position;
		this.position = position; 

		currentTime = Time.time;
		if (currentTime - lastUpdateTime > updateFrequency)
		{
			AreaManager.UpdateSectors(position, settings.prefabRenderDistance);
			lastUpdateTime = currentTime;
		}

	}

	public void SetCamera(Vector3 targetPosition)
	{
		float distance = 25.0f;
		
		Vector3 initialPosition = cam.transform.position;
		initialPosition.y = targetPosition.y;
		cam.transform.position = initialPosition;
		
		Vector3 directionToTarget = targetPosition - cam.transform.position;
		Quaternion lookRotation = Quaternion.LookRotation(directionToTarget);

		cam.transform.rotation = lookRotation;

		Vector3 offset = directionToTarget.normalized * distance;
		cam.transform.position = targetPosition - offset;

		Vector3 forward = cam.transform.forward;

		yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

		pitch = Mathf.Asin(forward.y) * Mathf.Rad2Deg;

		Quaternion finalRotation = Quaternion.Euler(pitch, yaw, 0f);
		cam.transform.rotation = finalRotation;
		position = cam.transform.position;
	}

	public void SetRenderLimit(){
		settings = SettingsManager.application;   
		cam.farClipPlane = settings.prefabRenderDistance;
		//camTransform.farClipPlane = settings.prefabRenderDistance;
	}
	
	public void Configure()
	{
		
		
		lockCam = false;
		dragXarrow = false;
		dragYarrow = false;
		dragZarrow = false;
		sync=false;
		
		if (cam == null) {
			Debug.LogError("No camera found with tag 'MainCamera'. Please assign a camera to the scene.");
			return; 
		}

		settings = SettingsManager.application;        
		if (object.ReferenceEquals(settings, null)) {
			Debug.LogError("SettingsManager.application is null. Ensure it is properly initialized.");
			return; 
		}
		
		//cam.depthTextureMode = DepthTextureMode.Depth;
		cam.farClipPlane = settings.prefabRenderDistance;	
		key = Keyboard.current;
		mouse = Mouse.current;
		
		SetRenderLimit();
	}
	
	public void SetGizmoEnabled(bool enabled)
    {
        if (_workGizmo != null)
        {
            _workGizmo.Gizmo.SetEnabled(enabled);
        }
    }

	void Update()
	{
		if (cam == null) return;
		if (lockCam == true) return;
		if (LoadScreen.Instance.isEnabled)
			return;

		//if (BindManager.IsPressed("altModifier"))
		//{
		//	SetGizmoEnabled(false);
		//}
		//else
		//{
			UpdateGizmoState();
		//}

		// Rotate camera (right click)
		if (BindManager.IsPressed("rotateCamera"))
		{
			mouseMovement = Mouse.current.delta;

			pitch -= mouseMovement.ReadValue().y * rotationSpeed;
			yaw += mouseMovement.ReadValue().x * rotationSpeed;
			if (pitch > 89f || pitch < -89f)
			{
				cam.transform.rotation *= Quaternion.Euler(pitch, yaw, 0f);
			}

			Quaternion dutchlessTilt = Quaternion.Euler(pitch, yaw, 0f);
			cam.transform.rotation = dutchlessTilt;
		}

		if (!AppManager.Instance.IsAnyInputFieldActive())
		{
			float sprint = 0.25f; // Default speed

			// Gizmo controls
			if (BindManager.WasPressedThisFrame("gizmoMove")) SetWorkGizmoId(GizmoId.Move);
			else if (BindManager.WasPressedThisFrame("gizmoRotate")) SetWorkGizmoId(GizmoId.Rotate);
			else if (BindManager.WasPressedThisFrame("gizmoScale")) SetWorkGizmoId(GizmoId.Scale);
			else if (BindManager.WasPressedThisFrame("gizmoUniversal")) SetWorkGizmoId(GizmoId.Universal);
			else if (BindManager.WasPressedThisFrame("gizmoToggleSpace")) ToggleGizmoSpace();
			else if (BindManager.WasPressedThisFrame("transparencyToggle")) FadeSelection();


				if (BindManager.WasPressedThisFrame("duplicate"))
				{
					DuplicateSelection();
					return;
				}
				if (BindManager.WasPressedThisFrame("createParent"))
				{
					CreateParent();
					return;
				}
				if (BindManager.WasPressedThisFrame("flatten"))
				{
					Flatten();
					return;
				}


			// Movement speed modifiers
			if (BindManager.IsPressed("moveVerySlow"))
			{
				sprint = 0.0375f; // Alt and Shift pressed
			}
			else if (BindManager.IsPressed("moveFast"))
			{
				sprint = 3f; // Only Shift pressed
			}
			else if (BindManager.IsPressed("moveSlow"))
			{
				sprint = 0.075f; // Only Alt pressed
			}

			float currentSpeed = movementSpeed * sprint * Time.deltaTime;

			globalMove = Vector3.zero;
			
			if(!Keyboard.current.ctrlKey.isPressed){

				if (BindManager.IsPressed("moveForward"))
				{
					globalMove += cam.transform.forward * currentSpeed;
				}
				if (BindManager.IsPressed("moveBackward"))
				{
					globalMove -= cam.transform.forward * currentSpeed;
				}
				if (BindManager.IsPressed("moveLeft"))
				{
					globalMove -= cam.transform.right * currentSpeed;
				}
				if (BindManager.IsPressed("moveRight"))
				{
					globalMove += cam.transform.right * currentSpeed;
				}
				if (BindManager.IsPressed("moveDown"))
				{
					globalMove -= cam.transform.up * currentSpeed;
				}
				if (BindManager.IsPressed("moveUp"))
				{
					globalMove += cam.transform.up * currentSpeed;
				}
			}
			
			
			if (BindManager.WasPressedThisFrame("delete"))
			{
				DeleteSelection();
			}

			if (globalMove != Vector3.zero)
			{
				cam.transform.position += globalMove;
				position = cam.transform.position;
				currentTime = Time.time;

				if (currentTime - lastUpdateTime > updateFrequency)
				{
					AreaManager.UpdateSectors(position, settings.prefabRenderDistance);
					lastUpdateTime = currentTime;
				}
				wasMoving = true; // Mark that the camera is moving
			}
			else if (wasMoving) // Camera stopped this frame
			{
				AreaManager.UpdateSectors(position, settings.prefabRenderDistance);
				wasMoving = false; // Reset moving state
			}

			AppManager.Instance.UpdateInspector();
		}
	}
	
	
	public void UpdateItemsWindow(){
		if(ItemsWindow.Instance != null){
			ItemsWindow.Instance.PopulateList();
			ItemsWindow.Instance.CheckSelection();
			itemTree.Rebuild();
			//ItemsWindow.Instance.FocusItem(_selectedObjects);
		}
	}
	
	public void CreateParent()
	{
		// Create a new parent
		GameObject newParent = new GameObject("Collection" + UnityEngine.Random.Range(0,10) + UnityEngine.Random.Range(0,10) + UnityEngine.Random.Range(0,10) + UnityEngine.Random.Range(0,10));
		newParent.tag = "Collection";
		newParent.transform.SetParent(PrefabManager.PrefabParent);
		
		// Position the parent at the average position of all selected objects
		Vector3 averagePosition = Vector3.zero;
		int validObjectCount = 0;
		
		foreach (GameObject go in _selectedObjects)
		{
			if (go != null)
			{
				averagePosition += go.transform.position;
				validObjectCount++;
			}
		}
		
		if (validObjectCount > 0)
		{
			newParent.transform.position = averagePosition / validObjectCount;
			
			// Set all selected objects as children of the new parent
			foreach (GameObject go in _selectedObjects)
			{
				if (go != null)
				{
					// GameObject sets to new parent
					go.transform.SetParent(newParent.transform, true); // true keeps world position
				}
			}
		}
		
		Select(newParent, false);
	}
	
	public void Select(GameObject obj, bool multi){
		if(!multi){
			Unselect();
		}
		_selectedObjects.Add(obj);
		EmissionHighlight(GetRenderers(obj), true);
		NotifySelectionChanged();
		UpdateItemsWindow();
	}
	
	public void CheckSelect(GameObject obj, bool multi){
		if(!multi){
			Unselect();
		}
		_selectedObjects.Add(obj);
		EmissionHighlight(GetRenderers(obj), true);
		
		NotifySelectionChanged();
		//UpdateItemsWindow();
	}
	
	public void Select(List<GameObject> objs){
		Unselect();

		foreach (GameObject obj in objs){
			_selectedObjects.Add(obj);
			EmissionHighlight(GetRenderers(obj), true);
		}
		
		NotifySelectionChanged();
		UpdateItemsWindow();
	}
	
	public void Flatten()
	{
		List<GameObject> flattenedObjects = new List<GameObject>();
		// Iterate through all selected objects
		foreach (GameObject go in _selectedObjects)
		{
			// Check if the object exists and is tagged as a "Collection"
			if (go != null && go.tag == "Collection")	{
				
				// Reparent all children to the base PrefabManager.PrefabParent
				while (go.transform.childCount > 0)				{
					Transform child = go.transform.GetChild(0);									
					child.SetParent(PrefabManager.PrefabParent, true); // true keeps world position
					flattenedObjects.Add(child.gameObject);
					
				}
				
				// Destroy the now-empty collection object
				GameObject.DestroyImmediate(go);
				continue;
			}
			
			//in the case we want to flatten individuals in a collection
			if (go !=  null && go.tag == "Prefab"){
				go.transform.SetParent(PrefabManager.PrefabParent, true);
				flattenedObjects.Add(go);
			}
			
		}
		
		Select(flattenedObjects);

	}
		
	public void DuplicateSelection()
	{
		
		foreach (GameObject go in _selectedObjects)
		{
			if (go != null)
			{
				// Create new object copying the original
				GameObject newObject = Instantiate(go, go.transform.position, go.transform.rotation);
				// Maintain the same parent as original
				newObject.transform.parent = go.transform.parent;
				Unselect (newObject); //unselect new objects
			}
		}
		
		
	}
	
	public void DeleteSelection()
	{
		if(_selectedObjects.Count < 1){ return; }
		if(_selectedObjects.Count == 1){
			    
				DungeonBaseSocket socket = _selectedObjects[0].GetComponent<DungeonBaseSocket>();
				// Check if the selected object has a user-defined DungeonBaseSocket
				if (socket != null && socket.isUserDefined)		{
					socket.Delete(); // Call the Delete method to remove socket data
				}
		}
		
		_workGizmo.Gizmo.SetEnabled(false);
		// For each object in _selectedObjects, destroy the object
		foreach (GameObject go in _selectedObjects) // Use ToList() to avoid modifying the collection while iterating
		{
			if (go != null)
			{
				//first find matching node by data in items window and delete it
				Node toDestroy = itemTree.FindFirstNodeByDataRecursive(go);
				if (toDestroy!=null){
				toDestroy.parentNode.nodes.RemoveWithoutNotify(toDestroy);
				}
				UnityEngine.Object.Destroy(go); // Use UnityEngine.Object.Destroy for game objects
				
			}
		}
		_selectedObjects.Clear();
		
		itemTree.Rebuild();
		UpdateGizmoState();
		PrefabManager.NotifyItemsChanged(false);
	}
	
	public GameObject FindParentWithTag(GameObject hitObject, string tag){
		while (hitObject != null && !hitObject.CompareTag(tag))
		{
			hitObject = hitObject.transform.parent?.gameObject;
		}
		return hitObject;
	}
	
	
public void SelectPath()
{
    if (Keyboard.current.altKey.isPressed) { return; }
	Debug.Log("selecting path...");

    int pathLayerMask = 1 << 9; // Paths are on layer 9
    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
    RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, pathLayerMask);

    if (hits.Length == 0)
    {
		ClearAndHideSelection();
        return;
    }

    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
    GameObject hitPathObject = hits[0].transform.gameObject;

    // Resolve the road and node objects
    GameObject roadObject = null;
    GameObject nodeObject = null;
    if (hitPathObject.CompareTag("Node"))
    {
        nodeObject = hitPathObject;
        roadObject = hitPathObject.transform.parent.gameObject;
    }
    else if (hitPathObject.CompareTag("Path"))
    {
        roadObject = hitPathObject;
		Debug.Log(roadObject);
    }   
	else if (hitPathObject.CompareTag("EasyRoads")){
		roadObject = hitPathObject.transform.parent.gameObject;
		Debug.Log(roadObject);
	}
	else
    {
		Debug.Log("non road object selected:" + hitPathObject.tag);
        ClearAndHideSelection(); 
        return;
    }
   
    if (roadObject == null)
    {
		ClearAndHideSelection();
        Debug.LogWarning("Could not resolve road object from hit.");
        return;
    }

    // NODE FIRST SELECTION
    if (nodeObject != null)
    {
		Debug.Log("node clicked");
		if(_selectedRoad == null){
			SelectRoad(roadObject,nodeObject);
			PathSelectionChanged(nodeObject);
			return;
		}
		
		
        if (Keyboard.current.leftShiftKey.isPressed)
        {
            // Multi-selection: toggle node inclusion
            if (_selectedObjects.Contains(nodeObject))
            {
                _selectedObjects.Remove(nodeObject);
                EmissionHighlight(GetRenderers(nodeObject), false);
				
				if(_selectedObjects.Count ==0){
					Unselect();
				}
				return;
				
            }
            else
            {
                _selectedObjects.Add(nodeObject);
                EmissionHighlight(GetRenderers(nodeObject), true);				
				PathSelectionChanged(nodeObject);
				return;
            }
        }
        else
        {
			SelectRoad(roadObject,nodeObject);
			PathSelectionChanged(nodeObject);
			return;
		}

    }
	
    // ROAD FIRST SELECTION
    else
    {
		Debug.Log("direct path selection");
		SelectRoad(roadObject);
		Debug.Log("getting firstNode");
		GameObject firstNode = _selectedRoad.GetComponent<NodeCollection>().GetFirstNode().gameObject;
		Debug.Log("setting firstNode");
		PathSelectionChanged(firstNode);
		Debug.Log("road selection passed.");
		
    }


}

	public void PathSelectionChanged(GameObject nodeObject)	{
		if (PathWindow.Instance == null)
		{
			Debug.LogError("PathWindow.Instance is null in PathSelectionChanged");
		}
		if (ItemsWindow.Instance == null)
		{
			Debug.LogError("ItemsWindow.Instance is null in PathSelectionChanged");
		}
		
		// Update PathWindow regardless of ItemsWindow state
		if (PathWindow.Instance != null)
		{
			Debug.Log("SelectPath: Updating PathWindow");
			PathWindow.Instance.UpdateData();
		}
		
		        // Sync with ItemsWindow if active
        if (ItemsWindow.Instance != null)
        {
            Node nodeInTree = ItemsWindow.Instance.tree?.FindFirstNodeByDataRecursive(nodeObject);
            if (nodeInTree != null)
            {
                nodeInTree.SetCheckedWithoutNotify(_selectedObjects.Contains(nodeObject));
                ItemsWindow.Instance.FocusList(nodeInTree);
				ItemsWindow.Instance.CheckSelection();
            }

        }

		UpdateGizmoState();
		NotifySelectionChanged();
	}

	public void NotifySelectionChanged()
    {
        OnSelectionChanged?.Invoke();
    }

	public void SelectRoad(GameObject roadObject, GameObject nodeObject = null){
			
			Unselect();
			_selectedRoad = roadObject;
			ShowRoad(roadObject);

			if (nodeObject == null || nodeObject == roadObject)
			{
				nodeObject = _selectedRoad.GetComponent<NodeCollection>().GetFirstNode().gameObject;
				if (nodeObject == null || nodeObject == roadObject)
				{
					Debug.Log("invalid road - no nodes");
					return;
				}
			}

            _selectedObjects.Add(nodeObject);
            EmissionHighlight(GetRenderers(nodeObject), true);
	}

    // Show the nodes of the specified road
    public void ShowRoad(GameObject road)
    {
        if (road == null)
        {
            Debug.LogWarning("Cannot show road: road GameObject is null.");
            return;
        }

        // Try to get NodeCollection from the road GameObject
        NodeCollection nodeCollection = road.GetComponent<NodeCollection>();
        if (nodeCollection == null)
        {
            // Check for "Nodes" child
            Transform nodes = road.transform.Find("Nodes");
            if (nodes != null)
            {
                nodeCollection = nodes.GetComponent<NodeCollection>();
            }
        }

        if (nodeCollection != null)
        {
            nodeCollection.ShowNodes();
            Debug.Log($"Showed nodes for road '{road.name}'.");
        }
        else
        {
            Debug.LogWarning($"No NodeCollection found for road '{road.name}'.");
        }
    }
	// replaces depopulate nodes for persistent data
	public void ClearAndHideSelection()
	{
		if(_selectedRoad == null)	{
			return;
		}
                NodeCollection nodeCollection = _selectedRoad.GetComponent<NodeCollection>();
                if (nodeCollection == null)
                {
                    Debug.Log("no road to unselect");
					return;
                }
		EmissionHighlight(GetRenderers(nodeCollection.gameObject),false);		
		nodeCollection.HideNodes();
		Unselect();
		UpdateItemsWindow();
		UpdateGizmoState();
	}


	public GameObject PopulateNodesForRoad(GameObject roadObject)
    {
        GameObject result = PopulateNodesForRoadInternal(roadObject); // Refactor to avoid duplication
        if (result != null)
        {
            OnSelectionChanged?.Invoke(); // Notify after populating road
        }
        return result;
    }

	public GameObject PopulateNodesForRoadInternal(GameObject roadObject)
	{
		if (roadObject == null) return null;

		// Ensure only one road is ever selected
		GameObject[] allPaths = GameObject.FindGameObjectsWithTag("Path");
		foreach (GameObject path in allPaths)
		{
			DepopulateNodesForRoad(path);
		}

		Transform existingNodes = roadObject.transform.Find("Nodes");
		if (existingNodes != null) return existingNodes.gameObject; // Return existing NodeCollection if already populated

		// Get the ERRoad from the road network using PathDataHolder
		NodeCollection pathDataHolder = roadObject.GetComponent<NodeCollection>();
		if (pathDataHolder == null || pathDataHolder.pathData == null)
		{
			Debug.LogError($"No PathDataHolder or PathData found on {roadObject.name}. Cannot populate nodes.");
			return null;
		}

		ERRoadNetwork roadNetwork = new ERRoadNetwork();
		ERRoad road = roadNetwork.GetRoadByName(pathDataHolder.pathData.name);
		if (road == null)
		{
			Debug.LogError($"Could not find ERRoad for {pathDataHolder.pathData.name} in the road network.");
			return null;
		}

		AppManager.Instance.ActivateWindow(9);

		GameObject nodeContainer = new GameObject("Nodes");
		nodeContainer.tag = "NodeParent";
		nodeContainer.transform.SetParent(roadObject.transform, false);
		NodeCollection nodeCollection = nodeContainer.AddComponent<NodeCollection>();
		nodeCollection.Initialize(road);
		nodeCollection.pathData = pathDataHolder.pathData; // Set pathData directly
		nodeCollection.PopulateNodes();

		_selectedRoad = roadObject;

		// Update the ItemsWindow tree
		if (ItemsWindow.Instance != null)
		{
			// Find the tree node corresponding to the roadObject
			Node pathNode = ItemsWindow.Instance.tree.FindFirstNodeByDataRecursive(roadObject);
			if (pathNode != null)
			{
				// Update the node's data to reference the NodeCollection
				pathNode.data = nodeContainer;

				// Clear existing child nodes (if any) to avoid duplicates
				pathNode.nodes.Clear();

				// Add each node from the NodeCollection as a child node in the tree
				foreach (Transform nodeTransform in nodeCollection.GetNodes())
				{
					if (nodeTransform != null)
					{
						Node childNode = new Node(nodeTransform.name) { data = nodeTransform.gameObject };
						pathNode.nodes.AddWithoutNotify(childNode);
					}
				}

				// Rebuild the tree to reflect changes
				ItemsWindow.Instance.tree.Rebuild();
				Debug.Log($"Updated tree for path '{roadObject.name}' with {nodeCollection.GetNodes().Count} nodes.");
			}
			else
			{
				Debug.LogWarning($"Could not find tree node for path '{roadObject.name}' to update with NodeCollection.");
			}
		}

		// Optionally log if no nodes were created (for debugging)
		if (nodeContainer.transform.childCount == 0)
		{
			Debug.LogWarning($"No nodes were populated for road {roadObject.name}. Selection set to road object anyway.");
		}

		Transform firstNode = nodeCollection.GetFirstNode();
		return firstNode != null ? firstNode.gameObject : null;
	}

public void DepopulateNodesForRoad(GameObject roadObject)
{
	PathManager.HideCurrentNodeCollection();

}
	
	public void SelectPrefabLight(GameObject go)
	{
		_selectedObjects.Add(go);		
	}
	
	public void UnselectPrefabLight(GameObject go)
	{
		_selectedObjects.Remove(go);
	}
	
	
    public void SelectPrefabWithoutNotify(GameObject go)
    {
        _selectedObjects.Add(go);
        AppManager.Instance.SetInspector(go);
        EmissionHighlight(GetRenderers(go), true);
        UpdateGizmoState();
        OnSelectionChanged?.Invoke(); // Notify listeners
    }

    public void SelectPrefab(GameObject go)
    {
        _selectedObjects.Add(go);
        AppManager.Instance.SetInspector(go);
        EmissionHighlight(GetRenderers(go), true);
        UpdateItemsWindow();
        UpdateGizmoState();
        OnSelectionChanged?.Invoke(); // Notify listeners
    }
	
	public void SelectSocketSoft(GameObject go)
    {
		//this is supposed to highlight and track in inspector without enabling the transform gizmo
		//however the gizmo still appears
		
        _selectedObjects.Add(go);
        AppManager.Instance.SetInspector(go);
        EmissionHighlight(GetRenderers(go), true);
        //OnSelectionChanged?.Invoke(); // Notify listeners
		_workGizmo.Gizmo.SetEnabled(false);
    }
	
public void SelectPrefab()
{
	int volumeLayerMask = 1 << 11; // volume layer
    int prefabLayerMask = 1 << 3; // prefab layer
    int landLayerMask = 1 << 10; // land layer
    int allLayersMask = ~0; // all layers

    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
    RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, allLayersMask);

    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

    float landDistance = float.MaxValue;
    foreach (var hit in hits)
    {
        if ((1 << hit.transform.gameObject.layer & landLayerMask) != 0)
        {
            landDistance = hit.distance;
            break;
        }
    }

    List<RaycastHit> prioritizedHits = new List<RaycastHit>();
    foreach (var hit in hits)
    {
        if ((1 << hit.transform.gameObject.layer & prefabLayerMask) != 0 || 
             (1 << hit.transform.gameObject.layer & volumeLayerMask) != 0 && hit.distance < landDistance)
        {
            prioritizedHits.Add(hit);
        }
    }
    foreach (var hit in hits)
    {
        if ((1 << hit.transform.gameObject.layer & (1 << 2)) != 0 && hit.distance < landDistance)
        {
            prioritizedHits.Add(hit);
        }
    }

    bool hitsUnchanged = prioritizedHits.SequenceEqual(previousHits);
    if (hitsUnchanged && prioritizedHits.Count > 1)
    {
        currentSelectionIndex = (currentSelectionIndex + 1) % prioritizedHits.Count;
    }
    else
    {
        currentSelectionIndex = 0;
    }
    previousHits = new List<RaycastHit>(prioritizedHits);

    GameObject hitPrefab = null;
    if (prioritizedHits.Count > 0)
    {
        hitPrefab = prioritizedHits[currentSelectionIndex].transform.gameObject;
    }

    if (hitPrefab != null)
    {
        GameObject hitObject = Keyboard.current.ctrlKey.isPressed
            ? FindParentWithTag(hitPrefab, "Prefab")
            : FindParentWithTag(hitPrefab, "Collection") ?? FindParentWithTag(hitPrefab, "Prefab");

        if (hitObject != null)
        {
            if (_selectedObjects.Contains(hitObject))
            {
                Unselect(hitObject);
                return;
            }

            // Clear previous checkmarks unless shift is pressed
            if (!Keyboard.current.leftShiftKey.isPressed)
            {
                ItemsWindow.Instance?.UnselectAllInTree();
            }

            if (Keyboard.current.leftShiftKey.isPressed)
            {
                _selectedObjects.Add(hitObject);
            }
            else
            {
                Unselect(); // Clear previous selection
                _selectedObjects.Add(hitObject);
            }

            Node node = itemTree?.FindFirstNodeByDataRecursive(hitObject);
            if (node != null && ItemsWindow.Instance != null)
            {
                ItemsWindow.Instance.FocusList(node);
            }
			
			/*
            List<LODMasterMesh> lodMasterMeshes = new List<LODMasterMesh>(hitObject.GetComponentsInChildren<LODMasterMesh>());
            if (lodMasterMeshes.Count > 0)
            {
                List<Renderer> renderers = new List<Renderer>();
                foreach (var lodMasterMesh in lodMasterMeshes)
                {
                    renderers.AddRange(lodMasterMesh.FetchRenderers());
                }
                EmissionHighlight(renderers, true);
            }
			
            else
            {
                EmissionHighlight(GetRenderers(hitObject), true);
            }
			*/
			EmissionHighlight(GetRenderers(hitObject), true);
            UpdateItemsWindow();
            UpdateGizmoState();
            return;
        }
    }

    if (!Keyboard.current.leftShiftKey.isPressed)
    {
        ItemsWindow.Instance?.UnselectAllInTree();
        Unselect();
    }
}
		
public void PaintNodes()
{
    //if (!Keyboard.current.altKey.isPressed)
     //   return;

    RaycastHit localHit;
    if (Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out localHit, Mathf.Infinity, layerMask))
    {
        Vector3 hitPoint = localHit.point;

        if (CameraManager.Instance._selectedRoad == null)
        {
            // Create a new road
            GameObject newRoadObject = PathManager.CreatePathAtPosition(hitPoint);
			
			
            if (newRoadObject == null)
            {
                Debug.LogError("Failed to create new road.");
                return;
            }

            // Select the new road and its first node
            Unselect();
            NodeCollection nodeCollection = newRoadObject.GetComponent<NodeCollection>();
            if (nodeCollection == null)
            {
                Debug.LogError($"NodeCollection not found on new road '{newRoadObject.name}'.");
                return;
            }

            _selectedRoad = newRoadObject;
			
			//nodeCollection.AddNodeAtPosition(hitPoint, _selectedObjects, 25f);
			
            Transform firstNode = nodeCollection.GetFirstNode();
            if (firstNode != null)
            {
                _selectedObjects.Add(firstNode.gameObject);
                EmissionHighlight(GetRenderers(firstNode.gameObject), true);
            }
            else
            {
                Debug.LogWarning($"No first node found for new road '{newRoadObject.name}'.");
            }

            // Sync with UI
            UpdateItemsWindow();
            UpdateGizmoState();
            if (PathWindow.Instance != null)
                PathWindow.Instance.SetSelection(newRoadObject);

            Debug.Log($"Created and selected new road '{newRoadObject.name}' at {hitPoint}.");
        }
        else
        {
            // Add node to existing road
            NodeCollection nodeCollection = _selectedRoad.GetComponent<NodeCollection>();
            if (nodeCollection == null)
            {
                Debug.LogError($"NodeCollection not found on selected road '{_selectedRoad.name}'.");
                return;
            }

            // Clear current selection and add the new node
            _selectedObjects.Clear();
            nodeCollection.AddNodeAtPosition(hitPoint, _selectedObjects, 25f);


            // Sync with UI
            UpdateItemsWindow();
            UpdateGizmoState();
            if (PathWindow.Instance != null)
                PathWindow.Instance.UpdateData();

            Debug.Log($"Added node to road '{_selectedRoad.name}' at {hitPoint}.");
        }
    }
}
	
public void DragNodes()
{
    RaycastHit localHit;
    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

    if (Physics.Raycast(ray, out localHit, Mathf.Infinity, layerMask))
    {
        Vector3 hitPoint = localHit.point;

        if (CameraManager.Instance._selectedRoad == null)
        {
            // Create a new road
            GameObject newRoadObject = PathManager.CreatePathAtPosition(hitPoint);
            if (newRoadObject == null)
            {
                Debug.LogError("Failed to create new road.");
                return;
            }

            // Select the new road and its first node
            Unselect();
            NodeCollection nodeCollection = newRoadObject.GetComponent<NodeCollection>();
            if (nodeCollection == null)
            {
                Debug.LogError($"NodeCollection not found on new road '{newRoadObject.name}'.");
                return;
            }

            _selectedRoad = newRoadObject;
			
			//nodeCollection.AddNodeAtPosition(hitPoint, _selectedObjects, 25f);
			
            Transform firstNode = nodeCollection.GetFirstNode();
            if (firstNode != null)
            {
                _selectedObjects.Add(firstNode.gameObject);
                EmissionHighlight(GetRenderers(firstNode.gameObject), true);
            }
            else
            {
                Debug.LogWarning($"No first node found for new road '{newRoadObject.name}'.");
            }

            // Sync with UI
            UpdateItemsWindow();
            UpdateGizmoState();
            if (PathWindow.Instance != null)
                PathWindow.Instance.SetSelection(newRoadObject);

            Debug.Log($"Created and selected new road '{newRoadObject.name}' at {hitPoint}.");
        }
        else
        {
            // Add node to existing road
            NodeCollection nodeCollection = _selectedRoad.GetComponent<NodeCollection>();
            if (nodeCollection == null)
            {
                Debug.LogError($"NodeCollection not found on selected road '{_selectedRoad.name}'.");
                return;
            }

            // Clear current selection and add the new node
            _selectedObjects.Clear();
            nodeCollection.AddNodeAtPosition(hitPoint, _selectedObjects, 25f);


            // Sync with UI
            UpdateItemsWindow();
            UpdateGizmoState();
            if (PathWindow.Instance != null)
                PathWindow.Instance.UpdateData();

            Debug.Log($"Added node to road '{_selectedRoad.name}' at {hitPoint} during drag.");
        }
    }
}
	
	public void Unselect(GameObject obj)
	{
		_selectedObjects.Remove(obj);
		if(obj.tag != "Path"){			
			EmissionHighlight(GetRenderers(obj),false); // Get the renderers from the object using helper method
		}
		UpdateItemsWindow();
		UpdateGizmoState();
	}
	
	public void UnselectChecked(GameObject obj)
	{
		_selectedObjects.Remove(obj);
		EmissionHighlight(GetRenderers(obj),false); // Get the renderers from the object using helper method
		//UpdateItemsWindow();
		//UpdateGizmoState();
	}
	
	public void Unselect()
	{
		foreach (GameObject obj in _selectedObjects)
		{
			if (obj != null)
			{
				EmissionHighlight(GetRenderers(obj), false);
			}
		}
		_selectedObjects.Clear();
		
		if(_selectedRoad != null){			
			NodeCollection nodeCollection = _selectedRoad.GetComponent<NodeCollection>();
			if (nodeCollection!=null){
				EmissionHighlight(GetRenderers(nodeCollection.gameObject),false);
				nodeCollection.HideNodes();
			}
			else{
				Debug.LogError("node collection not found, deselection failed");
			}
			_selectedRoad = null;
		}

		if (_objectMoveGizmo != null) _objectMoveGizmo.Gizmo.SetEnabled(false);
		if (_objectRotationGizmo != null) _objectRotationGizmo.Gizmo.SetEnabled(false);
		if (_objectScaleGizmo != null) _objectScaleGizmo.Gizmo.SetEnabled(false);
		if (_objectUniversalGizmo != null) _objectUniversalGizmo.Gizmo.SetEnabled(false);
		if (_workGizmo != null)
		{
			_workGizmo.Gizmo.SetEnabled(false);
			_workGizmo.SetTargetObjects(new List<GameObject>());
		}

		if (PathWindow.Instance != null)
		{
			Debug.Log("Unselect: Road was selected, resetting PathWindow");
			PathWindow.Instance.ClearUI(); // Reset potentialPathData and sync UI
			PathWindow.Instance.UpdateData(); // Ensure UI reflects the no-selection state
		}
		
		OnSelectionChanged?.Invoke();
	}

	public List<Renderer> GetRenderers(GameObject gameObject)
	{
		List<Renderer> renderers = new List<Renderer>();
		
		//List<LODMasterMesh> lodMasterMeshes = new List<LODMasterMesh>(gameObject.GetComponentsInChildren<LODMasterMesh>());
		/*		
		//add renderers from hlod
		if (lodMasterMeshes.Count > 0)
			{
				foreach (var lodMasterMesh in lodMasterMeshes)
					{
						renderers.AddRange(lodMasterMesh.FetchRenderers());
					}					
				return renderers;
			}
		
		*/
		//add renderers by recursive traversal (too expensive for large objects)
		AddRenderersFromChildren(ref renderers, gameObject);

		return renderers;
	}
	
	public void AddRenderersFromChildren(ref List<Renderer> renderers, GameObject obj)
	{
		// Check if the GameObject itself has a Renderer component
		Renderer renderer = obj.GetComponent<Renderer>();

		renderers.Add(renderer);

		// Recursively check all children
		foreach (Transform child in obj.transform)
		{
			AddRenderersFromChildren(ref renderers, child.gameObject);
		}
	}

public void FadeSelection()
{
    foreach (GameObject obj in _selectedObjects)
    {
        if (obj == null) continue;

        List<Renderer> renderers = GetRenderers(obj);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;

            Material[] materials = renderer.materials;
            foreach (Material material in materials)
            {
                if (material.HasProperty("_Color") || material.HasProperty("_BaseColor"))
                {
                    // Get the current color (try _Color for Standard shader, _BaseColor for URP/HDRP)
                    Color currentColor = material.HasProperty("_Color") 
                        ? material.GetColor("_Color") 
                        : material.GetColor("_BaseColor");

                    float currentAlpha = currentColor.a;
                    float newAlpha = currentAlpha > 0.5f ? 0.5f : 1.0f;

                    // Update the color with the new alpha
                    currentColor.a = newAlpha;
                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", currentColor);
                    }
                    else
                    {
                        material.SetColor("_BaseColor", currentColor);
                    }

                    // Ensure the material is set to a transparent rendering mode if alpha is modified
                    if (newAlpha < 1.0f)
                    {
                        material.SetFloat("_Mode", 3); // Transparent mode
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.EnableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    }
                    else
                    {
                        // Revert to opaque mode if alpha is 1.0
                        material.SetFloat("_Mode", 0); // Opaque mode
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.SetInt("_ZWrite", 1);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.DisableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    }
                }
            }
            renderer.materials = materials; // Apply changes
        }
    }
}

public void EmissionHighlight(List<Renderer> selection, bool enable)
{
    foreach (Renderer renderer in selection)
    {
        if (renderer == null) { continue; }

        try
        {
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);

            // Check if the material supports the required properties
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_SelectionOn"))
            {
                Color selectColor = AppManager.Instance.color4;
                propertyBlock.SetColor("_SelectionColor", selectColor);
                propertyBlock.SetFloat("_SelectionOn", enable ? 1.0f : 0.0f);

                // Apply shader keyword for selection (if needed)
                if (enable)
                    propertyBlock.SetFloat("_SELECTION_ON", 1.0f); // Enable keyword via property
                else
                    propertyBlock.SetFloat("_SELECTION_ON", 0.0f); // Disable keyword via property

                renderer.SetPropertyBlock(propertyBlock);

                //Debug.Log($"EmissionHighlight: Set _SelectionOn={enable ? 1.0f : 0.0f}, _SelectionColor={selectColor} on {renderer.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"Material on {renderer.gameObject.name} lacks '_SelectionOn'. Shader: {renderer.sharedMaterial?.shader.name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error highlighting {renderer.gameObject.name}: {e.Message}");
        }
    }
}

	/*
	// Helper method to undo emission highlight
	private void EmissionUnhighlight(List<Renderer> selection)
	{
		if (selection == null) return;

		foreach (Renderer renderer in selection)
		{
			if (renderer == null) continue;
			
			Material[] materials = renderer.materials;
			
			for (int i = 0; i < materials.Length; i++)
			{
				Material material = materials[i];
				if (material == null) continue;

				// Disable emission for the material
				material.DisableKeyword("_EMISSION");
				
				// Reset emission color to black
				material.SetColor("_EmissionColor", Color.black);

			}

				renderer.materials = materials;

		}
	}
*/
	
	private void SetWorkGizmoId(GizmoId gizmoId)
    {
        if (gizmoId == _workGizmoId) return;

        // Disable all gizmos first
        _objectMoveGizmo.Gizmo.SetEnabled(false);
        _objectRotationGizmo.Gizmo.SetEnabled(false);
        _objectScaleGizmo.Gizmo.SetEnabled(false);
        _objectUniversalGizmo.Gizmo.SetEnabled(false);


        // Set the new work gizmo
        _workGizmoId = gizmoId;
        switch(gizmoId)
        {
            case GizmoId.Move:
                _workGizmo = _objectMoveGizmo;
                break;
            case GizmoId.Rotate:
                _workGizmo = _objectRotationGizmo;
                break;
            case GizmoId.Scale:
                _workGizmo = _objectScaleGizmo;
                break;
            case GizmoId.Universal:
                _workGizmo = _objectUniversalGizmo;
                break;
        }

        // Enable the work gizmo if there are selected objects
        if (_selectedObjects.Count > 0)
        {
			if(SocketManager.firstSocket!=null){	//keep gizmo hidden when connecting sockets
				return;
			}
            _workGizmo.Gizmo.SetEnabled(true);
            _workGizmo.SetTargetPivotObject(_selectedObjects[_selectedObjects.Count - 1]);
            _workGizmo.RefreshPositionAndRotation();
        }
    }
	
	private void ToggleGizmoSpace()
	{
		// Check if any gizmo is null to avoid errors
		if (_objectMoveGizmo == null || _objectRotationGizmo == null || 
			_objectScaleGizmo == null || _objectUniversalGizmo == null) return;

		// Get the current space from the move gizmo as a reference (assume they’re all synced)
		bool isLocal = _objectMoveGizmo.TransformSpace == GizmoSpace.Local;
		
		// Toggle to the opposite space
		GizmoSpace newSpace = isLocal ? GizmoSpace.Global : GizmoSpace.Local;

		// Apply the new space to all gizmos
		_objectMoveGizmo.SetTransformSpace(newSpace);
		_objectRotationGizmo.SetTransformSpace(newSpace);
		_objectScaleGizmo.SetTransformSpace(newSpace);
		_objectUniversalGizmo.SetTransformSpace(newSpace);

		// Refresh the active work gizmo’s position and rotation
		if (_workGizmo != null && _workGizmo.Gizmo.IsEnabled)
		{
			_workGizmo.RefreshPositionAndRotation();
		}

		Debug.Log($"Gizmo space switched to {newSpace}");
	}
	
		
	
	public void UpdateGizmoState()
	{
		if (_workGizmo == null)
		{
			Debug.LogWarning("Work gizmo is null. Cannot update gizmo state.");
			return;
		}

		// Filter out null or destroyed objects from _selectedObjects
		//_selectedObjects.RemoveAll(obj => obj == null);
		
		if (_selectedObjects.Count > 0)
		{
			if(SocketManager.firstSocket!=null){	//keep gizmo hidden when connecting sockets
				return;
			}
			// Ensure all objects are still valid
			bool hasValidObjects = false;
			GameObject lastValidObject = null;

			for (int i = _selectedObjects.Count - 1; i >= 0; i--)
			{
				if (_selectedObjects[i] != null)
				{
					hasValidObjects = true;
					lastValidObject = _selectedObjects[i]; // Last valid object for pivot
					break; // We only need the last valid one for the pivot
				}
			}

			if (hasValidObjects && lastValidObject != null)
			{
				_workGizmo.Gizmo.SetEnabled(true);
				_workGizmo.SetTargetObjects(_selectedObjects); // Set all objects as targets
				try
				{
					_workGizmo.SetTargetPivotObject(lastValidObject); // Set pivot to last valid object
					_workGizmo.RefreshPositionAndRotation();
				}
				catch (MissingReferenceException ex)
				{
					Debug.LogWarning($"Missing reference encountered while updating gizmo state: {ex.Message}. Disabling gizmo.");
					_workGizmo.Gizmo.SetEnabled(false);
					_workGizmo.SetTargetObjects(new List<GameObject>()); // Clear targets
				}
			}
			else
			{
				// No valid objects left after filtering
				_workGizmo.Gizmo.SetEnabled(false);
				_workGizmo.SetTargetObjects(new List<GameObject>()); // Clear targets
			}
		}
		else
		{
			// No selected objects
			_workGizmo.Gizmo.SetEnabled(false);
			_workGizmo.SetTargetObjects(new List<GameObject>()); // Clear targets to avoid stale references
		}
	}
	
	void DragTransform()
	{
		Vector2 mousePosition = mouse.position.ReadValue();
		Ray ray = cam.ScreenPointToRay(mousePosition);
		if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, worldToolLayer))
		{
			Debug.Log($"Hit object: {hit.collider.gameObject.name}");

			if (hit.collider.gameObject == xPos)
			{
				dragXarrow = true;
			}
			else if (hit.collider.gameObject == yPos)
			{
				dragYarrow = true;
			}
			else if (hit.collider.gameObject == zPos)
			{
				dragZarrow = true;
			}
		}
		else
		{
			Debug.Log("Raycast did not hit any objects.");
		}
	}
	
	public void OnMapLoad(string mapName=""){
		Debug.Log("mapName map loaded");
		

		
		if (ItemsWindow.Instance!=null){
			ItemsWindow.Instance.PopulateList();
		}

	}
	
	public void CenterCamera()
	{
		if (cam == null || landTerrain == null)
		{
			Debug.LogWarning("Camera or Land terrain reference is missing.");
			return;
		}


		Bounds terrainBounds = landTerrain.terrainData.bounds;
		Vector3 terrainWorldCenter = landTerrain.transform.TransformPoint(terrainBounds.center);
		Vector3 terrainWorldSize = landTerrain.transform.TransformVector(terrainBounds.size);

		float distance = Mathf.Max(terrainWorldSize.x, terrainWorldSize.z) / (2 * Mathf.Tan(Mathf.Deg2Rad * cam.fieldOfView / 2));

		cam.transform.position = terrainWorldCenter + Vector3.up * distance;
		position = cam.transform.position;
		
		cam.transform.rotation = Quaternion.Euler(90,0,0);
		
		pitch = 90f;
		yaw = 0f;
	}
	
	private void SetupSnapListeners()
    {

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                snapFields[i].onEndEdit.AddListener((text) => UpdateSnapSettings());
            }

    }

private void UpdateSnapSettings()
{
	
    if (_workGizmo == null) return;


    float moveSnap = ParseSnapValue(snapFields[0].text);
    float rotateSnap = ParseSnapValue(snapFields[1].text);
    float scaleSnap = ParseSnapValue(snapFields[2].text);

    // Apply settings directly to the current work gizmo based on its type
    switch (_workGizmoId)
    {
        case GizmoId.Move:
            MoveGizmo moveGizmo = _workGizmo.Gizmo.GetFirstBehaviourOfType<MoveGizmo>();
            if (moveGizmo != null)
            {
                moveGizmo.SetSnapEnabled(true);

                    moveGizmo.Settings3D.SetXSnapStep(moveSnap);
                    moveGizmo.Settings3D.SetYSnapStep(moveSnap);
                    moveGizmo.Settings3D.SetZSnapStep(moveSnap);


            }
            break;

        case GizmoId.Rotate:
            RotationGizmo rotationGizmo = _workGizmo.Gizmo.GetFirstBehaviourOfType<RotationGizmo>();
            if (rotationGizmo != null)
            {
                rotationGizmo.SetSnapEnabled(true);

                    rotationGizmo.Settings3D.SetAxisSnapStep(0, rotateSnap);
                    rotationGizmo.Settings3D.SetAxisSnapStep(1, rotateSnap);
                    rotationGizmo.Settings3D.SetAxisSnapStep(2, rotateSnap);


            }
            break;

        case GizmoId.Scale:
            ScaleGizmo scaleGizmo = _workGizmo.Gizmo.GetFirstBehaviourOfType<ScaleGizmo>();
            if (scaleGizmo != null)
            {
				scaleGizmo.SetSnapEnabled(true);


                    scaleGizmo.Settings3D.SetXSnapStep(scaleSnap);
                    scaleGizmo.Settings3D.SetYSnapStep(scaleSnap);
                    scaleGizmo.Settings3D.SetZSnapStep(scaleSnap);


            }
            break;

        case GizmoId.Universal:
            UniversalGizmo universalGizmo = _workGizmo.Gizmo.GetFirstBehaviourOfType<UniversalGizmo>();
            if (universalGizmo != null)
            {

                universalGizmo.SetSnapEnabled(true);
				
                    universalGizmo.Settings3D.SetMvXSnapStep(moveSnap);
                    universalGizmo.Settings3D.SetMvYSnapStep(moveSnap);
                    universalGizmo.Settings3D.SetMvZSnapStep(moveSnap);

                    universalGizmo.Settings3D.SetRtAxisSnapStep(0, rotateSnap);
                    universalGizmo.Settings3D.SetRtAxisSnapStep(1, rotateSnap);
                    universalGizmo.Settings3D.SetRtAxisSnapStep(2, rotateSnap);

                    universalGizmo.Settings3D.SetScXSnapStep(scaleSnap);
                    universalGizmo.Settings3D.SetScYSnapStep(scaleSnap);
                    universalGizmo.Settings3D.SetScZSnapStep(scaleSnap);

            }
            break;
    }
	
	UpdateGizmoState();

}
    private float ParseSnapValue(string text)
    {
        if (string.IsNullOrEmpty(text) || !float.TryParse(text, out float value) || value <= 0f)
        {
            return 0f; // Return 0 to disable snapping
        }
        return value;
    }

	
	
}