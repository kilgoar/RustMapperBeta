using System;
using UnityEngine;

using UnityEditor;

using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using RustMapEditor.Variables;
using static RustMapEditor.Maths.Array;
using static AreaManager;
using static TerrainManager;
using static WorldSerialization;
using static ModManager;


public static class WorldConverter
{
	public static int BiomeChannels;
	
    public struct MapInfo
    {
        public int terrainRes;
        public int splatRes;
        public Vector3 size;
        public float[,,] splatMap;
        public float[,,] biomeMap;
        public bool[,] alphaMap;
        public TerrainInfo land;
        public TerrainInfo water;
        public TerrainMap<int> topology;
        public PrefabData[] prefabData;
        public PathData[] pathData;	
		
		public Texture2D blendMap;		
		public CircuitDataHolder circuitDataHolder;
		public CircuitData[] circuitData;
		public NPCData[] npcData;
		public ModifierData modifierData;
		public Dictionary<string, byte[]> otherFields;
    }

    public struct TerrainInfo
    {
        public float[,] heights;
    }
    
    public static MapInfo EmptyMap(int size, float landHeight, TerrainSplat.Enum ground = TerrainSplat.Enum.Grass, TerrainBiome.Enum biome = TerrainBiome.Enum.Temperate)
    {		
        MapInfo terrains = new MapInfo();

        int splatRes = Mathf.Clamp(Mathf.NextPowerOfTwo((int)(size * 0.50f)), 16, 2048);

        List<PathData> paths = new List<PathData>();
        List<PrefabData> prefabs = new List<PrefabData>();
		List<CircuitData> circuits = new List<CircuitData>();

        terrains.pathData = paths.ToArray();
        terrains.prefabData = prefabs.ToArray();
		terrains.circuitData = circuits.ToArray();

        terrains.terrainRes = Mathf.NextPowerOfTwo((int)(size * 0.50f)) + 1;
        terrains.size = new Vector3(size, 1000, size);

        terrains.land.heights = SetValues(new float[terrains.terrainRes, terrains.terrainRes], landHeight / 1000f, new Area(0, terrains.terrainRes, 0, terrains.terrainRes));
        terrains.water.heights = SetValues(new float[terrains.terrainRes, terrains.terrainRes], 0f, new Area(0, terrains.terrainRes, 0, terrains.terrainRes));

        terrains.splatRes = splatRes;
        terrains.splatMap = new float[splatRes, splatRes, 8];
        int gndIdx = TerrainSplat.TypeToIndex((int)ground);
        Parallel.For(0, splatRes, i =>
        {
            for (int j = 0; j < splatRes; j++)
                terrains.splatMap[i, j, gndIdx] = 1f;
        });

        terrains.biomeMap = new float[splatRes, splatRes, 5];
        int biomeIdx = TerrainBiome.TypeToIndex((int)biome);
        Parallel.For(0, splatRes, i =>
        {
            for (int j = 0; j < splatRes; j++)
                terrains.biomeMap[i, j, biomeIdx] = 1f;
        });

        terrains.alphaMap = new bool[splatRes, splatRes];
        Parallel.For(0, splatRes, i =>
        {
            for (int j = 0; j < splatRes; j++)
                terrains.alphaMap[i, j] = true;
        });
        terrains.topology = new TerrainMap<int>(new byte[(int)Mathf.Pow(splatRes, 2) * 4 * 1], 1);
		
		TerrainManager.InitializeTextures();
        return terrains;
    }


