using System;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using RustMapEditor.Variables;
using UIRecycleTreeNamespace;
using Dummiesman;

public static class ModManager
{
    public static List<WorldSerialization.MapData> moddingData = new List<WorldSerialization.MapData>();
	public static string currentSkin;
    public static readonly string[] KnownDataNames = new string[] 
    { 
        "ioentitydata", "vehiclespawnpoints", "lootcontainerdata", "vendingdata", 
        "npcspawnpoints", "bradleypathpoints", "anchorpaths", "mappassword",
		"buildingblocks", "oceanpathpoints"
    };
	
	public static void LoadObj(string path)
	{
		GameObject loadedObject;

		if (!File.Exists(path))
		{
			Debug.Log("Invalid OBJ path");
			return; // Exit early if the file doesn't exist
		}
		else
		{
			loadedObject = new OBJLoader().Load(path);
			loadedObject.transform.SetParent(PrefabManager.PrefabParent);

			// Look through each child of loaded object, generating a MeshCollider for each child based on its MeshFilter
			foreach (Transform child in loadedObject.transform)
			{
				MeshFilter meshFilter = child.GetComponent<MeshFilter>();
				if (meshFilter != null && meshFilter.sharedMesh != null)
				{
					MeshCollider meshCollider = child.gameObject.AddComponent<MeshCollider>();
					meshCollider.sharedMesh = meshFilter.sharedMesh;
					// Optional: Set convex if needed for physics
					meshCollider.convex = true;
				}
				// Set the child to the Prefab layer (layer 3)
				child.gameObject.layer = 3;
			}

			// Set the loaded object to the Prefab layer (layer 3)
			loadedObject.layer = 3;
			loadedObject.SetTagRecursively("Untagged");
			loadedObject.tag = "Prefab";
			// Rotate x by -90 degrees
			loadedObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
			// Set position to (0,0,0)
			loadedObject.transform.localPosition = Vector3.zero;
		}
	}
	
	[ConsoleCommand("TestConfirmation")]
	public static async void TestConfirmation()
	{
		bool result = await ConfirmationManager.Instance.ShowConfirmationAsync(
			title: "Test Confirmation",
			message: "This is a test confirmation dialog. Proceed?",
			yes: "OK",
			no: "Cancel"
		);

		ConsoleWindow.Instance.Post("Test output: " + result);
	}
	
	//compatibility for high-security data fields (prefab count + salt)
	public static string MapDataName(int PreFabCount, string DataName)
    {      
       try
       {
           using (var aes = Aes.Create())
           {
               var rfc2898DeriveBytes = new Rfc2898DeriveBytes(PreFabCount.ToString(), new byte[] { 73, 118, 97, 110, 32, 77, 101, 100, 118, 101, 100, 101, 118 });
               aes.Key = rfc2898DeriveBytes.GetBytes(32);
               aes.IV = rfc2898DeriveBytes.GetBytes(16);
               using (var memoryStream = new MemoryStream())
               {
                   using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                   {
                       var D = Encoding.Unicode.GetBytes(DataName);
                       cryptoStream.Write(D, 0, D.Length);
                       cryptoStream.Close();
                   }

                   return Convert.ToBase64String(memoryStream.ToArray());
               }
           }
       }
       catch { }
       return DataName;
    }

    public static void SetModdingData(List<WorldSerialization.MapData> data)
    {
        moddingData.Clear();
        if (data != null)
        {
            moddingData.AddRange(data);
        }
        else
        {
        }
    }

    public static List<WorldSerialization.MapData> GetModdingData()
    {
        return new List<WorldSerialization.MapData>(moddingData);
    }


    public static void AddOrUpdateModdingData(string name, byte[] data)
    {
        var existing = moddingData.Find(md => md.name == name);
        if (existing != null)
        {
            existing.data = data;
        }
        else
        {
            moddingData.Add(new WorldSerialization.MapData { name = name, data = data });
        }
    }

