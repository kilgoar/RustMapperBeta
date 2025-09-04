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
using RustMapEditor;
using RustMapEditor.Variables;
using static WorldSerialization;
using static TerrainManager;


public class TerrainWindow : MonoBehaviour
{
	public List<Toggle> layerToggles;
	public List<GameObject> layerPanels;	
	public List<Toggle> carveToggles, waterToggles;
	public List<Toggle> TopologyToggles;	
	public Slider strength, size, height, waterHeight, delay;
	public Text footer;
	
	public List<Toggle> monumentTopoToggles;
	public Toggle monumentTopoAllToggle; 
    public Toggle monumentTopoNothingToggle; 	
	
    public List<Toggle> monumentSplatToggles;
	public Toggle monumentSplatAllToggle; 
    public Toggle monumentSplatNothingToggle;
	
    public List<Toggle> monumentBiomeToggles;
	public Toggle monumentBiomeAllToggle; 
    public Toggle monumentBiomeNothingToggle; 
	
	public Toggle monHeight, monWater, monAlpha;
	
	int previousSplat = 0;
	
	public Transform brushRowParent;
    public List<GameObject> brushRows = new List<GameObject>();
    public const int BRUSHES_PER_ROW = 8;
	
	public Button TemplateButton, BrushesFolder;
	
	public int topo, lastIndex, carveIndex;	
	
	public List<Texture2D> loadedBrushTextures = new List<Texture2D>(); 
	public Toggle blendTextures;
	
    public string[] brushFiles;	
	public Toggle randomRotations;	
	Layers layers = new Layers() { Ground = TerrainSplat.Enum.Grass, Biome = TerrainBiome.Enum.Temperate, Topologies = TerrainTopology.Enum.Field};
	
	public WindowManager window;
	/*
	public bool rotations;
	public int targetTopo, paintMode, selectedSplatPaint;	
	public float brushStrength;
    public int brushSize, brushType;
    public float myBrushSize;
    public float terrainHeight;
    public float flattenHeight;
	*/
	public static TerrainWindow Instance { get; private set; }
	
