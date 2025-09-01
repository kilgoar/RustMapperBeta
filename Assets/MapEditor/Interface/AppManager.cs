using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text; 
using UnityEngine;
using UnityEngine.UI;
using RustMapEditor.Variables;
using UIRecycleTreeNamespace;
using static TerrainManager;

public class AppManager : MonoBehaviour
{
    public List<Toggle> windowToggles = new List<Toggle>();
    public List<GameObject> windowPanels = new List<GameObject>();
	public List<UIRecycleTree> RecycleTrees = new List<UIRecycleTree>();
	public List<Button> CloseButtons = new List<Button>();
	public List<InputField> allInputFields = new List<InputField>();
	public List<Text> allLabels = new List<Text>();
	public List<WindowManager> allWindowManagers = new List<WindowManager>();
	public List<Image> allImages = new List<Image>();
	public List<Sprite> allSprites = new List<Sprite>();
	
	private Dictionary<Text, string> textColorMapping = new Dictionary<Text, string>();
	
	public Color color1, color2, color3;
	
	public GameObject menuPanel;
	public GameObject loadScreen, quitScreen, compassScreen, confirmationTemplate, tooltipTemplate;
    public Toggle lockToggle;
	public string harmonyMessage;

    public Dictionary<Toggle, GameObject> windowDictionary = new Dictionary<Toggle, GameObject>();

    public TemplateWindow templateWindowPrefab;
	public Canvas uiCanvas;
	public static AppManager Instance { get; private set; }
	
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;            
            DontDestroyOnLoad(gameObject); 
			LoadTemplatePrefab();
        }
        else
        {
            Destroy(gameObject);
        }
    }
	
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void RuntimeInit()
    {
		HarmonyLoader.DeleteLog();
		Instance.harmonyMessage = HarmonyLoader.LoadHarmonyMods(Path.Combine(SettingsManager.AppDataPath(), "HarmonyMods"));
		Instance.harmonyMessage += "\n" + HarmonyLoader.LoadHarmonyMods(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "HarmonyMods"));
		
		LoadScreen.Instance.Show();
		
        SceneController.InitializeScene();
        SettingsManager.RuntimeInit();
		Debug.Log("settings manager loaded");
		BindManager.RuntimeInit();

		AssetManager.RuntimeInit();
				
		Debug.Log("settings loaded");
		Debug.Log("initializing rustmapper build: " + DebugManager.Instance.buildId);

        TerrainManager.RuntimeInit();
		Debug.Log("terrain manager initialized");
        PrefabManager.RuntimeInit();
		Debug.Log("prefab manager initialized");
        PathManager.RuntimeInit();
		Debug.Log("path manager initialized");
		AreaManager.Initialize();	
		Debug.Log("area manager initialized");
		MapManager.RuntimeInit();
		Debug.Log("map manager initialized");
		
		
    }

	private void Start()
	{
		if (windowToggles.Count != windowPanels.Count)
		{
			Debug.LogError("The number of window toggles and window panels must match.");
			return;
		}

		for (int i = 0; i < windowToggles.Count; i++)
		{
			windowDictionary.Add(windowToggles[i], windowPanels[i]);
		}

		foreach (var entry in windowDictionary)
		{
			entry.Key.onValueChanged.AddListener(delegate { OnWindowToggle(entry.Key, entry.Value); });
		}

		lockToggle.onValueChanged.AddListener(delegate { LockWindows(); });

		/*
		foreach (var entry in windowDictionary)
		{
			entry.Key.isOn = false;
			entry.Value.SetActive(false);

			int index = windowPanels.IndexOf(entry.Value);
			if (index >= 0 && index < RecycleTrees.Count && RecycleTrees[index] != null)
			{
				RecycleTrees[index].gameObject.SetActive(false);
			}
		}
		*/
		
        for (int i = 0; i < CloseButtons.Count; i++)
        {
            int index = i;
            CloseButtons[i].onClick.AddListener(() => CloseWindow(index));
        }
		
		CollectLabels();
		CollectInputFields();
		CollectWindowManagers();
		CollectImages(); 
		
		FilePreset application = SettingsManager.application;
			if (string.IsNullOrEmpty(application.startupSkin))
			{
				string appDataSkinsPath = Path.Combine(SettingsManager.AppDataPath(), "Custom", "Skins");
				application.startupSkin = Path.Combine(appDataSkinsPath, "darkmode.png");
			}
		Debug.Log("loading startup skin at " + application.startupSkin);
		ModManager.LoadSkin(application.startupSkin);
		
		LoadWindowStates();
	}
	
	
	public void LoadWindowStates()
	{
		if (SettingsManager.windowStates == null || SettingsManager.windowStates.Length == 0)
		{
			Debug.Log("No window states found in SettingsManager. Skipping load.");
			return;
		}

		if (SettingsManager.windowStates.Length != windowPanels.Count)
		{
			Debug.LogWarning($"Mismatch between saved window states ({SettingsManager.windowStates.Length}) and window panels ({windowPanels.Count}). Loading what is available.");
		}

		for (int i = 0; i < windowPanels.Count && i < SettingsManager.windowStates.Length; i++)
		{
			if (windowPanels[i] == null || windowToggles[i] == null)
			{
				Debug.LogWarning($"Null window panel or toggle at index {i}. Skipping.");
				continue;
			}

			WindowState state = SettingsManager.windowStates[i];
			RectTransform rect = windowPanels[i].GetComponent<RectTransform>();

			if (rect == null)
			{
				Debug.LogWarning($"Window panel at index {i} has no RectTransform. Skipping.");
				continue;
			}

			// Set active state
			windowPanels[i].SetActive(state.isActive);
			windowToggles[i].SetIsOnWithoutNotify(state.isActive);

			// Set position and scale
			rect.localPosition = state.position;
			rect.localScale = state.scale;

			// Handle associated RecycleTree if it exists
			if (i < RecycleTrees.Count && RecycleTrees[i] != null)
			{
				RecycleTrees[i].gameObject.SetActive(state.isActive);
				RectTransform treeRect = RecycleTrees[i].GetComponent<RectTransform>();
				if (treeRect != null)
				{
					treeRect.localScale = state.scale;
				}
				else
				{
					Debug.LogWarning($"RecycleTree at index {i} has no RectTransform.");
				}
			}
		}

		// Ensure menuPanel is on top
		if (menuPanel != null)
		{
			menuPanel.transform.SetAsLastSibling();
		}

		Debug.Log("Window states loaded successfully.");
	}
	