    public static Sprite CreateSprite(Texture2D texture)
    {
        if (texture == null)
        {
            Debug.LogError("Cannot create sprite from null texture.");
            return null;
        }

        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

public static Texture2D LoadTexture(string filePath)
{
    try
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"Texture file not found at path: {filePath}");
            return null;
        }

        byte[] pngBytes = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false); // Use RGBA32, no mipmaps, non-linear
        if (texture.LoadImage(pngBytes, false)) // Mark non-readable to save memory
        {
            texture.filterMode = FilterMode.Point; // Pixel-perfect loading
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, false);
            return texture;
        }
        else
        {
            UnityEngine.Object.Destroy(texture);
            Debug.LogError($"Failed to load texture data from: {filePath}");
            return null;
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error loading texture from {filePath}: {e.Message}");
        return null;
    }
}
[ConsoleCommand("LoadSkin")]
public static void LoadSkin(string path)
{
    Debug.Log($"Loading skin from {path}");

    // Load the skin texture from the provided path
    Texture2D skinTexture = LoadTexture(path);
    if (skinTexture == null)
    {
        Debug.LogError("Failed to load skin texture. Aborting skin application.");
        return;
    }
	currentSkin = path;
    // Load the sprite sheet from Resources
    string spriteSheetPath = "textures/UI/UIsprites";
    Texture2D spriteSheet = Resources.Load<Texture2D>(spriteSheetPath);
    if (spriteSheet == null)
    {
        Debug.LogError($"Sprite sheet not found at: Resources/{spriteSheetPath}");
        UnityEngine.Object.Destroy(skinTexture);
        return;
    }

    // Preprocess skinTexture to ensure transparent pixels are RGBA: 0,0,0,0
    Color[] pixels = skinTexture.GetPixels();
    for (int i = 0; i < pixels.Length; i++)
    {
        if (pixels[i].a <= 0f)
        {
            pixels[i] = Color.clear;
        }
    }
    skinTexture.SetPixels(pixels);
    skinTexture.Apply(false, false);

    // Set skinTexture settings for smooth scaling
    skinTexture.filterMode = FilterMode.Bilinear;
    skinTexture.wrapMode = TextureWrapMode.Clamp;
    skinTexture.Apply(true, false);

    // Validate texture dimensions
    if (skinTexture.width != spriteSheet.width || skinTexture.height != spriteSheet.height)
    {
        Debug.LogWarning($"Skin texture dimensions ({skinTexture.width}x{skinTexture.height}) do not match sprite sheet dimensions ({spriteSheet.width}x{spriteSheet.height}).");
    }

    // Load all sprites associated with the sprite sheet
    UnityEngine.Object[] assets = Resources.LoadAll(spriteSheetPath, typeof(Sprite));
    List<Sprite> originalSprites = new List<Sprite>();
    foreach (UnityEngine.Object asset in assets)
    {
        if (asset is Sprite sprite)
        {
            originalSprites.Add(sprite);
        }
    }

    if (originalSprites.Count == 0)
    {
        Debug.LogError($"No sprites found in the sprite sheet: {spriteSheetPath}");
        UnityEngine.Object.Destroy(skinTexture);
        return;
    }

    // Create new sprites from the skin texture
    List<Sprite> newSprites = new List<Sprite>();
    foreach (Sprite originalSprite in originalSprites)
    {
        if (originalSprite.rect.xMax > skinTexture.width || originalSprite.rect.yMax > skinTexture.height)
        {
            Debug.LogWarning($"Sprite '{originalSprite.name}' rect ({originalSprite.rect}) exceeds skin texture bounds ({skinTexture.width}x{skinTexture.height}). Skipping.");
            continue;
        }

        Vector2 normalizedPivot = new Vector2(
            originalSprite.pivot.x / originalSprite.rect.width,
            originalSprite.pivot.y / originalSprite.rect.height
        );

        Sprite newSprite = Sprite.Create(
            skinTexture,
            originalSprite.rect,
            normalizedPivot,
            originalSprite.pixelsPerUnit,
            0,
            SpriteMeshType.Tight,
            originalSprite.border,
            false
        );
        newSprite.name = originalSprite.name;
        newSprites.Add(newSprite);
		
		    // Check if sprite name is color1, color2, or color3 and extract first-pixel color
			if (newSprite.name == "color1" || newSprite.name == "color2" || newSprite.name == "color3" || newSprite.name == "color4")
			{
				// Get the first pixel color (bottom-left corner of the sprite's rect)
				Rect spriteRect = newSprite.rect;
				Color pixelColor = skinTexture.GetPixel((int)spriteRect.xMin, (int)spriteRect.yMin);

				// Assign to appropriate AppManager color field and log
				if (newSprite.name == "color1")
				{
					AppManager.Instance.color1 = pixelColor;
					Debug.Log($"Set AppManager.color1 to {pixelColor} for sprite '{newSprite.name}'");
				}
				else if (newSprite.name == "color2")
				{
					AppManager.Instance.color2 = pixelColor;
					Debug.Log($"Set AppManager.color2 to {pixelColor} for sprite '{newSprite.name}'");
				}
				else if (newSprite.name == "color3")
				{
					AppManager.Instance.color3 = pixelColor;
					Debug.Log($"Set AppManager.color3 to {pixelColor} for sprite '{newSprite.name}'");
				}
				else if (newSprite.name == "color4")
				{
					AppManager.Instance.color4 = pixelColor;
					Debug.Log($"Set AppManager.color4 to {pixelColor} for sprite '{newSprite.name}'");
				}
			}
		
    }

    if (newSprites.Count == 0)
    {
        Debug.LogError("No valid sprites created from skin texture. Aborting.");
        UnityEngine.Object.Destroy(skinTexture);
        return;
    }

    // Ensure AppManager.Instance is initialized
    if (AppManager.Instance == null)
    {
        Debug.LogError("AppManager.Instance is not initialized. Cannot update UI.");
        UnityEngine.Object.Destroy(skinTexture);
        return;
    }

    // Update all UI sprites in AppManager.Instance.allImages
    int updatedImages = 0;
    foreach (Image uiImage in AppManager.Instance.allImages)
    {
        if (uiImage != null && uiImage.sprite != null && originalSprites.Exists(s => s.name == uiImage.sprite.name))
        {
            Sprite matchingSprite = newSprites.Find(s => s.name == uiImage.sprite.name);
            if (matchingSprite != null)
            {
                uiImage.sprite = matchingSprite;
                updatedImages++;
            }
        }
    }

    // Update Button SpriteState in windowPanels and menuPanel
    int updatedButtons = 0;
    void UpdateButtonSpriteStates(GameObject go)
    {
        if (go == null) return;
        Button[] buttons = go.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            SpriteState spriteState = button.spriteState;
            bool modified = false;
            SpriteState newSpriteState = spriteState;

            if (spriteState.highlightedSprite != null)
            {
                Sprite newSprite = newSprites.Find(s => s.name == spriteState.highlightedSprite.name);
                if (newSprite != null)
                {
                    newSpriteState.highlightedSprite = newSprite;
                    modified = true;
                }
            }
            if (spriteState.pressedSprite != null)
            {
                Sprite newSprite = newSprites.Find(s => s.name == spriteState.pressedSprite.name);
                if (newSprite != null)
                {
                    newSpriteState.pressedSprite = newSprite;
                    modified = true;
                }
            }
            if (spriteState.selectedSprite != null)
            {
                Sprite newSprite = newSprites.Find(s => s.name == spriteState.selectedSprite.name);
                if (newSprite != null)
                {
                    newSpriteState.selectedSprite = newSprite;
                    modified = true;
                }
            }
            if (spriteState.disabledSprite != null)
            {
                Sprite newSprite = newSprites.Find(s => s.name == spriteState.disabledSprite.name);
                if (newSprite != null)
                {
                    newSpriteState.disabledSprite = newSprite;
                    modified = true;
                }
            }

            if (modified)
            {
                button.spriteState = newSpriteState;
                updatedButtons++;
            }
        }
    }

    foreach (var panel in AppManager.Instance.windowPanels)
    {
        UpdateButtonSpriteStates(panel);
    }

    if (AppManager.Instance.menuPanel != null)
    {
        UpdateButtonSpriteStates(AppManager.Instance.menuPanel);
    }

     // Update NodeStyle sprites in UIRecycleTrees using cloned NodeStyles
    int updatedNodeStyleSprites = 0;
    foreach (var tree in AppManager.Instance.RecycleTrees)
    {
        if (tree == null || tree.nodeStyles == null)
        {
            Debug.LogWarning($"UIRecycleTree is null or has no nodeStyles: {tree?.name}");
            continue;
        }

        // Clone the NodeStyle objects
        NodeStyle[] clonedNodeStyles = new NodeStyle[tree.nodeStyles.Length];
        for (int i = 0; i < tree.nodeStyles.Length; i++)
        {
            if (tree.nodeStyles[i] != null)
            {
                clonedNodeStyles[i] = UnityEngine.Object.Instantiate(tree.nodeStyles[i]);
                //Debug.Log($"Cloned NodeStyle '{tree.nodeStyles[i].name}' for UIRecycleTree '{tree.name}'");
            }
            else
            {
                Debug.LogWarning($"Null NodeStyle at index {i} in UIRecycleTree '{tree.name}'");
                clonedNodeStyles[i] = null;
            }
        }

        // Update sprites in cloned NodeStyles
        foreach (var style in clonedNodeStyles)
        {
            if (style != null)
            {
                // Clone sprites to avoid modifying original assets
                updatedNodeStyleSprites += UpdateNodeStyleSprites(style, originalSprites, newSprites);
            }
        }

        // Use reflection to update the private nodeStylesArray field
        try
        {
            FieldInfo nodeStylesArrayField = typeof(UIRecycleTreeNamespace.UIRecycleTree).GetField("nodeStylesArray", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nodeStylesArrayField != null)
            {
                nodeStylesArrayField.SetValue(tree, clonedNodeStyles);
                //Debug.Log($"Updated nodeStylesArray for UIRecycleTree '{tree.name}' with {clonedNodeStyles.Length} cloned NodeStyles");
            }
            else
            {
                Debug.LogError($"Could not find nodeStylesArray field in UIRecycleTree via reflection");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to set nodeStylesArray via reflection for UIRecycleTree '{tree.name}': {e.Message}");
        }

        // Rebuild the tree
        try
        {
            tree.Rebuild();
            //Debug.Log($"Rebuilt UIRecycleTree: {tree.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to rebuild UIRecycleTree '{tree.name}': {e.Message}");
        }

    }
	UpdateNodeTextStyleColors();
	AppManager.Instance.UpdateTextColors();
    Debug.Log($"Successfully applied skin from {path} with {newSprites.Count} sprites to {updatedImages} UI images, {updatedButtons} buttons, and {updatedNodeStyleSprites} NodeStyle sprites.");
	FilePreset settings  = SettingsManager.application;
	settings.startupSkin = path;
	SettingsManager.application = settings;
	SettingsManager.SaveSettings();
}

