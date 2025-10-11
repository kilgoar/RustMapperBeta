using System;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UIRecycleTreeNamespace;
using static WorldSerialization;

public class InspectorWindow : MonoBehaviour
{
    public Text footer, selection;
    public Button findInBreaker, saveCustom, applyTerrain;
	
	public Button mirrorScaleXButton;
	public Button mirrorScaleYButton;
	public Button mirrorScaleZButton;
	
	public Button mirrorPositionXButton;
	public Button mirrorPositionYButton;
	public Button mirrorPositionZButton;
	
	public Button rotateXPlus90Button;
	public Button rotateXMinus90Button;
	public Button rotateYPlus90Button;
	public Button rotateYMinus90Button;
	public Button rotateZPlus90Button;
	public Button rotateZMinus90Button;
	
	public enum Axis { X, Y, Z }
	public enum RotationDirection { Plus90, Minus90 }
	
	public GameObject socketPanel;
	public Toggle male, female, horizontal, vertical, collidersEnabled;
	
    public List<InputField> prefabDataFields = new List<InputField>();
    public List<InputField> snapFields = new List<InputField>();
    
    private GameObject _lastProcessedSelection;
    private Vector3 _lastPosition;
    private Vector3 _lastRotation;
    private Vector3 _lastScale;
    private string _lastCategory; // For collections
    private uint _lastId; // For prefabs
    public static InspectorWindow Instance { get; private set; }

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
    
    private void Start()
    {
        InitializeComponents();
    }
    
    private void InitializeComponents()
    {    
        findInBreaker.onClick.AddListener(FindBreakerWithPath);
        applyTerrain.onClick.AddListener(ApplyTerrain);    

        saveCustom.onClick.AddListener(() =>
        {
            if (CameraManager.Instance._selectedObjects.Count > 0)
            {
                SaveCollection(CameraManager.Instance._selectedObjects[^1]);
				HierarchyWindow.Instance?.LoadTree();
            }
            else
            {
                Debug.LogWarning("No object selected to save as a collection.");
            }
        });
		
	        // Add listeners for socket toggles
        male.onValueChanged.AddListener(isOn => UpdateSocketData(_lastProcessedSelection));
        female.onValueChanged.AddListener(isOn => UpdateSocketData(_lastProcessedSelection));
		
		    // Modified listeners to ensure mutual exclusivity
			horizontal.onValueChanged.AddListener(isOn => 
			{
				if (isOn)
				{
					UpdateSocketType(_lastProcessedSelection, true, false);
				}
				else if (vertical.isOn)
				{
					UpdateSocketType(_lastProcessedSelection, false, true);
				}
				else
				{
					// Prevent both from being off
					horizontal.SetIsOnWithoutNotify(true);
					UpdateSocketType(_lastProcessedSelection, true, false);
				}
			});
			
			vertical.onValueChanged.AddListener(isOn => 
			{
				if (isOn)
				{
					UpdateSocketType(_lastProcessedSelection, false, true);
				}
				else if (horizontal.isOn)
				{
					UpdateSocketType(_lastProcessedSelection, true, false);
				}
				else
				{
					// Prevent both from being off
					horizontal.SetIsOnWithoutNotify(true);
					UpdateSocketType(_lastProcessedSelection, true, false);
				}
			});
			
			
			// Add listeners for mirror scale buttons
			mirrorScaleXButton.onClick.AddListener(() => MirrorRotations(CameraManager.Instance._selectedObjects, Axis.X));
			mirrorScaleYButton.onClick.AddListener(() => MirrorRotations(CameraManager.Instance._selectedObjects, Axis.Y));
			mirrorScaleZButton.onClick.AddListener(() => MirrorRotations(CameraManager.Instance._selectedObjects, Axis.Z));
			
			mirrorPositionXButton.onClick.AddListener(() => MirrorPositions(CameraManager.Instance._selectedObjects, Axis.X));
			mirrorPositionYButton.onClick.AddListener(() => MirrorPositions(CameraManager.Instance._selectedObjects, Axis.Y));
			mirrorPositionZButton.onClick.AddListener(() => MirrorPositions(CameraManager.Instance._selectedObjects, Axis.Z));

			// Add listeners for rotation buttons
			rotateXPlus90Button.onClick.AddListener(() => Rotate90(CameraManager.Instance._selectedObjects, Axis.X, RotationDirection.Plus90));
			rotateXMinus90Button.onClick.AddListener(() => Rotate90(CameraManager.Instance._selectedObjects, Axis.X, RotationDirection.Minus90));
			rotateYPlus90Button.onClick.AddListener(() => Rotate90(CameraManager.Instance._selectedObjects, Axis.Y, RotationDirection.Plus90));
			rotateYMinus90Button.onClick.AddListener(() => Rotate90(CameraManager.Instance._selectedObjects, Axis.Y, RotationDirection.Minus90));
			rotateZPlus90Button.onClick.AddListener(() => Rotate90(CameraManager.Instance._selectedObjects, Axis.Z, RotationDirection.Plus90));
			rotateZMinus90Button.onClick.AddListener(() => Rotate90(CameraManager.Instance._selectedObjects, Axis.Z, RotationDirection.Minus90));
			
			collidersEnabled.onValueChanged.AddListener(isOn => UpdateColliderStates(isOn));
	}
  
