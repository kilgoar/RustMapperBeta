using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using EasyRoads3Dv3;
using System.Collections.Generic;
using System;
using RustMapEditor.Variables;
using static WorldSerialization;

	public enum RoadType
    {
        River,
        Powerline,
        Rail,
        CircleRoad,
        Road,
        Trail
    }

public class PathWindow : MonoBehaviour
{
    public Text nameField;
    public InputField widthField;
    public InputField innerPaddingField;
    public InputField outerPaddingField;
    public InputField innerFadeField;
    public InputField outerFadeField;
	public InputField terrainOffsetField;
	
	public InputField outerTopoWidthField;   //these will be passed into our paintroadlayers method and. this will paint the topo in the dropdown below into our areas that would, for the heightmap, otherwise use outer padding and inner padding. this will require refactoring of our parameters when creating the temporary mesh
	public Dropdown outerTopoDropdown;
	
    public Dropdown splatDropdown;
    public Dropdown topologyDropdown;
	public Dropdown roadTypeDropdown; 
	public Slider contour, density;     //add listeners and binds to be used in the Generate method
	
	private float potentialContour = 1f; // Default contour value
	private float potentialDensity = .25f;   // Default density value
	
	public Button ApplyLayers, Generate;  //add listener to generate and make sure Generate is interactable only under the condition there are two road nodes in selected road

    public NodeCollection currentPathHolder;
	public PathData potentialPathData;
	
    public ERRoad currentRoad;
    public List<TerrainSplat.Enum> splatEnums = new List<TerrainSplat.Enum>();
    public List<TerrainTopology.Enum> topologyEnums = new List<TerrainTopology.Enum>();
    public List<RoadType> roadTypeEnums = new List<RoadType>();
	
	private float potentialOuterTopoWidth;
    private int potentialOuterTopology; 
	
	public static PathWindow Instance { get; private set; }



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
    PopulateDropdowns();
    if (CameraManager.Instance != null)
    {
        CameraManager.Instance.OnSelectionChanged += UpdateData;
    }
    
    roadTypeDropdown.options.Clear();
    foreach (RoadType roadType in Enum.GetValues(typeof(RoadType)))
    {
        roadTypeEnums.Add(roadType);
        roadTypeDropdown.options.Add(new Dropdown.OptionData(roadType.ToString()));
    }
    potentialOuterTopoWidth = 0f; // Default
    potentialOuterTopology = (int)TerrainTopology.Enum.Road;
	potentialPathData = new PathData();
    ApplyRoadTypeDefaults(RoadType.Road); // Default to Road type
    roadTypeDropdown.value = roadTypeEnums.IndexOf(RoadType.Road); // Set dropdown to "Road"
    roadTypeDropdown.RefreshShownValue();
    UpdateUIFromPotentialData(); // Sync UI with initial potentialPathData
    