public static void UpdateNodeTextStyleColors()
{
    if (AppManager.Instance == null)
    {
        Debug.LogError("AppManager.Instance is not initialized. Cannot update NodeTextStyle colors.");
        return;
    }

    int updatedStyles = 0;
    foreach (var tree in AppManager.Instance.RecycleTrees)
    {
        if (tree == null || tree.nodeStyles == null)
        {
            Debug.LogWarning($"UIRecycleTree is null or has no nodeStyles: {tree?.name}");
            continue;
        }

        // Clone NodeStyle objects to avoid modifying original assets
        NodeStyle[] clonedNodeStyles = new NodeStyle[tree.nodeStyles.Length];
        for (int i = 0; i < tree.nodeStyles.Length; i++)
        {
            if (tree.nodeStyles[i] != null)
            {
                clonedNodeStyles[i] = UnityEngine.Object.Instantiate(tree.nodeStyles[i]);
                //Debug.Log($"Cloned NodeStyle '{tree.nodeStyles[i].name}' for UIRecycleTree '{tree.name}'");
                
                // Update the NodeTextStyle color based on NodeStyle name
                if (clonedNodeStyles[i].textStyle != null)
                {
                    if (clonedNodeStyles[i].name.Contains("HierarchyNodes 2") || clonedNodeStyles[i].name.Contains("HistoryNodePrefab"))
                    {
                        clonedNodeStyles[i].textStyle.color = AppManager.Instance.color2;
                        //Debug.Log($"Updated NodeTextStyle color to {AppManager.Instance.color2} for NodeStyle '{clonedNodeStyles[i].name}' in UIRecycleTree '{tree.name}'");
                    }
                    else if (clonedNodeStyles[i].name.Contains("HierarchyNodes 3") || clonedNodeStyles[i].name.Contains("HistoryNodeTerrain"))
                    {
                        clonedNodeStyles[i].textStyle.color = AppManager.Instance.color3;
                        //Debug.Log($"Updated NodeTextStyle color to {AppManager.Instance.color3} for NodeStyle '{clonedNodeStyles[i].name}' in UIRecycleTree '{tree.name}'");
                    }
                    else
                    {
                        clonedNodeStyles[i].textStyle.color = AppManager.Instance.color1;
                        //Debug.Log($"Updated NodeTextStyle color to {AppManager.Instance.color1} for NodeStyle '{clonedNodeStyles[i].name}' in UIRecycleTree '{tree.name}'");
                    }
                    updatedStyles++;
                }
                else
                {
                    Debug.LogWarning($"NodeTextStyle is null in NodeStyle '{tree.nodeStyles[i].name}' for UIRecycleTree '{tree.name}'");
                }
            }
            else
            {
                Debug.LogWarning($"Null NodeStyle at index {i} in UIRecycleTree '{tree.name}'");
                clonedNodeStyles[i] = null;
            }
        }

        // Update the private nodeStylesArray field using reflection
        try
        {
            FieldInfo nodeStylesArrayField = typeof(UIRecycleTreeNamespace.UIRecycleTree).GetField("nodeStylesArray", BindingFlags.NonPublic | BindingFlags.Instance);
            if (nodeStylesArrayField != null)
            {
                nodeStylesArrayField.SetValue(tree, clonedNodeStyles);
                //Debug.Log($"Updated nodeStylesArray for UIRecycleTree '{tree.name}' with {clonedNodeStyles.Length} cloned NodeStyles");
            }
            else
            {
                Debug.LogError($"Could not find nodeStylesArray field in UIRecycleTree via reflection");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to set nodeStylesArray via reflection for UIRecycleTree '{tree.name}': {e.Message}");
        }

        // Rebuild the tree to apply changes
        try
        {
            tree.Rebuild();
            Debug.Log($"Rebuilt UIRecycleTree: {tree.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to rebuild UIRecycleTree '{tree.name}': {e.Message}");
        }
    }

    Debug.Log($"Successfully updated {updatedStyles} NodeTextStyle colors to {AppManager.Instance.color1}.");
}

private static int UpdateNodeStyleSprites(object obj, List<Sprite> originalSprites, List<Sprite> newSprites, HashSet<object> visited = null)
{
    if (obj == null) return 0;
    if (visited == null) visited = new HashSet<object>();
    if (!visited.Add(obj)) return 0; // Avoid infinite recursion

    int updatedCount = 0;
    System.Type type = obj.GetType();
    FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    //Debug.Log(fields.Length + " fields found within " + type.Name);

    foreach (var field in fields)
    {
        // Handle Icon fields
        if (field.FieldType == typeof(UIRecycleTreeNamespace.Icon))
        {
            object iconObj = field.GetValue(obj);
            if (iconObj != null)
            {
                UIRecycleTreeNamespace.Icon icon = (UIRecycleTreeNamespace.Icon)iconObj;
                if (icon.sprite != null)
                {
                    // Find the matching new sprite based on the original sprite's name
                    Sprite matchingSprite = newSprites.Find(s => s.name == icon.sprite.name);
                    if (matchingSprite != null)
                    {
                        // Directly assign the new sprite to the Icon's sprite field
                        icon.sprite = matchingSprite;
                        updatedCount++;
                    }
                    else
                    {
                        Debug.Log($"No matching new sprite found for {icon.sprite.name} in {type.Name}.{field.Name}");
                    }
                }
                else
                {
                    //Debug.Log($"Icon.sprite is null in {type.Name}.{field.Name}");
                }
            }
            else
            {
                //Debug.Log($"Icon is null in {type.Name}.{field.Name}");
            }
        }

        // Traverse all other fields for nested Icons
        object nestedObj = field.GetValue(obj);
        if (nestedObj != null && field.FieldType.IsClass && field.FieldType != typeof(string) && !field.FieldType.IsArray)
        {
            // Recursively search for Icons in nested objects
            updatedCount += UpdateNodeStyleSprites(nestedObj, originalSprites, newSprites, visited);
        }
    }

    return updatedCount;
}

    [ConsoleCommand("Tools/Extract Sprite Sheet Info")]
     public static void ExtractSpriteSheetInfo()
    {
        // Load the sprite sheet from Resources
        string spriteSheetPath = "textures/UI/UIsprites"; // Path relative to Resources
        Texture2D spriteSheet = Resources.Load<Texture2D>(spriteSheetPath);
        if (spriteSheet == null)
        {
            Debug.LogError("Sprite sheet not found at: Resources/" + spriteSheetPath);
            return;
        }

        // Load all sprites associated with the sprite sheet
        UnityEngine.Object[] assets = Resources.LoadAll(spriteSheetPath, typeof(Sprite));
        List<Sprite> sprites = new List<Sprite>();

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite)
            {
                sprites.Add((Sprite)asset);
            }
        }

        // Log sprite information
        Debug.Log($"Found {sprites.Count} sprites in the sprite sheet:");
        foreach (Sprite sprite in sprites)
        {
            Debug.Log($"Sprite: {sprite.name}");
            Debug.Log($"  Position: {sprite.rect.position}");
            Debug.Log($"  Size: {sprite.rect.size}");
            Debug.Log($"  Pivot: {sprite.pivot}");
            Debug.Log($"  Pixels Per Unit: {sprite.pixelsPerUnit}");
            Debug.Log("---");
        }

        // Optionally, store the info as a string (e.g., for UI display or saving)
        string spriteInfo = GetSpriteInfoAsString(sprites);
        Debug.Log($"Sprite Info:\n{spriteInfo}");

        // Example: Save to a file (if you have write permissions, e.g., in Application.persistentDataPath)
        string outputPath = System.IO.Path.Combine(Application.persistentDataPath, "SpriteSheetInfo.txt");
        System.IO.File.WriteAllText(outputPath, spriteInfo);
        Debug.Log($"Sprite information saved to: {outputPath}");
    }

    private static string GetSpriteInfoAsString(List<Sprite> sprites)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Sprite Sheet Information - {System.DateTime.Now}");
        sb.AppendLine($"Total Sprites: {sprites.Count}");
        sb.AppendLine("--------------------------------");

        foreach (Sprite sprite in sprites)
        {
            sb.AppendLine($"Sprite: {sprite.name}");
            sb.AppendLine($"  Position: {sprite.rect.position}");
            sb.AppendLine($"  Size: {sprite.rect.size}");
            sb.AppendLine($"  Pivot: {sprite.pivot}");
            sb.AppendLine($"  Pixels Per Unit: {sprite.pixelsPerUnit}");
            sb.AppendLine("---");
        }

        return sb.ToString();
    }