public void CollectImages()
{
    allImages.Clear();
    allSprites.Clear();

    // Helper function to collect images from a GameObject's children
    void CollectFromGameObject(GameObject go)
    {
        if (go == null) return;
        Image[] images = go.GetComponentsInChildren<Image>(true); // Include inactive
        allImages.AddRange(images);
    }

    // Collect images from windowPanels, menuPanel, and Compass
    foreach (var panel in windowPanels)
    {
        CollectFromGameObject(panel);
    }
	
	CollectFromGameObject(tooltipTemplate);
	CollectFromGameObject(confirmationTemplate);
	
	CollectFromGameObject(loadScreen);
	CollectFromGameObject(quitScreen);
    CollectFromGameObject(menuPanel);
    CollectFromGameObject(Compass.Instance?.gameObject);

    // Collect sprites from UIRecycleTree's nodeStylesArray
    foreach (var tree in RecycleTrees)
    {
        if (tree != null)
        {
            // Collect images from the tree's GameObject
            CollectFromGameObject(tree.gameObject);

            // Collect sprites from nodeStylesArray
            NodeStyle[] nodeStyles = tree.nodeStyles;
            if (nodeStyles != null)
            {
                foreach (var style in nodeStyles)
                {
                    if (style != null)
                    {
                        CollectSpritesFromNodeStyle(style);
                    }
                }
            }
        }
    }

    Debug.Log(allImages.Count + " Image components found");
    Debug.Log(allSprites.Count + " Sprite components found");
}

private void CollectSpritesFromNodeStyle(object obj, HashSet<object> visited = null)
{
    if (obj == null) return;
    if (visited == null) visited = new HashSet<object>();
    if (!visited.Add(obj)) return; // Avoid infinite recursion

    System.Type type = obj.GetType();
    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    //Debug.Log(fields.Length + " fields found within " + type.Name);

    foreach (var field in fields)
    {
        // Handle Icon fields
        if (field.FieldType == typeof(UIRecycleTreeNamespace.Icon))        {
            object iconObj = field.GetValue(obj);
			UIRecycleTreeNamespace.Icon icon = (UIRecycleTreeNamespace.Icon)iconObj;
			allSprites.Add(icon.sprite);
			//Debug.Log("treesprite added to list");
            }
            else            {
                //Debug.Log($"Icon is null in {type.Name}.{field.Name}");
            }
        
        object nestedObj = field.GetValue(obj);
            if (nestedObj != null)
            {
                // Recursively search for Icons in nested objects
                CollectSpritesFromNodeStyle(nestedObj, visited);
            }
        
    }
}
	public void SaveWindowStates()    {
        WindowState[] states = new WindowState[windowPanels.Count];
        for (int i = 0; i < windowPanels.Count; i++)
        {
            RectTransform rect = windowPanels[i].GetComponent<RectTransform>();
            states[i] = new WindowState(
                windowPanels[i].activeSelf,
                rect.localPosition,
                rect.localScale
            );
        }
        SettingsManager.windowStates = states;
        SettingsManager.SaveSettings();
    }

	public void DeactivateWindow(int index)
    {
        if (index < 0 || index >= windowToggles.Count || index >= windowPanels.Count)
        {
            Debug.LogWarning($"Invalid window index: {index}. Must be within bounds of windowToggles and windowPanels arrays.");
            return;
        }

        if (windowToggles[index] == null || windowPanels[index] == null)
        {
            Debug.LogWarning($"Null reference found at index {index}: windowToggles or windowPanels is null.");
            return;
        }

        windowToggles[index].SetIsOnWithoutNotify(false); // Update toggle without triggering callback
        windowPanels[index].SetActive(false);

        // Deactivate associated RecycleTree if it exists
        if (index < RecycleTrees.Count && RecycleTrees[index] != null)
        {
            RecycleTrees[index].gameObject.SetActive(false);
        }

        SaveWindowStates(); // Persist the state change
    }
		
