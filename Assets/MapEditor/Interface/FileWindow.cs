using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using RustMapEditor.Variables;
using UIRecycleTreeNamespace;

public class FileWindow : MonoBehaviour
{
	FilePreset settings;

	public Slider newSizeSlider, newHeightSlider;
	public Dropdown splatDrop, biomeDrop, recentDrop, extensionsDrop;
	public UIRecycleTree tree;
	public InputField pathField, filenameField;
	public Button open, save, saveJson, savePrefab, saveMonument, savePNGs, export;
	public Text footer, displayPath;
	public string path, name, extension;

	private List<TerrainSplat.Enum> splatEnums = new List<TerrainSplat.Enum>();
	private List<TerrainBiome.Enum> biomeEnums = new List<TerrainBiome.Enum>();
	private Dictionary<string,int> recentFiles = new Dictionary<string,int>();
	
	

	public void Start()
	{
		settings = SettingsManager.application;
		extension = "map";
		name = "filename";

		PopulateLists();
		splatDrop.value = splatEnums.IndexOf(settings.newSplat);
		biomeDrop.value = biomeEnums.IndexOf(settings.newBiome);
		newSizeSlider.value = settings.newSize;
		newHeightSlider.value = settings.newHeight;
		

		splatDrop.onValueChanged.AddListener(delegate { StateChange(); });
		biomeDrop.onValueChanged.AddListener(delegate { StateChange(); });
		recentDrop.onValueChanged.AddListener(delegate { OnRecentChanged(); });
		extensionsDrop.onValueChanged.AddListener(delegate { OnExtensionChanged(); });

		newSizeSlider.onValueChanged.AddListener(delegate { StateChange(); });
		newHeightSlider.onValueChanged.AddListener(delegate { StateChange(); });
		
		tree.onNodeExpandStateChanged.AddListener(OnExpand);
		tree.onSelectionChanged.AddListener(OnSelect);	
		
		filenameField.onValueChanged.AddListener(FilenameChanged);
		
		open.onClick.AddListener(OpenFile);
		save.onClick.AddListener(SaveFile);
		LoadDriveList();
		OnRecentChanged();
	}
	
	public void OnEnable(){
		tree.FocusOn(tree.selectedNode);
	}
	
	public void OpenFile()
	{
		string fullPath = FilePath();
		
		// Only proceed if we have a valid path and name
		if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(name))
		{
			footer.text = "Invalid path or filename";
			return;
		}

		try
		{
			switch (extension.ToLower())
			{
				case "map":
					var world = new WorldSerialization();
					world.Load(fullPath);
					MapManager.Load(WorldConverter.WorldToTerrain(world), fullPath);
					break;
				case "prefab":
					MonumentManager.LoadREPrefab(fullPath);
					break;
				case "monument":
					MonumentManager.LoadMonument(fullPath);
					break;
				case "json":
					MapManager.LoadDumpJSON(fullPath);
					break;
				case "png":
					footer.text = $"Opening {extension} files is not supported";
					return;
				case "obj":
					ModManager.LoadObj(fullPath);
					return;
				default:
					Debug.LogError($"Unsupported extension: {extension}");
					footer.text = $"Unsupported extension: {extension}";
					return;
			}

			// Add to recent files and tree
			AddRecent(fullPath);
			List<string> pathList = new List<string> { fullPath };
			SettingsManager.AddPathsAsNodes(tree, pathList);
			
			footer.text = fullPath + " opened";
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to open file: {e.Message}");
			footer.text = $"Error opening file: {e.Message}";
		}
	}

	public void RefreshAssets(){
		HierarchyWindow.Instance?.LoadTree();
	}

	public void SaveFile()
	{
		string fullPath = FilePath();
		
		// Only proceed if we have a valid path and name
		if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(name))
		{
			footer.text = "Invalid path or filename";
			return;
		}

		try
		{
			switch (extension.ToLower())
			{
				case "map":
					MapManager.Save(fullPath);
					break;
				case "prefab":
					MapManager.SaveCustomPrefab(fullPath);
					RefreshAssets();
					break;
				case "monument":
					MapManager.SaveMonument(fullPath);
					RefreshAssets();
					break;
				case "json":
					MapManager.SaveJson(fullPath);
					break;
				case "png":
					SavePNGs(path);
					break;
				default:
					Debug.LogError($"Unsupported extension: {extension}");
					footer.text = $"Unsupported extension: {extension}";
					return;
			}

			// Add the full path to the tree
			List<string> pathList = new List<string> { fullPath };
			SettingsManager.AddPathsAsNodes(tree, pathList);
			
			footer.text = fullPath + " saved";
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to save file: {e.Message}");
			footer.text = $"Error saving file";
		}
	}

	public void SaveJson(string path){
		MapManager.SaveJson(path);
		footer.text = path + " saved";
	}	