    public void RetrieveSocketData(GameObject go)
    {
        // Disable socket panel by default
        socketPanel.SetActive(false);

        if (go == null) return;

        DungeonBaseSocket socket = go.GetComponent<DungeonBaseSocket>();
        if (socket != null)
        {
            // Enable socket panel if the GameObject has a DungeonBaseSocket component
            socketPanel.SetActive(true);

            // Set toggle states based on socket data
            male.SetIsOnWithoutNotify(socket.Male);
            female.SetIsOnWithoutNotify(socket.Female);
            horizontal.SetIsOnWithoutNotify(socket.Type == DungeonBaseSocketType.Horizontal);
            vertical.SetIsOnWithoutNotify(socket.Type == DungeonBaseSocketType.Vertical);

            // Validate Male/Female toggles
            if (!male.isOn && !female.isOn)
            {
                // Enforce rule: At least one must be active
                male.SetIsOnWithoutNotify(true);
                socket.Male = true;
            }

            // Validate Horizontal/Vertical toggles
            if (!horizontal.isOn && !vertical.isOn)
            {
                // Enforce rule: At least one must be active (default to Horizontal if none selected)
                horizontal.SetIsOnWithoutNotify(true);
                socket.Type = DungeonBaseSocketType.Horizontal;
            }
        }
    }
	

	
	private void UpdateSocketData(GameObject go)
    {
        if (go == null) return;

        DungeonBaseSocket socket = go.GetComponent<DungeonBaseSocket>();
        if (socket != null)
        {
            // Update Male/Female values
            socket.Male = male.isOn;
            socket.Female = female.isOn;

            // Enforce validation: At least one must be active
            if (!socket.Male && !socket.Female)
            {
                // If both are turned off, force Male to true
                male.SetIsOnWithoutNotify(true);
                socket.Male = true;
            }
        }
    }

	private void UpdateSocketType(GameObject go, bool isHorizontalOn, bool isVerticalOn)
	{
		if (go == null) return;

		DungeonBaseSocket socket = go.GetComponent<DungeonBaseSocket>();
		if (socket != null)
		{
			//Debug.Log($"Updating socket: Horizontal={isHorizontalOn}, Vertical={isVerticalOn}");
			horizontal.SetIsOnWithoutNotify(isHorizontalOn);
			vertical.SetIsOnWithoutNotify(isVerticalOn);
			socket.Type = isHorizontalOn ? DungeonBaseSocketType.Horizontal : DungeonBaseSocketType.Vertical;
			//Debug.Log($"Socket Type set to: {socket.Type}");
		}
	}

    public void DestroySocketListeners()
    {
        male.onValueChanged.RemoveAllListeners();
        female.onValueChanged.RemoveAllListeners();
        horizontal.onValueChanged.RemoveAllListeners();
        vertical.onValueChanged.RemoveAllListeners();
    }
    
