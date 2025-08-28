using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;
using UnityEditor;
#endif
using UnityEngine;
using RustMapEditor.Variables;
using System.Text.RegularExpressions;
using EasyRoads3Dv3;
using Rust;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

[Serializable]
public class AssetScene
{
    [JsonProperty("Name")]
    public string Name;

    [JsonProperty("AutoLoad")]
    public bool AutoLoad;

    [JsonProperty("CanUnload")]
    public bool CanUnload;

    [JsonProperty("IncludedAssets")]
    public string[] IncludedAssets;
}

[Serializable]
public class AssetSceneManifest
{
    [JsonProperty("Scenes")]
    public AssetScene[] Scenes;
}

public static class AssetManager
{
	#if UNITY_EDITOR
	#region Init
	[InitializeOnLoadMethod]
	private static void Init()
	{
		EditorApplication.update += OnProjectLoad;
		Callbacks.BundlesDisposed += FileWindowUpdate;

	}

	private static void OnProjectLoad()
	{
		EditorApplication.update -= OnProjectLoad;
		if (!IsInitialised && SettingsManager.LoadBundleOnLaunch)
			Initialise(Path.Combine(SettingsManager.application.rustDirectory, SettingsManager.BundlePathExt));
	}
	#endregion
	#endif
	
	public static void RuntimeInit()	{
		Callbacks.BundlesDisposed += FileWindowUpdate;
		
		if(!SettingsManager.application.loadbundleonlaunch){
			Debug.Log("skipping load on launch");
			IsInitialised = false;
			LoadScreen.Instance.Hide();
			Debug.Log("no asset bundle, load screen hidden");
			return;
		}		

		LoadScreen.Instance.Show();

		Debug.LogError(SettingsManager.application.rustDirectory);
		
		string bundleExt = 	Path.Combine("Bundles", "Bundles");
		
		string bundlePath = SettingsManager.application.rustDirectory;
		
		string[] pathSegments = { "Program Files (x86)", "Steam", "steamapps", "common", "Rust"};
		string bundleTry = Path.Combine(pathSegments);
		
		Debug.LogError(bundlePath);
		if (!ValidBundlePath(bundlePath)){
			List<string> drives = Directory.GetLogicalDrives().ToList();
			
			foreach (string drive in drives)			{
				string alternativePath = Path.Combine(drive, bundleTry);

				if (Directory.Exists(alternativePath))				{
					FilePreset app = SettingsManager.application;
					app.rustDirectory = alternativePath;
					SettingsManager.application = app;
					SettingsManager.SaveSettings();
					bundlePath = Path.Combine(alternativePath, bundleExt);
					break;
				}
			}
		}
		else {			
				bundlePath = Path.Combine(bundlePath, bundleExt);
				Initialise(bundlePath);
				return;
			}
		
		IsInitialised = false;
		Debug.LogError("failed to load bundles");

	}
	
	public static bool AreBundlesLoaded()
	{
		return IsInitialised && BundleLookup.Count > 0 && Manifest != null;
	}
	
	public static bool ValidBundlePath(string bundleRoot)
	{
		
		if (!Directory.Exists(SettingsManager.application.rustDirectory))		{
				Debug.LogError("Directory invalidated: " + bundleRoot);
			return false;
		}
		

		if (!bundleRoot.ToLower().EndsWith("rust") && 
			!bundleRoot.ToLower().EndsWith("ruststaging"))		{
				Debug.LogError("Not a valid Rust install directory: " + bundleRoot);
			return false;
		}

		bundleRoot= Path.Combine(bundleRoot,"Bundles","Bundles");
		var rootBundle = AssetBundle.LoadFromFile(bundleRoot);
		if (rootBundle == null)		{
			Debug.LogError("Couldn't load root AssetBundle - " + bundleRoot);
			return false;
		}
		rootBundle.Unload(false);
		return true;
	}
	
	private static void FileWindowUpdate()
	{
		SettingsWindow.Instance.UpdateButtonStates();
	}


	public static class Callbacks
    {
		public delegate void Bundle();

		/// <summary>Called after Rust Asset Bundles are loaded into the editor. </summary>
		public static event Bundle BundlesLoaded;

		/// <summary>Called after Rust Asset Bundles are unloaded from the editor. </summary>
		public static event Bundle BundlesDisposed;

		public static void OnBundlesLoaded() => BundlesLoaded?.Invoke();
		public static void OnBundlesDisposed() => BundlesDisposed?.Invoke();
	}

	public static GameManifest Manifest { get; private set; }
	
	public const string ManifestPath = "assets/manifest.asset";
	public const string AssetDumpPath = "AssetDump.txt";
	public const string MaterialsListPath = "MaterialsList.txt";
	public const string VolumesListPath = "VolumesList.txt";

	public static Dictionary<uint, string> IDLookup { get; private set; } = new Dictionary<uint, string>();
	public static Dictionary<string, uint> PathLookup { get; private set; } = new Dictionary<string, uint>();
	public static Dictionary<string, AssetBundle> BundleLookup { get; private set; } = new Dictionary<string, AssetBundle>();
	public static Dictionary<string, uint> PrefabLookup { get; private set; } = new Dictionary<string, uint>();
	
	public static Dictionary<string, AssetBundle> BundleCache { get; private set; } = new Dictionary<string, AssetBundle>(System.StringComparer.OrdinalIgnoreCase);
	private static Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
	public static Dictionary<string, UnityEngine.Object> AssetCache { get; private set; } = new Dictionary<string, UnityEngine.Object>();
	public static Dictionary<string, UnityEngine.Object> SceneAssetCache { get; private set; } = new Dictionary<string, UnityEngine.Object>();
	public static Dictionary<string, GameObject> VolumesCache { get; private set; } = new Dictionary<string, GameObject>();
	public static Dictionary<string, Texture2D> PreviewCache { get; private set; } = new Dictionary<string, Texture2D>();
	
