using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RustMapEditor.Variables;
using UIRecycleTreeNamespace;
using System.Linq;

public class SettingsWindow : MonoBehaviour
{
    FilePreset settings;
    
	public Button openAppData;
    public Slider prefabRender;
    public InputField directoryField;
	public Dropdown SkinDropdown;
    public Toggle assetLoadToggle;
    public UIRecycleTree tree;
    public Text footer;
	public string[] skinPaths;
	public GameObject bindsContent, bindTemplate;
	
	//binds content is our target for template copies in a scroll view
	
	//bind template's child objects are targets for cloning representations of items in the binds list (BindManager.binds):
	//BindLabel - this is the bindName followed by a description of the current keypress association and any true ctrl, alt, shift booleans from the Bind struct
	//ResetBindButton will trigger BindManager.Set(string bind lookup)
	//ClearBindButton will trigger BindMaanger.Remove(string bind lookup)
	
	private string FormatKey(Bind bind)
	{
		if (string.IsNullOrEmpty(bind.primaryInput))
		{
			return "Unbound";
		}

		// Build prefixes (Ctrl, Shift, Alt)
		List<string> prefixes = new List<string>();
		if (bind.requiresCtrl)
		{
			prefixes.Add("Ctrl");
		}
		if (bind.requiresShift)
		{
			prefixes.Add("Shift");
		}
		if (bind.requiresAlt)
		{
			prefixes.Add("Alt");
		}

		// Extract key from primaryInput (e.g., "<Keyboard>/w" -> "w")
		string[] parts = bind.primaryInput.Split('/');
		string key = parts[parts.Length - 1];

		// Handle special cases for mouse buttons
		if (key == "leftButton")
		{
			key = "Left Mouse";
		}
		else if (key == "rightButton")
		{
			key = "Right Mouse";
		}
		else
		{
			key = key.ToUpper(); // Convert to uppercase for other keys (e.g., "w" -> "W")
		}

		// Combine prefixes and key
		string inputDisplay;
		if (prefixes.Count > 0)
		{
			string combinedPrefixes = prefixes[0];
			for (int i = 1; i < prefixes.Count; i++)
			{
				combinedPrefixes += "+" + prefixes[i];
			}
			inputDisplay = combinedPrefixes + "+" + key;
		}
		else
		{
			inputDisplay = key;
		}

		return inputDisplay;
	}

	public void PopulateBindUI()
	{
		// Clear existing bind UI elements
		for (int i = 0; i < bindsContent.transform.childCount; i++)
		{
			Destroy(bindsContent.transform.GetChild(i).gameObject);
		}

		// Create UI for each bind
		List<Bind> bindList = BindManager.binds;
		for (int i = 0; i < bindList.Count; i++)
		{
			Bind bind = bindList[i];
			GameObject bindUI = Instantiate(bindTemplate, bindsContent.transform);
			Text bindLabel = bindUI.transform.Find("BindLabel").GetComponent<Text>();
			Button resetButton = bindUI.transform.Find("ResetBindButton").GetComponent<Button>();
			Button clearButton = bindUI.transform.Find("ClearBindButton").GetComponent<Button>();

			// Set label text using FormatKey
			bindLabel.text = bind.bindName + ": " + FormatKey(bind);

			// Add listeners for buttons
			resetButton.onClick.AddListener(delegate { BindManager.ResetBind(bind.bindName); });
			clearButton.onClick.AddListener(delegate { BindManager.RemoveBind(bind.bindName); });

			bindUI.SetActive(true);
		}
	}
    
    private List<string> drivePaths = new List<string>();
    
    public Button[] bundleButtons;
    
    public static SettingsWindow Instance { get; private set; }
    
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

    void OnEnable()
    {
        Initialize();
        UpdateButtonStates();
    }

    void Start()
    {
        InitializeListeners();
    }

    private void Initialize()
    {
        settings = SettingsManager.application;
        
        prefabRender.value = settings.prefabRenderDistance;
        directoryField.text = settings.rustDirectory;
        assetLoadToggle.isOn = settings.loadbundleonlaunch;
        //styleToggle.isOn = settings.terrainTextureSet;
		LoadSkins();
        LoadDriveList();
		PopulateBindUI();
    }