    /// <summary>Converts the MapInfo and TerrainMaps into a Unity map format.</summary>
    public static MapInfo ConvertMaps(MapInfo terrains, TerrainMap<byte> splatMap, TerrainMap<byte> biomeMap, TerrainMap<byte> alphaMap)
    {		
        terrains.splatMap = new float[splatMap.res, splatMap.res, 8];
        terrains.biomeMap = new float[biomeMap.res, biomeMap.res, 5];
        terrains.alphaMap = new bool[alphaMap.res, alphaMap.res];


        var groundTask = Task.Run(() =>
        {
            Parallel.For(0, terrains.splatRes, i =>
            {
                for (int j = 0; j < terrains.splatRes; j++)
				
                    for (int k = 0; k < 8; k++)
                        terrains.splatMap[i, j, k] = BitUtility.Byte2Float(splatMap[k, i, j]);
            });
        });
	
	
	

    var biomeTask = Task.Run(() =>
    {
        float[] biomeSums = new float[5]; // Track sum of each biome layer
        Parallel.For(0, terrains.splatRes, i =>
        {
            for (int j = 0; j < terrains.splatRes; j++)
            {
                float sum = 0f;
                for (int k = 0; k < BiomeChannels && k < 5; k++)
                {
                    float value = BitUtility.Byte2Float(biomeMap[k, i, j]);
                    terrains.biomeMap[i, j, k] = value;
                    biomeSums[k] += value;
                    sum += value;
                }
                // Fill remaining channels with 0 if BiomeChannels < 5
                for (int k = BiomeChannels; k < 5; k++)
                {
                    terrains.biomeMap[i, j, k] = 0f;
                }
                // Normalize weights to sum to 1
                if (sum > 0)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        terrains.biomeMap[i, j, k] /= sum;
                    }
                }
            }
        });
        Debug.Log($"ConvertMaps: Biome sums: Arid={biomeSums[0]}, Temperate={biomeSums[1]}, Tundra={biomeSums[2]}, Arctic={biomeSums[3]}, Jungle={biomeSums[4]}");
    });

        var alphaTask = Task.Run(() =>
        {
            Parallel.For(0, alphaMap.res, i =>
            {
                for (int j = 0; j < alphaMap.res; j++)
                {
                    if (alphaMap[0, i, j] > 0)
                        terrains.alphaMap[i, j] = true;
                    else
                        terrains.alphaMap[i, j] = false;
                }
            });
        });
        Task.WaitAll(groundTask, biomeTask, alphaTask);

        return terrains;
    }

    /// <summary>Parses World Serialization and converts into MapInfo struct.</summary>
    /// <param name="world">Serialization of the map file to parse.</param>
    public static MapInfo WorldToTerrain(WorldSerialization world)
    {		

        MapInfo terrains = new MapInfo();

		
        var terrainSize = new Vector3(world.world.size, 1000, world.world.size);
		
		
        var terrainMap = new TerrainMap<short>(world.GetMap("terrain").data, 1);
        var heightMap = new TerrainMap<short>(world.GetMap("height").data, 1);
        var waterMap = new TerrainMap<short>(world.GetMap("water").data, 1);
        var splatMap = new TerrainMap<byte>(world.GetMap("splat").data, 8);
        var topologyMap = new TerrainMap<int>(world.GetMap("topology").data, 1);
		
		HeightMapRes = terrainMap.res;
		SplatMapRes = splatMap.res;
		int resolution = splatMap.res; 
		Debug.LogError(resolution);
		
		BiomeChannels = world.GetMap("biome").data.Length / (resolution * resolution); // Calculate channels (4 or 5)
		Debug.LogError(BiomeChannels + " biomes found");
		var biomeMap = new TerrainMap<byte>(world.GetMap("biome").data, BiomeChannels);
		
		    var biomeMapData = world.GetMap("biome")?.data;
			if (biomeMapData == null)
			{
				Debug.LogError("Biome map data is null! Creating default 5-channel map.");
				BiomeChannels = 5;
				biomeMapData = new byte[resolution * resolution * 5];
			}
			else
			{
				BiomeChannels = biomeMapData.Length / (resolution * resolution);
				Debug.Log($"WorldToTerrain: Biome map: Data length={biomeMapData.Length}, Calculated BiomeChannels={BiomeChannels}, Expected=5");
				if (BiomeChannels != 5)
				{
					Debug.LogWarning($"Unexpected BiomeChannels={BiomeChannels}. Expected 5. Jungle layer may be missing.");
				}

			}

		
		var alphaMap = new TerrainMap<byte>(world.GetMap("alpha").data, 1);
		
		int alphaRes = AlphaMapRes; // Should match the saved AlphaMapRes
		Debug.Log($"Loading Alpha map with resolution: {alphaRes}x{alphaRes}, Length: {world.GetMap("alpha").data.Length} bytes");

		
		// Log the length of each terrain map
		Debug.Log("Terrain map length: " + world.GetMap("terrain").data.Length + " bytes");
		Debug.Log("Height map length: " + world.GetMap("height").data.Length + " bytes");
		Debug.Log("Water map length: " + world.GetMap("water").data.Length + " bytes");
		Debug.Log("Splat map length: " + world.GetMap("splat").data.Length + " bytes");
		Debug.Log("Topology map length: " + world.GetMap("topology").data.Length + " bytes");
		Debug.Log("Biome map length: " + world.GetMap("biome").data.Length + " bytes");
		Debug.Log("Alpha map length: " + world.GetMap("alpha").data.Length + " bytes");
		
        terrains.topology = topologyMap;

        terrains.pathData = world.world.paths.ToArray();
        terrains.prefabData = world.world.prefabs.ToArray();
        terrains.terrainRes = heightMap.res;
        terrains.splatRes = splatMap.res;
        terrains.size = terrainSize;
		
		
		
			ModManager.ClearModdingData();
			foreach (var name in ModManager.GetKnownDataNames())
			{
				if (name == "buildingblocks")
				{
					WorldSerialization.MapData buildData = world.GetMap(name);
					if (buildData != null)
					{
						ModManager.AddOrUpdateModdingData(name, buildData.data);
					}
					continue;
				}
				
				string hashedName = ModManager.MapDataName(world.world.prefabs.Count, name);
				WorldSerialization.MapData mapData = world.GetMap(hashedName);
				
				if (mapData != null)
				{
					mapData.name = name;
					ModManager.AddOrUpdateModdingData(name, mapData.data); 
				}
			}

			foreach (var data in ModManager.moddingData)
			{
				string topoName = data.name;
				if (topoName.Contains("custom_topology_"))
				{
					WorldSerialization.MapData topoData = world.GetMap(topoName);
					if (topoData != null)
					{
						ModManager.AddOrUpdateModdingData(topoName, topoData.data);
					}
				}
			}
		

        var heightTask = Task.Run(() => ShortMapToFloatArray(heightMap));
        var waterTask = Task.Run(() => ShortMapToFloatArray(waterMap));

        terrains = ConvertMaps(terrains, splatMap, biomeMap, alphaMap);

        Task.WaitAll(heightTask, waterTask);
        terrains.land.heights = heightTask.Result;
        terrains.water.heights = waterTask.Result;

			//terrains.land.heights = ShortMapToFloatArray(heightMap);
			//terrains.water.heights = ShortMapToFloatArray(waterMap);
		    terrains = ConvertMaps(terrains, splatMap, biomeMap, alphaMap);

        return terrains;
    }