	private void OpenBrushesFolder()
    {
        // Construct the full folder path
        string folderPath = System.IO.Path.Combine(SettingsManager.AppDataPath(), "Custom", "Brushes");
        // Replace forward slashes with backslashes for Windows compatibility
        string normalizedPath = folderPath.Replace("/", "\\");

        try
        {
            // Open the folder in Windows File Explorer
            System.Diagnostics.Process.Start("explorer.exe", normalizedPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to open folder {normalizedPath}: {e.Message}");
        }
    }
	
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
	
	public void Setup()
	{
		Debug.Log("Setting up terrain window");

		// Ensure MainScript.Instance is available
		if (MainScript.Instance == null)
		{
			Debug.LogError("MainScript.Instance is null during TerrainWindow setup!");
			return;
		}
		BrushesFolder.onClick.AddListener(OpenBrushesFolder);
		// Populate brush buttons first
		PopulateBrushButtons();

		// Validate toggle and panel lists
		if (layerToggles == null || layerPanels == null || layerToggles.Count != layerPanels.Count)
		{
			Debug.LogError("Invalid terrain window config: layerToggles or layerPanels issue");
			return;
		}

		// Add slider listeners (values are already set in Inspector)
		strength.onValueChanged.AddListener(_ => SendSettings());
		delay.onValueChanged.AddListener(_ => SendSettings());
		size.onValueChanged.AddListener(_ => SendSettings());
		height.onValueChanged.AddListener(_ => SendSettings());
		waterHeight.onValueChanged.AddListener(_ => SendSettings());
		randomRotations.onValueChanged.AddListener(OnRandomRotationsChanged);
		blendTextures.onValueChanged.AddListener(OnBlendTexturesChanged);
		
        MainScript.Instance.rotations = randomRotations.isOn;

		// Find and sync the initial layer toggle (based on Inspector state)
		int initialLayerIndex = -1;
		for (int i = 0; i < layerToggles.Count; i++)
		{
			if (layerToggles[i] != null && layerToggles[i].isOn)
			{
				initialLayerIndex = i;
				break;
			}
		}
		if (initialLayerIndex == -1) // Fallback if no toggle is on
		{
			initialLayerIndex = 0;
			layerToggles[0].isOn = true;
		}
		for (int i = 0; i < layerToggles.Count; i++)
		{
			if (i < layerPanels.Count && layerPanels[i] != null)
			{
				layerPanels[i].SetActive(i == initialLayerIndex);
			}
			if (i < layerToggles.Count && layerToggles[i] != null)
			{
				layerToggles[i].SetIsOnWithoutNotify(i == initialLayerIndex);
				layerToggles[i].interactable = i != initialLayerIndex;
				int index = i;
				layerToggles[i].onValueChanged.AddListener((isOn) => OnToggleChanged(index));
			}
		}
		OnToggleChanged(initialLayerIndex); // Push Inspector state to MainScript


		carveIndex = 0;
		carveToggles[0].isOn = true;

		for (int i = 0; i < carveToggles.Count; i++)
		{
			if (i < carveToggles.Count && carveToggles[i] != null)
			{
				carveToggles[i].SetIsOnWithoutNotify(i == 0);
				carveToggles[i].interactable = i != 0;
				int index = i;
				carveToggles[i].onValueChanged.AddListener((isOn) => OnCarveChanged(index));
			}
		}
		OnCarveChanged(carveIndex); // Push Inspector state to MainScript
		
		int initialWaterIndex = 0;
		waterToggles[0].isOn = true;

		for (int i = 0; i < waterToggles.Count; i++)
		{
			if (i < waterToggles.Count && waterToggles[i] != null)
			{
				waterToggles[i].SetIsOnWithoutNotify(i == 0);
				waterToggles[i].interactable = i != 0;
				int index = i;
				waterToggles[i].onValueChanged.AddListener((isOn) => OnWaterChanged(index));
			}
		}
		OnWaterChanged(initialWaterIndex); // Push Inspector state to MainScript

		// Find and sync the initial topology toggle (based on Inspector state)
		int initialTopoIndex = -1;
		for (int i = 0; i < TopologyToggles.Count; i++)
		{
			if (i < TopologyToggles.Count && TopologyToggles[i] != null)
			{
				int index = i;
				TopologyToggles[i].onValueChanged.AddListener((isOn) => OnTopologyChanged(index));
			}
		}
		OnTopologyChanged(initialTopoIndex); // Push Inspector state to MainScript

		// Set initial brush based on Inspector or first available
		if (loadedBrushTextures.Count > 0)
		{
			OnBrushSelected(0); // Default to first brush unless specified otherwise
		}
		
		        // Initialize monument splat toggles
        for (int i = 0; i < monumentSplatToggles.Count; i++)
        {
            if (monumentSplatToggles[i] != null)
            {
                int index = i;
                monumentSplatToggles[i].onValueChanged.AddListener((isOn) => OnMonumentSplatToggleChanged(index));
            }
        }
        UpdateMonumentSplatMask();
		
        monumentSplatAllToggle.onValueChanged.AddListener(OnMonumentSplatAllToggleChanged);
        monumentSplatNothingToggle.onValueChanged.AddListener(OnMonumentSplatNothingToggleChanged);
		
		monHeight.onValueChanged.AddListener(OnMonumentHeightToggleChanged);
		monWater.onValueChanged.AddListener(OnMonumentWaterToggleChanged);
		monAlpha.onValueChanged.AddListener(OnMonumentAlphaToggleChanged);

        // Initialize monument biome toggles
        for (int i = 0; i < monumentBiomeToggles.Count; i++)
        {
            if (monumentBiomeToggles[i] != null)
            {
                int index = i;
                monumentBiomeToggles[i].onValueChanged.AddListener((isOn) => OnMonumentBiomeToggleChanged(index));
            }
        }
        UpdateMonumentBiomeMask();
		
		if (monumentBiomeAllToggle != null)
        {
            monumentBiomeAllToggle.onValueChanged.AddListener(OnMonumentBiomeAllToggleChanged);
        }
        if (monumentBiomeNothingToggle != null)
        {
            monumentBiomeNothingToggle.onValueChanged.AddListener(OnMonumentBiomeNothingToggleChanged);
        }
		
		        // Initialize monument topology toggles (for monument TopologyMask)
        for (int i = 0; i < monumentTopoToggles.Count; i++)
        {
            if (monumentTopoToggles[i] != null)
            {
                int index = i;
                monumentTopoToggles[i].onValueChanged.AddListener((isOn) => OnMonumentTopoToggleChanged(index));
            }
        }
        UpdateMonumentTopologyMask();
		
		if (monumentTopoAllToggle != null)
        {
            monumentTopoAllToggle.onValueChanged.AddListener(OnMonumentTopoAllToggleChanged);
        }
        if (monumentTopoNothingToggle != null)
        {
            monumentTopoNothingToggle.onValueChanged.AddListener(OnMonumentTopoNothingToggleChanged);
        }
        UpdateMonumentTopologyMask();

        // Set initial brush
        if (loadedBrushTextures.Count > 0)
        {
            OnBrushSelected(0);
        }

        SendSettings();
    }
	
private void UpdateMonumentSplatMask()
{
    TerrainSplat.Enum splatMask = (TerrainSplat.Enum)0;
    var splatValues = System.Enum.GetValues(typeof(TerrainSplat.Enum));
    for (int i = 0; i < monumentSplatToggles.Count && i < splatValues.Length; i++)
    {
        if (monumentSplatToggles[i] != null && monumentSplatToggles[i].isOn)
        {
            splatMask |= (TerrainSplat.Enum)splatValues.GetValue(i);
        }
    }
    MonumentManager.CurrentRMPrefab.monument.SplatMask = splatMask;
    if (monumentSplatAllToggle != null)
    {
        monumentSplatAllToggle.SetIsOnWithoutNotify(splatMask == (TerrainSplat.Enum)(-1)); // EVERYTHING
    }
    if (monumentSplatNothingToggle != null)
    {
        monumentSplatNothingToggle.SetIsOnWithoutNotify(splatMask == (TerrainSplat.Enum)0); // NOTHING
    }
    Debug.Log($"Monument SplatMask updated: {splatMask}");
}

// Update the UpdateMonumentBiomeMask method
private void UpdateMonumentBiomeMask()
{
    TerrainBiome.Enum biomeMask = (TerrainBiome.Enum)0;
    var biomeValues = System.Enum.GetValues(typeof(TerrainBiome.Enum));
    for (int i = 0; i < monumentBiomeToggles.Count && i < biomeValues.Length; i++)
    {
        if (monumentBiomeToggles[i] != null && monumentBiomeToggles[i].isOn)
        {
            biomeMask |= (TerrainBiome.Enum)biomeValues.GetValue(i);
        }
    }
    MonumentManager.CurrentRMPrefab.monument.BiomeMask = biomeMask;
    if (monumentBiomeAllToggle != null)
    {
        monumentBiomeAllToggle.SetIsOnWithoutNotify(biomeMask == (TerrainBiome.Enum)(-1)); // EVERYTHING
    }
    if (monumentBiomeNothingToggle != null)
    {
        monumentBiomeNothingToggle.SetIsOnWithoutNotify(biomeMask == (TerrainBiome.Enum)0); // NOTHING
    }
    Debug.Log($"Monument BiomeMask updated: {biomeMask}");
}

// Update the UpdateMonumentTopologyMask method
private void UpdateMonumentTopologyMask()
{
    TerrainTopology.Enum topologyMask = (TerrainTopology.Enum)0;
    var topologyValues = System.Enum.GetValues(typeof(TerrainTopology.Enum));
    for (int i = 0; i < monumentTopoToggles.Count && i < topologyValues.Length; i++)
    {
        if (monumentTopoToggles[i] != null && monumentTopoToggles[i].isOn)
        {
            topologyMask |= (TerrainTopology.Enum)topologyValues.GetValue(i);
        }
    }
    MonumentManager.CurrentRMPrefab.monument.TopologyMask = topologyMask;
    if (monumentTopoAllToggle != null)
    {
        monumentTopoAllToggle.SetIsOnWithoutNotify(topologyMask == (TerrainTopology.Enum)(-1)); // EVERYTHING
    }
    if (monumentTopoNothingToggle != null)
    {
        monumentTopoNothingToggle.SetIsOnWithoutNotify(topologyMask == (TerrainTopology.Enum)0); // NOTHING
    }
    Debug.Log($"Monument TopologyMask updated: {topologyMask}");
}

// Update the OnMonumentHeightToggleChanged method
private void OnMonumentHeightToggleChanged(bool isOn)
{
    MonumentManager.CurrentRMPrefab.monument.HeightMap = isOn;
    Debug.Log($"Monument HeightMap updated: {isOn}");
}

// Update the OnMonumentWaterToggleChanged method
private void OnMonumentWaterToggleChanged(bool isOn)
{
    MonumentManager.CurrentRMPrefab.monument.WaterMap = isOn;
    Debug.Log($"Monument WaterMap updated: {isOn}");
}

// Update the OnMonumentAlphaToggleChanged method
private void OnMonumentAlphaToggleChanged(bool isOn)
{
    MonumentManager.CurrentRMPrefab.monument.AlphaMap = isOn;
    Debug.Log($"Monument AlphaMap updated: {isOn}");
}

private void OnMonumentSplatToggleChanged(int index)
{
    if (monumentSplatAllToggle != null) monumentSplatAllToggle.SetIsOnWithoutNotify(false);
    if (monumentSplatNothingToggle != null) monumentSplatNothingToggle.SetIsOnWithoutNotify(false);
    UpdateMonumentSplatMask();
}

private void OnMonumentTopoToggleChanged(int index)
{
    if (monumentTopoAllToggle != null) monumentTopoAllToggle.SetIsOnWithoutNotify(false);
    if (monumentTopoNothingToggle != null) monumentTopoNothingToggle.SetIsOnWithoutNotify(false);
    UpdateMonumentTopologyMask();
}

private void OnMonumentBiomeToggleChanged(int index)
{
    if (monumentBiomeAllToggle != null) monumentBiomeAllToggle.SetIsOnWithoutNotify(false);
    if (monumentBiomeNothingToggle != null) monumentBiomeNothingToggle.SetIsOnWithoutNotify(false);
    UpdateMonumentBiomeMask();
}



// Update the LoadMonumentToggles method
public void LoadMonumentToggles()
{
    // Update monument splat toggles
    var splatValues = System.Enum.GetValues(typeof(TerrainSplat.Enum));
    for (int i = 0; i < monumentSplatToggles.Count && i < splatValues.Length; i++)
    {
        TerrainSplat.Enum layer = (TerrainSplat.Enum)splatValues.GetValue(i);
        if (layer != (TerrainSplat.Enum)0)
        {
            monumentSplatToggles[i].SetIsOnWithoutNotify((MonumentManager.CurrentRMPrefab.monument.SplatMask & layer) != 0);
        }
    }
    if (monumentSplatAllToggle != null)
    {
        monumentSplatAllToggle.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.SplatMask == (TerrainSplat.Enum)(-1));
    }
    if (monumentSplatNothingToggle != null)
    {
        monumentSplatNothingToggle.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.SplatMask == (TerrainSplat.Enum)0);
    }

