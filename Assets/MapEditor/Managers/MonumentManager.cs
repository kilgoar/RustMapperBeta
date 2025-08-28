using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static WorldConverter;
using static WorldSerialization;
using static TerrainManager;
using ProtoBuf;
using ProtoBuf.Meta;

public static class MonumentManager
{
    public static RMPrefabData CurrentRMPrefab { get; private set; } = new RMPrefabData
    {
        monument = new RMMonument
        {
            SplatMask = (TerrainSplat.Enum)0,
            BiomeMask = (TerrainBiome.Enum)0,
            TopologyMask = (TerrainTopology.Enum)0,
            HeightMap = false,
            AlphaMap = false,
            WaterMap = false
        }
    };

    public static void Reset()
    {
        CurrentRMPrefab = new RMPrefabData
        {
            monument = new RMMonument
            {
                SplatMask = (TerrainSplat.Enum)0,
                BiomeMask = (TerrainBiome.Enum)0,
                TopologyMask = (TerrainTopology.Enum)0,
                HeightMap = true,
                AlphaMap = true,
                WaterMap = true
            },
            modifiers = new ModifierData(),
            prefabs = new List<PrefabData>(),
            checksum = null
        };
    }

	public static void SaveMonument(string path)
	{
		try
		{
			string name = path.Split('/').Last().Split('.')[0];
			Debug.Log($"Attempting to save monument: {name}");

			// Rename prefabs and NPCs with the monument name
			PrefabManager.RenamePrefabCategories(PrefabManager.CurrentMapPrefabs, ":" + name + "::");
			PrefabManager.RenameNPCs(PrefabManager.CurrentMapNPCs, ":" + name + "::");

			// Create WorldSerialization with CurrentRMPrefab data
			WorldSerialization world = TerrainToRMPrefab(TerrainManager.Land, TerrainManager.Water);

			// Save the prefab
			world.SaveRMPrefab(path);
			Debug.Log($"Monument saved successfully to {path}");

			// Update CurrentRMPrefab to reflect the saved state
			CurrentRMPrefab = world.rmPrefab;
			
			DebugRMPrefabReadout(CurrentRMPrefab);

		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to save monument to {path}: {ex.Message}");
		}
	}

	public static void LoadMonument(string loadPath){
		CoroutineManager.Instance.StartRuntimeCoroutine(LoadMonumentCoroutine(loadPath));
	}

    public static IEnumerator LoadMonumentCoroutine(string loadPath)    {

			yield return PrefabManager.DeletePrefabs(PrefabManager.CurrentMapPrefabs);
			PrefabManager.DeleteCircuits(PrefabManager.CurrentMapElectrics, 0);
			PrefabManager.DeleteNPCs(PrefabManager.CurrentMapNPCs, 0);
			PrefabManager.DeleteModifiers(PrefabManager.CurrentModifiers);
			PathManager.DeletePaths(PathManager.CurrentMapPaths);

			//load terrains to editor
			var world = new WorldSerialization();
			world.LoadRMPrefab(loadPath);
			MapInfo terrainInfo = RMPrefabToTerrain(world);
			TerrainManager.Load(terrainInfo, 0);

			DebugRMPrefabReadout(CurrentRMPrefab);
			AppManager.Instance.ActivateWindow(6); //open terrain window
			TerrainWindow.Instance.LoadMonumentToggles();
			TerrainWindow.Instance.OnToggleChanged(6); //show monument tab
			
			//apply CurrentRMPrefab to TerrainWindow
			
			// Center scene objects
			MapManager.CentreSceneObjects(terrainInfo);
			// Spawn prefabs, circuits, NPCs, and modifiers
			PrefabManager.SpawnPrefabs(terrainInfo.prefabData, 0);
			PrefabManager.SpawnCircuits(terrainInfo.circuitData, 0);
			PrefabManager.SpawnNPCs(terrainInfo.npcData, 0);
			PrefabManager.SpawnModifiers(terrainInfo.modifierData);	
    }

    public static void ApplyToMonument(RMMonument monument)
    {
        if (monument != null)
        {
            monument.SplatMask = CurrentRMPrefab.monument.SplatMask;
            monument.BiomeMask = CurrentRMPrefab.monument.BiomeMask;
            monument.TopologyMask = CurrentRMPrefab.monument.TopologyMask;
            monument.HeightMap = CurrentRMPrefab.monument.HeightMap;
            monument.WaterMap = CurrentRMPrefab.monument.WaterMap;
            monument.AlphaMap = CurrentRMPrefab.monument.AlphaMap;
        }
    }

	public static void LoadREPrefab(string loadPath)
	{
		CoroutineManager.Instance.StartRuntimeCoroutine(LoadREPrefabCoroutine(loadPath));
	}