    public void CreateListeners(GameObject go)
    {
        for (int i = 0; i < 9; i++)
        {
            int index = i;
            prefabDataFields[i].onEndEdit.AddListener(text =>
            {
                if (float.TryParse(text, out float value))
                {
                    UpdatePrefabData(go, index, value);
                }
                SendPrefabData(go);
            });
        }

        prefabDataFields[9].onEndEdit.AddListener(text => 
        {
            SendPrefabData(go);
        });

        prefabDataFields[10].onEndEdit.AddListener(text =>
        {
            if (uint.TryParse(text, out uint id) && go.GetComponent<PrefabDataHolder>() is PrefabDataHolder holder)
            {
                holder.prefabData.id = id;
                SendPrefabData(go);
            }
        });
    }
    
	private void ApplyTerrain()
	{
		if (CameraManager.Instance._selectedObjects.Count == 0) return;

		foreach (GameObject selected in CameraManager.Instance._selectedObjects)
		{
			TraverseAndApply(selected);
		}
	}

	private void TraverseAndApply(GameObject obj)
	{
		TerrainPlacement terrainPlacement = obj.GetComponent<TerrainPlacement>();
		if (terrainPlacement != null)
		{
			TerrainBounds dimensions = new TerrainBounds();
			Vector3 position = obj.transform.position;
			Quaternion rotation = obj.transform.rotation;
			Vector3 scale = obj.transform.localScale;

			terrainPlacement.ApplyHeight(position, rotation, scale, dimensions);
			terrainPlacement.ApplySplat(position, rotation, scale, dimensions);
			terrainPlacement.ApplyTopology(position, rotation, scale, dimensions);
			terrainPlacement.ApplyAlpha(position, rotation, scale, dimensions);
			terrainPlacement.ApplyWater(position, rotation, scale, dimensions);
			return; // End traversal on this branch
		}

		AddToHeightMap heightMap = obj.GetComponent<AddToHeightMap>();
		if (heightMap != null)
		{
			heightMap.Apply();
			return; // End traversal on this branch
		}

		// Continue traversal to children
		for (int i = 0; i < obj.transform.childCount; i++)
		{
			TraverseAndApply(obj.transform.GetChild(i).gameObject);
		}
	}
    
    public void SaveCollection(GameObject go)
    {
        if (go == null)
        {
            Debug.LogWarning("No collection provided to save.");
            return;
        }

        string savePath = System.IO.Path.Combine(SettingsManager.AppDataPath(), "custom", $"{go.name}.prefab");

        string customDir = System.IO.Path.Combine(SettingsManager.AppDataPath(), "custom");
        if (!System.IO.Directory.Exists(customDir))
        {
            System.IO.Directory.CreateDirectory(customDir);
        }

        MapManager.SaveCollectionPrefab(savePath, go.transform);
        Debug.Log($"Collection saved to: {savePath}");
    }
    
    public void FindBreakerWithPath()
    {
        AppManager.Instance.ActivateWindow(5);
        if (BreakerWindow.Instance != null)
        {
            BreakerWindow.Instance.FocusByPath(prefabDataFields[9].text);
        }
    }
    
    public void DestroyListeners()
    {
        for (int i = 0; i < prefabDataFields.Count; i++)
        {
            prefabDataFields[i].onEndEdit.RemoveAllListeners();
        }
    }
    
    
    