	private void OpenAppDataFolder()
    {
        // Construct the full folder path
        string folderPath = System.IO.Path.Combine(SettingsManager.AppDataPath());
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

    private void InitializeListeners()
    {
        /*
        prefabRender.onValueChanged.RemoveAllListeners();
        directoryField.onEndEdit.RemoveAllListeners();
        assetLoadToggle.onValueChanged.RemoveAllListeners();
        styleToggle.onValueChanged.RemoveAllListeners();
        tree.onNodeExpandStateChanged.RemoveAllListeners();
        tree.onSelectionChanged.RemoveAllListeners();
        bundleButtons[0].onClick.RemoveAllListeners();
        bundleButtons[1].onClick.RemoveAllListeners();
		*/
        SkinDropdown.onValueChanged.AddListener(OnSkinChanged);
        prefabRender.onValueChanged.AddListener(CameraChange);
        directoryField.onEndEdit.AddListener(DirectoryChange);
        assetLoadToggle.onValueChanged.AddListener(AssetLoader);
        //styleToggle.onValueChanged.AddListener(StyleChange);
        //styleToggle.onValueChanged.AddListener(ToggleStyle);
        tree.onNodeExpandStateChanged.AddListener(OnExpand);
        tree.onSelectionChanged.AddListener(OnSelect);
        bundleButtons[0].onClick.AddListener(OnLoadBundle);
        bundleButtons[1].onClick.AddListener(OnUnloadBundle);
		
		
		openAppData.onClick.AddListener(OpenAppDataFolder); 
    }
	
	public void OnSkinChanged(int selectedIndex)
	{
		// Check if MacroDropDown has any options
		if (SkinDropdown == null || SkinDropdown.options.Count == 0)
		{
			Debug.LogWarning("Empty SkinDropdown");
			return;
		}
		
		// Get the selected filename from the dropdown
		string selectedSkin = SkinDropdown.options[selectedIndex].text;
		// Reconstruct the full path
		string currentSkinPath = Path.Combine(SettingsManager.AppDataPath(), "Custom", "Skins", selectedSkin + ".png");
		
		ModManager.LoadSkin(currentSkinPath);
		
	}
	
	public void LoadSkins(){
		string skinsFolder = Path.Combine(SettingsManager.AppDataPath(), "Custom", "Skins");
		Debug.Log("loading skin list from: " + skinsFolder);
		
		// Get only .png filenames from the directory
		skinPaths = Directory.GetFiles(skinsFolder, "*.png")
							 .Select(Path.GetFileNameWithoutExtension)
							 .ToArray();
		
		Debug.Log("found " + skinPaths.Length + " in " + skinsFolder);
		SkinDropdown.ClearOptions();
		SkinDropdown.AddOptions(skinPaths.ToList());
	}

    private void OnLoadBundle()
    {
        settings.rustDirectory = directoryField.text;
        SettingsManager.application = settings;
        SettingsManager.SaveSettings();
		

		AssetManager.Initialise(Path.Combine(settings.rustDirectory, "Bundles", "Bundles"));
		LoadScreen.Instance.SetMessage1("");
		LoadScreen.Instance.Complete(1);
    }
    
    private void OnUnloadBundle()
    {
        Debug.Log("unloading bundles");
        AssetManager.Dispose();
    }
    
    public List<string> PathTests()
    {
        string fileGuess = "Program Files (x86)/Steam/steamapps/common/Rust";
        List<string> validPaths = new List<string>();
        DriveInfo[] drives = DriveInfo.GetDrives();

        foreach (DriveInfo drive in drives)
        {
            string fullPath = Path.Combine(drive.RootDirectory.FullName, fileGuess);
            if (Directory.Exists(fullPath))
            {
                validPaths.Add(fullPath);
            }
        }
        return validPaths;
    }

    public void LoadDriveList()
    {
        tree.Clear();
        drivePaths.Clear();

        // Add known Rust paths first
        List<string> testedPaths = PathTests();
        drivePaths.AddRange(testedPaths);

        // Add all drive roots
        DriveInfo[] drives = DriveInfo.GetDrives();
        foreach (DriveInfo drive in drives)
        {
            if (drive.IsReady)
            {
                drivePaths.Add(drive.Name);
            }
        }

        SettingsManager.AddPathsAsNodes(tree, drivePaths);
        tree.Rebuild(); // Force UI update
    }

    void AssetLoader(bool value) // Toggle requires a bool parameter
    {
        settings.loadbundleonlaunch = value;
        SettingsManager.application = settings;
        SettingsManager.SaveSettings();
    }
    
	public void Expand(Node node)
	{
		string folderPath = node.fullPath;
		string fullPath = folderPath;
		if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
			fullPath += Path.DirectorySeparatorChar; 
		List<string> paths = SettingsManager.AddFilePaths(fullPath, "map");
		SettingsManager.AddPathsAsNodes(tree, paths); 
		node.isExpanded = true;
	}
	
	public void OnExpand(Node node)
	{
		node.isSelected = true;
	}
    
	public void OnSelect(Node node)
	{
		if (node.isSelected)
		{
			Expand(node);
			footer.text = node.name;
			directoryField.text = node.fullPath;
			return;
		}
		node.CollapseAll();
		footer.text = "";
		directoryField.text = "";
	}
    
    void StyleChange(bool value) // Toggle requires a bool parameter
    {
        settings.terrainTextureSet = value;
        TerrainManager.SetTerrainReferences();
        TerrainManager.SetTerrainLayers();
        SettingsManager.SaveSettings();
    }
    
    void CameraChange(float value) // Slider requires a float parameter
    {			
        settings.prefabRenderDistance = value;
        SettingsManager.application = settings;
        CameraManager.Instance.SetRenderLimit();
        SettingsManager.SaveSettings();
    }

    void DirectoryChange(string value) // InputField requires a string parameter
    {
        settings.rustDirectory = value;
        SettingsManager.application = settings;		
        UpdateButtonStates();
        SettingsManager.SaveSettings();
    }
    
    void ToggleStyle(bool value) // Toggle requires a bool parameter
    {
        TerrainManager.SetTerrainLayers();
    }
    
    public void UpdateButtonStates()
    {
        bool isValidBundle = AssetManager.ValidBundlePath(directoryField.text);

        
        footer.text = AssetManager.IsInitialised ? "Bundles Loaded" : "Bundles not loaded";

        bundleButtons[0].interactable = isValidBundle && !AssetManager.IsInitialised;
        bundleButtons[1].interactable = AssetManager.IsInitialised;
    }
}