public void ActivateWindow(int index)
{
    if (index < 0 || index >= windowToggles.Count || index >= windowPanels.Count)
    {
        Debug.LogWarning($"Invalid window index: {index}. Must be within bounds of windowToggles and windowPanels arrays.");
        return;
    }

    if (windowToggles[index] == null || windowPanels[index] == null)
    {
        Debug.LogWarning($"Null reference found at index {index}: windowToggles or windowPanels is null.");
        return;
    }

    // Check for mutual exclusivity between Path (9) and Painting (6) windows
    if (index == 6 && windowPanels[9].activeSelf) // Activating Painting, Path is active
    {
        DeactivateWindow(9); // Deactivate Path
		CoroutineManager.Instance.ChangeStylus(2);
    }
    else if (index == 9 && windowPanels[6].activeSelf) // Activating Path, Painting is active
    {
        DeactivateWindow(6); // Deactivate Painting
		CoroutineManager.Instance.ChangeStylus(3);
    }

    windowToggles[index].isOn = true;
    windowPanels[index].SetActive(true);

    RectTransform windowRect = windowPanels[index].GetComponent<RectTransform>();
    if (windowRect == null)
    {
        Debug.LogWarning($"Window panel at index {index} has no RectTransform component.");
        return;
    }

    if (menuPanel == null)
    {
        Debug.LogWarning("menuPanel is null. Cannot adjust scale.");
        return;
    }

    RectTransform menuRect = menuPanel.GetComponent<RectTransform>();
    if (menuRect == null)
    {
        Debug.LogWarning("menuPanel has no RectTransform component. Cannot adjust scale.");
        return;
    }

    Vector3 menuScale = menuRect.localScale;
    Vector3 adjustedScale = menuScale - Vector3.one;

    adjustedScale.x = Mathf.Clamp(adjustedScale.x, 0.6f, 3f);
    adjustedScale.y = Mathf.Clamp(adjustedScale.y, 0.6f, 3f);
    adjustedScale.z = Mathf.Clamp(adjustedScale.z, 0.6f, 3f);

    windowRect.localScale = adjustedScale;

    ActivateRecycleTree(index, adjustedScale);

    SaveWindowStates(); // Persist the state change
}

	public void UpdateInspector(){
		var selectedObjects = CameraManager.Instance._selectedObjects;
		if(selectedObjects.Count != 0){
			ActivateInspector();
			InspectorWindow.Instance.UpdateData();
		}
		else{
			DeactivateInspector();
		}
	}
	
	public void SetInspector(GameObject go){
		ActivateInspector();
		InspectorWindow.Instance.SetSelection(go);
	}

	public void ActivateInspector(){
		if(!windowPanels[10].activeSelf){
			ActivateWindow(10);
		}
	}
	
	public void DeactivateInspector(){
		if(windowPanels[10].activeSelf){
			DeactivateWindow(10);
		}
	}

	public void ActivateRecycleTree(int index, Vector3 adjustedScale)
	{
		// Validate RecycleTrees list and index
		if (RecycleTrees == null || index < 0 || index >= RecycleTrees.Count)
		{
			// Silently return since RecycleTrees are optional
			return;
		}

		// Check if the RecycleTree at index exists
		if (RecycleTrees[index] == null)
		{
			// Silently return since it's fine for RecycleTrees to not exist
			return;
		}

		// Activate the RecycleTree GameObject
		RecycleTrees[index].gameObject.SetActive(true);

		// Safely handle RecycleTree scaling
		RectTransform treeRect = RecycleTrees[index].GetComponent<RectTransform>();
		if (treeRect != null)
		{
			treeRect.localScale = adjustedScale;
		}
		else
		{
			Debug.LogWarning($"RecycleTree at index {index} has no RectTransform component.");
		}
	}
		
	public void CloseWindow(int index)
    {
        if (index >= 0 && index < windowPanels.Count)        {
            windowPanels[index].SetActive(false);
			windowToggles[index].isOn = false; 
            //windowToggles[index].SetIsOnWithoutNotify(false);

            if (index < RecycleTrees.Count && RecycleTrees[index] != null)            {
                RecycleTrees[index].gameObject.SetActive(false);
            }
        }
    }
	
    public void SetColors()
    {
        // Define the default color mappings based on provided RGBA values
        Dictionary<string, string> defaultColorToAppColor = new Dictionary<string, string>
        {
            { "A4A4A4FF", "color1" }, // Maps to color1
            { "DED3C8FF", "color1" }, // Maps to color1
            { "323232FF", "color2" }, // Maps to color2
            { "DD5640FF", "color2" }, // Maps to color2
            { "814134FF", "color2" }, // Maps to color2 (loadscreen)
            { "07AAD1FF", "color2" }, // Maps to color3
            { "738E44FF", "color3" }, // Maps to color3
            { "65B4DDFF", "color3" }, // Maps to color3
            { "81AD35FF", "color3" }, // Maps to color3
            { "80AB36FF", "color3" }, // Maps to color3
            { "88C421FF", "color3" }, // Maps to color3
            { "738E45FF", "color3" }, // Maps to color3
            { "335666FF", "color2" }  // Maps to color3 (loadscreen)
            // Note: "000000FF" is not mapped yet as per instructions
        };

        // Clear existing mapping to avoid duplicates
        textColorMapping.Clear();

        // Map each Text component to its corresponding color identifier
        foreach (Text text in allLabels)
        {
            if (text != null)
            {
                string colorString = ColorUtility.ToHtmlStringRGBA(text.color);
                if (defaultColorToAppColor.TryGetValue(colorString, out string colorId))
                {
                    textColorMapping[text] = colorId;
                }
                else
                {
                    //Debug.LogWarning($"Text '{text.gameObject.name}' with color #{colorString} has no mapping. Skipping.");
                }
            }
        }

        // Apply the colors from color1, color2, color3 to the Text components
        UpdateTextColors();
    }
	
    public void UpdateTextColors()
    {
        foreach (var entry in textColorMapping)
        {
            Text text = entry.Key;
            string colorId = entry.Value;

            if (text != null)
            {
                switch (colorId)
                {
                    case "color1":
                        text.color = color1;
                        //Debug.Log($"Set Text '{text.gameObject.name}' to color1 ({ColorUtility.ToHtmlStringRGBA(color1)})");
                        break;
                    case "color2":
                        text.color = color2;
                        //Debug.Log($"Set Text '{text.gameObject.name}' to color2 ({ColorUtility.ToHtmlStringRGBA(color2)})");
                        break;
                    case "color3":
                        text.color = color3;
                        //Debug.Log($"Set Text '{text.gameObject.name}' to color3 ({ColorUtility.ToHtmlStringRGBA(color3)})");
                        break;
                    default:
                        Debug.LogWarning($"Unknown color ID '{colorId}' for Text '{text.gameObject.name}'.");
                        break;
                }
            }
        }
    }
	
	
	public void CollectInputFields()    {
		allInputFields.Clear();
        foreach (var panel in windowPanels)
        {
            if (panel != null)
            {
                InputField[] inputFields = panel.GetComponentsInChildren<InputField>(true);
                allInputFields.AddRange(inputFields);
            }
        }
    }
	
    private class ColorData
    {
        public int count;
        public List<Text> objects;

        public ColorData(int count, List<Text> objects)
        {
            this.count = count;
            this.objects = objects;
        }
    }

    public void CollectLabels()
    {
        allInputFields.Clear(); // Clear any existing input fields
        foreach (var panel in windowPanels)
        {
            if (panel != null)
            {
                Text[] texts = panel.GetComponentsInChildren<Text>(true);
                allLabels.AddRange(texts);
            }
        }
		
		allLabels.AddRange(compassScreen.GetComponentsInChildren<Text>(true));
		allLabels.AddRange(tooltipTemplate.GetComponentsInChildren<Text>(true));
		
		allLabels.AddRange(quitScreen.GetComponentsInChildren<Text>(true));
		allLabels.AddRange(loadScreen.GetComponentsInChildren<Text>(true));
        Debug.Log(allLabels.Count + " text objects found");
		
		SetColors();
        
        //AnalyzeTextColors();
    }

    private void AnalyzeTextColors()
    {
        // Dictionary to store color counts and associated Text objects
        Dictionary<Color, ColorData> colorData = new Dictionary<Color, ColorData>();

        // Iterate through all text components
        foreach (Text text in allLabels)
        {
            if (text != null)
            {
                Color textColor = text.color;

                // Check if color exists in dictionary
                if (colorData.ContainsKey(textColor))
                {
                    colorData[textColor].count++; // Increment count
                    colorData[textColor].objects.Add(text); // Add object to list
                }
                else
                {
                    colorData.Add(textColor, new ColorData(1, new List<Text> { text }));
                }
            }
        }

        // Log the color analysis
        Debug.Log("Text Color Analysis:");
        foreach (var entry in colorData)
        {
            string colorString = ColorUtility.ToHtmlStringRGBA(entry.Key);
            Debug.Log($"Color #{colorString}: {entry.Value.count} text objects");

            // List object names if count is less than 30
            if (entry.Value.count < 30)
            {
                Debug.Log($"  Objects with Color #{colorString}:");
                foreach (Text text in entry.Value.objects)
                {
                    Debug.Log($"    - {text.gameObject.name} (Text: '{text.text}')");
                }
            }
        }

        // Save the color analysis to a file
        string outputPath = System.IO.Path.Combine(Application.persistentDataPath, "TextColorAnalysis.txt");
        System.IO.File.WriteAllText(outputPath, GetColorAnalysisAsString(colorData));
        Debug.Log($"Color analysis saved to: {outputPath}");
    }

    private string GetColorAnalysisAsString(Dictionary<Color, ColorData> colorData)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Text Color Analysis - {System.DateTime.Now}");
        sb.AppendLine($"Total Text Objects: {allLabels.Count}");
        sb.AppendLine("--------------------------------");

        foreach (var entry in colorData)
        {
            string colorString = ColorUtility.ToHtmlStringRGBA(entry.Key);
            sb.AppendLine($"Color #{colorString}: {entry.Value.count} text objects");

            // Include object names if count is less than 30
            if (entry.Value.count < 30)
            {
                sb.AppendLine($"  Objects with Color #{colorString}:");
                foreach (Text text in entry.Value.objects)
                {
                    sb.AppendLine($"    - {text.gameObject.name} (Text: '{text.text}')");
                }
            }
            sb.AppendLine("---");
        }

        return sb.ToString();
    }
	
	public void CollectWindowManagers()
    {
		allWindowManagers.Clear();
        foreach (var panel in windowPanels)
        {
            if (panel != null)
            {
                WindowManager manager = panel.GetComponent<WindowManager>();
                allWindowManagers.Add(manager);
            }
        }
    }

    // Method to check if any input field is active
    public bool IsAnyInputFieldActive()    {
		
        foreach (var field in allInputFields)        {			
            if (field != null && field.isFocused)   {
                return true;
            }
        }
        return false;
    }
	
	public bool AnyDragging(){
		
		foreach (var manager in allWindowManagers){
			if (manager.isDragging || manager.isRescaling){
				return true;
			}
		}
			
		return false;

	}
	
	public void ScaleAllWindows(Vector3 scale)
	{
		// Clamp the scale values to the range 0.6f to 3f
		Vector3 clampedScale = scale;
		clampedScale.x = Mathf.Clamp(clampedScale.x, 0.6f, 3f);
		clampedScale.y = Mathf.Clamp(clampedScale.y, 0.6f, 3f);

		foreach (GameObject window in windowPanels)
		{
			if (window != null && window.activeInHierarchy) // Only scale active windows
			{
				RectTransform rect = window.GetComponent<RectTransform>();
				if (rect != null)
				{
					rect.localScale = clampedScale;
				}
			}
		}

		// Optionally scale associated RecycleTrees if they're active
		for (int i = 0; i < RecycleTrees.Count; i++)
		{
			if (i < windowPanels.Count && windowPanels[i].activeInHierarchy && RecycleTrees[i] != null)
			{
				RectTransform treeRect = RecycleTrees[i].GetComponent<RectTransform>();
				if (treeRect != null)
				{
					treeRect.localScale = clampedScale;
				}
			}
		}
		SaveWindowStates();
	}