    private bool HasTransformChanged(GameObject go)
    {
        if (go == null) return false;

        Transform transform = go.transform;
        PrefabDataHolder prefabHolder = go.GetComponent<PrefabDataHolder>();
        bool isCollection = go.CompareTag("Collection");

        // Check transform changes
        bool transformChanged = 
            !Mathf.Approximately(transform.localPosition.x, _lastPosition.x) ||
            !Mathf.Approximately(transform.localPosition.y, _lastPosition.y) ||
            !Mathf.Approximately(transform.localPosition.z, _lastPosition.z) ||
            !Mathf.Approximately(transform.eulerAngles.x, _lastRotation.x) ||
            !Mathf.Approximately(transform.eulerAngles.y, _lastRotation.y) ||
            !Mathf.Approximately(transform.eulerAngles.z, _lastRotation.z) ||
            !Mathf.Approximately(transform.localScale.x, _lastScale.x) ||
            !Mathf.Approximately(transform.localScale.y, _lastScale.y) ||
            !Mathf.Approximately(transform.localScale.z, _lastScale.z);

        // Check prefab-specific data if applicable
        bool dataChanged = false;
        if (prefabHolder != null && !isCollection)
        {
            PrefabData data = prefabHolder.prefabData;
            dataChanged = data.category != _lastCategory || data.id != _lastId;
        }
        else if (isCollection)
        {
            dataChanged = go.name != _lastCategory;
        }

        return transformChanged || dataChanged;
    }
    