    // Update monument biome toggles
    var biomeValues = System.Enum.GetValues(typeof(TerrainBiome.Enum));
    for (int i = 0; i < monumentBiomeToggles.Count && i < biomeValues.Length; i++)
    {
        TerrainBiome.Enum layer = (TerrainBiome.Enum)biomeValues.GetValue(i);
        if (layer != (TerrainBiome.Enum)0)
        {
            monumentBiomeToggles[i].SetIsOnWithoutNotify((MonumentManager.CurrentRMPrefab.monument.BiomeMask & layer) != 0);
        }
    }
    if (monumentBiomeAllToggle != null)
    {
        monumentBiomeAllToggle.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.BiomeMask == (TerrainBiome.Enum)(-1));
    }
    if (monumentBiomeNothingToggle != null)
    {
        monumentBiomeNothingToggle.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.BiomeMask == (TerrainBiome.Enum)0);
    }

    // Update monument topology toggles
    var topologyValues = System.Enum.GetValues(typeof(TerrainTopology.Enum));
    for (int i = 0; i < monumentTopoToggles.Count && i < topologyValues.Length; i++)
    {
        TerrainTopology.Enum layer = (TerrainTopology.Enum)topologyValues.GetValue(i);
        if (layer != (TerrainTopology.Enum)0)
        {
            monumentTopoToggles[i].SetIsOnWithoutNotify((MonumentManager.CurrentRMPrefab.monument.TopologyMask & layer) != 0);
        }
    }
    if (monumentTopoAllToggle != null)
    {
        monumentTopoAllToggle.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.TopologyMask == (TerrainTopology.Enum)(-1));
    }
    if (monumentTopoNothingToggle != null)
    {
        monumentTopoNothingToggle.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.TopologyMask == (TerrainTopology.Enum)0);
    }

    // Update monument map toggles
    if (monHeight != null)
    {
        monHeight.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.HeightMap);
    }
    if (monWater != null)
    {
        monWater.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.WaterMap);
    }
    if (monAlpha != null)
    {
        monAlpha.SetIsOnWithoutNotify(MonumentManager.CurrentRMPrefab.monument.AlphaMap);
    }

    Debug.Log($"Loaded Monument Toggles - SplatMask: {MonumentManager.CurrentRMPrefab.monument.SplatMask}, BiomeMask: {MonumentManager.CurrentRMPrefab.monument.BiomeMask}, TopologyMask: {MonumentManager.CurrentRMPrefab.monument.TopologyMask}, HeightMap: {MonumentManager.CurrentRMPrefab.monument.HeightMap}, WaterMap: {MonumentManager.CurrentRMPrefab.monument.WaterMap}, AlphaMap: {MonumentManager.CurrentRMPrefab.monument.AlphaMap}");
}
	
	private void OnMonumentTopoAllToggleChanged(bool isOn)
    {
        if (isOn)
        {
            foreach (var toggle in monumentTopoToggles)
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(true);
            }
            if (monumentTopoNothingToggle != null) monumentTopoNothingToggle.SetIsOnWithoutNotify(false);
        }
        UpdateMonumentTopologyMask();
    }

    private void OnMonumentTopoNothingToggleChanged(bool isOn)
    {
        if (isOn)
        {
            foreach (var toggle in monumentTopoToggles)
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(false);
            }
            if (monumentTopoAllToggle != null) monumentTopoAllToggle.SetIsOnWithoutNotify(false);
        }
        UpdateMonumentTopologyMask();
    }

	private void OnMonumentBiomeAllToggleChanged(bool isOn)
    {
        if (isOn)
        {
            foreach (var toggle in monumentBiomeToggles)
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(true);
            }
            if (monumentBiomeNothingToggle != null) monumentBiomeNothingToggle.SetIsOnWithoutNotify(false);
        }
        UpdateMonumentBiomeMask();
    }

    private void OnMonumentBiomeNothingToggleChanged(bool isOn)
    {
        if (isOn)
        {
            foreach (var toggle in monumentBiomeToggles)
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(false);
            }
            if (monumentBiomeAllToggle != null) monumentBiomeAllToggle.SetIsOnWithoutNotify(false);
        }
        UpdateMonumentBiomeMask();
    }
	
	private void OnMonumentSplatAllToggleChanged(bool isOn)
    {
        if (isOn)
        {
            foreach (var toggle in monumentSplatToggles)
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(true);
            }
            if (monumentSplatNothingToggle != null) monumentSplatNothingToggle.SetIsOnWithoutNotify(false);
        }
        UpdateMonumentSplatMask();
    }

    private void OnMonumentSplatNothingToggleChanged(bool isOn)
    {
        if (isOn)
        {
            foreach (var toggle in monumentSplatToggles)
            {
                if (toggle != null) toggle.SetIsOnWithoutNotify(false);
            }
            if (monumentSplatAllToggle != null) monumentSplatAllToggle.SetIsOnWithoutNotify(false);
        }
        UpdateMonumentSplatMask();
    }
	
	
	private void OnRandomRotationsChanged(bool isOn)
	{
		MainScript.Instance.rotations = isOn;
		//MainScript.Instance.RegenerateBrushWithRotation();
	}
	
	private void OnBlendTexturesChanged(bool isOn)
	{		
		Debug.Log(isOn + " blending enabled");
		
		if (isOn){
			previousSplat =	MainScript.Instance.selectedSplatPaint;
			MainScript.Instance.selectedSplatPaint = -1;
			return;
		}
		
		//when disabling choose previous splat
		if (MainScript.Instance.selectedSplatPaint == -1){
			MainScript.Instance.selectedSplatPaint = previousSplat;
		}

	}

	private void Start()
	{
		Setup();
	}
	
	void OnEnable()	{

		CoroutineManager.Instance.ChangeStylus(2);
		SetLayer(MainScript.Instance.brushType);		
		OnTopologyChanged(-1);
	}
	
	void SendSettings(){
		MainScript.Instance.delay = delay.value;
		MainScript.Instance.brushStrength = strength.value;
		Land.materialTemplate.SetFloat("_BrushStrength", (float)strength.value);
		MainScript.Instance.TerrainTarget(height.value);
		MainScript.Instance.WaterTarget(waterHeight.value);
		Land.materialTemplate.SetFloat("_TerrainTarget", (float)height.value);
		
		MainScript.Instance.ChangeBrushSize((int)size.value*2);
	}
	
	void OnDisable(){
		TerrainManager.UpdateHeightCache();
		CoroutineManager.Instance.ChangeStylus(1);
		Land.materialTemplate.SetFloat("_PreviewMode", 0f);

	}
	

	
	public void OnTopologyChanged(int index)
	{
		topo=index;
		float t = (float)index;
		MainScript.Instance.targetTopo = index;
		MainScript.Instance.selectedSplatPaint = 0;
		MainScript.Instance.RegenerateBrush();
		
		for (int i = 0; i < TopologyToggles.Count; i++)        {	
			bool isActive = i == index;
            TopologyToggles[i].SetIsOnWithoutNotify(isActive);
            TopologyToggles[i].interactable = !isActive;
        }
		Land.materialTemplate.SetFloat("_TopologyMode", t);
	}
	
	public void OnCarveChanged(int index)
	{
		carveIndex= index;
		MainScript.Instance.carveMode = index;
		
		for (int i = 0; i < carveToggles.Count; i++)
        {
            bool isActive = i == index;
            carveToggles[i].SetIsOnWithoutNotify(isActive);
            carveToggles[i].interactable = !isActive;
        }
	}
	
	public void OnWaterChanged(int index)
	{
		Debug.Log("changin water mode" + index);
		MainScript.Instance.waterPaintMode = index;
		
		for (int i = 0; i < waterToggles.Count; i++)
        {
            bool isActive = i == index;
            waterToggles[i].SetIsOnWithoutNotify(isActive);
            waterToggles[i].interactable = !isActive;
        }
	}
	
	public void PopulateBrushButtons()
	{
		// Clear existing rows except the first one (template row)
		for (int i = 1; i < brushRows.Count; i++)
		{
			Destroy(brushRows[i]);
		}
		brushRows.Clear();
		brushRows.Add(brushRowParent.gameObject); // Keep the original Brush Row

		Debug.LogError("made it this far 0 ");

		// Get brush paths from SettingsManager
		string brushPath = Path.Combine(SettingsManager.AppDataPath(), "Custom/Brushes");
		brushFiles = Directory.GetFiles(brushPath, "*.png");

		if (brushFiles.Length == 0)
		{
			Debug.Log("No brush images found in Custom/Brushes directory");
			//footer.text = "No brushes found"; // Update footer even if no brushes are loaded
			return;
		}

		// Load all textures and store them
		loadedBrushTextures.Clear();
		
		int count= 0;
		foreach (string file in brushFiles)
		{
			Texture2D texture = LoadTextureFromFile(file);
			if (texture != null)
			{
				loadedBrushTextures.Add(texture);
				count++;
			}
		}
		
		

		// Pass the loaded textures to MainScript
		MainScript.Instance.SetBrushTextures(loadedBrushTextures.ToArray());


		// Calculate number of rows needed
		int rowCount = Mathf.CeilToInt((float)loadedBrushTextures.Count / BRUSHES_PER_ROW);

		// Create additional rows if needed
		for (int i = 1; i < rowCount; i++)
		{
			GameObject newRow = Instantiate(brushRowParent.gameObject, brushRowParent.parent);
			newRow.name = $"Brush Row {i + 1}";
			
			int targetSiblingIndex = brushRows[i - 1].transform.GetSiblingIndex() + 1;
			newRow.transform.SetSiblingIndex(targetSiblingIndex);
			
			brushRows.Add(newRow);
			
			foreach (Transform child in newRow.transform)
			{
				Destroy(child.gameObject);
			}
		}


		// Create buttons for each brush
		for (int i = 0; i < loadedBrushTextures.Count; i++)
		{
			int rowIndex = i / BRUSHES_PER_ROW;
			int buttonIndex = i % BRUSHES_PER_ROW;

			GameObject buttonObj = Instantiate(TemplateButton.gameObject, brushRows[rowIndex].transform);
			Button button = buttonObj.GetComponent<Button>();
			buttonObj.SetActive(true); 
			RawImage brushImage = buttonObj.GetComponentInChildren<RawImage>();

			// Set the texture for display
			brushImage.texture = loadedBrushTextures[i];

			// Set button properties
			buttonObj.name = $"Brush_{Path.GetFileNameWithoutExtension(brushFiles[i])}";
			
			// Add click listener with brush ID
			int brushId = i; // Capture the index as the brush ID
			button.onClick.AddListener(() => OnBrushSelected(brushId));
			
	        if (i == 0) // Use first brush as default
				{
					button.Select(); // Visually highlight
					OnBrushSelected(brushId); // Push to MainScript
				}
		}

		// Update footer text with the number of brushes loaded
		//footer.text = $"{brushPath}";

		// Force layout rebuild
		LayoutRebuilder.ForceRebuildLayoutImmediate(brushRowParent.GetComponent<RectTransform>());
	}
	
    public Texture2D LoadTextureFromFile(string filePath)
    {
        try        {
            byte[] bytes = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(bytes))
            {
                return texture;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load brush texture from {filePath}: {e.Message}");
        }
        return null;
    }

    public void OnBrushSelected(int brushId)
    {	

		
        if (brushId >= 0 && brushId < loadedBrushTextures.Count)        {
            MainScript.Instance.SetBrush(brushId);
            Debug.Log($"Selected brush ID: {brushId} - {Path.GetFileName(brushFiles[brushId])}");
        }
        else
        {
            Debug.LogError($"Brush ID {brushId} is out of range for loaded textures.");
        }
    }


	
	public void SetLayer(int index){
		
		
		if (index == 1){   //show biomes
			TerrainManager.ChangeLayer(LayerType.Biome, TerrainTopology.TypeToIndex((int)layers.Topologies));
		}
		else 
		{
			TerrainManager.ChangeLayer(LayerType.Ground, TerrainTopology.TypeToIndex((int)layers.Topologies));
		}
		
		if(index == 0){    //height map editing
			OnTopologyChanged(-1); //hide topos
			return;
		}
		
		if (index == 2){                    //holes
			OnTopologyChanged(-1); //hide topos
			return;
		}
		
		if (index == 3){                   //topos
			if(MainScript.Instance.targetTopo == -1){ OnTopologyChanged(0); }
			OnTopologyChanged(MainScript.Instance.targetTopo); //hide topos			
		return;
		}
		
		if (index == 6){
			Land.materialTemplate.SetFloat("_PreviewMode", -1f);
			return;
		}
		
		
		
	}
	
	public void SampleHeightAtClick(RaycastHit hit)    {            
            // Get the height at the clicked position
            height.value = .001f*hit.point.y;         
    }
	