    CreateListeners(); // Create listeners once at start
	ApplyLayers.onClick.AddListener(ApplyTerrainAndSplat);
	Generate.onClick.AddListener(GenerateRoads);
}

	public void GenerateRoads()
	{
		if (currentPathHolder == null || currentRoad == null || currentPathHolder.pathData == null)
		{
			Debug.LogWarning("No valid road or path data selected to generate.");
			return;
		}

		if (currentPathHolder.pathData.nodes.Length < 2)
		{
			Debug.LogWarning("Selected road must have at least two nodes to generate.");
			return;
		}

		// Update PathData from UI to ensure current settings are applied
		UpdatePathDataFromUI();

		// Generate new multi-point road
		PathManager.GenerateMultiPoint(currentPathHolder.pathData, currentPathHolder, potentialContour, potentialDensity);
		Debug.Log($"Generated multi-point road '{currentPathHolder.pathData.name}' with {currentPathHolder.pathData.nodes.Length} waypoints using contour={potentialContour} and density={potentialDensity}.");
	}

	private void OnDestroy()
	{
		if (CameraManager.Instance != null)
		{
			CameraManager.Instance.OnSelectionChanged -= UpdateData;
		}
		DestroyListeners(); // Clean up all listeners
		ApplyLayers.onClick.RemoveListener(ApplyTerrainAndSplat); // Add this line
	}

    public void UpdateData()
    {
		Debug.Log("update data");
        if (CameraManager.Instance._selectedRoad != null)
        {
			
            PathData pathData = CameraManager.Instance._selectedRoad.GetComponent<NodeCollection>().pathData;
			
            GameObject roadObject = null;

            NodeCollection[] pathHolders = PathManager.CurrentMapPaths;
            foreach (var holder in pathHolders)
            {
                if (holder != null && holder.pathData == pathData)
                {
                    roadObject = holder.gameObject;
                    break;
                }
            }

            if (roadObject != null)
            {
                if (!gameObject.activeSelf && AppManager.Instance != null)
                {
                    Debug.Log($"Activating PathWindow for road: {roadObject.name}");
                    AppManager.Instance.ActivateWindow(9);
                }
                SetSelection(roadObject);
            }
            else
            {
                Debug.LogWarning($"Could not find road object for PathData: {pathData.name}");
                ClearUI();
            }
        }
        else if (CameraManager.Instance._selectedObjects.Count > 0)
        {
            GameObject selectedObject = CameraManager.Instance._selectedObjects[CameraManager.Instance._selectedObjects.Count - 1];
            if (selectedObject.CompareTag("Node") || selectedObject.CompareTag("NodeParent"))
            {
                NodeCollection pathHolder = selectedObject.GetComponentInParent<NodeCollection>();
                if (pathHolder != null)
                {
                    CameraManager.Instance._selectedRoad = pathHolder.gameObject;
                    SetSelection(pathHolder.gameObject);
                    return;
                }
            }
            ClearUI();
        }
        else
        {
            ClearUI();
            UpdateUIFromPotentialData(); // Show potential data when no selection
        }
    }

	public void ApplyTerrainAndSplat()
	{
		if (CameraManager.Instance._selectedRoad == null)
		{
			Debug.LogWarning("No valid path selected to apply terrain and splat settings.");
			return;
		}

		// Update the PathData from the UI first
		UpdatePathDataFromUI();
		Debug.Log($"Before PaintRoadLayers: outerTopoWidth={currentPathHolder.outerTopoWidth}, outerTopology={(TerrainTopology.Enum)currentPathHolder.outerTopology}, UI Dropdown={outerTopoDropdown.options[outerTopoDropdown.value].text}, UI Width={outerTopoWidthField.text}");

		// Apply terrain smoothing (heightmap indent)
		PathManager.UpdateTerrainHeightmap(currentRoad, currentPathHolder.pathData);
		// Apply splatmap settings
		
		PathManager.PaintRoadLayers(
			currentRoad,
			currentPathHolder.pathData,
			strength: 1f,
			outerTopoWidth: currentPathHolder.outerTopoWidth,
			outerTopology: currentPathHolder.outerTopology
		);

		Debug.Log($"Applied terrain smoothing and splatmap settings to '{currentRoad.GetName()}'.");
	}

    public void SetSelection(GameObject go)
    {
        if (go == null)
        {
            ClearUI();
            return;
        }

        currentPathHolder = go.GetComponent<NodeCollection>();
        currentRoad = go.GetComponent<ERModularRoad>()?.road;

        if (currentPathHolder == null || currentRoad == null)
        {
            Debug.LogWarning($"Selected object {go.name} is not a valid path with NodeCollection or ERRoad.");
            ClearUI();
            return;
        }

        RetrievePathData(currentPathHolder.pathData);
        roadTypeDropdown.interactable = false; // Disable dropdown when a road is selected
		
		UpdateGenerateButtonInteractability();
		
    }


	private void UpdateUIFromPotentialData()
	{
		Debug.Log("updating UI from defaults with: width=" + potentialPathData.width + ", topology=" + potentialPathData.topology + ", splat=" + potentialPathData.splat);
		nameField.text = potentialPathData.name;
		widthField.text = potentialPathData.width.ToString();
		innerPaddingField.text = potentialPathData.innerPadding.ToString();
		outerPaddingField.text = potentialPathData.outerPadding.ToString();
		innerFadeField.text = potentialPathData.innerFade.ToString();
		outerFadeField.text = potentialPathData.outerFade.ToString();
		terrainOffsetField.text = potentialPathData.terrainOffset.ToString();
		int splatIndex = splatEnums.IndexOf((TerrainSplat.Enum)potentialPathData.splat);
		int topoIndex = topologyEnums.IndexOf((TerrainTopology.Enum)potentialPathData.topology);
		Debug.Log($"Splat index: expected={potentialPathData.splat}, got={splatIndex}, Topology index: expected={potentialPathData.topology}, got={topoIndex}");
		splatDropdown.value = splatIndex;
		topologyDropdown.value = topoIndex;
		outerTopoWidthField.text = potentialOuterTopoWidth.ToString();
		outerTopoDropdown.value = topologyEnums.IndexOf((TerrainTopology.Enum)potentialOuterTopology);

		Canvas.ForceUpdateCanvases();
		splatDropdown.RefreshShownValue();
		topologyDropdown.RefreshShownValue();
		outerTopoDropdown.RefreshShownValue();
		Debug.Log("refreshed UI: topologyDropdown.value=" + topologyDropdown.value);
	}

    public void ApplyRoadTypeDefaults(RoadType roadType)
    {
		Debug.Log("applying default road type" + roadType);
        potentialPathData = new PathData();
        string prefix = roadType.ToString();
 
		potentialOuterTopoWidth = 0f; 
		potentialOuterTopology = (int)TerrainTopology.Enum.Road;
	 
        potentialPathData.name = $"New {prefix}";
		
		potentialPathData.terrainOffset = 0f;
        
		switch (roadType)
        {
            case RoadType.River:
				Debug.Log("road type river");
				potentialPathData.terrainOffset = 2f;
                potentialPathData.width = 36f;
				potentialPathData.innerPadding = 1f;
				potentialPathData.outerPadding = 1f;
				potentialPathData.innerFade = 10f;
				potentialPathData.outerFade = 20f;
                potentialPathData.splat = (int)TerrainSplat.Enum.Stones;				
                potentialPathData.topology = (int)TerrainTopology.Enum.River;
                break;
            case RoadType.Powerline:
				Debug.Log("road type powerlien");
                potentialPathData.width = 0f;
				potentialPathData.innerPadding = 0f;
				potentialPathData.outerPadding = 0f;
				potentialPathData.innerFade = 0f;
				potentialPathData.outerFade = 0f;
                potentialPathData.splat = (int)TerrainSplat.Enum.Dirt;				
                potentialPathData.topology = (int)TerrainTopology.Enum.Road;
                break;
            case RoadType.Rail:
				Debug.Log("road type rail");
                potentialPathData.width = 4f;
				potentialPathData.innerPadding = 1f;
				potentialPathData.outerPadding = 1f;
				potentialPathData.innerFade = 1f;
				potentialPathData.outerFade = 32f;
                potentialPathData.splat = (int)TerrainSplat.Enum.Gravel;				
                potentialPathData.topology = (int)TerrainTopology.Enum.Rail;
                break;
            case RoadType.CircleRoad:
				Debug.Log("road type circle road");
                potentialPathData.width = 12f;
				potentialPathData.innerPadding = 1f;
				potentialPathData.outerPadding = 1f;
				potentialPathData.innerFade = 1f;
				potentialPathData.outerFade = 8f;
                potentialPathData.splat = (int)TerrainSplat.Enum.Gravel;				
                potentialPathData.topology = (int)TerrainTopology.Enum.Road;
                break;
            case RoadType.Road:
				Debug.Log("road type road");
                potentialPathData.width = 10f;
				potentialPathData.innerPadding = 1f;
				potentialPathData.outerPadding = 1f;
				potentialPathData.innerFade = 1f;
				potentialPathData.outerFade = 8f;
                potentialPathData.splat = (int)TerrainSplat.Enum.Gravel;				
                potentialPathData.topology = (int)TerrainTopology.Enum.Road;
                break;
            case RoadType.Trail:
				Debug.Log("road type trail");
                potentialPathData.width = 4f;
				potentialPathData.innerPadding = 4f;
				potentialPathData.outerPadding = 1f;
				potentialPathData.innerFade = 1f;
				potentialPathData.outerFade = 8f;
                potentialPathData.splat = (int)TerrainSplat.Enum.Dirt;
                potentialPathData.topology = (int)TerrainTopology.Enum.Road;
                break;
		}
		Debug.Log($"Defaults applied: width={potentialPathData.width}, topology={potentialPathData.topology}, splat={potentialPathData.splat}");
    }

    public void RetrievePathData(WorldSerialization.PathData pathData)
    {
		Debug.Log("loading pathdata from world");
        terrainOffsetField.text = pathData.terrainOffset.ToString();
        nameField.text = pathData.name;
        widthField.text = pathData.width.ToString();
        innerPaddingField.text = pathData.innerPadding.ToString();
        outerPaddingField.text = pathData.outerPadding.ToString();
        innerFadeField.text = pathData.innerFade.ToString();
        outerFadeField.text = pathData.outerFade.ToString();
        splatDropdown.value = splatEnums.IndexOf((TerrainSplat.Enum)pathData.splat);
        topologyDropdown.value = topologyEnums.IndexOf((TerrainTopology.Enum)pathData.topology);
        
        outerTopoWidthField.text = currentPathHolder.outerTopoWidth.ToString();
        outerTopoDropdown.value = topologyEnums.IndexOf((TerrainTopology.Enum)currentPathHolder.outerTopology);
   
    }

	public void ClearUI()
	{
		Debug.Log("clearing UI");
		currentPathHolder = null;
		currentRoad = null;
		roadTypeDropdown.interactable = true;
		UpdateUIFromPotentialData();
		DestroyListeners();
		CreateListeners();
		
		UpdateGenerateButtonInteractability();		
	}
	
	