    private void UpdateTransformSnapshot(GameObject go)
    {
        if (go == null) return;

        Transform transform = go.transform;
        _lastPosition = transform.localPosition;
        _lastRotation = transform.eulerAngles;
        _lastScale = transform.localScale;

        PrefabDataHolder prefabHolder = go.GetComponent<PrefabDataHolder>();
        bool isCollection = go.CompareTag("Collection");
        if (prefabHolder != null && !isCollection)
        {
            PrefabData data = prefabHolder.prefabData;
            _lastCategory = data.category;
            _lastId = data.id;
        }
        else
        {
            _lastCategory = isCollection ? go.name : string.Empty;
            _lastId = 0;
        }
    }
    
public void RetrievePrefabData(GameObject go)
{
    DestroyListeners();

    PrefabDataHolder prefabHolder = go.GetComponent<PrefabDataHolder>();
    bool isCollection = go.CompareTag("Collection");
    saveCustom.interactable = isCollection;
    applyTerrain.interactable = (go.GetComponent<TerrainPlacement>() != null || go.GetComponentInChildren<AddToHeightMap>() != null);

    // Determine whether to use local or global transform based on CameraManager.isLocal
    bool useLocal = CameraManager.Instance.isLocal;
    Transform transform = go.transform;

    // Set the coordinate mode indicator
    string coordinateMode = useLocal ? "(Local)" : "(Global)";

    if (prefabHolder != null && !isCollection)
    {
        PrefabData data = prefabHolder.prefabData;
        prefabHolder.UpdatePrefabData();

        // Use local or global transform data based on isLocal
        Vector3 position = transform.localPosition;
        Vector3 rotation = useLocal ? transform.localEulerAngles : transform.eulerAngles;
        Vector3 scale = transform.localScale;

        prefabDataFields[0].text = position.x.ToString("F3");
        prefabDataFields[1].text = position.y.ToString("F3");
        prefabDataFields[2].text = position.z.ToString("F3");

        prefabDataFields[3].text = rotation.x.ToString("F3");
        prefabDataFields[4].text = rotation.y.ToString("F3");
        prefabDataFields[5].text = rotation.z.ToString("F3");

        prefabDataFields[6].text = scale.x.ToString("F3");
        prefabDataFields[7].text = scale.y.ToString("F3");
        prefabDataFields[8].text = scale.z.ToString("F3");

        prefabDataFields[9].text = data.category;
        prefabDataFields[10].text = data.id.ToString();

        if (AssetManager.IDLookup.TryGetValue(data.id, out string name))
        {
            // Extract just the file name without path or extension
            string fileName = Path.GetFileNameWithoutExtension(name);
            selection.text = $"{fileName} {coordinateMode}";
        }
        else
        {
            selection.text = $"{data.id} {coordinateMode}";
        }
    }
    else
    {
        // Use local or global transform data based on isLocal
        Vector3 position = transform.localPosition;
        Vector3 rotation = useLocal ? transform.localEulerAngles : transform.eulerAngles;
        Vector3 scale = transform.localScale;

        prefabDataFields[0].text = position.x.ToString("F3");
        prefabDataFields[1].text = position.y.ToString("F3");
        prefabDataFields[2].text = position.z.ToString("F3");

        prefabDataFields[3].text = rotation.x.ToString("F3");
        prefabDataFields[4].text = rotation.y.ToString("F3");
        prefabDataFields[5].text = rotation.z.ToString("F3");

        prefabDataFields[6].text = scale.x.ToString("F3");
        prefabDataFields[7].text = scale.y.ToString("F3");
        prefabDataFields[8].text = scale.z.ToString("F3");

        prefabDataFields[9].text = isCollection ? go.name : string.Empty;
        prefabDataFields[10].text = isCollection ? string.Empty : go.name;
        selection.text = isCollection ? $"{go.name} {coordinateMode}" : coordinateMode;
    }

    CreateListeners(go);
}

public void SendPrefabData(GameObject go)
{
    Vector3 position = new Vector3(
        float.Parse(prefabDataFields[0].text),
        float.Parse(prefabDataFields[1].text),
        float.Parse(prefabDataFields[2].text));

    Vector3 rotation = new Vector3(
        float.Parse(prefabDataFields[3].text),
        float.Parse(prefabDataFields[4].text),
        float.Parse(prefabDataFields[5].text));

    Vector3 scale = new Vector3(
        float.Parse(prefabDataFields[6].text),
        float.Parse(prefabDataFields[7].text),
        float.Parse(prefabDataFields[8].text));

    PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
    bool useLocal = CameraManager.Instance.isLocal;

    if (holder != null && !go.CompareTag("Collection"))
    {
        PrefabData data = holder.prefabData;
        data.position = new VectorData(position.x, position.y, position.z);
        data.rotation = new VectorData(rotation.x, rotation.y, rotation.z);
        data.scale = new VectorData(scale.x, scale.y, scale.z);
        data.category = prefabDataFields[9].text;
        data.id = uint.Parse(prefabDataFields[10].text);
        holder.CastPrefabData();

        // Apply transform based on isLocal
        Transform transform = go.transform;
        if (useLocal)
        {
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(rotation);
        }
        else
        {
            transform.localPosition = position;
            transform.rotation = Quaternion.Euler(rotation);
        }
        transform.localScale = scale; // Scale is always local
    }
    else if (go.CompareTag("Collection") || go.CompareTag("Untagged")) // Untagged is for sockets
    {
        // Apply transform based on isLocal
        Transform transform = go.transform;
        if (useLocal)
        {
            transform.localPosition = position;
            transform.localRotation = Quaternion.Euler(rotation);
        }
        else
        {
            transform.localPosition = position;
            transform.rotation = Quaternion.Euler(rotation);
        }
        transform.localScale = scale; // Scale is always local
        go.name = prefabDataFields[9].text;
    }

    CameraManager.Instance.UpdateGizmoState();
    UpdateTransformSnapshot(go); // Update snapshot after changes
}
	
    
    private void UpdatePrefabData(GameObject go, int index, float value)
    {
        PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
        if (holder == null) return;

        PrefabData data = holder.prefabData;
        int vectorIndex = index % 3;
        switch (index / 3)
        {
            case 0: // Position
                if (vectorIndex == 0) data.position.x = value;
                else if (vectorIndex == 1) data.position.y = value;
                else data.position.z = value;
                break;
            case 1: // Rotation
                if (vectorIndex == 0) data.rotation.x = value;
                else if (vectorIndex == 1) data.rotation.y = value;
                else data.rotation.z = value;
                break;
            case 2: // Scale
                if (vectorIndex == 0) data.scale.x = value;
                else if (vectorIndex == 1) data.scale.y = value;
                else data.scale.z = value;
                break;
        }
    }
	
public void MirrorYAndZRotations(List<GameObject> objects)
{
    //Debug.Log("mirroring Y and Z rotations");
    if (objects == null || objects.Count == 0) return;

    // UI field indices for Y and Z rotations
    int[] fieldIndices = new int[] { 4, 5 }; // prefabDataFields[4] for Y, [5] for Z

    // Apply mirrored Y and Z rotations to all selected objects
    foreach (GameObject go in objects)
    {
        if (go == null) continue;

        // Get the current rotation for this specific object
        Transform transform = go.transform;
        bool useLocal = CameraManager.Instance.isLocal;
        Vector3 rotation = useLocal ? transform.localEulerAngles : transform.eulerAngles;

        // Negate Y and Z rotations
        float newYValue = -rotation.y;
        float newZValue = -rotation.z;
        rotation.y = newYValue;
        rotation.z = newZValue;

        // Apply the rotation
        if (useLocal)
        {
            transform.localRotation = Quaternion.Euler(rotation);
        }
        else
        {
            transform.rotation = Quaternion.Euler(rotation);
        }

        // Update UI and data for the last processed selection
        if (go == _lastProcessedSelection)
        {
            // Update the UI fields for Y and Z rotations
            prefabDataFields[fieldIndices[0]].text = newYValue.ToString("F3"); // Y rotation
            prefabDataFields[fieldIndices[1]].text = newZValue.ToString("F3"); // Z rotation
            // Call SendPrefabData to sync PrefabDataHolder and snapshot
            SendPrefabData(go);
        }
        else
        {
            // For other objects, update their PrefabDataHolder if applicable
            PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
            if (holder != null && !go.CompareTag("Collection"))
            {
                PrefabData data = holder.prefabData;
                data.rotation = new VectorData(rotation.x, rotation.y, rotation.z);
                holder.CastPrefabData();
            }
            UpdateTransformSnapshot(go); // Update snapshot for consistency
        }
    }
    CameraManager.Instance.UpdateGizmoState();
}
	
public void MirrorRotations(List<GameObject> objects, Axis axis)
{
    //Debug.Log("mirroring rotation " + axis);
    if (objects == null || objects.Count == 0) return;

    // Determine which UI field to update for _lastProcessedSelection
    int fieldIndex = axis switch
    {
        Axis.X => 3, // prefabDataFields[3] for X rotation
        Axis.Y => 4, // prefabDataFields[4] for Y rotation
        Axis.Z => 5, // prefabDataFields[5] for Z rotation
        _ => -1 // Should not occur
    };

    if (fieldIndex == -1) return;

    // Apply mirrored rotation to all selected objects
    foreach (GameObject go in objects)
    {
        if (go == null) continue;

        // Get the current rotation for this specific object
        Transform transform = go.transform;
        bool useLocal = CameraManager.Instance.isLocal;
        Vector3 rotation = useLocal ? transform.localEulerAngles : transform.eulerAngles;

        // Negate the rotation for the targeted axis
        float newValue = 0f;
        switch (axis)
        {
            case Axis.X:
                newValue = -rotation.x;
                rotation.x = newValue;
                break;
            case Axis.Y:
                newValue = -rotation.y;
                rotation.y = newValue;
                break;
            case Axis.Z:
                newValue = -rotation.z;
                rotation.z = newValue;
                break;
        }

        // Apply the rotation
        if (useLocal)
        {
            transform.localRotation = Quaternion.Euler(rotation);
        }
        else
        {
            transform.rotation = Quaternion.Euler(rotation);
        }

        // Update UI and data for the last processed selection
        if (go == _lastProcessedSelection)
        {
            // Update the UI field for the targeted axis
            prefabDataFields[fieldIndex].text = newValue.ToString("F3");
            // Call SendPrefabData to sync PrefabDataHolder and snapshot
            SendPrefabData(go);
        }
        else
        {
            // For other objects, update their PrefabDataHolder if applicable
            PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
            if (holder != null && !go.CompareTag("Collection"))
            {
                PrefabData data = holder.prefabData;
                data.rotation = new VectorData(rotation.x, rotation.y, rotation.z);
                holder.CastPrefabData();
            }
            UpdateTransformSnapshot(go); // Update snapshot for consistency
        }
    }
    CameraManager.Instance.UpdateGizmoState();
}

public void MirrorPositions(List<GameObject> objects, Axis axis)
{
    //Debug.Log("mirroring position " + axis);
    if (objects == null || objects.Count == 0) return;

    // Determine which UI field to update for _lastProcessedSelection
    int fieldIndex = axis switch
    {
        Axis.X => 0, // prefabDataFields[0] for X position
        Axis.Y => 1, // prefabDataFields[1] for Y position
        Axis.Z => 2, // prefabDataFields[2] for Z position
        _ => -1 // Should not occur
    };

    if (fieldIndex == -1) return;

    // Apply mirrored position to all selected objects
    foreach (GameObject go in objects)
    {
        if (go == null) continue;

        // Get the current position for this specific object
        Transform transform = go.transform;
        bool useLocal = CameraManager.Instance.isLocal;
        Vector3 position = useLocal ? transform.localPosition : transform.position;

        // Negate the position for the targeted axis
        float newValue = 0f;
        switch (axis)
        {
            case Axis.X:
                newValue = -position.x;
                position.x = newValue;
                break;
            case Axis.Y:
                newValue = -position.y;
                position.y = newValue;
                break;
            case Axis.Z:
                newValue = -position.z;
                position.z = newValue;
                break;
        }

        // Apply the position
        if (useLocal)
        {
            transform.localPosition = position;
        }
        else
        {
            transform.position = position;
        }

        // Update UI and data for the last processed selection
        if (go == _lastProcessedSelection)
        {
            // Update the UI field for the targeted axis
            prefabDataFields[fieldIndex].text = newValue.ToString("F3");
            // Call SendPrefabData to sync PrefabDataHolder and snapshot
            SendPrefabData(go);
        }
        else
        {
            // For other objects, update their PrefabDataHolder if applicable
            PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
            if (holder != null && !go.CompareTag("Collection"))
            {
                PrefabData data = holder.prefabData;
                data.position = new VectorData(position.x, position.y, position.z);
                holder.CastPrefabData();
            }
            UpdateTransformSnapshot(go); // Update snapshot for consistency
        }
    }
    CameraManager.Instance.UpdateGizmoState();
}

// Rotate by +90 or -90 degrees along the specified axis by updating UI fields
public void Rotate90(List<GameObject> objects, Axis axis, RotationDirection direction)
{
    //Debug.Log("rotating " + axis + " " + direction);
    if (objects == null || objects.Count == 0) return;

    // Determine which UI field to update based on axis
    int fieldIndex = axis switch
    {
        Axis.X => 3, // prefabDataFields[3] for X rotation
        Axis.Y => 4, // prefabDataFields[4] for Y rotation
        Axis.Z => 5, // prefabDataFields[5] for Z rotation
        _ => -1 // Should not occur
    };

    if (fieldIndex == -1) return;

    // Get the current rotation value from the UI field (or default to 0 if empty/invalid)
    float currentValue = 0f;
    if (_lastProcessedSelection != null && !string.IsNullOrEmpty(prefabDataFields[fieldIndex].text))
    {
        float.TryParse(prefabDataFields[fieldIndex].text, out currentValue);
    }

    // Calculate new rotation value
    float angleChange = (direction == RotationDirection.Plus90) ? 90f : -90f;
    float newValue = currentValue + angleChange;
	newValue = (newValue % 360f); 

    // Update the UI field for the targeted axis
    if (_lastProcessedSelection != null)
    {
        prefabDataFields[fieldIndex].text = newValue.ToString("F3");
    }

    // Apply the updated rotation to all selected objects
    foreach (GameObject go in objects)
    {
        if (go == null) continue;

        // Update the rotation data for the object
        Transform transform = go.transform;
        bool useLocal = CameraManager.Instance.isLocal;
        Vector3 rotation = useLocal ? transform.localEulerAngles : transform.eulerAngles;

        // Modify only the targeted axis
        switch (axis)
        {
            case Axis.X:
                rotation.x = newValue;
                break;
            case Axis.Y:
                rotation.y = newValue;
                break;
            case Axis.Z:
                rotation.z = newValue;
                break;
        }

        // Apply the rotation
        if (useLocal)
        {
            transform.localRotation = Quaternion.Euler(rotation);
        }
        else
        {
            transform.rotation = Quaternion.Euler(rotation);
        }

        // Update UI and data for the last processed selection
        if (go == _lastProcessedSelection)
        {
            // Since we already set the field value, call SendPrefabData to sync PrefabDataHolder and snapshot
            SendPrefabData(go);
        }
        else
        {
            // For other objects, update their PrefabDataHolder if applicable
            PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
            if (holder != null && !go.CompareTag("Collection"))
            {
                PrefabData data = holder.prefabData;
                data.rotation = new VectorData(rotation.x, rotation.y, rotation.z);
                holder.CastPrefabData();
            }
            UpdateTransformSnapshot(go); // Update snapshot for consistency
        }
    }
    CameraManager.Instance.UpdateGizmoState();
}


// New method to update collider states
private void UpdateColliderStates(bool enable)
{
    var selectedObjects = CameraManager.Instance._selectedObjects;
    if (selectedObjects == null || selectedObjects.Count == 0) return;

    foreach (GameObject go in selectedObjects)
    {
        if (go == null) continue;

        // Get all colliders in the object and its children
        Collider[] colliders = go.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)  {
            collider.enabled = enable;
        }
    }

