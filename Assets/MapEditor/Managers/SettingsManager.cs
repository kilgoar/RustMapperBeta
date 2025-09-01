using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using RustMapEditor.Variables;
using static BreakerSerialization;
using UIRecycleTreeNamespace;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

public class Vector3ContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);
        
        if (property.DeclaringType == typeof(Vector3))
        {
            if (property.PropertyName == "normalized" || 
                property.PropertyName == "magnitude" || 
                property.PropertyName == "sqrMagnitude")
            {
                property.ShouldSerialize = instance => false;
            }
        }
        
        return property;
    }
}


public static class SettingsManager
{
	public static string SettingsPath;
	
    #region Init
	#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void Init()
    {
        SettingsPath = "EditorSettings.json";
        if (!File.Exists(SettingsPath))
        {
			Debug.Log("no EditorSettings.json found, creating empty from home directory");
			
            try
            {
                using (StreamWriter write = new StreamWriter(SettingsPath, false))
                {
                    // Serialize with Newtonsoft.Json instead of JsonUtility
                    string json = JsonConvert.SerializeObject(new EditorSettings(), Formatting.Indented);
                    write.Write(json);
                }
                Debug.Log($"Created new settings file at {SettingsPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating settings file at {SettingsPath}: {e.Message}");
            }
        }

        LoadSettings();
    }
    #endif
	#endregion
	
	
	public static string AppDataPath()
	{
		
		#if UNITY_EDITOR
			return Path.GetDirectoryName(Application.dataPath);
		#else
			return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RustMapper");
		#endif
	}
	