public void SavePNGs(string directory)
{
    var texturesToSave = new (Texture texture, string name)[]
    {
        (TerrainManager.BiomeTexture, "Biome0"),
        (TerrainManager.Biome1Texture, "Biome1"),
        (TerrainManager.Land.terrainData.alphamapTextures[0], "Splat0"),
        (TerrainManager.Land.terrainData.alphamapTextures[1], "Splat1"),
        (TerrainManager.HeightTexture, "Height"),
        (TopologyData.TopologyTexture, "Topology"),
    };

    foreach (var (texture, name) in texturesToSave)
    {
        TerrainManager.SyncHeightTextures();
        Texture2D tex = null;
        try
        {
            if (texture is RenderTexture rt)
            {
                if (name == "Height")
                {
                    tex = RenderTextureToTexture2DR16(rt);
                }
                else
                {
                    tex = RenderTextureToTexture2D(rt);
                }
            }
            else if (texture is Texture2D t2d)
            {
                if (name == "Height")
                {
                    // Ensure HeightTexture is readable
                    if (!t2d.isReadable)
                    {
                        RenderTexture tempRT = RenderTexture.GetTemporary(t2d.width, t2d.height, 0, RenderTextureFormat.R16);
                        Graphics.Blit(t2d, tempRT);
                        tex = RenderTextureToTexture2DR16(tempRT);
                        RenderTexture.ReleaseTemporary(tempRT);
                    }
                    else
                    {
                        // Directly use the readable Texture2D
                        tex = new Texture2D(t2d.width, t2d.height, TextureFormat.R16, false);
                        tex.SetPixels(t2d.GetPixels());
                        tex.Apply();
                    }
                }
                else
                {
                    // Non-heightmap textures
                    RenderTexture tempRT = RenderTexture.GetTemporary(t2d.width, t2d.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(t2d, tempRT);
                    tex = RenderTextureToTexture2D(tempRT);
                    RenderTexture.ReleaseTemporary(tempRT);
                }
            }
            else
            {
                Debug.LogError($"Unsupported texture type for {name}: {texture.GetType()}");
                continue;
            }

            // Save as 16-bit PNG for heightmap
            byte[] bytes;
            if (name == "Height")
            {
                bytes = tex.EncodeToPNG(); 
            }
            else
            {
                bytes = tex.EncodeToPNG();
            }
            string filePath = Path.Combine(directory, $"{name}.png");
            File.WriteAllBytes(filePath, bytes);
            Debug.Log($"Saved {name}.png to {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to process {name}: {e.Message}");
        }
        finally
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }

    footer.text = directory + " saved";
}


private Texture2D RenderTextureToTexture2DR16(RenderTexture rt)
{
    RenderTexture.active = rt;
    Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.R16, false);
    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
    tex.Apply();
    RenderTexture.active = null;
    return tex;
}







	// Helper method to convert RenderTexture to Texture2D
	private Texture2D RenderTextureToTexture2D(RenderTexture renderTexture)
	{
		RenderTexture.active = renderTexture;
		Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
		texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
		texture.Apply();
		RenderTexture.active = null;
		return texture;
	}


	public void SavePrefab(string path){
		MapManager.SaveCustomPrefab(path);
		footer.text = path + " saved";
	}
	
	public void SaveMonument(string path){
		MapManager.SaveMonument(path);
		footer.text = path + " saved";
	}

	public void FilenameChanged(string change)	{		
		name = change;
		displayPath.text = FilePath();
		SetInteractable();
	}
	
	private void SetInteractable()	{
		// If input is empty, disable both buttons
		if (string.IsNullOrEmpty(name))		{
			save.interactable = false;
			open.interactable = false;
			return;
		}

		// Check directory existence for save button
		save.interactable = !string.IsNullOrEmpty(path) && Directory.Exists(path);

		// Check file existence and extension for open button
		if (extension.ToLower() is "png")		{
			open.interactable = false;
		}
		else		{
			open.interactable = File.Exists(FilePath());
		}
	}
	
	
	public string FilePath(){
			string fullPath = Path.Combine(path, name);
			fullPath = fullPath + "." + extension;
			return fullPath;
	}
	
	private void SetFilePath(string nodePath)
	{
		string directory = Path.GetDirectoryName(nodePath);
		
		
		if (string.IsNullOrEmpty(directory))
		{
			// Try root path
			directory = Path.GetPathRoot(nodePath) + "\\";
			if (string.IsNullOrEmpty(directory))
			{
				Debug.Log("Directory not found for path: " + nodePath);
				return;
			}
		}
		
		path = directory;
		
		// If there is an extension at the end of the path
		if (!string.IsNullOrEmpty(Path.GetExtension(nodePath)))
		{
			name = Path.GetFileNameWithoutExtension(nodePath);
		}
		else{
			if(!string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(nodePath))){
				path = Path.Combine(path, Path.GetFileNameWithoutExtension(nodePath) + "\\");
			}
		}
		// If we are just cruising through directories, name does not need changing
		// (no else needed as name retains its previous value)
		
		Debug.LogError(name + " is current file");
	}
	
	public void OnSelect(Node node)
	{
		if (node.isSelected)
		{
			Expand(node);
			SetFilePath(node.fullPath);
			
			// Check if the node is a file (has an extension) or a folder
			if (Path.HasExtension(node.fullPath))	{				
				string filename = Path.GetFileNameWithoutExtension(node.fullPath);
				
				if (!string.IsNullOrEmpty(filename))		{
					name = filename;
					filenameField.text = filename;
				}
			}
			
			displayPath.text = path + name + "." + extension;
			return;
		}
		
		node.CollapseAll();
	}
	
	public void OnExpand(Node node)
	{
		node.isSelected = true;
	}

	public void Expand(Node node)
	{
		string folderPath = node.fullPath;
		string fullPath = folderPath;
		if (!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
			fullPath += Path.DirectorySeparatorChar; 
		List<string> paths = SettingsManager.AddFilePaths(fullPath, extension);
		SettingsManager.AddPathsAsNodes(tree, paths); 
		node.isExpanded = true;
	}

	public void OnRecentChanged()
	{
		// Check if dropdown has valid options
		if (recentDrop == null || recentDrop.options == null || recentDrop.options.Count == 0)
		{
			Debug.LogWarning("Dropdown is unpopulated or null. Cannot process recent file selection.");
			return;
		}

		// Check if selected index is valid
		if (recentDrop.value < 0 || recentDrop.value >= recentDrop.options.Count)
		{
			Debug.LogWarning($"Invalid dropdown index: {recentDrop.value}. Options count: {recentDrop.options.Count}");
			return;
		}

		// Safely get the selected option
		string recentSelect = recentDrop.options[recentDrop.value].text;
		if (string.IsNullOrWhiteSpace(recentSelect))
		{
			Debug.LogWarning("Selected recent file path is empty or invalid.");
			return;
		}

		Debug.Log($"Selected recent file: '{recentSelect}'");
		SetFilePath(recentSelect);
		FocusPath(recentSelect);
	}
	
	public void FocusPath(string path)
	{
		Node file = FindNodeByPath(tree, path);
		tree.FocusOn(file);
		file.isSelected = true;
	}

	public Node FindNodeByPath(UIRecycleTree tree, string path)
	{
		string drive = Path.GetPathRoot(path);
		string[] parts = path.Substring(drive.Length).Split(Path.DirectorySeparatorChar);

		if (parts.Length == 0)
		{
			//Debug.LogError("Invalid path: " + path);
			return null;
		}
		
		Node nextNode = tree.rootNode;
		
		foreach (string folder in parts){
			
			Node[] searchNodes = nextNode.GetAllChildrenRecursive();
			
			foreach (Node node in searchNodes)		{
				if (node.name.Equals(folder))			{
					nextNode = node;
					Expand(node);
				}
			}
		}
		
		
		return nextNode;
	}


	public void LoadDriveList()
	{
		tree.Clear();
		DriveInfo[] drives = DriveInfo.GetDrives();
		List<string> driveRoots = new List<string>();

		foreach (DriveInfo drive in drives)
		{
			if (drive.IsReady)
			{
				string root = drive.Name;
				driveRoots.Add(root);
			}
		}
		SettingsManager.AddPathsAsNodes(tree, driveRoots);

		// Filter and add recent files
		List<string> filteredRecentFiles = FilterRecentFilesByExtension(settings.recentFiles);
		Debug.Log($"Found {filteredRecentFiles.Count} recent files with extension '{extension}'");
		SettingsManager.AddPathsAsNodes(tree, filteredRecentFiles);

	}

	// Helper method to filter recent files by extension without using .Where
	private List<string> FilterRecentFilesByExtension(IEnumerable<string> recentFiles)
	{
		List<string> filteredFiles = new List<string>();

		// Check for null or empty inputs
		if (recentFiles == null || string.IsNullOrEmpty(extension))
		{
			Debug.LogWarning("Recent files list or extension is null or empty.");
			return filteredFiles;
		}

		try
		{

			foreach (string file in recentFiles)
			{
				// Skip null or empty file paths
				if (string.IsNullOrEmpty(file))
				{
					continue;
				}

				// Skip files without an extension
				if (!Path.HasExtension(file))
				{
					continue;
				}

				// Get file extension, remove leading dot, and convert to lowercase
				string fileExtension = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
				Debug.Log(fileExtension);
				
				// Compare extensions case-insensitively
				if (string.Equals(fileExtension, extension, StringComparison.OrdinalIgnoreCase))
				{
					filteredFiles.Add(file);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error filtering recent files: {ex.Message}");
		}

		return filteredFiles;
	}

   public void AddRecent(string path)
    {
        if (settings.recentFiles == null)    {
            settings.recentFiles = new List<string>();
        }

        if (settings.recentFiles.Contains(path))      {
                settings.recentFiles.Remove(path);
            }
			
        settings.recentFiles.Insert(0, path);

        if (settings.recentFiles.Count > 12)      {
                settings.recentFiles.RemoveAt(settings.recentFiles.Count - 1);
            }

            SettingsManager.application = settings;
            SettingsManager.SaveSettings();
            PopulateLists();
      
    }
	
	public void PopulateLists()	{
		
		foreach (TerrainSplat.Enum splat in Enum.GetValues(typeof(TerrainSplat.Enum)))
		{
			splatEnums.Add(splat);
			splatDrop.options.Add(new Dropdown.OptionData(splat.ToString()));
		}

		foreach (TerrainBiome.Enum biome in Enum.GetValues(typeof(TerrainBiome.Enum)))
		{
			biomeEnums.Add(biome);
			biomeDrop.options.Add(new Dropdown.OptionData(biome.ToString()));
		}
		
		if (settings.recentFiles == null)
		{
			settings.recentFiles = new List<string>();
		}

       // Clear existing options in the dropdown
        recentDrop.options.Clear();

        // Add only recent files matching the current extension
        if(settings.recentFiles.Count > 0){
			
			foreach (string file in settings.recentFiles)			{
				Debug.Log(Path.GetExtension(file).ToLower() + " populating recent dropdown");
				
				if (Path.GetExtension(file).ToLower() == "."+extension.ToLower())				{
					recentDrop.options.Add(new Dropdown.OptionData(file));
				}
			}
			recentDrop.RefreshShownValue();
		}
		
	    extensionsDrop.options.Clear(); // Clear existing options to avoid duplicates
		List<string> extensions = new List<string> { "map", "prefab", "monument", "json", "png", "obj" };
		foreach (string ext in extensions)
		{
			extensionsDrop.options.Add(new Dropdown.OptionData(ext));
		}
		extensionsDrop.RefreshShownValue();
		
	}

    public void OnExtensionChanged()
    {
        extension = extensionsDrop.options[extensionsDrop.value].text;
        // refresh the tree with files matching the new extension
        LoadDriveList();
        // refresh recent files to show only those matching the extension
        PopulateLists();
		SetInteractable();
    }

	public void StateChange()
	{
		FilePreset application = SettingsManager.application;
		application.newSplat = splatEnums[splatDrop.value]; 
		application.newBiome = biomeEnums[biomeDrop.value];
		application.newSize = (int)newSizeSlider.value;    
		application.newHeight = newHeightSlider.value;		
		SettingsManager.application = application;
	}

	public void NewFile()
	{
		MapManager.CreateMap(SettingsManager.application.newSize, SettingsManager.application.newSplat, SettingsManager.application.newBiome, SettingsManager.application.newHeight * 1000f);
	}
}