    // Update UI to reflect the new state
    if (_lastProcessedSelection != null)   {
        RetrieveColliderState(_lastProcessedSelection);
    }
}

// New method to retrieve and update the collider toggle state
public void RetrieveColliderState(GameObject go)
{
    if (go == null)
    {
        collidersEnabled.SetIsOnWithoutNotify(false);
        return;
    }

    var selectedObjects = CameraManager.Instance._selectedObjects;
    bool anyColliderEnabled = false;

    // Check all selected objects for any enabled colliders
    foreach (GameObject selected in selectedObjects)
    {
        if (selected == null) continue;
        Collider[] colliders = selected.GetComponentsInChildren<Collider>();
        if (colliders.Any(collider => collider.enabled))
        {
            anyColliderEnabled = true;
            break;
        }
    }

    // Update toggle state without triggering the listener
    collidersEnabled.SetIsOnWithoutNotify(anyColliderEnabled);
}

// Update SetSelection to include collider state retrieval
public void SetSelection(GameObject go)
{
    if (go == null)
    {
        DefaultPrefabData();
        _lastProcessedSelection = null;
        collidersEnabled.SetIsOnWithoutNotify(false); // Disable toggle when no selection
        return;
    }
    RetrievePrefabData(go);
    RetrieveSocketData(go);
    RetrieveColliderState(go); // Add collider state retrieval
    _lastProcessedSelection = go;
    UpdateTransformSnapshot(go);
    CameraManager.Instance.UpdateSnapSettings();
}