public static void ButtonClickMethod(){
	Debug.Log("button click log entry");
}

[ConsoleCommand("Sample code for programmatic window creation")]
public static TemplateWindow CreateSampleWindow()
{
    if (AppManager.Instance == null)
    {
        Debug.LogError("AppManager.Instance is not initialized. Cannot create sample window.");
        return null;
    }

    // Create the sample window
    // A Rect defines a rectangular area with (x, y, width, height).
    // - x: Horizontal position from the left edge of the screen (positive moves right).
    // - y: Vertical position from the top of the screen (negative moves down in Unity UI).
    // - width: The width of the rectangle in pixels.
    // - height: The height of the rectangle in pixels.
    TemplateWindow sampleWindow = AppManager.Instance.CreateWindow(
        titleText: "Sample Plugin Window",
        rect: new Rect(300, -150, 647, 400),
		Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "HarmonyMods", "sampleIcon.png")
    );

    if (sampleWindow != null)
    {

        Toggle sampleToggle = AppManager.Instance.CreateToggle(
            sampleWindow.transform,
            new Rect(300, -70, 100, 20), 
            "Toggle Your FACE off!"
        );
        sampleToggle.onValueChanged.AddListener((value) => Debug.Log($"Toggle value changed to: {value}"));

        Button sampleButton = AppManager.Instance.CreateButton(
            sampleWindow.transform,
            new Rect(20, -50, 100, 22), 
            "Buttons?",
			ButtonClickMethod
        );

        Button sampleBrightButton = AppManager.Instance.CreateBrightButton(
            sampleWindow.transform,
            new Rect(20, -100, 100, 22), 
            "RED Buttons?"
        );
        sampleBrightButton.onClick.AddListener(() => Debug.Log("Bright Button clicked!"));

        Text sampleLabel = AppManager.Instance.CreateLabelText(
            sampleWindow.transform,
            new Rect(250, -100, 300, 50),
            "Just TRY to label me"
        );

        Slider sampleSlider = AppManager.Instance.CreateSlider(
            sampleWindow.transform,
            new Rect(20, -150, 300, 25)
        );
        if (sampleSlider != null)
        {
            sampleSlider.minValue = 0f;
            sampleSlider.maxValue = 100f;
            sampleSlider.value = 50f;
            sampleSlider.onValueChanged.AddListener((value) => Debug.Log($"Slider value changed to: {value}"));
        }

        Dropdown sampleDropdown = AppManager.Instance.CreateDropdown(
            sampleWindow.transform,
            new Rect(20, -220, 620, 40)
        );
        sampleDropdown.options.Clear();
        sampleDropdown.options.Add(new Dropdown.OptionData("How DARE you"));
        sampleDropdown.options.Add(new Dropdown.OptionData("Monkey suits"));
        sampleDropdown.options.Add(new Dropdown.OptionData("Gingerbreads"));
		sampleDropdown.options.Add(new Dropdown.OptionData("Unreasonably long dropdown entry pushing the limits of description for a user interface"));
        sampleDropdown.value = 0;
        sampleDropdown.onValueChanged.AddListener((value) => Debug.Log($"Dropdown value changed to: {sampleDropdown.options[value].text}"));

        InputField sampleInput = AppManager.Instance.CreateInputField(
            sampleWindow.transform,
            new Rect(20, -290, 620, 40),
            "This could be your next hell project"
        );
        sampleInput.onValueChanged.AddListener((value) => Debug.Log($"Input field value changed to: {value}"));
    }
	SkinGameObject(sampleWindow.gameObject);
    return sampleWindow;
}
	
    public static void ClearModdingData()
    {
        moddingData.Clear();
    }

	public static void SkinGameObject(GameObject target)
	{
		AppManager.Instance.CollectImages(target);
		AppManager.Instance.CollectLabels(target);
		AppManager.Instance.CollectInputFields(target);
		AppManager.Instance.SetColors(target);
		LoadSkin(SettingsManager.application.startupSkin);
	}


    public static string[] GetKnownDataNames()
    {
        return KnownDataNames;
    }
	
}