public void OnWindowToggle(Toggle windowToggle, GameObject windowPanel)
{
    bool windowState = windowToggle.isOn;
    windowPanel.SetActive(windowState);
    windowPanel.transform.SetAsLastSibling();

    int index = windowPanels.IndexOf(windowPanel);

    // Enforce mutual exclusivity between Path (9) and Painting (6) windows
    if (windowState)
    {
        if (index == 6 && windowPanels[9].activeSelf) // Painting activated, Path is active
        {
            DeactivateWindow(9); // Deactivate Path
			CoroutineManager.Instance.ChangeStylus(2); //force paintbrush
        }
        else if (index == 9 && windowPanels[6].activeSelf) // Path activated, Painting is active
        {
            DeactivateWindow(6); // Deactivate Painting
			CoroutineManager.Instance.ChangeStylus(3); //force path selection
        }
    }

    // Apply menu's adjusted scale (menu scale - 1) to the window if activated
    if (windowState && menuPanel != null)
    {
        RectTransform windowRect = windowPanel.GetComponent<RectTransform>();
        Vector3 menuScale = menuPanel.GetComponent<RectTransform>().localScale;
        Vector3 adjustedScale = menuScale - Vector3.one;

        adjustedScale.x = Mathf.Clamp(adjustedScale.x, 0.6f, 3f);
        adjustedScale.y = Mathf.Clamp(adjustedScale.y, 0.6f, 3f);

        if (windowRect != null)
        {
            windowRect.localScale = adjustedScale;
        }
    }

    // Handle RecycleTree if it exists
    if (index >= 0 && index < RecycleTrees.Count && RecycleTrees[index] != null)
    {
        RecycleTrees[index].gameObject.SetActive(windowState);

        if (windowState)
        {
            int windowSiblingIndex = windowPanel.transform.GetSiblingIndex();
            RecycleTrees[index].transform.SetSiblingIndex(windowSiblingIndex + 1);

            // Apply the adjusted scale to the tree
            if (menuPanel != null)
            {
                RectTransform treeRect = RecycleTrees[index].GetComponent<RectTransform>();
                Vector3 menuScale = menuPanel.GetComponent<RectTransform>().localScale;
                Vector3 adjustedScale = menuScale - Vector3.one;

                adjustedScale.x = Mathf.Clamp(adjustedScale.x, 0.6f, 3f);
                adjustedScale.y = Mathf.Clamp(adjustedScale.y, 0.6f, 3f);

                if (treeRect != null)
                {
                    treeRect.localScale = adjustedScale;
                }
            }
        }
    }

    if (menuPanel != null)
    {
        menuPanel.transform.SetAsLastSibling();
    }

    SaveWindowStates();
    SettingsManager.SaveSettings();
}
	
	public void LockWindows()    {
			lockToggle.targetGraphic.enabled = !lockToggle.isOn;
			SettingsManager.SaveSettings();
	}

	public void LoadTemplatePrefab()
    {
        if (templateWindowPrefab == null)
        {
            templateWindowPrefab = Resources.Load<TemplateWindow>("TemplateWindow");
            if (templateWindowPrefab == null)
            {
                Debug.LogError("Failed to load TemplateWindow prefab");
            }
        }
    }

    public TemplateWindow CreateWindow(string titleText, Rect rect, string iconPath)
    {
        if (Instance == null)
        {
            Debug.LogError("AppManager Instance is not initialized.");
            return null;
        }

        if (templateWindowPrefab == null)
        {
            LoadTemplatePrefab();
            if (templateWindowPrefab == null)
            {
                return null;
            }
        }

        if (uiCanvas == null)
        {
            Debug.LogError("uiCanvas is not assigned in AppManager. Cannot create window.");
            return null;
        }

        // Instantiate the window 
        TemplateWindow newWindow = Instantiate(templateWindowPrefab, uiCanvas.transform);
        RectTransform windowRect = newWindow.GetComponent<RectTransform>();

        //  (empties the template of sub templates)
        CleanUpNonEssentialChildren(newWindow);

        // Configure the remaining essential components
        newWindow.title.text = titleText;
        windowRect.anchoredPosition = new Vector2(rect.x, rect.y);
        windowRect.sizeDelta = new Vector2(rect.width, rect.height);

        if (menuPanel != null)
        {
            Vector3 menuScale = menuPanel.GetComponent<RectTransform>().localScale;
            Vector3 adjustedScale = menuScale - Vector3.one;
            adjustedScale.x = Mathf.Clamp(adjustedScale.x, 0.6f, 3f);
            adjustedScale.y = Mathf.Clamp(adjustedScale.y, 0.6f, 3f);
            windowRect.localScale = adjustedScale;
        }
        else
        {
            windowRect.localScale = Vector3.zero;
        }

        newWindow.gameObject.SetActive(false);

        // Add WindowManager component and configure it
        WindowManager windowManager = newWindow.gameObject.AddComponent<WindowManager>();
        ConfigureWindowManager(windowManager, newWindow);

		CustomizeCloseButton(newWindow.close, iconPath);

        // Create a toggle in MenuManager and replace the window's toggle
        if (MenuManager.Instance != null)
        {
            Toggle newToggle = MenuManager.Instance.CreateWindowToggle(iconPath);
            if (newToggle != null)
            {
                newWindow.toggle = newToggle;
            }
            else
            {
                Debug.LogWarning("Failed to create toggle for window in MenuManager.");
            }
        }
        else
        {
            Debug.LogWarning("MenuManager Instance is not available to create window toggle.");
        }

        RegisterWindow(newWindow);

        return newWindow;
    }

    public void CleanUpNonEssentialChildren(TemplateWindow newWindow)
    {
        // Define essential GameObjects to keep
        List<GameObject> essentialObjects = new List<GameObject>
        {
            newWindow.gameObject,           // The panel (root)
            newWindow.title.gameObject,     // The title Text
            newWindow.close.gameObject,      // The close Button
			newWindow.footer.gameObject,
			newWindow.rescale.gameObject
        };

        // Get all children and destroy non-essential ones
        List<GameObject> childrenToDestroy = new List<GameObject>();
        foreach (Transform child in newWindow.transform)
        {
            if (!essentialObjects.Contains(child.gameObject))
            {
                childrenToDestroy.Add(child.gameObject);
            }
        }

        foreach (GameObject child in childrenToDestroy)
        {
            Destroy(child);
        }
    }

    public void ConfigureWindowManager(WindowManager windowManager, TemplateWindow newWindow)
    {
        windowManager.WindowsPanel = newWindow.gameObject; // The window itself is the panel
        windowManager.Window = newWindow.gameObject;       // Same GameObject for simplicity
        windowManager.lockToggle = lockToggle;             // Use AppManager's lockToggle

        // Get the RectTransform for positioning
        RectTransform windowRect = newWindow.GetComponent<RectTransform>();

        // Use the close button as rescaleButton, or create a new one if needed
        windowManager.rescaleButton = newWindow.rescale;

        // Tree remains null unless specified
        windowManager.Tree = null;
    }

    public void RegisterWindow(TemplateWindow window)
    {
        if (window.toggle == null || window.close == null)
        {
            Debug.LogError("TemplateWindow prefab is missing required components (Toggle or Close Button).");
            return;
        }

        windowToggles.Add(window.toggle);
        windowPanels.Add(window.gameObject);
        CloseButtons.Add(window.close);

        windowDictionary.Add(window.toggle, window.gameObject);
        window.toggle.onValueChanged.AddListener(delegate { OnWindowToggle(window.toggle, window.gameObject); });

        int index = CloseButtons.Count - 1;
        CloseButtons[index].onClick.AddListener(() => CloseWindow(index));

		CollectLabels();
		CollectInputFields();
		CollectWindowManagers();
		CollectImages();
        SaveWindowStates();
		
        if (menuPanel != null)
        {
            menuPanel.transform.SetAsLastSibling();
        }
    }
	

	public void CustomizeCloseButton(Button closeButton, string iconPath)
	{
		if (closeButton == null)
		{
			Debug.LogWarning("Close button is null. Cannot customize.");
			return;
		}

		Image buttonImage = closeButton.GetComponent<Image>();
		if (buttonImage == null)
		{
			Debug.LogWarning("Close button has no Image component to customize.");
			return;
		}

		// Define the green color used in the toggle's active state
		Color greenColor = new Color32(0x72, 0x8D, 0x44, 0xFF); // #728d44

		if (string.IsNullOrEmpty(iconPath))
		{
			Debug.LogWarning("iconPath is empty. Applying green color to close button.");
			buttonImage.color = greenColor; // Default to green if no iconPath
			return;
		}

		// Load the sprite from the iconPath using ModManager
		Texture2D texture = ModManager.LoadTexture(iconPath);
		if (texture != null)
		{
			Sprite newSprite = ModManager.CreateSprite(texture);
			if (newSprite != null)
			{
				buttonImage.sprite = newSprite;
				buttonImage.type = Image.Type.Simple; // Adjust as needed (e.g., Sliced for 9-slice sprites)
				buttonImage.color = greenColor; // Apply the green color to tint the sprite
			}
		}
		else
		{
			// Fallback: Use the green color if sprite loading fails
			Debug.LogWarning($"Failed to load sprite from {iconPath}. Applying green color.");
			buttonImage.color = greenColor;
		}
	}

    // UI Element Creation Methods
    public Toggle CreateToggle(Transform parent, Rect rect, string text = "")
    {
        if (templateWindowPrefab == null || templateWindowPrefab.toggle == null)
        {
            Debug.LogError("TemplateWindow prefab or its Toggle is not available.");
            return null;
        }
        Toggle newToggle = Instantiate(templateWindowPrefab.toggle, parent);
        RectTransform toggleRect = newToggle.GetComponent<RectTransform>();
        toggleRect.anchoredPosition = new Vector2(rect.x, rect.y);
        toggleRect.sizeDelta = new Vector2(rect.width, rect.height);

        // Set the label if it exists as a child
        Text label = newToggle.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = text;
        }
        else if (!string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("Toggle prefab has no child Text component to set label.");
        }

        return newToggle;
    }

    public Button CreateButton(Transform parent, Rect rect, string text = "")
    {
        if (templateWindowPrefab == null || templateWindowPrefab.button == null)
        {
            Debug.LogError("TemplateWindow prefab or its default Button is not available.");
            return null;
        }
        Button newButton = Instantiate(templateWindowPrefab.button, parent);
        RectTransform buttonRect = newButton.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(rect.x, rect.y);
        buttonRect.sizeDelta = new Vector2(rect.width, rect.height);

        // Set the label if it exists as a child
        Text label = newButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = text;
        }
        else if (!string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("Button prefab has no child Text component to set label.");
        }

        return newButton;
    }

    public Button CreateBrightButton(Transform parent, Rect rect, string text = "")
    {
        if (templateWindowPrefab == null || templateWindowPrefab.buttonbright == null)
        {
            Debug.LogError("TemplateWindow prefab or its Bright Button is not available.");
            return null;
        }
        Button newButton = Instantiate(templateWindowPrefab.buttonbright, parent);
        RectTransform buttonRect = newButton.GetComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(rect.x, rect.y);
        buttonRect.sizeDelta = new Vector2(rect.width, rect.height);

        // Set the label if it exists as a child
        Text label = newButton.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text = text;
        }
        else if (!string.IsNullOrEmpty(text))
        {
            Debug.LogWarning("Bright Button prefab has no child Text component to set label.");
        }

        return newButton;
    }

    public Text CreateLabelText(Transform parent, Rect rect, string text = "")
    {
        if (templateWindowPrefab == null || templateWindowPrefab.label == null)
        {
            Debug.LogError("TemplateWindow prefab or its Label Text is not available.");
            return null;
        }
        Text newText = Instantiate(templateWindowPrefab.label, parent);
        RectTransform textRect = newText.GetComponent<RectTransform>();
        textRect.anchoredPosition = new Vector2(rect.x, rect.y);
        textRect.sizeDelta = new Vector2(rect.width, rect.height);
        newText.text = text;
        return newText;
    }

    public Slider CreateSlider(Transform parent, Rect rect)
    {
        if (templateWindowPrefab == null || templateWindowPrefab.slider == null)
        {
            Debug.LogError("TemplateWindow prefab or its Slider is not available.");
            return null;
        }
        Slider newSlider = Instantiate(templateWindowPrefab.slider, parent);
        RectTransform sliderRect = newSlider.GetComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(rect.x, rect.y);
        sliderRect.sizeDelta = new Vector2(rect.width, rect.height);
        return newSlider;
    }

    public Dropdown CreateDropdown(Transform parent, Rect rect)
    {
        if (templateWindowPrefab == null || templateWindowPrefab.dropdown == null)
        {
            Debug.LogError("TemplateWindow prefab or its Dropdown is not available.");
            return null;
        }
        Dropdown newDropdown = Instantiate(templateWindowPrefab.dropdown, parent);
        RectTransform dropdownRect = newDropdown.GetComponent<RectTransform>();
        dropdownRect.anchoredPosition = new Vector2(rect.x, rect.y);
        dropdownRect.sizeDelta = new Vector2(rect.width, rect.height);
        return newDropdown;
    }

    public InputField CreateInputField(Transform parent, Rect rect, string text = "")
    {
        if (templateWindowPrefab == null || templateWindowPrefab.inputField == null)
        {
            Debug.LogError("TemplateWindow prefab or its InputField is not available.");
            return null;
        }
        InputField newInputField = Instantiate(templateWindowPrefab.inputField, parent);
        RectTransform inputRect = newInputField.GetComponent<RectTransform>();
        inputRect.anchoredPosition = new Vector2(rect.x, rect.y);
        inputRect.sizeDelta = new Vector2(rect.width, rect.height);
        newInputField.text = text;
        allInputFields.Add(newInputField);
        return newInputField;
    }

 


}