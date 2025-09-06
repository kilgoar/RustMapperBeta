using System;
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
	
	public GameObject socketPanel;
	public Toggle male, female, horizontal, vertical;
	
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
            }
            else
            {
                Debug.LogWarning("No object selected to save as a collection.");
            }
        });
		
	        // Add listeners for socket toggles
        male.onValueChanged.AddListener(isOn => UpdateSocketData(_lastProcessedSelection));
        female.onValueChanged.AddListener(isOn => UpdateSocketData(_lastProcessedSelection));
        horizontal.onValueChanged.AddListener(isOn => UpdateSocketType(_lastProcessedSelection, isOn, vertical.isOn));
        vertical.onValueChanged.AddListener(isOn => UpdateSocketType(_lastProcessedSelection, horizontal.isOn, isOn));
  
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
            // Enforce validation: At least one type (Horizontal or Vertical) must be active
            if (!isHorizontalOn && !isVerticalOn)
            {
                // If both are turned off, force Horizontal to true
                horizontal.SetIsOnWithoutNotify(true);
                socket.Type = DungeonBaseSocketType.Horizontal;
            }
            else
            {
                // Update Type based on toggle states
                if (isHorizontalOn)
                {
                    socket.Type = DungeonBaseSocketType.Horizontal;
                    vertical.SetIsOnWithoutNotify(false); // Ensure only one is active
                }
                else if (isVerticalOn)
                {
                    socket.Type = DungeonBaseSocketType.Vertical;
                    horizontal.SetIsOnWithoutNotify(false); // Ensure only one is active
                }
            }
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
            TerrainPlacement terrainPlacement = selected.GetComponent<TerrainPlacement>();
            if (terrainPlacement != null)
            {
                TerrainBounds dimensions = new TerrainBounds();
                Vector3 position = selected.transform.position;
                Quaternion rotation = selected.transform.rotation;
                Vector3 scale = selected.transform.localScale;
                
                terrainPlacement.ApplyHeight(position, rotation, scale, dimensions);
                terrainPlacement.ApplySplat(position, rotation, scale, dimensions);
                terrainPlacement.ApplyTopology(position, rotation, scale, dimensions);
                terrainPlacement.ApplyAlpha(position, rotation, scale, dimensions);
                terrainPlacement.ApplyWater(position, rotation, scale, dimensions);
            }
            else
            {
                
				AddToHeightMap[] colliderHeights = selected.GetComponentsInChildren<AddToHeightMap>();
				
				if (colliderHeights.Length > 0)
				{
					foreach (AddToHeightMap heightMap in colliderHeights)
					{
						heightMap.Apply();
					}
				}
				else
				{
					Debug.LogWarning($"No AddToHeightMap components found on {selected.name} or its children");
				}
			}
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
    
    public void SetSelection(GameObject go)
    {
        if (go == null)
        {
            DefaultPrefabData();
            _lastProcessedSelection = null;
            return;
        }
        RetrievePrefabData(go);
		RetrieveSocketData(go);
        _lastProcessedSelection = go;
        UpdateTransformSnapshot(go); // Store initial transform data
    }
    
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
                    RetrievePrefabData(lastSelected); // Refresh UI
                    UpdateTransformSnapshot(lastSelected); // Update snapshot
                }
            }
        }
        else if (_lastProcessedSelection != null)
        {
            // No selection, clear UI
            DefaultPrefabData();
            _lastProcessedSelection = null;
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
        
        if (prefabHolder != null && !isCollection)
        {
            PrefabData data = prefabHolder.prefabData;
            prefabHolder.UpdatePrefabData();

            prefabDataFields[0].text = data.position.x.ToString("F3");
            prefabDataFields[1].text = data.position.y.ToString("F3");
            prefabDataFields[2].text = data.position.z.ToString("F3");

            prefabDataFields[3].text = data.rotation.x.ToString("F3");
            prefabDataFields[4].text = data.rotation.y.ToString("F3");
            prefabDataFields[5].text = data.rotation.z.ToString("F3");

            prefabDataFields[6].text = data.scale.x.ToString("F3");
            prefabDataFields[7].text = data.scale.y.ToString("F3");
            prefabDataFields[8].text = data.scale.z.ToString("F3");

            prefabDataFields[9].text = data.category;
            prefabDataFields[10].text = data.id.ToString();
			
			if (AssetManager.IDLookup.TryGetValue(data.id, out string name))
			{
				// Extract just the file name without path or extension
				string fileName = Path.GetFileNameWithoutExtension(name);
				selection.text = fileName;
			}
            else
            {
                selection.text = data.id.ToString();
            }
        }
        else
        {
            Transform transform = go.transform;
            prefabDataFields[0].text = transform.localPosition.x.ToString("F3");
            prefabDataFields[1].text = transform.localPosition.y.ToString("F3");
            prefabDataFields[2].text = transform.localPosition.z.ToString("F3");

            Vector3 rotation = transform.eulerAngles;
            prefabDataFields[3].text = rotation.x.ToString("F3");
            prefabDataFields[4].text = rotation.y.ToString("F3");
            prefabDataFields[5].text = rotation.z.ToString("F3");
            
            prefabDataFields[6].text = transform.localScale.x.ToString("F3");
            prefabDataFields[7].text = transform.localScale.y.ToString("F3");
            prefabDataFields[8].text = transform.localScale.z.ToString("F3");

            prefabDataFields[9].text = isCollection ? go.name : string.Empty;
            prefabDataFields[10].text = isCollection ? string.Empty : go.name;
            selection.text = isCollection ? go.name : string.Empty;
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
        if (holder != null && !go.CompareTag("Collection"))
        {
            PrefabData data = holder.prefabData;
            data.position = new VectorData(position.x, position.y, position.z);
            data.rotation = new VectorData(rotation.x, rotation.y, rotation.z);
            data.scale = new VectorData(scale.x, scale.y, scale.z);
            data.category = prefabDataFields[9].text;
            data.id = uint.Parse(prefabDataFields[10].text);
            holder.CastPrefabData();
        }
        else if (go.CompareTag("Collection"))
        {
            go.transform.localPosition = position;
            go.transform.rotation = Quaternion.Euler(rotation);
            go.transform.localScale = scale;
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

    public void DefaultPrefabData()
    {
        DestroyListeners();
        DestroySocketListeners();
        for (int i = 0; i < prefabDataFields.Count; i++)
        {
            prefabDataFields[i].text = string.Empty;
        }
        socketPanel.SetActive(false);
        _lastPosition = Vector3.zero;
        _lastRotation = Vector3.zero;
        _lastScale = Vector3.zero;
        _lastCategory = string.Empty;
        _lastId = 0;
    }
}