	public static void RuntimeInit()
    {
		try
		{
			
			SettingsPath = Path.Combine(AppDataPath(), "EditorSettings.json");
			if (!File.Exists(SettingsPath)){
					Debug.Log("no EditorSettings.json found in appdata, copying default configuration from home directory");				
					CopyDirectory("Presets", Path.Combine(AppDataPath(), "Presets"));
					CopyDirectory("Custom", Path.Combine(AppDataPath(), "Custom"));					
			}
			CopyEditorSettings(AppDataPath());
			
			

			
		}
		catch(Exception ex){			
			Debug.LogError($"Error initializing directories: {ex.Message}\nStackTrace: {ex.StackTrace}");  
		}
		
		//UpdateBreakerFragmentsIfNewer();
		EnsureDefaultBrushes();
		LoadFragmentLookup();
		LoadSettings();
		ManageSkins();
    }
	
	
	
	
	private static void ManageSkins()
	{
		try
		{
			// Define paths
			string defaultSkinsPath = Path.Combine("Custom", "Skins"); // Source: next to executable
			string appDataSkinsPath = Path.Combine(AppDataPath(), "Custom", "Skins"); // Target: AppData/RustMapper/Custom/Skins

			// Create Skins directory if it doesn't exist
			if (!Directory.Exists(appDataSkinsPath))
			{
				Directory.CreateDirectory(appDataSkinsPath);
				Debug.Log($"Created Skins directory at {appDataSkinsPath}");
			}

			// Define the skin files to manage
			string[] skinFiles = { "classic.png", "cabinet.png", "darkmode.png" };

			// Paths for the source and destination darkmode.png
			string sourceDarkmodePath = Path.Combine(defaultSkinsPath, "darkmode.png");
			string appDataDarkmodePath = Path.Combine(appDataSkinsPath, "darkmode.png");

			// Check if darkmode.png exists in source and if it’s newer than the AppData version
			bool updateSkins = false;
			if (File.Exists(sourceDarkmodePath))
			{
				if (!File.Exists(appDataDarkmodePath))
				{
					updateSkins = true; // No darkmode.png in AppData, trigger update
					Debug.Log("No darkmode.png found in AppData, will copy all default skins");
				}
				else
				{
					try
					{
						// Check if both files exist
						if (!File.Exists(sourceDarkmodePath))
						{
							Debug.LogWarning($"Source file not found: {sourceDarkmodePath}. Cannot compare modification times.");
							return;
						}

						// Compare file modification times
						DateTime sourceDarkmodeTime = File.GetLastWriteTimeUtc(sourceDarkmodePath);
						DateTime appDataDarkmodeTime = File.GetLastWriteTimeUtc(appDataDarkmodePath);

						// Compare file creation times (optional)
						DateTime sourceCreationTime = File.GetCreationTimeUtc(sourceDarkmodePath);
						DateTime appDataCreationTime = File.GetCreationTimeUtc(appDataDarkmodePath);

						if (sourceDarkmodeTime > appDataDarkmodeTime || sourceCreationTime > appDataCreationTime)
						{
							updateSkins = true;
							Debug.Log($"Newer darkmode.png detected in source directory (modification: {sourceDarkmodeTime:yyyy-MM-dd HH:mm:ss} UTC, creation: {sourceCreationTime:yyyy-MM-dd HH:mm:ss} UTC) " +
									  $"compared to app data (modification: {appDataDarkmodeTime:yyyy-MM-dd HH:mm:ss} UTC, creation: {appDataCreationTime:yyyy-MM-dd HH:mm:ss} UTC). Updating all skins.");
						}
						else
						{
							Debug.Log($"No update needed. Source file (modification: {sourceDarkmodeTime:yyyy-MM-dd HH:mm:ss} UTC, creation: {sourceCreationTime:yyyy-MM-dd HH:mm:ss} UTC) " +
									  $"is not newer than app data file (modification: {appDataDarkmodeTime:yyyy-MM-dd HH:mm:ss} UTC, creation: {appDataCreationTime:yyyy-MM-dd HH:mm:ss} UTC).");
						}
					}
					catch (Exception ex)
					{
						Debug.LogError($"Error comparing file times for {sourceDarkmodePath} and {appDataDarkmodePath}: {ex.Message}");
					}
				}
			}
			else
			{
				Debug.LogWarning($"Source darkmode.png not found at {sourceDarkmodePath}, skipping skin update");
			}

			// Update all skins if necessary
			if (updateSkins)
			{
				foreach (string skin in skinFiles)
				{
					string sourceSkinPath = Path.Combine(defaultSkinsPath, skin);
					string destSkinPath = Path.Combine(appDataSkinsPath, skin);

					if (File.Exists(sourceSkinPath))
					{
						File.Copy(sourceSkinPath, destSkinPath, true); // Overwrite if exists
						Debug.Log($"Updated {skin} in {appDataSkinsPath}");
					}
					else
					{
						Debug.LogWarning($"Source skin {skin} not found at {sourceSkinPath}, skipped");
					}
				}
			}

			// Load the default skin (e.g., darkmode.png) after updating
			if (File.Exists(application.startupSkin))
			{
				ModManager.LoadSkin(application.startupSkin);
				Debug.Log($"Loaded skin: {application.startupSkin}");
			}
			else
			{
				Debug.LogWarning($"Failed to load startupSkin");
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error managing skins: {ex.Message}\nStackTrace: {ex.StackTrace}");
		}
	}
	
	
	private static void EnsureDefaultBrushes()
    {
        string defaultBrushesPath = Path.Combine(Application.dataPath, "..", "Custom", "Brushes"); // Source: next to executable
        string appDataBrushesPath = Path.Combine(AppDataPath(), "Custom", "Brushes"); // Target: AppData/RustMapper/Custom/Brushes

        if (!Directory.Exists(appDataBrushesPath))
        {
            if (Directory.Exists(defaultBrushesPath))
            {
                CopyDirectory(defaultBrushesPath, appDataBrushesPath);
                Debug.Log($"Populated {appDataBrushesPath} with default brushes from {defaultBrushesPath}");
            }
            else
            {
                Debug.LogError($"Default brushes directory not found at {defaultBrushesPath}. Creating empty Brushes folder.");
                Directory.CreateDirectory(appDataBrushesPath);
            }
        }
    }
	
	public static List<string> GetScriptFiles()
	{
		string scriptsPath = Path.Combine(AppDataPath(), "Presets", "Scripts");
		List<string> scriptFiles = new List<string>();

		try
		{
			if (!Directory.Exists(scriptsPath))
			{
				Debug.LogWarning($"Scripts directory not found at: {scriptsPath}");
				return scriptFiles;
			}

			foreach (string file in Directory.EnumerateFiles(scriptsPath, "*.rmml", SearchOption.TopDirectoryOnly))
			{
				scriptFiles.Add(Path.GetFileName(file)); // Just the filename, not the full path
			}
		}
		catch (UnauthorizedAccessException ex)
		{
			Debug.LogWarning($"Access denied to scripts directory: {ex.Message}");
		}
		catch (IOException ex)
		{
			Debug.LogWarning($"IO error accessing scripts directory: {ex.Message}");
		}

		return scriptFiles;
	}
	
	public static List<string> GetScriptCommands(string scriptName)
	{
		List<string> commands = new List<string>();
		string scriptPath = Path.Combine(AppDataPath(), "Presets", "Scripts", scriptName);

		try
		{
			if (!File.Exists(scriptPath))
			{
				Debug.LogWarning($"Script file not found at: {scriptPath}");
				return commands;
			}

			// Read all lines from the file
			string[] lines = File.ReadAllLines(scriptPath);
			foreach (string line in lines)
			{
				string trimmedLine = line.Trim();
				// Skip empty lines or comments (assuming '#' or '//' as comment starters)
				if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("#") && !trimmedLine.StartsWith("//"))
				{
					commands.Add(trimmedLine);
				}
			}
		}
		catch (UnauthorizedAccessException ex)
		{
			Debug.LogWarning($"Access denied to script file {scriptName}: {ex.Message}");
		}
		catch (IOException ex)
		{
			Debug.LogWarning($"IO error reading script file {scriptName}: {ex.Message}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"Unexpected error reading script file {scriptName}: {ex.Message}");
		}

		return commands;
	}
	
	private static void UpdateBreakerFragmentsIfNewer()
	{
		string defaultFragmentsPath = "Presets/breakerFragments.json";
		string targetFragmentsPath = Path.Combine(AppDataPath(), "Presets", "breakerFragments.json");
		string autosavePath = Path.Combine(AppDataPath(), "Presets", "autosaveFragments.json");

		if (File.Exists(defaultFragmentsPath))
		{
			if (File.Exists(targetFragmentsPath))
			{
				// Save existing file as autosave before overwriting
				File.Copy(targetFragmentsPath, autosavePath, true);
				Debug.Log("Saved current breakerFragments as autosaveFragments.json.");
			}

			FileInfo defaultFileInfo = new FileInfo(defaultFragmentsPath);
			FileInfo targetFileInfo = new FileInfo(targetFragmentsPath);

			if (!File.Exists(targetFragmentsPath) || defaultFileInfo.LastWriteTimeUtc > targetFileInfo.LastWriteTimeUtc)
			{
				File.Copy(defaultFragmentsPath, targetFragmentsPath, true);
				Debug.Log("Updated breakerFragments.json with the default version.");
			}
		}
		else
		{
			Debug.LogWarning("Default breakerFragments.json not found.");
		}
	}
	

	
	public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destFile, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            string directoryName = Path.GetFileName(directory);
            CopyDirectory(directory, Path.Combine(destinationDir, directoryName));
        }
    }

	public static void CopyFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        try
        {
            // Check if the source file exists
            if (File.Exists(sourcePath))
            {
                // Copy the file to the destination
                File.Copy(sourcePath, destinationPath, overwrite);
                
                // Log success
                Debug.Log($"File copied from: {sourcePath} to: {destinationPath}");
            }
            else
            {
                // Log warning if source file does not exist
                Debug.LogWarning($"Source file not found at: {sourcePath}");
            }
        }
        catch (IOException e)
        {
            // Handle IO exceptions (like disk full, file in use, etc.)
            Debug.LogError($"Error copying file: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            // Handle permission issues
            Debug.LogError($"Permission denied when copying file: {e.Message}");
        }
    }

	public static void CopyEditorSettings(string destinationDirectory)
	{
		string[] filesToCopy = { "EditorSettings.json", "blacklist.json", "VolumesList.txt" };
		
		foreach (string fileName in filesToCopy)
		{
			string sourceFile = fileName;
			string destFile = Path.Combine(destinationDirectory, fileName);

			if (File.Exists(sourceFile) && !File.Exists(destFile))
			{
				File.Copy(sourceFile, destFile, true);
				Debug.Log($"File not found. Copied default {fileName} to active directory: {destFile}");
			}
			else
			{
				Debug.Log($"{destFile} exists or default has been removed");
			}
		}
	}

	
	public static List<string> AddFilePaths(string path, string extension)
	{
		List<string> pathsList = new List<string>();
		string absolutePath = Path.GetFullPath(path); // Resolve fully

		if (string.IsNullOrWhiteSpace(absolutePath) || !Directory.Exists(absolutePath))
		{
			Debug.LogWarning($"Invalid path: {absolutePath}");
			return pathsList;
		}

		try
		{
			foreach (string directory in Directory.EnumerateDirectories(absolutePath, "*", SearchOption.TopDirectoryOnly))
			{
				pathsList.Add(Path.GetFullPath(directory) + Path.DirectorySeparatorChar);
			}
			foreach (string file in Directory.EnumerateFiles(absolutePath, "*." + extension, SearchOption.TopDirectoryOnly))
			{
				pathsList.Add(Path.GetFullPath(file));
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"Error at {absolutePath}: {ex.Message}");
		}
		return pathsList;
	}

	public static List<string> GetBrushPaths()
    {
        string brushPath = Path.Combine(AppDataPath(), "Custom/Brushes");
        if (!Directory.Exists(brushPath))
        {
            Debug.LogWarning($"Brush directory not found at: {brushPath}");
            return new List<string>();
        }

        
		List<string> paths = Directory.GetFiles(brushPath, "*.png")
            .Concat(Directory.GetFiles(brushPath, "*.jpg"))
            .ToList();
			
		return paths;
    }

	public static List<string> GetDataPaths(string path, string root, string extension = ".prefab")    
	{
		List<string> pathsList = new List<string>();

		string[] directories = Directory.GetDirectories(path);
		string[] files = Directory.GetFiles(path);

		int index = path.IndexOf(root, StringComparison.Ordinal);
		
		if (index != -1)		{
			pathsList.Add("~" + path.Substring(index));
		}

		foreach (string directory in directories)   		{
			pathsList.AddRange(GetDataPaths(directory, root));
		}

		foreach (string file in files)    		{
			int fileIndex = file.IndexOf(root, StringComparison.Ordinal);
			
			if (fileIndex != -1)        			{
				pathsList.Add("~" + file.Substring(fileIndex));
			}
		}

		return pathsList;
	}

	
	public static void AddPathsAsNodes(UIRecycleTree tree, List<string> paths)
	{
		
		Dictionary<string, Node> nodeMap = new Dictionary<string, Node>();

		foreach (Node existingNode in tree.rootNode.nodes)
		{
			PopulateNodeMap(existingNode, nodeMap, string.Empty);
		}

		foreach (string path in paths)
		{

			string normalizedPath = path.Replace("\\", "/", StringComparison.Ordinal);
			string[] parts = normalizedPath.Split('/');
			Node currentNode = null;

			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i];
				string fullPath = string.Join("/", parts, 0, i + 1);
				

				if (!nodeMap.TryGetValue(fullPath, out currentNode))
				{
					currentNode = new Node(part);
					nodeMap[fullPath] = currentNode;

					if (i == 0)					{						
						tree.rootNode.nodes.AddWithoutNotify(currentNode);
					}
					else					{
						string parentPath = string.Join("/", parts, 0, i);
						if (nodeMap.TryGetValue(parentPath, out Node parentNode))
						{
							parentNode.nodes.AddWithoutNotify(currentNode);
							currentNode.parentNode = parentNode;
						}
					}

					currentNode.tree = tree;
				}
			}
		}
		tree.Rebuild();

	}

	private static void PopulateNodeMap(Node node, Dictionary<string, Node> nodeMap, string parentPath)
	{
		string fullPath = string.IsNullOrEmpty(parentPath) ? node.name : $"{parentPath}/{node.name}";
		nodeMap[fullPath] = node;

		foreach (Node child in node.nodes)		{
			PopulateNodeMap(child, nodeMap, fullPath);
		}
	}
	
	public static void UpdateFavorite(Node node){
		if (node.isChecked){
			AddFavorite(node);
			return;
		}
		RemoveFavorite(node);
	}
	
	public static void AddFavorite(Node node){
			string fullPath = node.fullPath;
			if(node.data!=null){
				fullPath = (string)node.data;
			}

			faves.favoriteCustoms.Add(fullPath);

			
		SaveSettings();
	}
	
	public static void RemoveFavorite(Node node)
	{
		string fullPath = node.fullPath;
		if(node.data!=null){
			fullPath =  (string)node.data;
		}

		faves.favoriteCustoms.Remove(fullPath);


		SaveSettings();
	}
	
	public static void CheckFavorites(UIRecycleTree tree)
	{
		CheckNode(tree.rootNode);
		tree.Rebuild();
	}

	public static int GetNodeStyleIndex(Node node, string fullPath)
	{
		if (node.hasChildren)
		{
			return 1; // Folders get styleIndex 1
		}
		
		string path = fullPath + ".prefab";

		// Check blacklist status for leaf nodes
		if (PrefabManager.ItemBlacklist.TryGetValue(path, out ItemSettings settings))
		{
			//Debug.Log("alternate node style found for" + path);
			if (settings.blacklisted)
			{
				return 3; // Blacklisted leaf nodes
			}
			if (settings.hidden)
			{
				return 2; // Hidden leaf nodes
			}
		}

		return 0; // Default for non-blacklisted, non-hidden leaf nodes
	}

	private static void CheckNode(Node node)
	{
		string fullPath = node.fullPath;
		if (node.data != null)
		{
			fullPath = (string)node.data;
		}
		


		if(node.fullPath!= "~Favorites"){


			bool isFavorite = faves.favoriteCustoms.Contains(fullPath);
			node.SetCheckedWithoutNotify(isFavorite);
			node.styleIndex = GetNodeStyleIndex(node, fullPath);

			// Handle removal of favorites only if it's under the Favorites directory
			if (node.fullPath.StartsWith("~Favorites/", StringComparison.Ordinal) && !isFavorite)
			{
				Node faveRoot = node.parentNode;

				if (faveRoot != null)
				{
					// Remove node from its parent before processing children to avoid concurrent modification
					faveRoot.nodes.Remove(node);
					return; // Exit early since this node was removed
				}
			}
		
		}

		// Create a copy of the current node's children to avoid issues with collection modification
		var children = new List<Node>(node.nodes);
		foreach (Node child in children)
		{
			CheckNode(child);
		}


		// Ensure 'Favorites' root node is always checked and has style index 1, and is not processed with the other nodes
		if (node.fullPath == "~Favorites")	{
			node.SetCheckedWithoutNotify(true);
			node.styleIndex = 1;
		}
	}
	
	public static void ConvertPathsToNodes(UIRecycleTree tree, List<string> paths, string extension, string searchQuery = "")
	{
		
		tree.Clear();
		Dictionary<string, Node> nodeMap = new Dictionary<string, Node>();

		// Create a root node for "~Favorites" explicitly
		Node favoritesRootNode = new Node("~Favorites");
		tree.rootNode.nodes.AddWithoutNotify(favoritesRootNode);
		favoritesRootNode.tree = tree;
							
		foreach (string path in paths)
		{
			// Check if the path matches the extension or starts with "~Favorites/"
			if (path.EndsWith(extension, StringComparison.Ordinal) || 
				extension.Equals("override", StringComparison.Ordinal) || 
				path.StartsWith("~Favorites/", StringComparison.Ordinal))
			{
				string searchPath = path.Replace(extension, "", StringComparison.Ordinal)
										.Replace("\\", "/", StringComparison.Ordinal);

				// Strip the prefix "~Geology/" if present
				if (searchPath.StartsWith("~Geology/", StringComparison.Ordinal))
				{
					searchPath = searchPath.Substring("~Geology/".Length);
				}

				// Proceed if it matches the search query or if the query is empty
				if (string.IsNullOrEmpty(searchQuery) || searchPath.Contains(searchQuery, StringComparison.Ordinal))
				{
					bool isFavoritePath = path.StartsWith("~Favorites/", StringComparison.Ordinal);

					if (isFavoritePath)
					{


						// Extract the filename (last part of the path)
						string filename = System.IO.Path.GetFileName(path);

						// Create a node for the filename
						Node favoriteNode = new Node(filename);

						// Attach the actual path (without "~Favorites/") to node.data
						string actualPath = path.Substring("~Favorites/".Length);
						/*
						if (!actualPath.EndsWith(extension, StringComparison.Ordinal))
						{
							actualPath += extension; // Add extension if not already present
						}
						*/
						favoriteNode.data = actualPath;
						
						// Add the node directly under the "~Favorites" root
						favoritesRootNode.nodes.AddWithoutNotify(favoriteNode);
						favoriteNode.parentNode = favoritesRootNode;
						favoriteNode.tree = tree;
					}
					else
					{
						// Handle non-"~Favorites/" paths (e.g., "~Geology/")
						string[] parts = searchPath.Split('/');
						Node currentNode = null;

						for (int i = 0; i < parts.Length; i++)
						{
							string part = parts[i];
							string fullPath = string.Join("/", parts, 0, i + 1);

							if (!nodeMap.TryGetValue(fullPath, out currentNode))
							{
								currentNode = new Node(part);
								nodeMap[fullPath] = currentNode;

								if (i == 0)
								{
									// Add top-level nodes directly to the tree root
									tree.rootNode.nodes.AddWithoutNotify(currentNode);
								}
								else
								{
									string parentPath = string.Join("/", parts, 0, i);
									if (nodeMap.TryGetValue(parentPath, out Node parentNode))
									{
										parentNode.nodes.AddWithoutNotify(currentNode);
										currentNode.parentNode = parentNode;
									}
								}

								currentNode.tree = tree;
							}
						}
					}
				}
			}
		}
		tree.Rebuild();
	}
	
	public static void ConvertPathsToNodes(UIRecycleTree tree, List<string> paths, string extension1, string extension2, string searchQuery = "", bool showAll = true)
	{
		tree.Clear();
		Dictionary<string, Node> nodeMap = new Dictionary<string, Node>();

		// Create a root node for "~Favorites" explicitly
		Node favoritesRootNode = new Node("~Favorites");
		tree.rootNode.nodes.AddWithoutNotify(favoritesRootNode);
		favoritesRootNode.tree = tree;

		foreach (string path in paths)
		{
			// Check if the path matches either extension or starts with "~Favorites/"
			if (path.EndsWith(extension1, StringComparison.Ordinal) || 
				path.EndsWith(extension2, StringComparison.Ordinal) ||
				extension1.Equals("override", StringComparison.Ordinal) || 
				path.StartsWith("~Favorites/", StringComparison.Ordinal))
			{
				string searchPath = path;
				// Remove first extension only
				if (path.EndsWith(extension1, StringComparison.Ordinal))
					searchPath = path.Replace(extension1, "", StringComparison.Ordinal);

				searchPath = searchPath.Replace("\\", "/", StringComparison.Ordinal);

				// Check blacklist status
				bool isBlacklisted = false;
				bool isHidden = false;
				if (PrefabManager.ItemBlacklist.TryGetValue(path, out ItemSettings settings))
				{
					isBlacklisted = settings.blacklisted;
					isHidden = settings.hidden;
				}

				// Skip blacklisted or hidden paths if showAll is false
				if (!showAll && (isBlacklisted || isHidden))
				{
					continue;
				}

				// Strip the prefix "~Geology/" if present
				if (searchPath.StartsWith("~Geology/", StringComparison.Ordinal))
				{
					searchPath = searchPath.Substring("~Geology/".Length);
				}

				// Proceed if it matches the search query or if the query is empty
				if (string.IsNullOrEmpty(searchQuery) || searchPath.Contains(searchQuery, StringComparison.Ordinal))
				{
					bool isFavoritePath = path.StartsWith("~Favorites/", StringComparison.Ordinal);

					if (isFavoritePath)
					{
						// Extract the filename (last part of the path)
						string filename = System.IO.Path.GetFileName(path);

						// Create a node for the filename
						Node favoriteNode = new Node(filename);

						// Attach the actual path (without "~Favorites/") to node.data
						string actualPath = path.Substring("~Favorites/".Length);
						favoriteNode.data = actualPath;

						// Add the node directly under the "~Favorites" root
						favoritesRootNode.nodes.AddWithoutNotify(favoriteNode);
						favoriteNode.parentNode = favoritesRootNode;
						favoriteNode.tree = tree;
					}
					else
					{
						// Handle non-"~Favorites/" paths (e.g., "~Geology/")
						string[] parts = searchPath.Split('/');
						Node currentNode = null;

						for (int i = 0; i < parts.Length; i++)
						{
							string part = parts[i];
							string fullPath = string.Join("/", parts, 0, i + 1);

							if (!nodeMap.TryGetValue(fullPath, out currentNode))
							{
								currentNode = new Node(part);
								nodeMap[fullPath] = currentNode;

								if (i == 0)
								{
									// Add top-level nodes directly to the tree root
									tree.rootNode.nodes.AddWithoutNotify(currentNode);
								}
								else
								{
									string parentPath = string.Join("/", parts, 0, i);
									if (nodeMap.TryGetValue(parentPath, out Node parentNode))
									{
										parentNode.nodes.AddWithoutNotify(currentNode);
										currentNode.parentNode = parentNode;
									}
								}

								currentNode.tree = tree;

							}
						}
					}
				}
			}
		}
		tree.Rebuild();
	}
	
	
    public const string BundlePathExt = @"\Bundles\Bundles";
	
	public static bool style { get; set; }
    public static string RustDirectory { get; set; }
    public static float PrefabRenderDistance { get; set; }
    public static float PathRenderDistance { get; set; }
    public static float WaterTransparency { get; set; }
    public static bool LoadBundleOnLaunch { get; set; }
    public static bool TerrainTextureSet { get; set; }
	
	public static Favorites faves { get; set; }
	public static FilePreset application { get; set; }
	public static CrazingPreset crazing { get; set; }
	public static PerlinSplatPreset perlinSplat { get; set; }
	public static RipplePreset ripple { get; set; }
	public static OceanPreset ocean { get; set; }
	public static TerracingPreset terracing { get; set; }
	public static PerlinPreset perlin { get; set; }
	public static GeologyPreset geology { get; set; }
	public static ReplacerPreset replacer { get; set; }
	public static string[] breakerPresets { get; set; }
	public static string[] geologyPresets { get; set; }
	public static string[] geologyPresetLists { get; set; }
    public static string[] PrefabPaths { get; private set; }
	public static List<string> macro { get; set; } = new List<string>();
	public static bool macroSources {get; set; }
 	public static RustCityPreset city {get; set; }
	public static BreakerPreset breaker {get; set;}
	public static FragmentLookup fragmentIDs {get; set;}
	public static BreakerSerialization breakerSerializer = new BreakerSerialization();
    public static WindowState[] windowStates { get; set; }
    public static MenuState menuState { get; set; }
	public static List<Bind> binds{ get; set; }
	
    /// <summary>Saves the current EditorSettings to a JSON file.</summary>
	public static void SaveSettings()
	{
		try
		{
			BindManager.GetBinds();
			
			string directory = Path.GetDirectoryName(SettingsPath);
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
				Debug.Log($"Created directory: {directory}");
			}

			// Check if file is writable
			if (File.Exists(SettingsPath))
			{
				File.SetAttributes(SettingsPath, FileAttributes.Normal); // Remove read-only
			}

			using (StreamWriter write = new StreamWriter(SettingsPath, false))
			{
				EditorSettings editorSettings = new EditorSettings
				(
					RustDirectory, PrefabRenderDistance, PathRenderDistance, WaterTransparency,
					LoadBundleOnLaunch, TerrainTextureSet, style, crazing, perlinSplat, ripple,
					ocean, terracing, perlin, geology, replacer, city, breaker, macroSources,
					application, faves, windowStates, menuState, binds
				);
				JsonSerializerSettings settings = new JsonSerializerSettings
				{
					ContractResolver = new Vector3ContractResolver(),
					Formatting = Formatting.Indented
				};
				string json = JsonConvert.SerializeObject(editorSettings, settings);
				write.Write(json);
				//Debug.Log($"Saved settings to: {SettingsPath}");
			}
		}
		catch (UnauthorizedAccessException ex)
		{
			Debug.LogError($"Access denied to {SettingsPath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
		}
		catch (IOException ex)
		{
			Debug.LogError($"IO error for {SettingsPath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"General error saving settings to {SettingsPath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
		}
	}

	public static Dictionary<string,uint> ListToDict(List<FragmentPair> fragmentPairs)
		{
			Dictionary<string,uint> namelist = new Dictionary<string,uint>();
			foreach(FragmentPair pair in fragmentPairs)
			{
				namelist.Add(pair.fragment, pair.id);
			}
			return namelist;
		}
		
	public static List<FragmentPair> DictToList(Dictionary<string,uint> fragmentNamelist)
		{
			List<FragmentPair> namePairs = new List<FragmentPair>();
			FragmentPair fragPair = new FragmentPair();
			foreach (KeyValuePair<string,uint> pair in fragmentNamelist)
			{
				fragPair.fragment = pair.Key;
				fragPair.id = pair.Value;
				namePairs.Add(fragPair);
			}
			return namePairs;
		}
	
	public static void SaveFragmentLookup()
	{
		using (StreamWriter write = new StreamWriter(Path.Combine(AppDataPath(), "Presets","breakerFragments.json"), false))
		{
			string json = JsonConvert.SerializeObject(fragmentIDs, Formatting.Indented);
			write.Write(json);
			fragmentIDs.Deserialize();
		}
	}

	public static void LoadFragmentLookup()
	{
		try
		{
			fragmentIDs = new FragmentLookup();
			string filePath = Path.Combine(AppDataPath(), "Presets", "breakerFragments.json");

			if (!File.Exists(filePath))
			{
				Debug.LogError($"Fragment lookup file not found at: {filePath}");
				return;
			}

			using (StreamReader reader = new StreamReader(filePath))
			{
				string json = reader.ReadToEnd();
				fragmentIDs = JsonConvert.DeserializeObject<FragmentLookup>(json);
				if (fragmentIDs == null)
				{
					Debug.LogError("Failed to deserialize breakerFragments.json");
					return;
				}
				fragmentIDs.Deserialize();
			}
		}
		catch (FileNotFoundException ex)
		{
			Debug.LogError($"Fragment lookup file not found: {ex.Message}");
		}
		catch (JsonException ex)
		{
			Debug.LogError($"JSON deserialization error in breakerFragments.json: {ex.Message}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Unexpected error in LoadFragmentLookup: {ex.Message}\n{ex.StackTrace}");
		}
	}

	
	public static void SaveBreakerPreset(string filename)
    {
		breakerSerializer.breaker = breaker;
		breakerSerializer.Save(Path.Combine(AppDataPath() ,"Presets","Breaker",$"{filename}.breaker"));

    }
	
	public static void LoadBreakerPreset(string filename)
	{
		breaker = breakerSerializer.Load(Path.Combine(AppDataPath() +  $"Presets/Breaker/{filename}.breaker"));

	}
	
	
	public static void SaveGeologyPreset()
	{
		using (StreamWriter write = new StreamWriter(Path.Combine(AppDataPath() , "Presets","Geology", $"{geology.title}.json"), false))
		{
			string json = JsonConvert.SerializeObject(geology, Formatting.Indented);
			write.Write(json);
		}
	}
	
	public static void DeleteGeologyPreset()
	{
		string path = Path.Combine(AppDataPath() , "Presets","Geology", $"{geology.title}.json");
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	
	public static void SaveReplacerPreset()
	{
		using (StreamWriter write = new StreamWriter(Path.Combine(AppDataPath() , "Presets","Geology", $"{geology.title}.json"), false))
		{
			string json = JsonConvert.SerializeObject(replacer, Formatting.Indented);
			write.Write(json);
		}
	}
	
	
	
	public static void LoadGeologyPreset(string filename)
	{
		using (StreamReader reader = new StreamReader(Path.Combine(AppDataPath() , "Presets","Geology", $"{geology.title}.json")))
		{
			geology = JsonConvert.DeserializeObject<GeologyPreset>(reader.ReadToEnd());
		}
	}
	
	public static GeologyPreset GetGeologyPreset(string filename)
	{
		if (File.Exists(filename))
		{
			using (StreamReader reader = new StreamReader(filename))
			{
				return JsonConvert.DeserializeObject<GeologyPreset>(reader.ReadToEnd());
			}
		}
		else
			return new GeologyPreset("file not found");
	}

	
	public static void LoadReplacerPreset(string filename)
	{
		using (StreamReader reader = new StreamReader(AppDataPath() + $"Presets/Replacer/{filename}.json"))
		{
			replacer = JsonConvert.DeserializeObject<ReplacerPreset>(reader.ReadToEnd());
		}
	}
	
	public static void LoadGeologyMacro(string filename)
	{
		macro = new List<string>();
		string macroPath = Path.Combine(AppDataPath(), "Presets","Geology","Macros",$"{filename}.macro");
		using (StreamReader reader = new StreamReader(macroPath))
		{
			string jsonContent = reader.ReadToEnd();
			GeologyMacroWrapper wrapper = JsonConvert.DeserializeObject<GeologyMacroWrapper>(jsonContent);
			if (wrapper != null && wrapper.macroList != null)
			{
				macro = wrapper.macroList;
			}
		}
	}
	
	public static void SaveGeologyMacro(string macroTitle)
	{
		GeologyMacroWrapper wrapper = new GeologyMacroWrapper { macroList = macro };
		string jsonContent = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
		string macroPath = Path.Combine(AppDataPath(), "Presets","Geology","Macros",$"{macroTitle}.macro");
		using (StreamWriter writer = new StreamWriter(macroPath, false))
		{
			writer.Write(jsonContent);
		}
	}
	
	public static void RemovePreset(int index)
	{
		if (index >= 0 && index < macro.Count)
		{
			macro.RemoveAt(index);
		}
	}
	
	public static bool MacroExists(string macroTitle){
		string macroPath = Path.Combine(AppDataPath(), "Presets","Geology","Macros",$"{macroTitle}.macro");
		return File.Exists(macroPath);
	}
	
	public static void AddToMacro(string macroTitle)
	{
		string macroPath = Path.Combine(AppDataPath(), "Presets","Geology","Macros",$"{macroTitle}.macro");
		macro.Add(macroPath);
	}
	

	public static void LoadSettings()
	{
		try
		{
			string settingsPath = SettingsPath;
			
			if (string.IsNullOrEmpty(settingsPath))
			{
				Debug.LogError("Settings path is null or empty");
				return;
			}

			if (!File.Exists(settingsPath))
			{
				Debug.LogError($"Config file not found at: {settingsPath}");
				return;
			}

			using (StreamReader reader = new StreamReader(settingsPath))
			{

				string json = reader.ReadToEnd();
				if (string.IsNullOrEmpty(json))
				{
					Debug.LogError($"Config file is empty at: {settingsPath}");
					return;
				}
				Debug.Log("loading settings from " + settingsPath);
				EditorSettings editorSettings = JsonConvert.DeserializeObject<EditorSettings>(json);


				RustDirectory = editorSettings.rustDirectory ;
				PrefabRenderDistance = editorSettings.prefabRenderDistance;
				PathRenderDistance = editorSettings.pathRenderDistance;
				WaterTransparency = editorSettings.waterTransparency;
				LoadBundleOnLaunch = editorSettings.loadbundleonlaunch;
				PrefabPaths = editorSettings.prefabPaths ; 
				style = editorSettings.style ;
				crazing = editorSettings.crazing;
				perlinSplat = editorSettings.perlinSplat;
				ripple = editorSettings.ripple;
				ocean = editorSettings.ocean;
				terracing = editorSettings.terracing;
				perlin = editorSettings.perlin;
				geology = editorSettings.geology;
				replacer = editorSettings.replacer;
				city = editorSettings.city;
				macroSources = editorSettings.macroSources ; 
				application = editorSettings.application ;
				faves = editorSettings.faves ;
				windowStates = editorSettings.windowStates ; 
				menuState = editorSettings.menuState;
				binds = editorSettings.binds;
				Debug.Log(binds.Count + " binds loaded from disk");
			}
		}
		catch (FileNotFoundException ex)
		{
			Debug.LogError($"Settings file not found: {ex.Message}");
			SetDefaultSettings();
		}
		catch (JsonException ex)
		{
			Debug.LogError($"JSON deserialization error in settings file: {ex.Message} ... File may have been corrupted");
			SetDefaultSettings();
		}
		catch (IOException ex)
		{
			Debug.LogError($"IO error while reading settings file: {ex.Message}");
			SetDefaultSettings();
		}
		catch (Exception ex)
		{
			Debug.LogError($"Unexpected error in LoadSettings: {ex.Message}\n{ex.StackTrace}");
			SetDefaultSettings();
		}

		LoadPresets();
		LoadMacros();
	}
	
	public static void LoadPresets()
	{
		geologyPresets = Directory.GetFiles(Path.Combine(AppDataPath(), "Presets", "Geology"), "*.json");
		breakerPresets = Directory.GetFiles(Path.Combine(AppDataPath(), "Presets", "Breaker"));
	}
	
	public static void LoadMacros()
	{
		geologyPresetLists = SettingsManager.GetPresetTitles(Path.Combine(AppDataPath() , "Presets","Geology","Macros"));
	}
	
	public static string[] GetPresetTitles(string path)
	{
		char[] delimiters = { '/', '.'};
		string[] geologyPresets = Directory.GetFiles(path);
		string[] parse;
		string[] filenames = new string [geologyPresets.Length];
		int filenameID;
		
		for(int i = 0; i < geologyPresets.Length; i++)
		{
			parse = geologyPresets[i].Split(delimiters);
			filenameID = parse.Length - 2;
			filenames[i] = parse[filenameID];
		}
		return filenames;
	}
	
	public static string[] GetDirectoryTitles(string path)
	{
		
			return Directory.GetDirectories(path);

	}

	/// <summary> Sets the EditorSettings back to default values based on a stock configuration. </summary>
	public static void SetDefaultSettings()
	{
		RustDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Rust";
		ToolTips.rustDirectoryPath.text = RustDirectory;
		PrefabRenderDistance = 700f;
		PathRenderDistance = 250f;
		WaterTransparency = 0.2f;
		LoadBundleOnLaunch = true;
		TerrainTextureSet = false;
		style = true;
		
		application = new FilePreset
		{
			rustDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Rust",
			prefabRenderDistance = 5000f,
			pathRenderDistance = 5000f,
			waterTransparency = 0f,
			loadBatch = 128,
			newSize = 1000,
			newHeight = 0.5f,
		newBiome = TerrainBiome.Enum.Temperate,
		newSplat = TerrainSplat.Enum.Grass,    
		};
		
		crazing = new CrazingPreset();
		perlinSplat = new PerlinSplatPreset();
		ripple = new RipplePreset();
		ocean = new OceanPreset();
		terracing = new TerracingPreset();
		perlin = new PerlinPreset();
		geology = new GeologyPreset();
		replacer = new ReplacerPreset();
		city = new RustCityPreset();
		breaker = new BreakerPreset();
		macroSources = false;
		faves = new Favorites();
		windowStates = new WindowState[0]; // Empty array as default
		menuState = new MenuState();
		PrefabPaths = new string[0]; // Empty array as default
		macro = new List<string>(); // Empty list as default
		binds = new List<Bind>();
		Debug.Log("Default Settings set.");
	}
	
	public static void SetDefaultGeology(){
		geology = new GeologyPreset("Default");
	}
}

[Serializable]
public struct EditorSettings
{
    public string rustDirectory;
    public float prefabRenderDistance;
    public float pathRenderDistance;
    public float waterTransparency;
    public bool loadbundleonlaunch;
    public bool terrainTextureSet;
	public bool style;
	
	public FilePreset application;
	public CrazingPreset crazing;
	public PerlinSplatPreset perlinSplat;
	public RipplePreset ripple;
	public OceanPreset ocean;
	public TerracingPreset terracing;
	public PerlinPreset perlin;
	public GeologyPreset geology;
	public ReplacerPreset replacer;
	public string[] prefabPaths;
	public RustCityPreset city;
	public BreakerPreset breaker;
	public bool macroSources;
	public Favorites faves;
	
	public WindowState[] windowStates;
    public MenuState menuState;         
	public List<Bind> binds;
	
    public EditorSettings
    (
        string rustDirectory = @"C:\Program Files (x86)\Steam\steamapps\common\Rust", float prefabRenderDistance = 700f, float pathRenderDistance = 200f, 
        float waterTransparency = 0.2f, bool loadbundleonlaunch = false, bool terrainTextureSet = false, bool style = true, CrazingPreset crazing = new CrazingPreset(), PerlinSplatPreset perlinSplat = new PerlinSplatPreset(),
		RipplePreset ripple = new RipplePreset(), OceanPreset ocean = new OceanPreset(), TerracingPreset terracing = new TerracingPreset(), PerlinPreset perlin = new PerlinPreset(), GeologyPreset geology = new GeologyPreset(), 
		ReplacerPreset replacer = new ReplacerPreset(), RustCityPreset city = new RustCityPreset(), BreakerPreset breaker = new BreakerPreset(), bool macroSources = true, FilePreset application = new FilePreset(), Favorites faves = new Favorites(),        WindowState[] windowStates = null, MenuState menuState = new MenuState(), List<Bind> binds = null
   
	)
        {
            this.rustDirectory = rustDirectory;
            this.prefabRenderDistance = prefabRenderDistance;
            this.pathRenderDistance = pathRenderDistance;
            this.waterTransparency = waterTransparency;
            this.loadbundleonlaunch = loadbundleonlaunch;
            this.terrainTextureSet = terrainTextureSet;
			this.style = style;
			this.crazing = crazing;
			this.perlinSplat = perlinSplat;
            this.prefabPaths = SettingsManager.PrefabPaths;
			this.ripple = ripple;
			this.ocean = ocean;
			this.terracing = terracing;
			this.perlin = perlin;
			this.geology = geology;
			this.replacer = replacer;
			this.city = city;
			this.breaker = breaker;
			this.macroSources = macroSources;
			this.application = application;
			this.faves = faves;
			this.windowStates = windowStates;
			this.menuState = menuState;
			this.binds = binds;
        }
}