	public static IEnumerator LoadREPrefabCoroutine(string loadPath)
	{
		// Clear existing scene objects
		yield return PrefabManager.DeletePrefabs(PrefabManager.CurrentMapPrefabs);
		PrefabManager.DeleteCircuits(PrefabManager.CurrentMapElectrics, 0);
		PrefabManager.DeleteNPCs(PrefabManager.CurrentMapNPCs, 0);
		PrefabManager.DeleteModifiers(PrefabManager.CurrentModifiers);
		PathManager.DeletePaths(PathManager.CurrentMapPaths);

		// Load REPrefab from file
		var world = new WorldSerialization();
		world.LoadREPrefab(loadPath);
		MapInfo terrainInfo = REPrefabToTerrain(world, loadPath);
		TerrainManager.Load(terrainInfo, 0);

		// Debug readout of the converted RMPrefabData
		DebugRMPrefabReadout(CurrentRMPrefab);

		// Center scene objects
		MapManager.CentreSceneObjects(terrainInfo);

		// Spawn prefabs, circuits, NPCs, and modifiers
		PrefabManager.SpawnPrefabs(terrainInfo.prefabData, 0);
		PrefabManager.SpawnCircuits(terrainInfo.circuitData, 0);
		PrefabManager.SpawnNPCs(terrainInfo.npcData, 0);
		PrefabManager.SpawnModifiers(terrainInfo.modifierData);

		yield return null;
	}
	
public static MapInfo REPrefabToTerrain(WorldSerialization world, string loadPath)
{
    MapInfo terrain = new MapInfo();

    try
    {
        if (world == null || world.rePrefab == null)
        {
            Debug.LogError("WorldSerialization or REPrefabData is null. Returning empty MapInfo.");
            return terrain;
        }

        REPrefabData rePrefab = world.rePrefab;
		Debug.Log("reprefab world loaded");

        // Convert REPrefabData to RMPrefabData
        RMPrefabData rmPrefab = new RMPrefabData
        {
            modifiers = rePrefab.modifiers ?? new ModifierData(),
            prefabs = rePrefab.prefabs ?? new List<PrefabData>(),
            circuits = rePrefab.circuits,
            npcs = rePrefab.npcs,
            emptychunk1 = rePrefab.emptychunk1,
            emptychunk3 = rePrefab.emptychunk3,
            emptychunk4 = rePrefab.emptychunk4,
            buildingchunk = rePrefab.buildingchunk,
            checksum = rePrefab.checksum,
            monument = new RMMonument
            {
                SplatMask = TerrainSplat.Enum.Grass,
                BiomeMask = TerrainBiome.Enum.Temperate,
                TopologyMask = TerrainTopology.Enum.Field,
                HeightMap = true,
                AlphaMap = true,
                WaterMap = true
            }
        };
		
		Debug.Log("reprefab object converted to rmprefab");

            // Initialize terrain-related fields with temporary defaults
            terrain.size = new Vector3(rmPrefab.modifiers.size + rmPrefab.modifiers.fade, 1000f, rmPrefab.modifiers.size + rmPrefab.modifiers.fade);
            terrain.terrainRes = 1;
            terrain.splatRes = 1;
            terrain.water.heights = new float[terrain.terrainRes, terrain.terrainRes];
            terrain.splatMap = new float[terrain.splatRes, terrain.splatRes, 8];
            terrain.biomeMap = new float[terrain.splatRes, terrain.splatRes, 5];
            terrain.alphaMap = new bool[terrain.splatRes, terrain.splatRes];
            terrain.topology = new TerrainMap<int>(new byte[terrain.splatRes * terrain.splatRes * 4], 1);
			
		Debug.Log("terrains initialized");
			
        // Copy extensible fields by serializing and deserializing the entire object
        using (var memoryStream = new System.IO.MemoryStream())
        {
            // Serialize REPrefabData to capture all fields (including extensible ones)
            ProtoBuf.Serializer.Serialize(memoryStream, rePrefab);
            memoryStream.Position = 0;
            // Deserialize into RMPrefabData, merging with existing fields
            ProtoBuf.Serializer.Merge(memoryStream, rmPrefab);
            Debug.Log("Successfully merged extensible fields from REPrefabData to RMPrefabData.");
        }

        // Set default size and extents for the monument
        float defaultSize = rmPrefab.modifiers?.size ?? 1000f;
        rmPrefab.monument.size = new Vector3(defaultSize + (rmPrefab.modifiers?.fade ?? 0f), 1000f, defaultSize + (rmPrefab.modifiers?.fade ?? 0f));
        rmPrefab.monument.extents = new Vector3(rmPrefab.monument.size.x / 2f, rmPrefab.monument.size.y / 2f, rmPrefab.monument.size.z / 2f);
        rmPrefab.monument.offset = Vector3.zero;

        // Log modifier data
        Debug.Log($"ModifierData Loaded: size={rmPrefab.modifiers.size}, fade={rmPrefab.modifiers.fade}, " +
                  $"fill={rmPrefab.modifiers.fill}, counter={rmPrefab.modifiers.counter}, id={rmPrefab.modifiers.id}");

        // Load terrain data from associated files if loadPath is provided
        if (!string.IsNullOrEmpty(loadPath))
        {
            string basePath = loadPath;

                // Load and process heightmap
                string heightPath = basePath + ".heights";
                Debug.Log(heightPath + " is height texture file");
                if (File.Exists(heightPath))
                {
                    byte[] heightData = File.ReadAllBytes(heightPath);
                    Texture2D heightTexture = WorldSerialization.DeserializeTexture(heightData, TextureFormat.RGBA32);

                    if (heightTexture != null)
                    {
                        Debug.Log($"Loaded height texture of size {heightTexture.width}x{heightTexture.height}");
                        // Decode heightmap to float[,] first
                        float[,] heightArray = DecodeHeightmap(heightTexture, terrain.size, terrain.size / 2f, heightTexture.width, heightTexture.height, new Vector3(0f, 0.5f, 0f));
                        UnityEngine.Object.Destroy(heightTexture);

                        // Calculate next power of 2 plus 1 for heightmap resolution
                        terrain.terrainRes = Mathf.NextPowerOfTwo(heightArray.GetLength(0) - 1) + 1;
                        Debug.Log($"Upscaling heightmap array to resolution: {terrain.terrainRes}x{terrain.terrainRes}");

                        // Resize height array
                        terrain.land.heights = ResizeHeightArray(heightArray, terrain.terrainRes);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to deserialize heightmap from {heightPath}.");
                    }
                }
                
			//blendmap is always empty because rustedit

                string splat0Path = basePath + ".splat0";
                string splat1Path = basePath + ".splat1";
                if (File.Exists(splat0Path) && File.Exists(splat1Path))
                {
                    byte[] splat0Data = File.ReadAllBytes(splat0Path);
                    byte[] splat1Data = File.ReadAllBytes(splat1Path);
                    Texture2D splat0Texture = WorldSerialization.DeserializeTexture(splat0Data, TextureFormat.RGBA32);
                    Texture2D splat1Texture = WorldSerialization.DeserializeTexture(splat1Data, TextureFormat.RGBA32);

                    if (splat0Texture != null && splat1Texture != null)
                    {
                        // Validate texture data
                        Color[] splat0Pixels = splat0Texture.GetPixels();
                        //bool hasValidSplat0Data = splat0Pixels.Any(c => c.r > 0 || c.g > 0 || c.b > 0 || c.a > 0);
                        //Debug.Log($"Splat0 texture has valid data: {hasValidSplat0Data}");

                        // Calculate next power of 2 for splat resolution
                        terrain.splatRes = Mathf.NextPowerOfTwo(splat0Texture.width);
                        Debug.Log($"Upscaling splatmaps to resolution: {terrain.splatRes}x{terrain.splatRes}");

                        // Resize splat textures
                        Texture2D resizedSplat0Texture = ResizeTexture(splat0Texture, terrain.splatRes, TextureFormat.RGBA32);
                        Texture2D resizedSplat1Texture = ResizeTexture(splat1Texture, terrain.splatRes, TextureFormat.RGBA32);

                        // Validate resized texture data
                        Color[] resizedSplat0Pixels = resizedSplat0Texture.GetPixels();
                        //bool hasValidResizedSplat0Data = resizedSplat0Pixels.Any(c => c.r > 0 || c.g > 0 || c.b > 0 || c.a > 0);
                        //Debug.Log($"Resized splat0 texture has valid data: {hasValidResizedSplat0Data}");

                        terrain.splatMap = CombineSplatMaps(resizedSplat0Texture, resizedSplat1Texture);
                        UnityEngine.Object.Destroy(splat0Texture);
                        UnityEngine.Object.Destroy(splat1Texture);
                        UnityEngine.Object.Destroy(resizedSplat0Texture);
                        UnityEngine.Object.Destroy(resizedSplat1Texture);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to deserialize splatmaps from {splat0Path} or {splat1Path}.");
                    }
                }

                // Load and resize biomemap
                string biomePath = basePath + ".biome";
                if (File.Exists(biomePath))
                {
                    byte[] biomeData = File.ReadAllBytes(biomePath);
                    Texture2D biomeTexture = WorldSerialization.DeserializeTexture(biomeData, TextureFormat.RGBA32);
                    if (biomeTexture != null)
                    {
                        Debug.Log($"Upscaling biomemap to resolution: {terrain.splatRes}x{terrain.splatRes}");
                        Texture2D resizedBiomeTexture = ResizeTexture(biomeTexture, terrain.splatRes, TextureFormat.RGBA32);
                        terrain.biomeMap = TextureToSplatMap(resizedBiomeTexture, 5);
                        UnityEngine.Object.Destroy(biomeTexture);
                        UnityEngine.Object.Destroy(resizedBiomeTexture);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to deserialize biomemap from {biomePath}.");
                    }
                }

                // Load and resize alphamap
                string alphaPath = basePath + ".alpha";
                if (File.Exists(alphaPath))
                {
                    byte[] alphaData = File.ReadAllBytes(alphaPath);
                    RenderTexture alphaTexture = WorldSerialization.DeserializeTexture(alphaData, RenderTextureFormat.ARGB32);
                    if (alphaTexture != null)
                    {
                        Debug.Log($"Upscaling alphamap to resolution: {terrain.splatRes}x{terrain.splatRes}");
                        RenderTexture resizedAlphaTexture = ResizeRenderTexture(alphaTexture, terrain.splatRes);
                        terrain.alphaMap = TextureToAlphaMap(resizedAlphaTexture, true);
                        UnityEngine.Object.Destroy(alphaTexture);
                        UnityEngine.Object.Destroy(resizedAlphaTexture);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to deserialize alphamap from {alphaPath}.");
                    }
                }


                // Load and resize topologymap
                string topologyPath = basePath + ".topology";
                if (File.Exists(topologyPath))
                {
                    byte[] topologyData = File.ReadAllBytes(topologyPath);
                    Texture2D topologyTexture = WorldSerialization.DeserializeTexture(topologyData, TextureFormat.RGBA32);
                    if (topologyTexture != null)
                    {
                        Debug.Log($"Upscaling topologymap to resolution: {terrain.splatRes}x{terrain.splatRes}");
                        Texture2D resizedTopologyTexture = ResizeTexture(topologyTexture, terrain.splatRes, TextureFormat.RGBA32);
                        terrain.topology = TextureToTopologyMap(resizedTopologyTexture, terrain.splatRes);
                        UnityEngine.Object.Destroy(topologyTexture);
                        UnityEngine.Object.Destroy(resizedTopologyTexture);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to deserialize topologymap from {topologyPath}.");
                    }
                }

            // Load watermap
            string waterPath = basePath + ".water";
            if (File.Exists(waterPath))
            {
                rmPrefab.monument.watermap = File.ReadAllBytes(waterPath);
                Debug.Log($"Loaded watermap from {waterPath}, size: {rmPrefab.monument.watermap.Length} bytes");
            }


        }
		
		terrain.prefabData = rePrefab.prefabs?.ToArray() ?? new PrefabData[0];
        // Debug readout of the converted RMPrefabData
        DebugRMPrefabReadout(rmPrefab);

        // Update CurrentRMPrefab
        CurrentRMPrefab = rmPrefab;

        return terrain;
    }
    catch (Exception err)
    {
        Debug.LogError($"Error during REPrefabToTerrain conversion: {err.Message}");
        return terrain;
    }
}