//todo: traverse collection for SocketInfo and serialize it
	public static WorldSerialization CollectionToREPrefab(Transform parent)
	{
		WorldSerialization world = new WorldSerialization();

		try
		{
			if (parent == null)
			{
				Debug.LogError("Parent Transform is null; no prefabs can be processed.");
				return world;
			}

			// Collect all PrefabDataHolder components in the hierarchy (flattening nesting)
			List<PrefabDataHolder> prefabHolders = new List<PrefabDataHolder>();
			CollectPrefabDataHolders(parent, prefabHolders);

			// Process each PrefabDataHolder and convert to local space relative to the parent
			foreach (PrefabDataHolder holder in prefabHolders)
			{
				if (holder.prefabData != null)
				{
					// Create a copy of the prefab data
					PrefabData localPrefab = holder.prefabData;

					// Get the world position and rotation from the holder's transform
					Vector3 worldPosition = holder.transform.position;
					Quaternion worldRotation = holder.transform.rotation;

					// Convert to local space relative to the parent
					localPrefab.position = parent.InverseTransformPoint(worldPosition);
					localPrefab.rotation = Quaternion.Inverse(parent.rotation) * worldRotation;

					// Add the adjusted prefab to the REPrefab data
					world.rePrefab.prefabs.Add(localPrefab);
				}
			}

			// Find the SocketInfo child object
			Transform socketInfoTransform = parent.Find("SocketInfo");
			if (socketInfoTransform != null)
			{
				Debug.Log("socket info found");
				// Collect all DungeonBaseSocket components under SocketInfo
				DungeonBaseSocket[] sockets = socketInfoTransform.GetComponentsInChildren<DungeonBaseSocket>(true);
				foreach (DungeonBaseSocket socket in sockets)
				{
					if (socket != null)
					{
						// Create SocketInfo from DungeonBaseSocket
						SocketInfo socketInfo = SocketInfo.FromDungeonBaseSocket(socket);

						// Convert position and rotation to local space relative to the parent
						socketInfo.Position = parent.InverseTransformPoint(socket.transform.position);
						socketInfo.Rotation = (Quaternion.Inverse(parent.rotation) * socket.transform.rotation).eulerAngles;

						// Add to REPrefabData sockets list
						world.rePrefab.sockets.Add(socketInfo);
					}
				}
				// Add positive feedback about the number of sockets added
				Debug.Log($"Successfully added {world.rePrefab.sockets.Count} sockets from SocketInfo to REPrefab data.");
			}
			else
			{
				Debug.LogWarning("No SocketInfo child found under parent; no sockets processed.");
			}

			// Initialize empty collections for other REPrefab components
			//world.rePrefab.electric.circuitData = new List<CircuitData>();
			//world.rePrefab.npcs.bots = new List<NPCData>();
			world.rePrefab.modifiers = new ModifierData();

			return world;
		}
		catch (NullReferenceException err)
		{
			Debug.LogError("Error during prefab conversion: " + err.Message);
			return world;
		}
	}

	/// <summary>Recursively collects all PrefabDataHolder components from a Transform and its children.</summary>
	/// <param name="current">Current Transform to inspect.</param>
	/// <param name="holders">List to store collected PrefabDataHolder components.</param>
	private static void CollectPrefabDataHolders(Transform current, List<PrefabDataHolder> holders)
	{
		// Check if the current object has a PrefabDataHolder
		PrefabDataHolder holder = current.GetComponent<PrefabDataHolder>();
		if (holder != null)
		{
			holders.Add(holder);
		}

		// Recursively process all children
		foreach (Transform child in current)
		{
			CollectPrefabDataHolders(child, holders);
		}
	}

	public static float[,,] TextureToSplatMap(Texture2D texture, int channels)
	{
		int width = texture.width;
		float[,,] splatMap = new float[width, width, channels];
		Color[] pixels = texture.GetPixels();

		for (int y = 0; y < width; y++)
		{
			for (int x = 0; x < width; x++)
			{
				Color pixel = pixels[y * width + x];
				splatMap[y, x, 0] = pixel.r;
				splatMap[y, x, 1] = pixel.g;
				splatMap[y, x, 2] = pixel.b;
				splatMap[y, x, 3] = pixel.a;
				if (channels > 4)
				{
					splatMap[y, x, 4] = 0f; // Jungle channel, default to 0
				}
			}
		}
		return splatMap;
	}

	public static bool[,] TextureToAlphaMap(RenderTexture texture)
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
				alphaMap[y, x] = pixels[y * width + x].r > 0.5f;
			}
		}
		return alphaMap;
	}

	public static TerrainMap<int> TextureToTopologyMap(Texture2D texture, int resolution)
	{
		TerrainMap<int> topologyMap = new TerrainMap<int>(new byte[resolution * resolution * 4], 1);
		Color[] pixels = texture.GetPixels();

		for (int y = 0; y < resolution; y++)
		{
			for (int x = 0; x < resolution; x++)
			{
				Color pixel = pixels[y * resolution + x];
				topologyMap[0, y, x] = (int)(pixel.r * 255) | ((int)(pixel.g * 255) << 8) | ((int)(pixel.b * 255) << 16) | ((int)(pixel.a * 255) << 24);
			}
		}
		return topologyMap;
	}

    /// <summary>Converts Unity terrains to WorldSerialization.</summary>
    public static WorldSerialization TerrainToWorld(Terrain land, Terrain water, (int prefab, int path, int terrain) ID = default) 
    {
		TerrainManager.SyncTerrainResolutions();
		
        WorldSerialization world = new WorldSerialization();
        world.world.size = (uint) land.terrainData.size.x;

        var textureResolution = SplatMapRes;

        byte[] splatBytes = new byte[textureResolution * textureResolution * 8];
        var splatMap = new TerrainMap<byte>(splatBytes, 8);
        var splatTask = Task.Run(() =>
        {
            Parallel.For(0, 8, i =>
            {
                for (int j = 0; j < textureResolution; j++)
                    for (int k = 0; k < textureResolution; k++)
                        splatMap[i, j, k] = BitUtility.Float2Byte(Ground[j, k, i]);
            });
            splatBytes = splatMap.ToByteArray();
        });

        byte[] biomeBytes = new byte[textureResolution * textureResolution * 5];
        var biomeMap = new TerrainMap<byte>(biomeBytes, 5);
        var biomeTask = Task.Run(() =>
        {
            Parallel.For(0, 5, i =>
            {
                for (int j = 0; j < textureResolution; j++)
                    for (int k = 0; k < textureResolution; k++)
                        biomeMap[i, j, k] = BitUtility.Float2Byte(Biome[j, k, i]);
            });
            biomeBytes = biomeMap.ToByteArray();
        });


        byte[] alphaBytes = new byte[textureResolution * textureResolution * 1];
        var alphaMap = new TerrainMap<byte>(alphaBytes, 1);
        bool[,] terrainHoles = GetAlphaMap();
        var alphaTask = Task.Run(() =>
        {
            Parallel.For(0, textureResolution, i =>
            {
                for (int j = 0; j < textureResolution; j++)
                    alphaMap[0, i, j] = BitUtility.Bool2Byte(terrainHoles[i, j]);
            });
            alphaBytes = alphaMap.ToByteArray();
        });

        var topologyTask = Task.Run(() => TopologyData.SaveTopologyLayers());

        foreach (PrefabDataHolder p in PrefabManager.CurrentMapPrefabs)
        {
            if (p.prefabData != null)
            {
                p.UpdatePrefabData(); // Updates the prefabdata before saving.
				p.AlwaysBreakPrefabs();
                world.world.prefabs.Insert(0, p.prefabData);
            }
        }
		
		#if UNITY_EDITOR
        Progress.Report(ID.prefab, 0.99f, "Saved " + PrefabManager.CurrentMapPrefabs.Length + " prefabs.");
		#endif
		
		Debug.Log("Saving " + PathManager.CurrentMapPaths.Length + " paths");
        foreach (NodeCollection p in PathManager.CurrentMapPaths)
        {
				Debug.Log(p.pathData.nodes.Length + " nodes found ");
                world.world.paths.Insert(0, p.pathData);
        }
		
		#if UNITY_EDITOR
        Progress.Report(ID.path, 0.99f, "Saved " + PathManager.CurrentMapPaths.Length + " paths.");
		#endif

        byte[] landHeightBytes = FloatArrayToByteArray(land.terrainData.GetHeights(0, 0, HeightMapRes, HeightMapRes));
        byte[] waterHeightBytes = FloatArrayToByteArray(water.terrainData.GetHeights(0, 0, HeightMapRes, HeightMapRes));

        Task.WaitAll(splatTask, biomeTask, alphaTask, topologyTask);
		
		#if UNITY_EDITOR
        Progress.Report(ID.terrain, 0.99f, "Saved " + TerrainSize.x + " size map.");
		#endif
		
		
		// Log the length of each terrain map
		Debug.Log("Land height map length: " + landHeightBytes.Length + " bytes");
		Debug.Log("Water height map length: " + waterHeightBytes.Length + " bytes");
		Debug.Log("Splat map length: " + splatBytes.Length + " bytes");
		Debug.Log("Biome map length: " + biomeBytes.Length + " bytes");
		Debug.Log("Alpha map length: " + alphaBytes.Length + " bytes");
		var topologyMap = TopologyData.GetTerrainMap();
		byte[] topologyBytes = topologyMap.ToByteArray();
		Debug.Log("Topology map length: " + topologyBytes.Length + " bytes");
		
		
		// Add modding data from ModManager
		var moddingData = ModManager.GetModdingData();
		if (moddingData != null && moddingData.Count > 0)
		{
			foreach (var md in moddingData)
			{
				if (md.name == "buildingblocks" || md.name.Contains("custom_topology_"))
				{
					world.AddMap(md.name, md.data);
				}
				else
				{
					// Normal case: Hash the name
					string hashedName = ModManager.MapDataName(world.world.prefabs.Count, md.name);
					world.AddMap(hashedName, md.data);
				}
			}
		}
		else
		{
		}
		

        world.AddMap("terrain", landHeightBytes);
        world.AddMap("height", landHeightBytes);
        world.AddMap("water", waterHeightBytes);
        world.AddMap("splat", splatBytes);
        world.AddMap("biome", biomeBytes);
        world.AddMap("alpha", alphaBytes);
        world.AddMap("topology", TopologyData.GetTerrainMap().ToByteArray());
        return world;
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
            regionHeights[z, x] = (combinedHeight * size.y) / 1000f + .5f + (offset.y/1000f);
        }
    }

    // Log a sample of the final heights
    Debug.Log($"Decoded Heights Sample: [0,0]={regionHeights[0, 0]}, [{height/2},{width/2}]={regionHeights[height/2, width/2]}");
    return regionHeights;
}

	public static float[,,] CombineSplatMaps(Texture2D splat0, Texture2D splat1)
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
	
	public static MapInfo RMPrefabToTerrain(WorldSerialization world)
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

			/*
			// Load watermap
			if (monument.watermap != null)
			{
				Texture2D waterTexture = WorldSerialization.DeserializeTexture(monument.watermap, TextureFormat.RGBA32);
				if (waterTexture != null)
				{
					terrain.water.heights = DecodeHeightMap(waterTexture, monument.size.y, 0f);
					UnityEngine.Object.Destroy(waterTexture);
				}
			}
			*/

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


			if (monument.topologymap != null)
			{
				Texture2D topologyTexture = WorldSerialization.DeserializeTexture(monument.topologymap, TextureFormat.RGBA32);
				if (topologyTexture != null)
				{
					terrain.topology = TextureToTopologyMap(topologyTexture, terrain.splatRes);
					UnityEngine.Object.Destroy(topologyTexture);
				}
			}
			TopologyData.UpdateTexture();


			// Populate prefab-related fields
			terrain.prefabData = world.rmPrefab.prefabs.ToArray();
			//terrain.circuitData = world.rmPrefab.electric.circuitData.ToArray();
			//terrain.npcData = world.rmPrefab.npcs.bots.ToArray();
			terrain.modifierData = world.rmPrefab.modifiers;
	
			/*
			for (int k = 0; k < terrain.circuitData.Length; k++)
			{
				terrain.circuitData[k].connectionsIn = terrain.circuitData[k].branchIn.ToArray();
				terrain.circuitData[k].connectionsOut = terrain.circuitData[k].branchOut.ToArray();
			}
			*/
			
			return terrain;
		}
		catch (Exception err)
		{
			Debug.LogError($"Error during RMPrefabToTerrain conversion: {err.Message}");
			return terrain;
		}
	}

	