public void UpdatePathDataFromUI()
{
	Debug.Log("sending path data to road");
    if (currentPathHolder != null && currentRoad != null)
    {
        WorldSerialization.PathData data = currentPathHolder.pathData;
        data.name = nameField.text;
        data.width = float.TryParse(widthField.text, out float width) ? width : data.width;
        data.innerPadding = float.TryParse(innerPaddingField.text, out float innerPadding) ? Mathf.Max(1f, innerPadding) : Mathf.Max(1f, data.innerPadding);
        data.outerPadding = float.TryParse(outerPaddingField.text, out float outerPadding) ? Mathf.Max(1f, outerPadding) : Mathf.Max(1f, data.outerPadding);
        data.innerFade = float.TryParse(innerFadeField.text, out float innerFade) ? Mathf.Max(1f, innerFade) : Mathf.Max(1f, data.innerFade);
        data.outerFade = float.TryParse(outerFadeField.text, out float outerFade) ? Mathf.Max(1f, outerFade) : Mathf.Max(1f, data.outerFade);
        data.terrainOffset = float.TryParse(terrainOffsetField.text, out float terrainOffset) ? terrainOffset : data.terrainOffset;
        data.splat = (int)splatEnums[splatDropdown.value];
        data.topology = (int)topologyEnums[topologyDropdown.value];
        
        currentPathHolder.outerTopoWidth = float.TryParse(outerTopoWidthField.text, out float outerTopoWidth) ? outerTopoWidth : currentPathHolder.outerTopoWidth;
        currentPathHolder.outerTopology = (int)topologyEnums[outerTopoDropdown.value];

        PathManager.ReconfigureRoad(currentRoad, data);

        if (ItemsWindow.Instance != null)
        {
            ItemsWindow.Instance.PopulateList();
        }
        CameraManager.Instance.NotifySelectionChanged();
        Debug.Log($"Updated pathwindow '{data.name}' with new data.");
    }
    else
    {
        potentialPathData.name = nameField.text;
        potentialPathData.width = float.TryParse(widthField.text, out float width) ? width : potentialPathData.width;
        potentialPathData.innerPadding = float.TryParse(innerPaddingField.text, out float innerPadding) ? Mathf.Max(1f, innerPadding) : Mathf.Max(1f, potentialPathData.innerPadding);
        potentialPathData.outerPadding = float.TryParse(outerPaddingField.text, out float outerPadding) ? Mathf.Max(1f, outerPadding) : Mathf.Max(1f, potentialPathData.outerPadding);
        potentialPathData.innerFade = float.TryParse(innerFadeField.text, out float innerFade) ? Mathf.Max(1f, innerFade) : Mathf.Max(1f, potentialPathData.innerFade);
        potentialPathData.outerFade = float.TryParse(outerFadeField.text, out float outerFade) ? Mathf.Max(1f, outerFade) : Mathf.Max(1f, potentialPathData.outerFade);
        potentialPathData.terrainOffset = float.TryParse(terrainOffsetField.text, out float terrainOffset) ? terrainOffset : potentialPathData.terrainOffset;
        potentialPathData.splat = (int)splatEnums[splatDropdown.value];
        potentialPathData.topology = (int)topologyEnums[topologyDropdown.value];
        
        potentialOuterTopoWidth = float.TryParse(outerTopoWidthField.text, out float outerTopoWidth) ? outerTopoWidth : potentialOuterTopoWidth;
        potentialOuterTopology = (int)topologyEnums[outerTopoDropdown.value];
    }
}

	public void OnRoadTypeChanged(int index)
	{
		Debug.Log("road type changed to " + index);
		RoadType selectedType = roadTypeEnums[index];
		potentialPathData = new PathData();
		ApplyRoadTypeDefaults(selectedType); // Set potentialPathData
		UpdateUIFromPotentialData(); // Update UI explicitly after changing the type
		Debug.Log($"Set potential road type to '{selectedType}'.");
	}

    public void CreateListeners()
    {
        terrainOffsetField.onEndEdit.AddListener(text => UpdatePathDataFromUI());
        widthField.onEndEdit.AddListener(text => UpdatePathDataFromUI());
        innerPaddingField.onEndEdit.AddListener(text => UpdatePathDataFromUI());
        outerPaddingField.onEndEdit.AddListener(text => UpdatePathDataFromUI());
        innerFadeField.onEndEdit.AddListener(text => UpdatePathDataFromUI());
        outerFadeField.onEndEdit.AddListener(text => UpdatePathDataFromUI());
        splatDropdown.onValueChanged.AddListener(value => UpdatePathDataFromUI());
        topologyDropdown.onValueChanged.AddListener(value => UpdatePathDataFromUI());
        roadTypeDropdown.onValueChanged.AddListener(OnRoadTypeChanged);
        outerTopoWidthField.onEndEdit.AddListener(text => UpdatePathDataFromUI());
        outerTopoDropdown.onValueChanged.AddListener(value => UpdatePathDataFromUI());

		contour.onValueChanged.AddListener(value =>	potentialContour = value);
		density.onValueChanged.AddListener(value => potentialDensity = value);
		

    }

    public void DestroyListeners()
    {
        terrainOffsetField.onEndEdit.RemoveAllListeners();
        widthField.onEndEdit.RemoveAllListeners();
        innerPaddingField.onEndEdit.RemoveAllListeners();
        outerPaddingField.onEndEdit.RemoveAllListeners();
        innerFadeField.onEndEdit.RemoveAllListeners();
        outerFadeField.onEndEdit.RemoveAllListeners();
        splatDropdown.onValueChanged.RemoveAllListeners();
        topologyDropdown.onValueChanged.RemoveAllListeners();
        roadTypeDropdown.onValueChanged.RemoveAllListeners();
        outerTopoWidthField.onEndEdit.RemoveAllListeners();
        outerTopoDropdown.onValueChanged.RemoveAllListeners();
		contour.onValueChanged.RemoveAllListeners();
		density.onValueChanged.RemoveAllListeners();
    }

	private void UpdateGenerateButtonInteractability()
	{
		if (Generate != null)
		{
			bool isInteractable = currentPathHolder != null && 
								 currentRoad != null && 
								 currentPathHolder.pathData != null && 
								 currentPathHolder.pathData.nodes.Length > 1;
			Generate.interactable = isInteractable;
			Debug.Log($"Generate button interactable: {isInteractable}");
		}
	}

    void OnEnable()
    {
        CoroutineManager.Instance.ChangeStylus(3);
        Debug.Log("Path window enabled");
        UpdateData(); // Reflect current selection when activated
		
		UpdateGenerateButtonInteractability();
    }

	void OnDisable()
	{
		CoroutineManager.Instance.ChangeStylus(1);
		Debug.Log("Path window disabled");

		if (CameraManager.Instance != null && CameraManager.Instance._selectedRoad != null)
		{
			GameObject currentRoad = CameraManager.Instance._selectedRoad;
			if (currentRoad != null)
			{
				CameraManager.Instance.DepopulateNodesForRoad(currentRoad);
			}
			CameraManager.Instance._selectedRoad = null;
			CameraManager.Instance._selectedObjects.Clear();
			CameraManager.Instance.NotifySelectionChanged();
			CameraManager.Instance.UpdateGizmoState();
			Debug.Log("Selected road unselected, nodes depopulated, and gizmos updated.");
		}

		DestroyListeners();
	}

	public void SendPathData()
	{
		Debug.Log("send path data");
		if (currentPathHolder == null || currentRoad == null)
		{
			Debug.LogWarning("No valid path selected to update.");
			return;
		}

		PathData data = currentPathHolder.pathData;

		// Update PathData with new values from UI
		data.name = nameField.text;
		data.width = float.TryParse(widthField.text, out float width) ? width : data.width;
		data.innerPadding = float.TryParse(innerPaddingField.text, out float innerPadding) ? innerPadding : data.innerPadding;
		data.outerPadding = float.TryParse(outerPaddingField.text, out float outerPadding) ? outerPadding : data.outerPadding;
		data.innerFade = float.TryParse(innerFadeField.text, out float innerFade) ? innerFade : data.innerFade;
		data.outerFade = float.TryParse(outerFadeField.text, out float outerFade) ? outerFade : data.outerFade;
		data.splat = (int)splatEnums[splatDropdown.value];
		data.topology = (int)topologyEnums[topologyDropdown.value];

		// Update the road configuration
		PathManager.ReconfigureRoad(currentRoad, data);


		// Sync with ItemsWindow if active
		if (ItemsWindow.Instance != null)
		{
			ItemsWindow.Instance.PopulateList(); // Rebuild the tree to reflect the updated road name/properties
		}

		CameraManager.Instance.NotifySelectionChanged(); // Notify listeners of the change
		Debug.Log($"Updated pathwindow '{data.name}' with new data.");
	}

    public void PopulateDropdowns()
    {
		Debug.Log("populate dropdowns");
        splatDropdown.options.Clear();
        foreach (TerrainSplat.Enum splat in Enum.GetValues(typeof(TerrainSplat.Enum)))
        {
            splatEnums.Add(splat);
            splatDropdown.options.Add(new Dropdown.OptionData(splat.ToString()));
        }
        splatDropdown.RefreshShownValue();

        topologyDropdown.options.Clear();
        foreach (TerrainTopology.Enum topology in Enum.GetValues(typeof(TerrainTopology.Enum)))
        {
            topologyEnums.Add(topology);
            topologyDropdown.options.Add(new Dropdown.OptionData(topology.ToString()));
        }
        topologyDropdown.RefreshShownValue();
		

		outerTopoDropdown.options.Clear();
		foreach (TerrainTopology.Enum topology in Enum.GetValues(typeof(TerrainTopology.Enum)))
		{
			outerTopoDropdown.options.Add(new Dropdown.OptionData(topology.ToString()));
		}
		outerTopoDropdown.RefreshShownValue();
		
    }





}