    // Resize a Texture2D using bilinear filtering
    private static Texture2D ResizeTexture(Texture2D source, int targetSize, TextureFormat format)
    {
        RenderTexture renderTex = new RenderTexture(targetSize, targetSize, 0, RenderTextureFormat.ARGB32);
        renderTex.filterMode = FilterMode.Bilinear;
        RenderTexture.active = renderTex;

        source.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, renderTex);

        Texture2D result = new Texture2D(targetSize, targetSize, format, false);
        result.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(renderTex);
        return result;
    }
	
	    // Resize a RenderTexture (for alphamap)
    private static RenderTexture ResizeRenderTexture(RenderTexture source, int targetSize)
    {
        RenderTexture result = new RenderTexture(targetSize, targetSize, 0, RenderTextureFormat.ARGB32);
        result.filterMode = FilterMode.Bilinear;
        RenderTexture.active = result;

        source.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, result);

        RenderTexture.active = null;
        return result;
    }
	
	public static bool[,] TextureToAlphaMap(RenderTexture texture, bool RE = false)
	{
		int width = texture.width;
		bool[,] alphaMap = new bool[width, width];

		Texture2D tempTexture = new Texture2D(width, width, TextureFormat.RGBA32, false);
		RenderTexture.active = texture;
		tempTexture.ReadPixels(new Rect(0, 0, width, width), 0, 0);
		tempTexture.Apply();
		RenderTexture.active = null;

		Color[] pixels = tempTexture.GetPixels();
		UnityEngine.Object.Destroy(tempTexture);

		for (int y = 0; y < width; y++)
		{
			for (int x = 0; x < width; x++)
			{
				if(!RE){
				alphaMap[y, x] = pixels[y * width + x].r > 0.5f;
				continue;
				}
				alphaMap[y, x] = pixels[y * width + x].a > 0.5f;
			}
		}
		return alphaMap;
	}