public static WorldSerialization TerrainToRMPrefab(Terrain land, Terrain water)
{
    WorldSerialization worldSerialization = new WorldSerialization();
    try
    {
        RMPrefabData rmPrefab = new RMPrefabData();    // Process modifiers, NPCs, prefabs, circuits (unchanged)
    if (PrefabManager.CurrentModifiers?.modifierData != null)
        rmPrefab.modifiers = PrefabManager.CurrentModifiers.modifierData;
	
	        rmPrefab.circuits = MonumentManager.CurrentRMPrefab?.circuits;
            rmPrefab.emptychunk1 = MonumentManager.CurrentRMPrefab?.emptychunk1;
            rmPrefab.npcs = MonumentManager.CurrentRMPrefab?.npcs;
            rmPrefab.emptychunk3 = MonumentManager.CurrentRMPrefab?.emptychunk3;
            rmPrefab.emptychunk4 = MonumentManager.CurrentRMPrefab?.emptychunk4;
            rmPrefab.buildingchunk = MonumentManager.CurrentRMPrefab?.buildingchunk;

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
	
	float lowOff = 500f-minHeight;
	
    float padding = 20f;
    minHeight -= padding;
    maxHeight += padding;
    float heightRange = maxHeight - minHeight;

    // Initialize RMMonument
    RMMonument monument = new RMMonument
    {
        size = new Vector3(land.terrainData.size.x, heightRange, land.terrainData.size.z),
        extents = new Vector3(land.terrainData.size.x / 2f, heightRange / 2f, land.terrainData.size.z / 2f),
        offset = new Vector3(0f, -padding - lowOff, 0f),
        HeightMap = true,
        AlphaMap = true,
        WaterMap = true,
        SplatMask = TerrainSplat.Enum.Grass,
        BiomeMask = TerrainBiome.Enum.Temperate,
        TopologyMask = TerrainTopology.Enum.Field
    };

    // Texture resolution
    var textureResolution = SplatMapRes;
    var heightMapRes = HeightMapRes;

    TerrainManager.SyncBiomeTexture();
    TerrainManager.SyncAlphaTexture();

    // Process alphamaps (unchanged)
    Texture2D[] alphamapTextures = land.terrainData.alphamapTextures;
    if (alphamapTextures == null || alphamapTextures.Length < 2)
    {
        Debug.LogError("Terrain alphamap textures (Control0 and Control1) are not available.");
    }
    else
    {
        monument.splatmap0 = WorldSerialization.SerializeTexture(alphamapTextures[0]);
        monument.splatmap1 = WorldSerialization.SerializeTexture(alphamapTextures[1]);
    }

    // Process biome, alpha, blend, topology maps (unchanged)
    if (TerrainManager.BiomeTexture == null || TerrainManager.Biome1Texture == null)
    {
        Debug.LogError("BiomeTexture or Biome1Texture is not initialized.");
    }
    else
    {
        monument.biomemap = WorldSerialization.SerializeTexture(TerrainManager.BiomeTexture);
    }

    if (TerrainManager.AlphaTexture == null)
    {
        Debug.LogError("AlphaTexture is not initialized.");
    }
    else
    {
        monument.alphamap = WorldSerialization.SerializeTexture(TerrainManager.AlphaTexture);
    }

    monument.blendmap = WorldSerialization.SerializeTexture(TerrainManager.BlendMapTexture);

    TopologyData.UpdateTexture();
    monument.topologymap = WorldSerialization.SerializeTexture(TopologyData.TopologyTexture);

    // Process heightmap
    monument.heightmap = EncodeHeightMap(
        land.terrainData.GetHeights(0, 0, heightMapRes, heightMapRes),
        heightMapRes,
        "HeightMapTexture",
        minHeight,
        heightRange
    );

   /*
    monument.watermap = EncodeHeightMap(
        water.terrainData.GetHeights(0, 0, heightMapRes, heightMapRes),
        heightMapRes,
        "WaterMapTexture",
        minWaterHeight,
        waterHeightRange
    ); */          rmPrefab.monument = monument;
    worldSerialization.rmPrefab = rmPrefab;
	
	Debug.Log("Saving RM Prefab:");
	MonumentManager.DebugRMPrefabReadout(rmPrefab);

    return worldSerialization;
}
catch (NullReferenceException err)
{
    Debug.LogError("Error during RMPrefab conversion: " + err.Message);
    return worldSerialization;
}}

	/*
	
public static WorldSerialization TerrainToRMPrefab(Terrain land, Terrain water)
{
    WorldSerialization worldSerialization = new WorldSerialization();
    try
    {
        RMPrefabData rmPrefab = new RMPrefabData();

        // Process modifiers, NPCs, prefabs, circuits (unchanged)
        if (PrefabManager.CurrentModifiers?.modifierData != null)
            rmPrefab.modifiers = PrefabManager.CurrentModifiers.modifierData;
        foreach (NPCDataHolder p in PrefabManager.CurrentMapNPCs)
        {
            if (p.bots != null)
                rmPrefab.npcs.bots.Insert(0, p.bots);
        }
        foreach (PrefabDataHolder p in PrefabManager.CurrentMapPrefabs)
        {
            if (p.prefabData != null)
            {
                p.AlwaysBreakPrefabs();
                rmPrefab.prefabs.Add(p.prefabData);
            }
        }
        foreach (CircuitDataHolder p in PrefabManager.CurrentMapElectrics)
        {
            if (p.circuitData != null)
            {
                p.UpdateCircuitData();
                rmPrefab.electric.circuitData.Insert(0, p.circuitData);
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
		
		float lowOff = 500f-minHeight;
		float padding = 20f;
		
        minHeight -= padding;
        maxHeight += padding;
        float heightRange = maxHeight - minHeight;

        // Initialize RMMonument
        RMMonument monument = new RMMonument
        {
            size = new Vector3(land.terrainData.size.x, heightRange, land.terrainData.size.z),
            extents = new Vector3(land.terrainData.size.x / 2f, heightRange / 2f, land.terrainData.size.z / 2f),
            offset = new Vector3(0f, -padding - lowOff, 0f),
            HeightMap = true,
            AlphaMap = true,
            WaterMap = true,
            SplatMask = TerrainSplat.Enum.Grass,
            BiomeMask = TerrainBiome.Enum.Temperate,
            TopologyMask = TerrainTopology.Enum.Field
        };

        // Texture resolution
        var textureResolution = SplatMapRes;
        var heightMapRes = HeightMapRes;

        monument.blendmap = WorldSerialization.SerializeTexture(TerrainManager.BlendMapTexture);

        TerrainManager.SyncBiomeTexture();
        TerrainManager.SyncAlphaTexture();

        // Process alphamaps (unchanged)
        Texture2D[] alphamapTextures = land.terrainData.alphamapTextures;
        if (alphamapTextures == null || alphamapTextures.Length < 2)
        {
            Debug.LogError("Terrain alphamap textures (Control0 and Control1) are not available.");
        }
        else
        {
            monument.splatmap0 = WorldSerialization.SerializeTexture(alphamapTextures[0]);
            monument.splatmap1 = WorldSerialization.SerializeTexture(alphamapTextures[1]);
        }

        // Process biome, alpha, blend, topology maps (unchanged)
        if (TerrainManager.BiomeTexture == null || TerrainManager.Biome1Texture == null)
        {
            Debug.LogError("BiomeTexture or Biome1Texture is not initialized.");
        }
        else
        {
            monument.biomemap = WorldSerialization.SerializeTexture(TerrainManager.BiomeTexture);
        }

        if (TerrainManager.AlphaTexture == null)
        {
            Debug.LogError("AlphaTexture is not initialized.");
        }
        else
        {
            monument.alphamap = WorldSerialization.SerializeTexture(TerrainManager.AlphaTexture);
        }


		
        TopologyData.UpdateTexture();
        monument.topologymap = WorldSerialization.SerializeTexture(TopologyData.TopologyTexture);

        // Process heightmap
        monument.heightmap = EncodeHeightMap(
            land.terrainData.GetHeights(0, 0, heightMapRes, heightMapRes),
            heightMapRes,
            "HeightMapTexture",
            minHeight,
            heightRange
        );

       
        monument.watermap = EncodeHeightMap(
            water.terrainData.GetHeights(0, 0, heightMapRes, heightMapRes),
            heightMapRes,
            "WaterMapTexture",
            minWaterHeight,
            waterHeightRange
        );
    

        rmPrefab.monument = monument;
        worldSerialization.rmPrefab = rmPrefab;

        return worldSerialization;
    }
    catch (NullReferenceException err)
    {
        Debug.LogError("Error during RMPrefab conversion: " + err.Message);
        return worldSerialization;
    }
}
*/
	
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
	