	public static Dictionary<string, string> GuidToPath { get; private set; } = new Dictionary<string, string>();
	public static Dictionary<string, string> PathToGuid { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	
	public static  Dictionary<string, uint[]> MonumentLayers { get; private set; }  = new Dictionary<string, uint[]>();
	
	public static List<uint> MonumentList { get; private set; } = new List<uint>();
	public static List<uint> ColliderBlocks { get; private set; } = new List<uint>();
	

	public static List<string> AssetPaths { get; private set; } = new List<string>();

	public static bool IsInitialised { get; private set; }

    public static Dictionary<string, string> MaterialLookup { get; private set; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
	public static Dictionary<string, Shader> ShaderCache    { get; private set; } = new Dictionary<string, Shader>(System.StringComparer.OrdinalIgnoreCase);
	public static Dictionary<string, AsyncOperation> SceneLoadOperations { get; private set; } = new Dictionary<string, AsyncOperation>(StringComparer.OrdinalIgnoreCase);
	
	public static Dictionary<string, Dictionary<string, GameObject>> SceneCache { get; private set; } = new Dictionary<string, Dictionary<string, GameObject>>(StringComparer.OrdinalIgnoreCase);
	public static AssetSceneManifest AssetSceneManifest { get; private set; } 


	public static T GetAsset<T>(string filePath) where T : UnityEngine.Object
	{
		if (!BundleLookup.TryGetValue(filePath, out AssetBundle bundle))
			return null;

		return bundle.LoadAsset<T>(filePath);
	}
	
	public static UnityEngine.Object GetAssetByGuid(string guid)
	{
		if (string.IsNullOrEmpty(guid))
		{
			Debug.LogWarning("GUID is empty.");
			return null;
		}

		if (GuidToPath.TryGetValue(guid, out string path))
		{
			if (AssetCache.TryGetValue(path, out UnityEngine.Object cachedAsset))
			{
				Debug.Log($"Loaded resource from cache for GUID: {guid}, Path: {path}");
				return cachedAsset;
			}

			UnityEngine.Object asset = GetAsset<UnityEngine.Object>(path);
			if (asset != null)
			{
				AssetCache[path] = asset;
				Debug.Log($"Loaded resource from bundle for GUID: {guid}, Path: {path}");
				return asset;
			}
			else
			{
				Debug.LogError($"Failed to load asset from bundle for GUID: {guid}, Path: {path}. Path not in BundleLookup or asset missing.");
				return null;
			}
		}
		else
		{
			Debug.LogError($"GUID not found in GuidToPath: {guid}. Total GUIDs loaded: {GuidToPath.Count}");
			return null;
		}
	}
	
	public static T GetAssetByGuid<T>(string guid) where T : UnityEngine.Object
	{
		return GetAssetByGuid(guid) as T;
	}
	
	#if UNITY_EDITOR
	public static void Initialise(string bundlesRoot)
	{
		/// <summary>Loads the Rust bundles into memory.</summary>
		/// <param name="bundlesRoot">The file path to the Rust bundles file.</param>
		if (!Coroutines.IsInitialising && !IsInitialised)
			EditorCoroutineUtility.StartCoroutineOwnerless(Coroutines.Initialise(bundlesRoot));
		if (IsInitialised)
			Debug.Log("Bundles already loaded.");
	}

	/// <summary>Loads the Rust bundles at the currently set directory.</summary>
	public static void Initialise() => Initialise(Path.Combine(SettingsManager.application.rustDirectory , SettingsManager.BundlePathExt));

	public static void Dispose()
	{
		if (!Coroutines.IsInitialising && IsInitialised)
			EditorCoroutineUtility.StartCoroutineOwnerless(Coroutines.Dispose());
	}
	#else
	
	public static void Initialise(string bundlesRoot)
	{
		if (!Coroutines.IsInitialising && !IsInitialised)
			CoroutineManager.Instance.StartCoroutine(Coroutines.Initialise(bundlesRoot));
		if (IsInitialised)
			Debug.Log("Bundles already loaded.");
	}

	/// <summary>Loads the Rust bundles at the currently set directory.</summary>
	public static void Initialise()
	{
		string bundleExt = Path.Combine("Bundles", "Bundles");
		Initialise(Path.Combine(SettingsManager.application.rustDirectory , bundleExt));
		
	}

	public static void Dispose()
	{
		if (!Coroutines.IsInitialising && IsInitialised)
			CoroutineManager.Instance.StartCoroutine(Coroutines.Dispose());
		

	}
	#endif


    public static T LoadAsset<T>(string filePath) where T : UnityEngine.Object
	{
		T asset;

		if (AssetCache.ContainsKey(filePath))
			asset = AssetCache[filePath] as T;
		else
		{
			asset = GetAsset<T>(filePath);
			if (asset != null){
				AssetCache.Add(filePath, asset);
			}
			else{
					if(SceneAssetCache.ContainsKey(filePath)){
						asset = SceneAssetCache[filePath] as T;
						AssetCache.Add(filePath, asset);
					}
					else
					{
						asset = null;
					}
				}
			
		}

		return asset;
	}

	public static GameObject LoadPrefab(string filePath)
    {
        if (AssetCache.ContainsKey(filePath))
            return AssetCache[filePath] as GameObject;

		GameObject go;
		/*
		//if it's a volume we return a default
		if(filePath.Contains("volume")||filePath.Contains("radiation")||filePath.Contains("spawner")||filePath.Contains("ai_obstacle")||filePath.Contains("ai_monument_navmesh")){
			if(filePath.Contains("sphere")){
				return PrefabManager.DefaultSphereVolume;
			}
			return PrefabManager.DefaultCubeVolume;
		}*/
		
		if(VolumesCache.ContainsKey(filePath)){
			go = (GameObject)VolumesCache[filePath];
			PrefabManager.SetupVolume(go, filePath);
			AssetCache.Add(filePath, go);
			return go;
		}
		
		if (SceneAssetCache.ContainsKey(filePath)){
			go = (GameObject)SceneAssetCache[filePath];
		}
		else
		{
			go = GetAsset<GameObject>(filePath);
		}
		

		//configure, cache, and return
		if (go != null)    {		
		
			PrefabManager.Setup(go, filePath);
			AssetCache.Add(filePath, go);
			//go.SetActive(true);
			return go;

            }
            Debug.LogWarning("Prefab not loaded from bundle: " + filePath);
            return PrefabManager.DefaultPrefab;

    }
	

	/// <summary>Returns a preview image of the asset located at the filepath. Caches the results.</summary>
	public static Texture2D GetPreview(string filePath)
    {
		#if UNITY_EDITOR
		if (PreviewCache.TryGetValue(filePath, out Texture2D preview))
			return preview;
        else
        {
			var prefab = LoadPrefab(filePath);
			if (prefab.name == "DefaultPrefab")
				return AssetPreview.GetAssetPreview(prefab);

			prefab.SetActive(true);
			var tex = AssetPreview.GetAssetPreview(prefab) ?? new Texture2D(60, 60);
			PreviewCache.Add(filePath, tex);
			prefab.SetActive(false);
			return tex;
        }
		#else
		return new Texture2D(60, 60);
		#endif
    }

	public static bool isMonument(uint id){
		return MonumentList.Contains(id);
	}
	
public static void SetVolumesCache()
{
    int loaded = 0;
    GameObject volumesRoot = new GameObject("VolumesRoot"); // Create parent object
    volumesRoot.SetActive(false); // Keep inactive to avoid scene clutter

    if (File.Exists(Path.Combine(SettingsManager.AppDataPath(), VolumesListPath)))
    {
        Debug.Log("loaded " + VolumesListPath);
        var volumes = File.ReadAllLines(VolumesListPath);
        Debug.Log("getting " + volumes.Length + " volumes");
        for (int i = 0; i < volumes.Length; i++)
        {
            try
            {
                var lineSplit = volumes[i].Split(':');
                if (lineSplit.Length < 4) continue;

                lineSplit[0] = lineSplit[0].Trim(); // Volume Type
                lineSplit[1] = lineSplit[1].Trim(); // Prefab Path
                lineSplit[2] = lineSplit[2].Trim(); // Hex Color
                lineSplit[3] = lineSplit[3].Trim(); // Scale Multiplier
                
                if (!int.TryParse(lineSplit[3], out int scaleMultiplier))
                {
                    Debug.LogWarning($"Invalid scale multiplier '{lineSplit[3]}' for volume {lineSplit[1]}");
                    scaleMultiplier = 1;
                }

                switch (lineSplit[0])
                {
                    case "Cube":
                        GameObject transCube = Resources.Load<GameObject>("Prefabs/TranslucentCube");
                        if (transCube != null)
                        {
                            GameObject instantiatedTransCube = UnityEngine.Object.Instantiate(transCube);
                            instantiatedTransCube.transform.SetParent(volumesRoot.transform); // Parent to VolumesRoot
                            MeshRenderer transCubeRenderer = instantiatedTransCube.GetComponentInChildren<MeshRenderer>();
                            if (transCubeRenderer != null && ColorUtility.TryParseHtmlString($"#{lineSplit[2]}", out Color transCubeColor))
                            {
                                transCubeColor.a = 0.5f;
                                transCubeRenderer.material.SetColor("_Color", transCubeColor);
                            }
                            else
                            {
                                Debug.LogWarning($"MeshRenderer not found or invalid color '{lineSplit[2]}' for {lineSplit[1]}");
                            }
                            if (scaleMultiplier > 0)
                            {
                                instantiatedTransCube.transform.localScale *= scaleMultiplier;
                            }
                            
                            VolumesCache.Add(lineSplit[1], instantiatedTransCube);
                            loaded++;
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to load Prefabs/TranslucentCube for {lineSplit[1]}");
                        }
                        break;
                    case "Sphere":
                        GameObject transSphere = Resources.Load<GameObject>("Prefabs/TranslucentSphere");
                        if (transSphere != null)
                        {
                            GameObject instantiatedTransSphere = UnityEngine.Object.Instantiate(transSphere);
                            instantiatedTransSphere.transform.SetParent(volumesRoot.transform); // Parent to VolumesRoot
                            MeshRenderer transSphereRenderer = instantiatedTransSphere.GetComponentInChildren<MeshRenderer>();
                            if (transSphereRenderer != null && ColorUtility.TryParseHtmlString($"#{lineSplit[2]}", out Color transSphereColor))
                            {
                                transSphereColor.a = 0.5f;
                                transSphereRenderer.material.SetColor("_Color", transSphereColor);
                            }
                            else
                            {
                                Debug.LogWarning($"MeshRenderer not found or invalid color '{lineSplit[2]}' for {lineSplit[1]}");
                            }
                            if (scaleMultiplier > 0)
                            {
                                instantiatedTransSphere.transform.localScale *= scaleMultiplier;
                            }
                            
                            VolumesCache.Add(lineSplit[1], instantiatedTransSphere);
                            loaded++;
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to load Prefabs/TranslucentSphere for {lineSplit[1]}");
                        }
                        break;
                    default:
                        Debug.LogWarning($"Unknown volume type '{lineSplit[0]}' for {lineSplit[1]}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing volume at index {i}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
    else
    {
        Debug.LogWarning($"VolumesListPath not found: {VolumesListPath}");
    }
    Debug.Log("loaded " + loaded + " volume placeholders");
}
	
	/// <summary>Adds the volume gizmo component to the prefabs in the VolumesList.</summary>
	public static void SetVolumeGizmos()
    {
		if (File.Exists(Path.Combine(SettingsManager.AppDataPath() , VolumesListPath)))
        {
			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
			GameObject.DestroyImmediate(cube);
			var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			var sphereMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
			GameObject.DestroyImmediate(sphere);
			
			
			var volumes = File.ReadAllLines(Path.Combine(SettingsManager.AppDataPath() , VolumesListPath));
            for (int i = 0; i < volumes.Length; i++)
            {
				var lineSplit = volumes[i].Split(':');
				lineSplit[0] = lineSplit[0].Trim(' '); // Volume Mesh Type
				lineSplit[1] = lineSplit[1].Trim(' '); // Prefab Path
                
				var prefab = LoadPrefab(lineSplit[1]);
				
				if (prefab.TryGetComponent<VolumeGizmo>(out VolumeGizmo vg))
				{
					Debug.LogWarning("mesh already exists");
				}
				
				else
					
				{
					switch (lineSplit[0])
					{
						case "Cube":
							LoadPrefab(lineSplit[1]).AddComponent<VolumeGizmo>().mesh = cubeMesh;
							break;
						case "Sphere":
							LoadPrefab(lineSplit[1]).AddComponent<VolumeGizmo>().mesh = sphereMesh;
							break;
					}
				}
            }
        }
    }

	public static string pathToName(string path)
	{
			path = path.Replace(@"\","/");
			string[] pathFragment = path.Split('/');
			string filename = pathFragment[pathFragment.Length-1];
			//filename = filename.Replace('_',' ');
			//string[] extension = filename.Split('.');
			//filename = extension[0];
			return filename;
	}

	public static string ToName(uint i)
	{
		if ((int)i == 0)
			return i.ToString();
		if (IDLookup.TryGetValue(i, out string str))
		{
			return pathToName(str);
		}
		return i.ToString();
	}
	
	public static uint fragmentToID(string fragment, string parent, string monument)
	{
		string newFragment = Regex.Replace(fragment, @"\s?\(.*?\)$", "").ToLower();
		string newParent = Regex.Replace(parent, @"\s?\(.*?\)$", "");
		uint parentID = 0;
		uint ID = 0;
		uint returnID = 0;


		try
			{
				ID = SettingsManager.fragmentIDs.fragmentNamelist[fragment];
				return ID;
			}
		catch (KeyNotFoundException)
			{
			}

		if (SettingsManager.fragmentIDs.fragmentNamelist.TryGetValue("/" + newParent + "/", out parentID))
		{
			return parentID;
		}
		
		// Final lookup for direct fragment match
		if (SettingsManager.fragmentIDs.fragmentNamelist.TryGetValue(newFragment, out ID))
		{
			return ID;
		}
		
		// Attempt to get ID from PrefabLookup
		if (PrefabLookup.TryGetValue(newFragment, out ID))
		{
			returnID = ID;
		}
		else
		{
			newFragment = specialParse(newFragment, monument);
			if (PrefabLookup.TryGetValue(newFragment, out ID))
			{
				returnID = ID;
			}
		}
		
		// Special case for Oilrig
		if (monument == "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_2.prefab")
		{
			if (PrefabLookup.TryGetValue("oilrig_small/" + newFragment, out ID))
			{
				returnID = ID;
			}
		}
		
		// If parent ID was found, return it
		if (parentID != 0)
		{
			return returnID;
		}
		

		
		return returnID;
	}


	public static string specialParse(string str, string str2)
	{
		
		string[] parse, parse2;

		
	
		if(str.Contains("GCD1"))

		{
							parse = str.Split('_');
							str = parse[1];
		}			
		else if(str.Contains("GCD") || str.Contains("BCD") || str.Contains("RCD") || str.Contains("GDC"))
		{
							parse = str.Split('_');
							if(str2 == "Oilrig 2")
							{
								str = parse[1];
							}
							else
							{
								str = parse[2];
							}
		}
		else if (str.Contains("outbuilding") || str.Contains("rowhouse"))
			{
						//remove color tags
						parse2 = str.Split('-');
						str = parse2[0];
			}
		else
		{			
			if (str2 == "assets/bundled/prefabs/autospawn/monument/arctic_bases/arctic_research_base_a.prefab")
			{
				string[] parse4;
				int trash = 0;
				parse4 = str.Split('_');
													
					if (int.TryParse(parse4[parse4.GetLength(0)-1], out trash))
					{
						if ((!str.Contains("trail") && trash != 300 && trash != 600 && trash != 900))
						{
							str = str.Replace("_" + trash.ToString(), "");
										//parse4[parse4.GetLength(0)-1] = "";
										//str = string.Join("_",parse4);
										//str = str.Remove(str.Length-1);
						}
						else if (str.Contains("rock"))
						{
							str = str.Replace("_" + trash.ToString(), "");
							Debug.LogError(str);
							//parse4[parse4.GetLength(0)-1] = "";
							//str = string.Join("_",parse4);
							//str = str.Remove(str.Length-1);
							
						}
					}
			}
		}
			
		
		
		return str;
	}

	public static uint partialToID(string str, string str2)
	{
		string path, prefab, folder;
		string[] parse, parse2, parse3;
		folder = "";

		
	
		if(str.Contains("GCD1"))

		{
							parse = str.Split('_');
							str = parse[1];
		}			
		else if(str.Contains("GCD") || str.Contains("BCD") || str.Contains("RCD") || str.Contains("GDC"))
					{
							parse = str.Split('_');
							if(str2 == "Oilrig 2")
							{
								str = parse[1];
							}
							else
							{
								str = parse[2];
							}
					}
		
		
		
		parse3 = str.Split(' ');
		str = parse3[0].ToLower();
		//remove most number tags
		
		if (str2 == "arctic research base a")
		{
			string[] parse4;
			int trash = 0;
			parse4 = str.Split('_');
												
				if (int.TryParse(parse4[parse4.GetLength(0)-1], out trash))
				{
					if (!str.Contains("trail") && trash != 300 && trash != 600 && trash != 900)
					{
									parse4[parse4.GetLength(0)-1] = "";
									str = string.Join("_",parse4);
									str = str.Remove(str.Length-1);
					}
				}
		}
		else if (str2 == "Oilrig 1")
		{
			folder = "prefabs_large_oilrig";
		}
		else if (str2 == "Oilrig 2")
		{
			folder = "prefabs_small_oilrig";
		}
		
		
		if (string.IsNullOrEmpty(str))
			return 0;
		

		
		if (string.IsNullOrEmpty(folder))
		{

			foreach (KeyValuePair<string, uint> kvp in PathLookup)
			{
				path = kvp.Key;
				parse = path.Split('/');
				prefab = parse[parse.Length -1];
				
				if ((prefab == (str+".prefab")))
				{
					return kvp.Value;
				}
			}
			
			//if can't find the rowhouse or outbuilding try again, without color tags
			if (str.Contains("outbuilding") || str.Contains("rowhouse"))
			{
				foreach (KeyValuePair<string, uint> kvp in PathLookup)
				{
					path = kvp.Key;
						parse = path.Split('/');
						prefab = parse[parse.Length -1];
						
						//remove color tags
						parse2 = str.Split('-');
						str = parse2[0];
						if ((prefab == (str+".prefab")))
						{
							return kvp.Value;
						}
				}
			}
			
		}
		
		else
		{
		
			foreach (KeyValuePair<string, uint> kvp in PathLookup)
			{
				path = kvp.Key;
				parse = path.Split('/');
				prefab = parse[parse.Length -1];
				
				if ((prefab == (str+".prefab")) && path.Contains(folder))
				{
					return kvp.Value;
				}
			}
			
			foreach (KeyValuePair<string, uint> kvp in PathLookup)
			{
				path = kvp.Key;
				parse = path.Split('/');
				prefab = parse[parse.Length -1];
				
				if ((prefab == (str+".prefab")))
				{
					return kvp.Value;
				}
			}
			
		}
		
		return 0;
	}
		

public static void AssetDump()
{
    if (Manifest == null)
    {
        Debug.LogError("Manifest is null. Cannot dump contents.");
        return;
    }
    if (BundleLookup.Count == 0)
    {
        Debug.LogError("BundleLookup is empty. Ensure bundles are loaded before dumping.");
        return;
    }

    // Original AssetDump logic
    string dumpPath = "ManifestDump.txt";
    using (StreamWriter streamWriter = new StreamWriter(dumpPath, false))
    {
        // Header
        streamWriter.WriteLine("=== Rust Manifest and Bundle Dump ===");
        streamWriter.WriteLine($"Date: {DateTime.Now}");
        streamWriter.WriteLine();

        // Section 1: Pooled Strings (Paths and Hashes)
        streamWriter.WriteLine("--- Pooled Strings (Manifest) ---");
        streamWriter.WriteLine($"Total entries: {Manifest.pooledStrings.Length}");
        var pooledStringsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pooledString in Manifest.pooledStrings)
        {
            string path = pooledString.str;
            uint hash = pooledString.hash;
            string entry = $"{path} : Hash={hash}";
            if (PathToGuid.TryGetValue(path, out string guid))
            {
                entry += $" : GUID={guid}";
            }
            streamWriter.WriteLine(entry);
            pooledStringsSet.Add(path);
        }
        streamWriter.WriteLine();

        // Section 2: GUID Paths
        streamWriter.WriteLine("--- GUID Paths (Manifest) ---");
        streamWriter.WriteLine($"Total entries: {Manifest.guidPaths.Length}");
        int guidPathUniqueCount = 0;
        foreach (var guidPath in Manifest.guidPaths)
        {
            string path = guidPath.name;
            string guid = guidPath.guid;
            string entry = $"{path} : GUID={guid}";
            if (PathLookup.TryGetValue(path, out uint hash))
            {
                entry += $" : Hash={hash}";
            }
            if (!pooledStringsSet.Contains(path))
            {
                streamWriter.WriteLine(entry);
                guidPathUniqueCount++;
                pooledStringsSet.Add(path);
            }
        }
        streamWriter.WriteLine($"Unique entries (not in Pooled Strings): {guidPathUniqueCount}");
        streamWriter.WriteLine();

        // Section 3: Prefab Properties
        streamWriter.WriteLine("--- Prefab Properties (Manifest) ---");
        streamWriter.WriteLine($"Total entries: {Manifest.prefabProperties.Length}");
        int prefabUniqueCount = 0;
        foreach (var prefab in Manifest.prefabProperties)
        {
            string path = prefab.name;
            string guid = prefab.guid;
            string entry = $"{path} : GUID={guid}";
            if (PathLookup.TryGetValue(path, out uint hash))
            {
                entry += $" : Hash={hash}";
            }
            if (!pooledStringsSet.Contains(path))
            {
                streamWriter.WriteLine(entry);
                prefabUniqueCount++;
                pooledStringsSet.Add(path);
            }
        }
        streamWriter.WriteLine($"Unique entries (not in Pooled Strings): {prefabUniqueCount}");
        streamWriter.WriteLine();

        // Section 5: All Bundle Assets
        streamWriter.WriteLine("--- All Bundle Assets (BundleLookup) ---");
        streamWriter.WriteLine($"Total entries: {BundleLookup.Count}");
        int bundleUniqueCount = 0;
        foreach (var assetPath in BundleLookup.Keys.OrderBy(k => k))
        {
            string entry = $"{assetPath}";
            bool hasAdditionalInfo = false;

            if (PathLookup.TryGetValue(assetPath, out uint hash))
            {
                entry += $" : Hash={hash}";
                hasAdditionalInfo = true;
            }
            if (PathToGuid.TryGetValue(assetPath, out string guid))
            {
                entry += $" : GUID={guid}";
                hasAdditionalInfo = true;
            }

            streamWriter.WriteLine(entry);
            if (!pooledStringsSet.Contains(assetPath))
            {
                bundleUniqueCount++;
            }
        }
        streamWriter.WriteLine($"Unique entries (not in previous sections): {bundleUniqueCount}");
    }
    Debug.Log($"Manifest and bundle contents dumped to {dumpPath}");
}

	public static string ToPath(uint i)
	{
		if ((int)i == 0)
			return i.ToString();
		if (IDLookup.TryGetValue(i, out string str))
			return str;
		return i.ToString();
	}

	public static uint ToID(string str)
	{
		if (string.IsNullOrEmpty(str))
			return 0;
		if (PathLookup.TryGetValue(str, out uint num))
			return num;
		return 0;
	}
	
	public static string MaterialPath(string str)
	{
		str = str+".mat";
		if (string.IsNullOrEmpty(str)){
			return "";
		}
		if (MaterialLookup.TryGetValue(str, out string path)){
			return path;
		}
		
		return "";
	}	
	
public static void LoadShaderCache()
{
    ShaderCache.Clear();
    Debug.Log($"Shader cache cleared and ready for loading. Total assets in BundleLookup: {BundleLookup.Count}");
    foreach (string path in BundleLookup.Keys)
    {
        if (path.EndsWith(".shader", StringComparison.Ordinal))
        {
            try
            {  
                Shader shader = LoadAsset<Shader>(path);
                if (shader != null)
                {
                    ShaderCache[shader.name] = shader;
                    Debug.Log($"Loaded shader into ShaderCache: {shader.name} (Path: {path})");
                }
                else
                {
                    Debug.LogWarning($"Shader not found at path: {path}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error loading shader from path: {path}. Exception: {e.Message}");
            }
        }
    }

}

	public static void UpdateShader(Material mat){
		CoroutineManager.Instance.StartRuntimeCoroutine(Coroutines.UpdateShader(mat));
	}
	
	
	public static void FixRenderMode(Material mat, Shader shader = null){
		CoroutineManager.Instance.StartRuntimeCoroutine(Coroutines.UpdateShader(mat));
	}
	
	private static class Coroutines
    {
		public static bool IsInitialising { get; private set; }

	public static IEnumerator Initialise(string bundlesRoot)
	{
		IsInitialising = true;
		string assetScenesBundlePath = Path.Combine(bundlesRoot, "..", "shared", "assetscenes.bundle");

		#if UNITY_EDITOR
		ProgressManager.RemoveProgressBars("Asset Bundles");
		int progressID = Progress.Start("Load Asset Bundles", null, Progress.Options.Sticky);
		int bundleID = Progress.Start("Bundles", null, Progress.Options.Sticky, progressID);
		int materialID = Progress.Start("Materials", null, Progress.Options.Sticky, progressID);
		int prefabID = Progress.Start("Replace Prefabs", null, Progress.Options.Sticky, progressID);
		Progress.Report(bundleID, 0f);
		Progress.Report(materialID, 0f);
		Progress.Report(prefabID, 0f);
		#endif

		// Check if assetscenes.bundle exists
		if (File.Exists(assetScenesBundlePath))
		{
			Debug.Log($"Found assetscenes.bundle at {assetScenesBundlePath}. Using scene-based asset loading.");
			#if UNITY_EDITOR

			yield return EditorCoroutineUtility.StartCoroutineOwnerless(LoadBundles(bundlesRoot, (0, 0, 0)));	
			yield return EditorCoroutineUtility.StartCoroutineOwnerless(SetBundleReferences((progressID, bundleID)));	
			yield return CoroutineManager.Instance.StartRuntimeCoroutine(LoadAssetSceneManifest(assetScenesBundlePath));			
			yield return CoroutineManager.Instance.StartRuntimeCoroutine(AutoLoadScenes());
			if (!IsInitialising)
			{
				Progress.Finish(bundleID, Progress.Status.Failed);
				Progress.Finish(materialID, Progress.Status.Failed);
				Progress.Finish(prefabID, Progress.Status.Failed);
				yield break;
			}
			#else

			yield return CoroutineManager.Instance.StartRuntimeCoroutine(LoadBundles(bundlesRoot, (0, 0, 0)));
			yield return CoroutineManager.Instance.StartRuntimeCoroutine(SetBundleReferences((0, 0)));
			yield return CoroutineManager.Instance.StartRuntimeCoroutine(LoadAssetSceneManifest(assetScenesBundlePath));
			yield return CoroutineManager.Instance.StartRuntimeCoroutine(AutoLoadScenes());
			#endif
		}
		else
		{
			Debug.Log($"assetscenes.bundle not found at {assetScenesBundlePath}. Falling back to legacy bundle loading.");
			#if UNITY_EDITOR
			yield return EditorCoroutineUtility.StartCoroutineOwnerless(LoadBundles(bundlesRoot, (progressID, bundleID, materialID)));
			if (!IsInitialising)
			{
				Progress.Finish(bundleID, Progress.Status.Failed);
				Progress.Finish(materialID, Progress.Status.Failed);
				Progress.Finish(prefabID, Progress.Status.Failed);
				yield break;
			}
			yield return EditorCoroutineUtility.StartCoroutineOwnerless(SetBundleReferences((progressID, bundleID)));
			
			#else
			yield return CoroutineManager.Instance.StartRuntimeCoroutine(LoadBundles(bundlesRoot, (0, 0, 0)));
			yield return CoroutineManager.Instance.StartRuntimeCoroutine(SetBundleReferences((0, 0)));
			#endif
		}

		IsInitialised = true; IsInitialising = false;
		SetVolumeGizmos();


		#if UNITY_EDITOR
		PrefabManager.ReplaceWithLoaded(PrefabManager.CurrentMapPrefabs, prefabID);
		#else
		PrefabManager.ReplaceWithLoaded(PrefabManager.CurrentMapPrefabs, 0);
		#endif
	}
	

public static IEnumerator LoadAssetSceneManifest(string bundlePath)
{
    string manifestPath = Path.Combine(bundlePath, "..", "..", "AssetSceneManifest.json");

    if (!File.Exists(manifestPath))
    {
        Debug.LogError($"AssetSceneManifest.json not found at: {manifestPath}");
        yield break;
    }

    string jsonContent = File.ReadAllText(manifestPath);
    AssetSceneManifest = JsonConvert.DeserializeObject<AssetSceneManifest>(jsonContent);
    if (AssetSceneManifest == null || AssetSceneManifest.Scenes == null || AssetSceneManifest.Scenes.Length == 0)
    {
        Debug.LogError("AssetSceneManifest is empty or contains no scenes.");
        AssetSceneManifest = null;
        yield break;
    }



    // Find the assetscenes.bundle in BundleCache
    string assetScenesBundleName = "shared/assetscenes.bundle";
    if (!BundleCache.TryGetValue(assetScenesBundleName, out AssetBundle assetScenesBundle))
    {
        Debug.LogError($"Asset bundle '{assetScenesBundleName}' not found in BundleCache.");
        yield break;
    }

    // Get scene paths from BundleLookup that belong to assetscenes.bundle
    List<string> scenePaths = new List<string>();
    foreach (var kvp in BundleLookup)
    {
        if (kvp.Value == assetScenesBundle && kvp.Key.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            scenePaths.Add(kvp.Key);
        }
    }

    if (scenePaths.Count == 0)
    {
        Debug.LogWarning($"No scenes found in {assetScenesBundleName}.");
        // Unload the bundle since no scenes are present
        assetScenesBundle.Unload(false);
        BundleCache.Remove(assetScenesBundleName);
        Debug.Log($"Unloaded asset bundle: {assetScenesBundleName}");
        yield break;
    }

}

public static IEnumerator LoadAssetScene(string sceneName)
{
    // Check if the scene is already loaded
    if (SceneCache.ContainsKey(sceneName))
    {
        Debug.LogWarning($"Asset scene '{sceneName}' is already loaded in SceneCache.");
        yield break;
    }

    // Ensure AssetSceneManifest is loaded
    if (AssetSceneManifest == null || AssetSceneManifest.Scenes == null)
    {
        Debug.LogError($"AssetSceneManifest is not loaded. Cannot load scene: {sceneName}");
        yield break;
    }

    // Find the scene in the manifest
    AssetScene assetScene = AssetSceneManifest.Scenes.FirstOrDefault(s => s.Name.Equals(sceneName, StringComparison.OrdinalIgnoreCase));
    if (assetScene == null)
    {
        Debug.LogError($"Scene '{sceneName}' not found in AssetSceneManifest.");
        yield break;
    }

    // Find the assetscenes.bundle in BundleCache
    string assetScenesBundleName = "shared/assetscenes.bundle";
    if (!BundleCache.TryGetValue(assetScenesBundleName, out AssetBundle assetScenesBundle))
    {
        Debug.LogError($"Asset bundle '{assetScenesBundleName}' not found in BundleCache.");
        yield break;
    }

    // Verify the scene path exists in BundleLookup
    string scenePath = sceneName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? sceneName : $"{sceneName}.unity";
	scenePath = "Assets/Scenes/AssetScenes/" + scenePath;
    if (!BundleLookup.ContainsKey( scenePath))
    {
		Debug.LogError($"Scene path '{scenePath}' not found in BundleLookup. Available paths: {string.Join(", ", BundleLookup.Keys)}");
        yield break;
    }

    // Set up load parameters
    LoadSceneParameters parameters = new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics3D);
    Debug.Log($"Starting to load asset scene: {scenePath}");

    // Load the scene asynchronously
    AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scenePath, parameters);
    if (asyncLoad == null)
    {
        Debug.LogError($"Failed to initiate async load for scene: {scenePath}");
        yield break;
    }

    asyncLoad.allowSceneActivation = false;
    SceneLoadOperations[scenePath] = asyncLoad; // Store AsyncOperation
    Scene currentActiveScene = SceneManager.GetActiveScene();

    while (!asyncLoad.isDone)
    {
        if (asyncLoad.progress >= 0.9f)
        {
            //Debug.Log($"Asset scene '{scenePath}' loaded, activating...");
            asyncLoad.allowSceneActivation = true;
            //SceneManager.SetActiveScene(currentActiveScene);
        }
        yield return null;
    }

    // Verify the scene is loaded and valid
    Scene loadedScene = SceneManager.GetSceneByName(sceneName);
    if (!loadedScene.IsValid() || !loadedScene.isLoaded)
    {
        Debug.LogError($"Failed to get asset scene after loading: {scenePath}");
        SceneLoadOperations.Remove(scenePath); // Clean up on failure
        yield break;
    }

    Debug.Log($"Asset scene '{scenePath}' activated successfully.");

    // Cache root GameObjects in SceneCache
    GameObject[] rootGameObjects = loadedScene.GetRootGameObjects();
    Dictionary<string, GameObject> prefabDictionary = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    foreach (GameObject gameObject in rootGameObjects)
    {
		SceneAssetCache.Add(gameObject.name, gameObject);
    }

    SceneLoadOperations.Remove(scenePath); // Clean up after successful load
    yield break;
}

public static IEnumerator AutoLoadScenes()
{
    // Ensure AssetSceneManifest is loaded
    if (AssetSceneManifest == null || AssetSceneManifest.Scenes == null)
    {
        Debug.LogError("AssetSceneManifest is not loaded or contains no scenes. Cannot autoload scenes.");
        yield break;
    }

    // Get all scenes marked for autoloading that aren't already loaded
    List<string> scenesToLoad = AssetSceneManifest.Scenes
        //.Where(scene => scene.AutoLoad && !SceneCache.ContainsKey(scene.Name))
        .Select(scene => scene.Name)
        .ToList();

    if (scenesToLoad.Count == 0)
    {
        Debug.Log("No scenes marked for autoloading or all AutoLoad scenes are already loaded.");
        yield break;
    }

    Debug.Log($"Starting autoload for {scenesToLoad.Count} scenes: {string.Join(", ", scenesToLoad)}");

    // Use the existing LoadAssetScenes method to load the scenes
    yield return LoadAssetScenes(scenesToLoad);
}
public static IEnumerator LoadAssetScenes(List<string> sceneNames)
{
    if (sceneNames == null || sceneNames.Count == 0)
    {
        Debug.LogWarning("No scenes provided to LoadAssetScenes.");
        LoadScreen.Instance?.Hide(); // Null check for safety
        yield break;
    }

    Debug.Log($"Starting to load {sceneNames.Count} scenes: {string.Join(", ", sceneNames)}");
    if (LoadScreen.Instance == null)
    {
        Debug.LogError("LoadScreen.Instance is null. Ensure LoadScreen is initialized in the scene.");
        yield break;
    }

    LoadScreen.Instance.Show();
    yield return null; // Ensure Show() takes effect before proceeding

    // Track active coroutines
    List<Coroutine> activeCoroutines = new List<Coroutine>();
    foreach (string sceneName in sceneNames)
    {
        if (!SceneCache.ContainsKey(sceneName))
        {

            Coroutine coroutine = CoroutineManager.Instance.StartRuntimeCoroutine(LoadAssetScene(sceneName));
            activeCoroutines.Add(coroutine);
        }
        else
        {
            Debug.LogWarning($"Scene '{sceneName}' is already loaded in SceneCache.");
        }
    }

    // Calculate progress based on SceneLoadOperations and loaded scenes
    while (activeCoroutines.Count > 0 || SceneLoadOperations.Count > 0)
    {
        float totalProgress = 0f;
        int validScenes = 0;
        int completedScenes = 0;

        foreach (string sceneName in sceneNames)
        {
            string scenePath = sceneName.EndsWith(".unity") ? sceneName : $"{sceneName}.unity";
            scenePath = "Assets/Scenes/AssetScenes/" + scenePath;

            Scene scene = SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(sceneName));
            if (scene.IsValid() && scene.isLoaded)
            {
                totalProgress += 1f;
                validScenes++;
                completedScenes++;
            }
            else if (SceneLoadOperations.TryGetValue(scenePath, out AsyncOperation asyncOp))
            {
                // Apply Rust server code math: asyncOperation.progress / 0.9f * 0.99f
                float sceneProgress = asyncOp.progress / 0.9f * 0.99f;
                totalProgress += sceneProgress;
                validScenes++;
            }
        }

        float averageProgress = validScenes > 0 ? Mathf.Min(totalProgress / validScenes, 0.999f) : 1f;

        // Update LoadScreen
        LoadScreen.Instance.Progress1(averageProgress);
        LoadScreen.Instance.SetMessage1($"Loading Asset Scenes: {(averageProgress * 100f):F1}%");

        if (completedScenes >= sceneNames.Count || averageProgress >= 0.999f)
        {
            LoadScreen.Instance.Progress1(1f);
            LoadScreen.Instance.SetMessage1("Asset Scenes Loaded");
            LoadScreen.Instance.Complete(1); // Complete scene loading
            break;
        }

        yield return null;
    }
	Debug.Log("Asset Scenes Completed");
	IsInitialised = true;
	Callbacks.OnBundlesLoaded();  //signal assets ready (bundles loaded legacy)
}

public static float GetAssetSceneProgress(List<string> sceneNames)
{
    if (sceneNames == null || sceneNames.Count == 0)
    {
        return 1f;
    }

    float totalProgress = 0f;
    int validScenes = 0;

    foreach (string sceneName in sceneNames)
    {
        string scenePath = sceneName.EndsWith(".unity") ? sceneName : $"{sceneName}.unity";
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            totalProgress += 1f;
            validScenes++;
        }
        else if (SceneLoadOperations.TryGetValue(scenePath, out AsyncOperation asyncOp))
        {
            totalProgress += asyncOp.progress;
            validScenes++;
        }
        // If the scene is neither loaded nor in SceneLoadOperations, it's not being loaded (progress = 0)
    }

    return validScenes > 0 ? totalProgress / validScenes : 1f;
}
	
	
	public static IEnumerator PopulateMaterialLookup()
	{
		MaterialLookup.Clear();

		foreach (string path in BundleLookup.Keys)
		{
			if (path != null && path.EndsWith(".mat", StringComparison.Ordinal))
			{
				string[] parse = path.Split('/');
				
				// Check if the array has elements before accessing the last one
				if (parse.Length > 0)
				{
					string name = parse[parse.Length - 1];

					try
					{
						// Add to dictionary only if the name is not null or empty
						if (!string.IsNullOrEmpty(name))
						{
							MaterialLookup.Add(name, path);
						}
					}
					catch (ArgumentException) // Handle if the key already exists in the dictionary
					{
						Debug.LogWarning($"Material with name '{name}' already exists in MaterialLookup. Path: {path}");
					}
					catch (Exception e) // Log other exceptions for debugging
					{
						Debug.LogError($"Unexpected error adding material '{name}' to lookup: {e.Message}");
					}
				}
			}

			yield return null;
		}
	}
		



	public static IEnumerator Dispose()
	{
		IsInitialising = true;
		ProgressManager.RemoveProgressBars("Unload Asset Bundles");

		#if UNITY_EDITOR
		int progressID = Progress.Start("Unload Asset Bundles", null, Progress.Options.Sticky);
		int bundleID = Progress.Start("Bundles", null, Progress.Options.Sticky, progressID);
		int prefabID = Progress.Start("Prefabs", null, Progress.Options.Sticky, progressID);
		Progress.Report(bundleID, 0f);
		Progress.Report(prefabID, 0f);
		PrefabManager.ReplaceWithDefault(PrefabManager.CurrentMapPrefabs, prefabID);
		#else
		PrefabManager.ReplaceWithDefault(PrefabManager.CurrentMapPrefabs, 0);
		#endif

		while (PrefabManager.IsChangingPrefabs)
			yield return null;

		// Unload all loaded scenes
		foreach (string scenePath in SceneCache.Keys.ToList())
		{
			Scene scene = SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(scenePath));
			if (scene.IsValid() && scene.isLoaded)
			{
				AsyncOperation asyncUnload = SceneManager.UnloadSceneAsync(scene);
				if (asyncUnload != null)
				{
					yield return asyncUnload;
				}
			}
		}

		for (int i = 0; i < BundleCache.Count; i++)
		{
			#if UNITY_EDITOR
			Progress.Report(bundleID, (float)i / BundleCache.Count, "Unloading: " + BundleCache.ElementAt(i).Key);
			#endif
			BundleCache.ElementAt(i).Value.Unload(true);
			yield return null;
		}

		int bundleCount = BundleCache.Count;
		BundleLookup.Clear();
		BundleCache.Clear();
		AssetCache.Clear();
		SceneCache.Clear();
		SceneLoadOperations.Clear(); // Clear SceneLoadOperations

		#if UNITY_EDITOR
		Progress.Report(bundleID, 0.99f, "Unloaded: " + bundleCount + " bundles.");
		Progress.Finish(bundleID, Progress.Status.Succeeded);
		#endif

		IsInitialised = false;
		IsInitialising = false;
		Callbacks.OnBundlesDisposed();
	}

		public static IEnumerator LoadBundles(string bundleRoot, (int progress, int bundle, int material) ID)
        {
			if (!Directory.Exists(SettingsManager.application.rustDirectory))
			{
				Debug.LogError("Directory does not exist: " + bundleRoot);
				LoadScreen.Instance.Hide();
				IsInitialising = false;
				yield break;
			}

			if (!SettingsManager.application.rustDirectory.EndsWith("Rust") && !SettingsManager.application.rustDirectory.EndsWith("RustStaging"))
			{
				Debug.LogError("Not a valid Rust install directory: " + SettingsManager.application.rustDirectory);
				LoadScreen.Instance.Hide();
				IsInitialising = false;
				yield break;
			}

			var rootBundle = AssetBundle.LoadFromFile(bundleRoot);
			if (rootBundle == null)
			{
				Debug.LogError("Couldn't load root AssetBundle - " + bundleRoot);
				LoadScreen.Instance.Hide();
				IsInitialising = false;
				yield break;
			}

			var manifestList = rootBundle.LoadAllAssets<AssetBundleManifest>();
			if (manifestList.Length != 1)
			{
				Debug.LogError("Couldn't find AssetBundleManifest - " + manifestList.Length);
				LoadScreen.Instance.Hide();
				IsInitialising = false;
				yield break;
			}

			var assetManifest = manifestList[0];
			var bundles = assetManifest.GetAllAssetBundles();
			LoadScreen.Instance.Show();
			
			for (int i = 0; i < bundles.Length; i++)
			{
				#if UNITY_EDITOR
				Progress.Report(ID.bundle, (float)i / bundles.Length, "Loading: " + bundles[i]);
				#endif
				LoadScreen.Instance.SetMessage("Loading Asset Bundles");
				LoadScreen.Instance.Progress((float)i/bundles.Length);
				var bundlePath = Path.GetDirectoryName(bundleRoot) + Path.DirectorySeparatorChar + bundles[i];
				if (File.Exists(bundlePath)) 
				{
                    var asset = AssetBundle.LoadFromFileAsync(bundlePath);
                    yield return asset;

                    if (asset == null)
                    {
                        Debug.LogError("Couldn't load AssetBundle - " + bundlePath);
						LoadScreen.Instance.Hide();
                        IsInitialising = false;
                        yield break;
                    }
                    BundleCache.Add(bundles[i], asset.assetBundle);
                }
				yield return null;
			}
			
			rootBundle.Unload(true);
		}
		
public static IEnumerator SetBundleReferences((int parent, int bundle) ID)
{
    var sw = new System.Diagnostics.Stopwatch();
    sw.Start();

    // Populate BundleLookup with asset names and scene paths from loaded bundles
    foreach (var asset in BundleCache.Values)
    {
        foreach (var filename in asset.GetAllAssetNames())
        {
            BundleLookup.Add(filename, asset);
            if (sw.Elapsed.TotalMilliseconds >= 0.5f)
            {
                yield return null;
                sw.Restart();
            }
        }
        foreach (var filename in asset.GetAllScenePaths())
        {
            BundleLookup.Add(filename, asset);
            if (sw.Elapsed.TotalMilliseconds >= 0.5f)
            {
                yield return null;
                sw.Restart();
            }
        }
        yield return null;
    }

    #if UNITY_EDITOR
    Progress.Report(ID.bundle, 0.99f, "Loaded " + BundleCache.Count + " bundles.");
    Progress.Finish(ID.bundle, Progress.Status.Succeeded);
    #endif

    // Load the GameManifest from the bundles
    Manifest = GetAsset<GameManifest>(ManifestPath); // "assets/manifest.asset"
    if (Manifest == null)
    {
        Debug.LogError("Couldn't load GameManifest.");
        Dispose();
        #if UNITY_EDITOR
        Progress.Finish(ID.parent, Progress.Status.Failed);
        #endif
        yield break;
    }

    // Debug: Verify guidPaths and prefabProperties
    Debug.Log($"Manifest.guidPaths length: {Manifest.guidPaths.Length}");
    Debug.Log($"Manifest.prefabProperties length: {Manifest.prefabProperties.Length}");

    // Populate GuidToPath and PathToGuid from Manifest
    GuidToPath.Clear();
    PathToGuid.Clear();
    foreach (var prop in Manifest.prefabProperties)
    {
        GuidToPath[prop.guid] = prop.name;
        PathToGuid[prop.name] = prop.guid;
    }
    foreach (var guidPath in Manifest.guidPaths)
    {
        if (!GuidToPath.ContainsKey(guidPath.guid))
        {
            GuidToPath[guidPath.guid] = guidPath.name;
            PathToGuid[guidPath.name] = guidPath.guid;
        }
    }

    var setLookups = Task.Run(() =>
    {
        string[] parse;
        string name;
        string monumentTag = "";
        for (uint i = 0; i < Manifest.pooledStrings.Length; ++i)
        {
            IDLookup.Add(Manifest.pooledStrings[i].hash, Manifest.pooledStrings[i].str);
            PathLookup.Add(Manifest.pooledStrings[i].str, Manifest.pooledStrings[i].hash);

            monumentTag = "";

            if (Manifest.pooledStrings[i].str.EndsWith(".png"))
            {
                parse = Manifest.pooledStrings[i].str.Split('/');
                monumentTag = parse[parse.Length - 2]; // Extract the second-to-last element as the monumentTag

                if (!MonumentLayers.ContainsKey(monumentTag))
                {
                    MonumentLayers[monumentTag] = new uint[8];
                }

                int index = GetMapIndex(Manifest.pooledStrings[i].str);
                if (index > -1)
                {
                    MonumentLayers[monumentTag][index] = Manifest.pooledStrings[i].hash;
                    //Debug.Log($"Mapped '{Manifest.pooledStrings[i].str}' to MonumentLayers['{monumentTag}'][{index}] with hash {Manifest.pooledStrings[i].hash}");
                }
            }

            if (Manifest.pooledStrings[i].str.EndsWith(".prefab"))
            {
                if (Manifest.pooledStrings[i].str.Contains("prefabs_small_oilrig"))
                {
                    monumentTag = "oilrig_small/";
                }

                if (Manifest.pooledStrings[i].str.Contains("client")) //dangerous 
                {
                    monumentTag = "EVENT SYSTEMS DISABLED ";
                }

                parse = Manifest.pooledStrings[i].str.Split('/');
                name = parse[parse.Length - 1];
                name = name.Replace(".prefab", "");
                name = monumentTag + name;

                try
                {
                    PrefabLookup.Add(name, Manifest.pooledStrings[i].hash);
                }
                catch
                {
                    // Ignore duplicates silently for now
                }
            }

            if (ToID(Manifest.pooledStrings[i].str) != 0)
            {
                AssetPaths.Add(Manifest.pooledStrings[i].str);
            }

            if (Manifest.pooledStrings[i].str.Contains("autospawn/monument", StringComparison.Ordinal))
            {
                MonumentList.Add(ToID(Manifest.pooledStrings[i].str));
            }
        }
        AssetDump();

    });

    while (!setLookups.IsCompleted)
    {
        if (sw.Elapsed.TotalMilliseconds >= 0.1f)
        {
            yield return null;
            sw.Restart();
        }
    }

}

	//fake
	public static IEnumerator UpdateShader(Material mat, Shader shader)
	{
		yield return null;
	}
	
	public static IEnumerator UpdateShader(Material mat)
	{
		if (mat == null)
		{
			Debug.LogWarning("Material is null. Skipping update.");
			yield break;
		}

		Shader developerShader = Shader.Find("Developer/LocalCoordDiffuse");
		Shader standardShader = Shader.Find("Custom/Rust/Standard");
		Shader standardFourShader = Shader.Find("Custom/Rust/StandardBlend4Way");
		Shader specularShader = Shader.Find("Standard (Specular setup)");
		Shader decalShader = Shader.Find("Legacy Shaders/Decal");
		Shader standardShaderSpecular = Shader.Find("Custom/Rust/StandardSpecular");
		Shader standardShaderBlend = Shader.Find("Custom/Rust/StandardBlendLayer");		
		Shader standardShaderTerrainBlend = Shader.Find("Custom/Rust/StandardTerrainBlendLayer");
		Shader standardDecal = Shader.Find("Custom/Rust/StandardDecal");
		Shader coreFoliage = Shader.Find("Custom/CoreFoliage");
		Shader standardFourSpecularShader = Shader.Find("Custom/Rust/StandardBlend4WaySpecular");
		Shader standardTerrain  = Shader.Find("Custom/Rust/StandardTerrain");
		Shader coreFoliageBillboard = Shader.Find("Custom/CoreFoliageBillboard");
		Shader cliffShader = Shader.Find("Custom/NatureCliff");
		Shader cliffShaderLOD = Shader.Find("Custom/NatureCliff_LOD");
		
		if (mat.shader.name.Contains("Nature/Cliff_LOD"))
		{
			mat.shader = cliffShaderLOD;	
			yield break;
		}	
		
		if (mat.shader.name.Contains("Nature/Cliff"))
		{
			mat.shader = cliffShader;	
			yield break;
		}		
		
		//Debug.Log(mat.name);
		if (mat.name.Equals("concrete_e_sewers_detail")){
			mat.shader = standardFourShader;
			yield break;
		}

		if (mat.shader.name.Equals("Developer/LocalCoord Diffuse (Specular Setup)") || mat.shader.name.Equals("Developer/LocalCoord Diffuse (Metallic Setup)"))
		{
			mat.shader = developerShader;
			yield break;
		}
		
		if (mat.shader.name.Equals("Core/Foliage Billboard"))
		{
			mat.shader = coreFoliageBillboard;
			yield break;
		}
		
		if (mat.shader.name.Contains("Core/Foliage"))
		{
			mat.shader = coreFoliage;	
			yield break;
		}
		
		
		if (mat.shader.name.Equals("Rust/Standard Terrain"))
		{
			mat.shader = standardTerrain;
			yield break;
		}
		
		
		
		if (mat.shader.name.Equals("Standard (Specular setup)") ||mat.shader.name.Equals("Rust/Standard") || mat.shader.name.Equals("Rust/Standard + Wind") || mat.shader.name.Equals("Rust/Standard Cloth")
			|| mat.shader.name.Equals("Rust/Standard Particle") || mat.shader.name.Equals("Rust/Standard Snow Area") || mat.shader.name.Equals("Rust/Standard Wire") || mat.shader.name.Equals("Rust/Standard + Specular Glare") || mat.shader.name.Equals("Rust/Standard Packed Mask Blend"))		{
			mat.shader = standardShader;
			yield break;
		}
		
		if (mat.shader.name.Equals("Rust/Standard Blend 4-Way"))		{
			mat.shader = standardFourShader;
			yield break;
		}
		
		if ( mat.shader.name.Equals("Rust/Standard (Specular setup)") || mat.shader.name.Equals("Rust/Standard + Wind (Specular setup)") || mat.shader.name.Equals("Rust/Standard + Decal (Specular setup)"))		{
			mat.shader = standardShader;
			yield break;
		}
		
		if (mat.shader.name.Equals("Rust/Standard Blend Layer") || mat.shader.name.Equals("Rust/Standard Blend Layer (Specular setup)"))		{
			mat.shader = standardShaderBlend;
			yield break;
		}
		
		if(mat.shader.name.Equals("Rust/Standard Terrain Blend (Specular setup)")){
			mat.shader = standardShaderTerrainBlend;
			yield break;
		}
		
		
		if (mat.shader.name.Equals("Rust/Standard Blend 4-Way (Specular setup)") )
		{
			mat.shader = standardFourSpecularShader;
			yield break;
		}
		
		if (mat.shader.name.Contains("Rust/Standard Decal"))
		{
			mat.shader = standardShader;
			yield break;
		}
		
		if (mat.shader.name.Contains("Nature/Water"))
		{
			mat.shader = standardShaderBlend;
			mat.SetOverrideTag("RenderType", "Transparent");
			yield break;
		}
		

		

		/*
		if (mat.shader.name.Contains("Decal"))
		{
			mat.shader = decalShader;			
			mat.SetOverrideTag("RenderType", "TransparentCutout");
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
			mat.SetInt("_ZWrite", 1);
			mat.EnableKeyword("_ALPHATEST_ON");
			mat.DisableKeyword("_ALPHABLEND_ON");
			mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			mat.SetFloat("_Mode", 1f);
			mat.SetFloat("_Cutoff", 0.5f); // Default cutoff
			mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
			yield break;
		}
		*/
		
		
		//int renderQueue = mat.renderQueue;

		// Determine mode based on render queue
		/*
		if (renderQueue <= (int)UnityEngine.Rendering.RenderQueue.Geometry) // 2000
		{
			mat.shader = standardShader;
			mat.SetOverrideTag("RenderType", "");
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
			mat.SetInt("_ZWrite", 1);
			mat.DisableKeyword("_ALPHATEST_ON");
			mat.DisableKeyword("_ALPHABLEND_ON");
			mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			mat.SetFloat("_Mode", 0f);
			mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
			mat.SetFloat("_Metallic", 0.25f); 
			mat.SetFloat("_Glossiness", 0.25f);
		}
		
		
		if (renderQueue > (int)UnityEngine.Rendering.RenderQueue.Geometry &&
				 renderQueue <= 2450) // Transparent Cutout range
		{
			mat.shader = standardShader;
			mat.SetOverrideTag("RenderType", "TransparentCutout");
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
			mat.SetInt("_ZWrite", 1);
			mat.EnableKeyword("_ALPHATEST_ON");
			mat.DisableKeyword("_ALPHABLEND_ON");
			mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			mat.SetFloat("_Mode", 1f);
			mat.SetFloat("_Cutoff", 0.5f); // Default cutoff
			mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
		}
		*/
		
		/*
		if (renderQueue > 2450) // Transparent range
		{
			mat.shader =  standardShader;
			mat.SetOverrideTag("RenderType", "Transparent");
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			mat.SetInt("_ZWrite", 0);
			mat.DisableKeyword("_ALPHATEST_ON");
			mat.EnableKeyword("_ALPHABLEND_ON");
			mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			mat.SetFloat("_Mode", 2f);
			mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
		}
		else
		{
			//Debug.LogWarning($"Material {mat.name} has an unsupported render queue: {renderQueue}. No changes applied.");
			yield break;
		}
		*/

		//Debug.Log($"Material '{mat.name}' updated successfully. Mode: {mat.GetFloat("_Mode")}, Render Queue: {mat.renderQueue}");
		yield return null;
	}

		private static int GetMapIndex(string fileName)
		{
			if (fileName.Contains("heighttexture")) return 0;
			if (fileName.Contains("splattexture0")) return 1;
			if (fileName.Contains("splattexture1")) return 2;
			if (fileName.Contains("alphatexture")) return 3;
			if (fileName.Contains("biometexture")) return 4;
			if (fileName.Contains("topologytexture")) return 5;
			if (fileName.Contains("watermap")) return 6;
			if (fileName.Contains("watertexture")) return 7;
			return -1; // Invalid index if no match
		}


		private static void SetKeyword(Material mat, string keyword, bool state)
		{
			if (state)
				mat.EnableKeyword(keyword);
			else
				mat.DisableKeyword(keyword);
		}
	}
}