// Update UpdateData to refresh collider state when necessary
public void UpdateData()
{
    var selectedObjects = CameraManager.Instance._selectedObjects;
    if (selectedObjects.Count > 0)
    {
        GameObject lastSelected = selectedObjects[^1];
        if (lastSelected != _lastProcessedSelection)
        {
            // New selection, update UI and snapshot
            SetSelection(lastSelected);
        }
        else
        {
            // Same object, check for transform changes
            if (HasTransformChanged(lastSelected))
            {
                RetrievePrefabData(lastSelected);
                UpdateTransformSnapshot(lastSelected);
            }
            RetrieveColliderState(lastSelected); // Refresh collider toggle state
        }
    }
    else if (_lastProcessedSelection != null)
    {
        // No selection, clear UI
        DefaultPrefabData();
        _lastProcessedSelection = null;
        collidersEnabled.SetIsOnWithoutNotify(false); // Disable toggle when no selection
    }
}

// Update DefaultPrefabData to reset collider toggle
public void DefaultPrefabData()
{
    DestroyListeners();
    DestroySocketListeners();
    for (int i = 0; i < prefabDataFields.Count; i++)
    {
        prefabDataFields[i].text = string.Empty;
    }
    socketPanel.SetActive(false);
    collidersEnabled.SetIsOnWithoutNotify(false); // Reset collider toggle
    selection.text = CameraManager.Instance.isLocal ? "(Local)" : "(Global)";
    _lastPosition = Vector3.zero;
    _lastRotation = Vector3.zero;
    _lastScale = Vector3.zero;
    _lastCategory = string.Empty;
    _lastId = 0;
}
}