/// <summary>Attaches a Monument component to a GameObject, populating it with RMMonument data.</summary>
/// <param name="rmPrefab">The RMPrefabData containing RMMonument data.</param>
/// <param name="go">The GameObject to attach the Monument component to.</param>
public static void AttachMonument(RMPrefabData rmPrefab, GameObject go)
{
    if (rmPrefab == null)
    {
        Debug.LogError("RMPrefabData is null. Cannot attach Monument component.");
        return;
    }
    if (go == null)
    {
        Debug.LogError("GameObject is null. Cannot attach Monument component.");
        return;
    }

    try
    {
        // Add or get the Monument component
        Monument monumentComponent = go.GetComponent<Monument>();
        if (monumentComponent == null)
            monumentComponent = go.AddComponent<Monument>();

        // Populate Monument component with RMMonument data
        if (rmPrefab.monument != null)
        {
            RMMonument rmMonument = rmPrefab.monument;

            // Set basic Monument properties
            monumentComponent.size = rmMonument.size;
            monumentComponent.extents = rmMonument.extents;
            monumentComponent.offset = rmMonument.offset;
            monumentComponent.HeightMap = rmMonument.HeightMap;
            monumentComponent.AlphaMap = rmMonument.AlphaMap;
            monumentComponent.WaterMap = rmMonument.WaterMap;
            monumentComponent.SplatMask = rmMonument.SplatMask;
            monumentComponent.BiomeMask = rmMonument.BiomeMask;
            monumentComponent.TopologyMask = rmMonument.TopologyMask;
            monumentComponent.Radius = rmMonument.size.x / 2f; // Default to half the size.x
            monumentComponent.Fade = Mathf.Min(rmMonument.size.x, rmMonument.size.z) * 0.1f; // Default to 10% of min dimension

            // Deserialize and assign textures synchronously
            // Heightmap
            if (rmMonument.heightmap != null)
            {
                Texture2D heightTexture = WorldSerialization.DeserializeTexture(
                    rmMonument.heightmap,
                    TextureFormat.RGBA32
                );
                if (heightTexture != null)
                {
                    monumentComponent.heightmap = new Texture2DRef { cachedInstance = heightTexture };
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize heightmap texture.");
                }
            }

            // Splatmap0
            if (rmMonument.splatmap0 != null)
            {
                Texture2D splat0Texture = WorldSerialization.DeserializeTexture(
                    rmMonument.splatmap0,
                    TextureFormat.RGBA32
                );
                if (splat0Texture != null)
                {
                    monumentComponent.splatmap0 = new Texture2DRef { cachedInstance = splat0Texture };
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize splatmap0 texture.");
                }
            }

            // Splatmap1
            if (rmMonument.splatmap1 != null)
            {
                Texture2D splat1Texture = WorldSerialization.DeserializeTexture(
                    rmMonument.splatmap1,
                    TextureFormat.RGBA32
                );
                if (splat1Texture != null)
                {
                    monumentComponent.splatmap1 = new Texture2DRef { cachedInstance = splat1Texture };
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize splatmap1 texture.");
                }
            }

            // Alphamap
            if (rmMonument.alphamap != null)
            {
                RenderTexture alphaTexture = WorldSerialization.DeserializeTexture(
                    rmMonument.alphamap,
                    RenderTextureFormat.ARGB32
                );
                if (alphaTexture != null)
                {
                    // Convert RenderTexture to Texture2D for Texture2DRef
                    Texture2D alphaTexture2D = new Texture2D(alphaTexture.width, alphaTexture.height, TextureFormat.RGBA32, false);
                    RenderTexture.active = alphaTexture;
                    alphaTexture2D.ReadPixels(new Rect(0, 0, alphaTexture.width, alphaTexture.height), 0, 0);
                    alphaTexture2D.Apply();
                    monumentComponent.alphamap = new Texture2DRef { cachedInstance = alphaTexture2D };
                    UnityEngine.Object.Destroy(alphaTexture);
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize alphamap texture.");
                }
            }

            // Biomemap
            if (rmMonument.biomemap != null)
            {
                Texture2D biomeTexture = WorldSerialization.DeserializeTexture(
                    rmMonument.biomemap,
                    TextureFormat.RGBA32
                );
                if (biomeTexture != null)
                {
                    monumentComponent.biomemap = new Texture2DRef { cachedInstance = biomeTexture };
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize biomemap texture.");
                }
            }

            // Topologymap
            if (rmMonument.topologymap != null)
            {
                Texture2D topologyTexture = WorldSerialization.DeserializeTexture(
                    rmMonument.topologymap,
                    TextureFormat.RGBA32
                );
                if (topologyTexture != null)
                {
                    monumentComponent.topologymap = new Texture2DRef { cachedInstance = topologyTexture };
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize topologymap texture.");
                }
            }

            // Watermap
            if (rmMonument.watermap != null)
            {
                Texture2D waterTexture = WorldSerialization.DeserializeTexture(
                    rmMonument.watermap,
                    TextureFormat.RGBA32
                );
                if (waterTexture != null)
                {
                    monumentComponent.watermap = new Texture2DRef { cachedInstance = waterTexture };
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize watermap texture.");
                }
            }

            // Blendmap
            if (rmMonument.blendmap != null)
            {
                Texture2D blendTexture = WorldSerialization.DeserializeTexture(
                    rmMonument.blendmap,
                    TextureFormat.RGBA32
                );
                if (blendTexture != null)
                {
                    monumentComponent.blendmap = new Texture2DRef { cachedInstance = blendTexture };
                }
                else
                {
                    Debug.LogWarning("Failed to deserialize blendmap texture.");
                }
            }
        }
        else
        {
            Debug.LogWarning("RMMonument data is null. Monument component will have default values.");
        }

        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(go);
        #endif
    }
    catch (Exception err)
    {
        Debug.LogError($"Error during AttachMonument: {err.Message}");
    }
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

            // Populate prefab-related fields
            terrain.prefabData = rePrefab.prefabs?.ToArray() ?? new PrefabData[0];
            //terrain.circuitData = rePrefab.electric?.circuitData?.ToArray() ?? new CircuitData[0];
            //terrain.npcData = rePrefab.npcs?.bots?.ToArray() ?? new NPCData[0];
            terrain.modifierData = rePrefab.modifiers ?? new ModifierData();

            // Handle circuit connections
            for (int k = 0; k < terrain.circuitData.Length; k++)
            {
                terrain.circuitData[k].connectionsIn = terrain.circuitData[k].branchIn?.ToArray();
                terrain.circuitData[k].connectionsOut = terrain.circuitData[k].branchOut?.ToArray();
            }

            // Store unknown fields for round-trip serialization
            terrain.otherFields = new Dictionary<string, byte[]>
            {
                { "emptychunk1", rePrefab.emptychunk1 },
                { "emptychunk3", rePrefab.emptychunk3 },
                { "emptychunk4", rePrefab.emptychunk4 },
                { "buildingchunk", rePrefab.buildingchunk }
            };

            // Log modifier data
            Debug.Log($"ModifierData Loaded: size={terrain.modifierData.size}, fade={terrain.modifierData.fade}, " +
                      $"fill={terrain.modifierData.fill}, counter={terrain.modifierData.counter}, id={terrain.modifierData.id}");

            // Initialize terrain-related fields with temporary defaults
            terrain.size = new Vector3(terrain.modifierData.size + terrain.modifierData.fade, 1000f, terrain.modifierData.size + terrain.modifierData.fade);
            terrain.terrainRes = 1;
            terrain.splatRes = 1;
            terrain.water.heights = new float[terrain.terrainRes, terrain.terrainRes];
            terrain.splatMap = new float[terrain.splatRes, terrain.splatRes, 8];
            terrain.biomeMap = new float[terrain.splatRes, terrain.splatRes, 5];
            terrain.alphaMap = new bool[terrain.splatRes, terrain.splatRes];
            terrain.topology = new TerrainMap<int>(new byte[terrain.splatRes * terrain.splatRes * 4], 1);

            Debug.Log("Load path is " + loadPath);

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

                // Load and resize splatmaps
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
                        terrain.alphaMap = TextureToAlphaMap(resizedAlphaTexture);
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

                // Reinitialize arrays with correct resolutions
                terrain.water.heights = new float[terrain.terrainRes, terrain.terrainRes];
                terrain.splatMap = new float[terrain.splatRes, terrain.splatRes, 8];
                terrain.biomeMap = new float[terrain.splatRes, terrain.splatRes, 5];
                terrain.alphaMap = new bool[terrain.splatRes, terrain.splatRes];
                terrain.topology = new TerrainMap<int>(new byte[terrain.splatRes * terrain.splatRes * 4], 1);
            }

            return terrain;
        }
        catch (Exception err)
        {
            Debug.LogError($"Error during REPrefabToTerrain conversion: {err.Message}");
            return terrain;
        }
    }

    // Resize a float[,] height array using bilinear interpolation
    public static float[,] ResizeHeightArray(float[,] source, int targetSize)
    {
        int sourceSize = source.GetLength(0);
        float[,] resized = new float[targetSize, targetSize];

        for (int x = 0; x < targetSize; x++)
        {
            for (int y = 0; y < targetSize; y++)
            {
                // Map target coordinates to source coordinates
                float srcX = x * (float)(sourceSize - 1) / (targetSize - 1);
                float srcY = y * (float)(sourceSize - 1) / (targetSize - 1);

                // Bilinear interpolation
                int x0 = Mathf.FloorToInt(srcX);
                int x1 = Mathf.Min(x0 + 1, sourceSize - 1);
                int y0 = Mathf.FloorToInt(srcY);
                int y1 = Mathf.Min(y0 + 1, sourceSize - 1);

                float fx = srcX - x0;
                float fy = srcY - y0;

                float h00 = source[x0, y0];
                float h10 = source[x1, y0];
                float h01 = source[x0, y1];
                float h11 = source[x1, y1];

                float h0 = Mathf.Lerp(h00, h10, fx);
                float h1 = Mathf.Lerp(h01, h11, fx);
                resized[x, y] = Mathf.Lerp(h0, h1, fy);
            }
        }
        return resized;
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

	
	public static MapInfo WorldToREPrefab(WorldSerialization world)
	{
		MapInfo refab = new MapInfo();
		refab.prefabData = world.rePrefab.prefabs.ToArray();
		refab.circuitData = new CircuitData[0]; // Assigns an empty byte array
		refab.npcData = new NPCData[0]; 
		refab.modifierData = world.rePrefab.modifiers;
		
		for (int k = 0; k < refab.circuitData.Length; k++)
		{
			refab.circuitData[k].connectionsIn = refab.circuitData[k].branchIn.ToArray();
			refab.circuitData[k].connectionsOut = refab.circuitData[k].branchOut.ToArray();
		}
		return refab;
	}
	
	public static MapInfo WorldToRMPrefab(WorldSerialization world)
	{
		MapInfo refab = new MapInfo();
		refab.prefabData = world.rmPrefab.prefabs.ToArray();
		refab.circuitData = new CircuitData[0]; // Assigns an empty byte array
		refab.npcData = new NPCData[0]; 
		refab.modifierData = world.rmPrefab.modifiers;
		
		for (int k = 0; k < refab.circuitData.Length; k++)
		{
			refab.circuitData[k].connectionsIn = refab.circuitData[k].branchIn.ToArray();
			refab.circuitData[k].connectionsOut = refab.circuitData[k].branchOut.ToArray();
		}
		return refab;
	}
	


	
	public static WorldSerialization TerrainToCustomPrefab((int prefab, int circuit) ID) 
    {
		WorldSerialization world = new WorldSerialization();

			try
			{
				
			if (PrefabManager.CurrentModifiers?.modifierData!= null)
				world.rePrefab.modifiers = PrefabManager.CurrentModifiers.modifierData;
			
			
			foreach(NPCDataHolder p in PrefabManager.CurrentMapNPCs)
			{
				if (p.bots != null)
				{
					//world.rePrefab.npcs.bots.Insert(0, p.bots);
				}
			}
			
			
			
			foreach (PrefabDataHolder p in PrefabManager.CurrentMapPrefabs)
			{
				if (p.prefabData != null)
				{
					//p.UpdatePrefabData();
					p.AlwaysBreakPrefabs(); // Updates the prefabdata before saving.
					world.rePrefab.prefabs.Add(p.prefabData);
				}
			}
			foreach (CircuitDataHolder p in PrefabManager.CurrentMapElectrics)
			{
				if (p.circuitData != null)
				{
					p.UpdateCircuitData(); // Updates the circuitdata before saving.
					//world.rePrefab.electric.circuitData.Insert(0, p.circuitData);
				}
			}
			#if UNITY_EDITOR
			Progress.Report(ID.prefab, 0.99f, "Saved " + PrefabManager.CurrentMapPrefabs.Length + " prefabs.");
			Progress.Report(ID.circuit, 0.99f, "Saved " + PrefabManager.CurrentMapPrefabs.Length + " circuits.");
			#endif

			return world;
			}
			catch(NullReferenceException err)
			{
					Debug.LogError(err.Message);
					return world;
			}
    }
	
}