public static WorldSerialization TerrainToRMPrefab(Terrain land, Terrain water)
{
	RMPrefabData currentPrefab = CloneRMPrefabData(CurrentRMPrefab);
    WorldSerialization worldSerialization = new WorldSerialization();
    try
    {
        // Initialize with CurrentRMPrefab to preserve existing data
        RMPrefabData rmPrefab = CloneRMPrefabData(currentPrefab);

				
        // Validate input terrains
        if (land == null || land.terrainData == null)
        {
            Debug.LogError("Land terrain or its data is null. Cannot serialize RMPrefab.");
            return worldSerialization;
        }
        if (water == null || water.terrainData == null)
        {
            Debug.LogWarning("Water terrain or its data is null. Watermap serialization skipped.");
        }

        // Process modifiers, prefabs, NPCs, and circuits
        if (PrefabManager.CurrentModifiers?.modifierData != null)
            rmPrefab.modifiers = PrefabManager.CurrentModifiers.modifierData;

        rmPrefab.prefabs.Clear();
        foreach (PrefabDataHolder p in PrefabManager.CurrentMapPrefabs)
        {
            if (p.prefabData != null)
            {
                p.AlwaysBreakPrefabs();
                rmPrefab.prefabs.Add(p.prefabData);
            }
        }

        // Compute height range
        float[,] heights = land.terrainData.GetHeights(0, 0, HeightMapRes, HeightMapRes);
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        for (int y = 0; y < HeightMapRes; y++)
        {
            for (int x = 0; x < HeightMapRes; x++)
            {
                float height = heights[y, x] * land.terrainData.size.y;
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
        }

        float lowOff = 500f - minHeight;
        float padding = 20f;
        minHeight -= padding;
        maxHeight += padding;
        float heightRange = maxHeight - minHeight;

        // Update monument fields, preserving UI-set values
        rmPrefab.monument.size = new Vector3(land.terrainData.size.x, heightRange, land.terrainData.size.z);
        rmPrefab.monument.extents = new Vector3(land.terrainData.size.x / 2f, heightRange / 2f, land.terrainData.size.z / 2f);
        rmPrefab.monument.offset = new Vector3(0f, -padding - lowOff, 0f);

        // Texture resolution
        int textureResolution = SplatMapRes;
        int heightMapRes = HeightMapRes;

        // Process alphamaps
        Texture2D[] alphamapTextures = land.terrainData.alphamapTextures;
        if (alphamapTextures == null || alphamapTextures.Length < 2)
        {
            Debug.LogError("Terrain alphamap textures (Control0 and Control1) are not available.");
        }
        else
        {
            rmPrefab.monument.splatmap0 = WorldSerialization.SerializeTexture(alphamapTextures[0]);
            rmPrefab.monument.splatmap1 = WorldSerialization.SerializeTexture(alphamapTextures[1]);
        }

        // Process biome, alpha, blend, and topology maps
        if (TerrainManager.BiomeTexture == null || TerrainManager.Biome1Texture == null)
        {
            Debug.LogError("BiomeTexture or Biome1Texture is not initialized.");
        }
        else
        {
            rmPrefab.monument.biomemap = WorldSerialization.SerializeTexture(TerrainManager.BiomeTexture);
        }

        if (TerrainManager.AlphaTexture == null)
        {
            Debug.LogError("AlphaTexture is not initialized.");
        }
        else
        {
            rmPrefab.monument.alphamap = WorldSerialization.SerializeTexture(TerrainManager.AlphaTexture);
        }

        rmPrefab.monument.blendmap = WorldSerialization.SerializeTexture(TerrainManager.BlendMapTexture);

        TopologyData.UpdateTexture();
        rmPrefab.monument.topologymap = WorldSerialization.SerializeTexture(TopologyData.TopologyTexture);

        // Process heightmap
        rmPrefab.monument.heightmap = EncodeHeightMap(
            land.terrainData.GetHeights(0, 0, heightMapRes, heightMapRes),
            heightMapRes,
            "HeightMapTexture",
            minHeight,
            heightRange
        );

        // Process watermap if enabled
        if (rmPrefab.monument.WaterMap && water != null && water.terrainData != null)
        {
            float[,] waterHeights = water.terrainData.GetHeights(0, 0, heightMapRes, heightMapRes);
            float minWaterHeight = float.MaxValue;
            float maxWaterHeight = float.MinValue;
            for (int y = 0; y < heightMapRes; y++)
            {
                for (int x = 0; x < heightMapRes; x++)
                {
                    float height = waterHeights[y, x] * water.terrainData.size.y;
                    minWaterHeight = Mathf.Min(minWaterHeight, height);
                    maxWaterHeight = Mathf.Max(maxWaterHeight, height);
                }
            }
            float waterHeightRange = maxWaterHeight - minWaterHeight;
            rmPrefab.monument.watermap = EncodeHeightMap(
                waterHeights,
                heightMapRes,
                "WaterMapTexture",
                minWaterHeight,
                waterHeightRange
            );
        }

        worldSerialization.rmPrefab = rmPrefab;
        CurrentRMPrefab = rmPrefab; // Update static data
        return worldSerialization;
    }
    catch (Exception err)
    {
        Debug.LogError($"Error during RMPrefab conversion: {err.Message}");
        return worldSerialization;
    }
}

// Helper method to deep clone RMPrefabData
private static RMPrefabData CloneRMPrefabData(RMPrefabData source)
{
    if (source == null) return new RMPrefabData();

    var clone = new RMPrefabData
    {
        monument = new RMMonument
        {
            SplatMask = source.monument?.SplatMask ?? TerrainSplat.Enum.Grass,
            BiomeMask = source.monument?.BiomeMask ?? TerrainBiome.Enum.Temperate,
            TopologyMask = source.monument?.TopologyMask ?? TerrainTopology.Enum.Field,
            HeightMap = source.monument?.HeightMap ?? true,
            AlphaMap = source.monument?.AlphaMap ?? true,
            WaterMap = source.monument?.WaterMap ?? true,
            size = source.monument?.size ?? Vector3.zero,
            extents = source.monument?.extents ?? Vector3.zero,
            offset = source.monument?.offset ?? Vector3.zero,
            heightmap = source.monument?.heightmap,
            splatmap0 = source.monument?.splatmap0,
            splatmap1 = source.monument?.splatmap1,
            biomemap = source.monument?.biomemap,
            alphamap = source.monument?.alphamap,
            blendmap = source.monument?.blendmap,
            topologymap = source.monument?.topologymap,
            watermap = source.monument?.watermap
        },
        modifiers = source.modifiers != null ? new ModifierData
        {
            size = source.modifiers.size,
            fade = source.modifiers.fade,
            fill = source.modifiers.fill,
            counter = source.modifiers.counter,
            id = source.modifiers.id
        } : new ModifierData(),
        prefabs = source.prefabs != null ? new List<PrefabData>(source.prefabs) : new List<PrefabData>(),
        npcs = source.npcs != null ? (byte[])source.npcs.Clone() : null,
        circuits = source.circuits != null ? (byte[])source.circuits.Clone() : null,
        checksum = source.checksum != null ? (string)source.checksum.Clone() : null,
        emptychunk1 = source.emptychunk1 != null ? (byte[])source.emptychunk1.Clone() : null,
        emptychunk3 = source.emptychunk3 != null ? (byte[])source.emptychunk3.Clone() : null,
        emptychunk4 = source.emptychunk4 != null ? (byte[])source.emptychunk4.Clone() : null,
        buildingchunk = source.buildingchunk != null ? (byte[])source.buildingchunk.Clone() : null
    };

    // Handle ProtoBuf extensible fields
    if (source is ProtoBuf.IExtensible extensible)
    {
        var extensionObject = extensible.GetExtensionObject(false);
        if (extensionObject != null)
        {
            using (var memoryStream = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(memoryStream, source);
                memoryStream.Position = 0;
                ProtoBuf.Serializer.Merge(memoryStream, clone);
            }
        }
    }

    return clone;
}

    public static MapInfo RMPrefabToTerrain(WorldSerialization world, bool doTerrains = true)
    {
        MapInfo terrain = new MapInfo();

        try
        {
            if (world.rmPrefab == null || world.rmPrefab.monument == null)
            {
                Debug.LogError("RMPrefabData or RMMonument is null. Returning empty MapInfo.");
                return terrain;
            }

            RMMonument monument = world.rmPrefab.monument;

            // Set basic MapInfo fields
            terrain.size = monument.size;
            terrain.size.y = 1000f;
            terrain.terrainRes = 1;
            terrain.splatRes = 1;

            // Initialize arrays with defaults
            terrain.water.heights = new float[terrain.terrainRes, terrain.terrainRes];
            terrain.splatMap = new float[terrain.splatRes, terrain.splatRes, 8];
            terrain.biomeMap = new float[terrain.splatRes, terrain.splatRes, 5];
            terrain.alphaMap = new bool[terrain.splatRes, terrain.splatRes];
            terrain.topology = new TerrainMap<int>(new byte[terrain.splatRes * terrain.splatRes * 4], 1);

            // Load heightmap
            if (monument.heightmap != null)
            {
                Texture2D heightTexture = WorldSerialization.DeserializeTexture(monument.heightmap, TextureFormat.RGBA32);
                if (heightTexture != null)
                {
                    terrain.terrainRes = heightTexture.width;
                    Vector3 extents = monument.extents;
                    terrain.land.heights = DecodeHeightmap(heightTexture, monument.size, extents, terrain.terrainRes, terrain.terrainRes, monument.offset);
                    UnityEngine.Object.Destroy(heightTexture);
                }
            }

            // Load blendmap
            if (monument.blendmap != null)
            {
                Texture2D blendTexture = WorldSerialization.DeserializeTexture(monument.blendmap, TextureFormat.RGBA32);
                if (blendTexture != null)
                {
                    terrain.blendMap = blendTexture;
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize blendmap texture.");
                }
            }

            // Load splatmaps
            if (monument.splatmap0 != null && monument.splatmap1 != null)
            {
                Texture2D splat0Texture = WorldSerialization.DeserializeTexture(monument.splatmap0, TextureFormat.RGBA32);
                Texture2D splat1Texture = WorldSerialization.DeserializeTexture(monument.splatmap1, TextureFormat.RGBA32);
                if (splat0Texture != null && splat1Texture != null)
                {
                    terrain.splatRes = splat0Texture.width;
                    terrain.splatMap = CombineSplatMaps(splat0Texture, splat1Texture);
                    UnityEngine.Object.Destroy(splat0Texture);
                    UnityEngine.Object.Destroy(splat1Texture);
                }
            }

            // Load biomemap
            if (monument.biomemap != null)
            {
                Texture2D biomeTexture = WorldSerialization.DeserializeTexture(monument.biomemap, TextureFormat.RGBA32);
                if (biomeTexture != null)
                {
                    terrain.biomeMap = TextureToSplatMap(biomeTexture, 5);
                    UnityEngine.Object.Destroy(biomeTexture);
                }
            }

        // Load alphamap
        if (monument.alphamap != null)
        {
            RenderTexture alphaTexture = WorldSerialization.DeserializeTexture(monument.alphamap, RenderTextureFormat.ARGB32);
            if (alphaTexture != null)
            {
                terrain.alphaMap = TextureToAlphaMap(alphaTexture);
                UnityEngine.Object.Destroy(alphaTexture);
            }
        }

        // Load topologymap
        if (monument.topologymap != null)
        {
            Texture2D topologyTexture = WorldSerialization.DeserializeTexture(monument.topologymap, TextureFormat.RGBA32);
            if (topologyTexture != null)
            {
                terrain.topology = TextureToTopologyMap(topologyTexture, terrain.splatRes);
                UnityEngine.Object.Destroy(topologyTexture);
            }
        }
		
		//TerrainManager.Load(terrain,0);
        TopologyData.UpdateTexture();
        terrain.prefabData = world.rmPrefab.prefabs.ToArray();
        CurrentRMPrefab = world.rmPrefab;

        return terrain;
    }
    catch (Exception err)
    {
        Debug.LogError($"Error during RMPrefabToTerrain conversion: {err.Message}");
        return terrain;
    }
}

private static byte[] EncodeHeightMap(float[,] heights, int resolution, string textureName, float minHeight, float heightRange)
{
    Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
    {
        name = textureName,
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear
    };
    Color[] pixels = new Color[resolution * resolution];
    for (int y = 0; y < resolution; y++)
    {
        for (int x = 0; x < resolution; x++)
        {
            float worldHeight = heights[y, x] * TerrainManager.Land.terrainData.size.y; // Convert to world-space height
            float normalizedHeight = (worldHeight - minHeight) / heightRange; // Normalize to [0,1]
            pixels[y * resolution + x] = BitUtility.EncodeHeight(normalizedHeight);
        }
    }
    texture.SetPixels(pixels);
    texture.Apply();
    byte[] result = WorldSerialization.SerializeTexture(texture);
    UnityEngine.Object.Destroy(texture);
    return result;
}

private static float[,] DecodeHeightmap(Texture2D heightTexture, Vector3 size, Vector3 extents, int width, int height, Vector3 offset)
{
    if (heightTexture == null || !heightTexture.isReadable)
    {
        Debug.Log("heightTexture is null or not readable");
        return new float[height, width];
    }

    float[,] regionHeights = new float[height, width];

    for (int x = 0; x < width; x++)
    {
        for (int z = 0; z < height; z++)
        {
            // Normalized UV coordinates for sampling the texture
            float u = Mathf.Clamp01((float)x / (width - 1));
            float v = Mathf.Clamp01((float)z / (height - 1));

            // Sample the heightmap bilinearly
            float combinedHeight = BitUtility.SampleHeightBilinear(heightTexture, u, v);

            // Debug the raw sampled value and final height
            if (x == 0 && z == 0 || x == width / 2 && z == height / 2)
            {
                Debug.Log($"UV ({u}, {v}): Raw Sampled Height={combinedHeight}, Scaled Height={combinedHeight * size.y}");
            }

            // Scale by the vertical size to get the decoded height value
            regionHeights[z, x] = (combinedHeight * size.y) / 1000f + 0.5f + (offset.y / 1000f);
        }
    }

    // Log a sample of the final heights
    Debug.Log($"Decoded Heights Sample: [0,0]={regionHeights[0, 0]}, [{height/2},{width/2}]={regionHeights[height/2, width/2]}");
    return regionHeights;
}

private static float[,,] CombineSplatMaps(Texture2D splat0, Texture2D splat1)
{
    int width = splat0.width;
    float[,,] splatMap = new float[width, width, 8];
    Color[] splat0Pixels = splat0.GetPixels();
    Color[] splat1Pixels = splat1.GetPixels();

    for (int y = 0; y < width; y++)
    {
        for (int x = 0; x < width; x++)
        {
            int idx = y * width + x;
            splatMap[y, x, 0] = splat0Pixels[idx].r;
            splatMap[y, x, 1] = splat0Pixels[idx].g;
            splatMap[y, x, 2] = splat0Pixels[idx].b;
            splatMap[y, x, 3] = splat0Pixels[idx].a;
            splatMap[y, x, 4] = splat1Pixels[idx].r;
            splatMap[y, x, 5] = splat1Pixels[idx].g;
            splatMap[y, x, 6] = splat1Pixels[idx].b;
            splatMap[y, x, 7] = splat1Pixels[idx].a;
        }
    }
    return splatMap;
}

    public static void DebugMonumentReadout(RMMonument monument)
    {
        if (monument == null)
        {
            Debug.LogWarning("RMMonument is null. No debug information to display.");
            return;
        }

        Debug.Log("=== RMMonument Debug Readout ===");

        // Simple fields
        Debug.Log($"SplatMask: {monument.SplatMask}");
        Debug.Log($"BiomeMask: {monument.BiomeMask}");
        Debug.Log($"TopologyMask: {monument.TopologyMask}");
        Debug.Log($"HeightMap (enabled): {monument.HeightMap}");
        Debug.Log($"AlphaMap (enabled): {monument.AlphaMap}");
        Debug.Log($"WaterMap (enabled): {monument.WaterMap}");
        Debug.Log($"Size: {monument.size}");
        Debug.Log($"Extents: {monument.extents}");
        Debug.Log($"Offset: {monument.offset}");

        // Array fields with size information
        Debug.Log($"Heightmap Size: {(monument.heightmap != null ? monument.heightmap.Length : 0)} bytes");
        Debug.Log($"Splatmap0 Size: {(monument.splatmap0 != null ? monument.splatmap0.Length : 0)} bytes");
        Debug.Log($"Splatmap1 Size: {(monument.splatmap1 != null ? monument.splatmap1.Length : 0)} bytes");
        Debug.Log($"Biomemap Size: {(monument.biomemap != null ? monument.biomemap.Length : 0)} bytes");
        Debug.Log($"Alphamap Size: {(monument.alphamap != null ? monument.alphamap.Length : 0)} bytes");
        Debug.Log($"Blendmap Size: {(monument.blendmap != null ? monument.blendmap.Length : 0)} bytes");
        Debug.Log($"Topologymap Size: {(monument.topologymap != null ? monument.topologymap.Length : 0)} bytes");

        // Additional texture resolution information (if available)
        if (monument.heightmap != null)
        {
            Texture2D heightTexture = WorldSerialization.DeserializeTexture(monument.heightmap, TextureFormat.RGBA32);
            if (heightTexture != null)
            {
                Debug.Log($"Heightmap Resolution: {heightTexture.width}x{heightTexture.height}");
                UnityEngine.Object.Destroy(heightTexture);
            }
        }

        if (monument.splatmap0 != null)
        {
            Texture2D splat0Texture = WorldSerialization.DeserializeTexture(monument.splatmap0, TextureFormat.RGBA32);
            if (splat0Texture != null)
            {
                Debug.Log($"Splatmap0 Resolution: {splat0Texture.width}x{splat0Texture.height}");
                UnityEngine.Object.Destroy(splat0Texture);
            }
        }

        if (monument.splatmap1 != null)
        {
            Texture2D splat1Texture = WorldSerialization.DeserializeTexture(monument.splatmap1, TextureFormat.RGBA32);
            if (splat1Texture != null)
            {
                Debug.Log($"Splatmap1 Resolution: {splat1Texture.width}x{splat1Texture.height}");
                UnityEngine.Object.Destroy(splat1Texture);
            }
        }

        if (monument.biomemap != null)
        {
            Texture2D biomeTexture = WorldSerialization.DeserializeTexture(monument.biomemap, TextureFormat.RGBA32);
            if (biomeTexture != null)
            {
                Debug.Log($"Biomemap Resolution: {biomeTexture.width}x{biomeTexture.height}");
                UnityEngine.Object.Destroy(biomeTexture);
            }
        }

        if (monument.alphamap != null)
        {
            RenderTexture alphaTexture = WorldSerialization.DeserializeTexture(monument.alphamap, RenderTextureFormat.ARGB32);
            if (alphaTexture != null)
            {
                Debug.Log($"Alphamap Resolution: {alphaTexture.width}x{alphaTexture.height}");
                UnityEngine.Object.Destroy(alphaTexture);
            }
        }

        if (monument.blendmap != null)
        {
            Texture2D blendTexture = WorldSerialization.DeserializeTexture(monument.blendmap, TextureFormat.RGBA32);
            if (blendTexture != null)
            {
                Debug.Log($"Blendmap Resolution: {blendTexture.width}x{blendTexture.height}");
                UnityEngine.Object.Destroy(blendTexture);
            }
        }

        if (monument.topologymap != null)
        {
            Texture2D topologyTexture = WorldSerialization.DeserializeTexture(monument.topologymap, TextureFormat.RGBA32);
            if (topologyTexture != null)
            {
                Debug.Log($"Topologymap Resolution: {topologyTexture.width}x{topologyTexture.height}");
                UnityEngine.Object.Destroy(topologyTexture);
            }
        }

        Debug.Log("=== End RMMonument Debug Readout ===");
    }
	



		public static void DebugRMPrefabReadout(RMPrefabData prefab)
		{
			if (prefab == null)
			{
				Debug.LogWarning("RMPrefabData is null. No debug information to display.");
				return;
			}

			Debug.Log("=== RMPrefabData Debug Readout ===");
			
			DebugMonumentReadout(prefab.monument); // Reuse existing method for RMMonument



			// Simple fields
			Debug.Log($"Checksum: {(prefab.checksum != null ? prefab.checksum : "null")}");

			// Collection fields
			Debug.Log($"Modifiers: {(prefab.modifiers != null ? "Not null" : "null")}");
			if (prefab.modifiers != null)
			{
				// Assuming ModifierData has a meaningful ToString() or specific properties
				Debug.Log($"Modifiers Details: {prefab.modifiers.ToString()}");
			}

			Debug.Log($"Prefabs Count: {(prefab.prefabs != null ? prefab.prefabs.Count : 0)}");
			if (prefab.prefabs != null && prefab.prefabs.Count > 0)
			{
				Debug.Log($"Sample Prefab (first): {(prefab.prefabs[0] != null ? prefab.prefabs[0].ToString() : "null")}");
			}

			// Byte array fields with lengths
			Debug.Log($"Circuits Size: {(prefab.circuits != null ? prefab.circuits.Length : 0)} bytes");
			Debug.Log($"Emptychunk1 Size: {(prefab.emptychunk1 != null ? prefab.emptychunk1.Length : 0)} bytes");
			Debug.Log($"NPCs Size: {(prefab.npcs != null ? prefab.npcs.Length : 0)} bytes");
			Debug.Log($"Emptychunk3 Size: {(prefab.emptychunk3 != null ? prefab.emptychunk3.Length : 0)} bytes");
			Debug.Log($"Emptychunk4 Size: {(prefab.emptychunk4 != null ? prefab.emptychunk4.Length : 0)} bytes");
			Debug.Log($"Buildingchunk Size: {(prefab.buildingchunk != null ? prefab.buildingchunk.Length : 0)} bytes");
			// Check for extended (undefined) members via IExtensible
			
				Debug.Log("--- Extended Members ---");
				try
				{
					var extensionObject = ((IExtensible)prefab).GetExtensionObject(false);
					if (extensionObject != null)
					{
						Debug.Log($"Extended Members Present");
					}
					else
					{
						Debug.Log("No Extended Members Present");
					}
				}
				catch (System.Exception ex)
				{
					Debug.LogWarning($"Failed to inspect extended members: {ex.Message}");
				}


			Debug.Log("=== End RMPrefabData Debug Readout ===");
		}



}