public void OnToggleChanged(int index){
	
	Debug.Log(index + " paint mode");
	
    if (index == 0 || index == 1) { MainScript.Instance.paintMode = -1; } // splat and biome
    else if (index == 2) { MainScript.Instance.paintMode = -3; } // alpha
    else if (index == 3) { MainScript.Instance.paintMode = -2; } // topology
    else if (index == 4) { MainScript.Instance.paintMode = 4; } // heights
	else if (index == 5) { MainScript.Instance.paintMode = -5; Land.materialTemplate.SetFloat("_PreviewMode", -0f); } // water
	else if (index == 6) { MainScript.Instance.paintMode = -4; } //monument blend map

    MainScript.Instance.brushType = index;
    MainScript.Instance.selectedSplatPaint = 0; // Reset to paint mode for topology
    SetLayer(index);

    // Regenerate brush to ensure correct data for the new mode
    MainScript.Instance.GenerateBrush();

    for (int i = 0; i < layerPanels.Count; i++)    {
        bool isActive = i == index;
        layerPanels[i].SetActive(isActive);
        layerToggles[i].SetIsOnWithoutNotify(isActive);
        layerToggles[i].interactable = !isActive;
    }

    Debug.Log($"Switched to layer {index}, paintMode={MainScript.Instance.paintMode}, brushSize={MainScript.Instance.brushSize}");
    LayoutRebuilder.ForceRebuildLayoutImmediate(this.GetComponent<RectTransform>());
}
	
	

	
}
