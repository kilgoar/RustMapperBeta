using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using ProtoBuf.Meta;
using LZ4;
using Newtonsoft.Json; 
using System.Runtime.CompilerServices;

using RustMapEditor.Variables;

public class BreakerSerialization
{
	public BreakerPreset breaker = new BreakerPreset();
	

	public void Save(string fileName)
    {
        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    
                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Compress))
                        Serializer.Serialize(compressionStream, breaker);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
		
    public BreakerPreset Load(string fileName)
    {
        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    
                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Decompress))
					{
						breaker = Serializer.Deserialize<BreakerPreset>(compressionStream);
						return breaker;
					}
				
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
			return breaker;
        }
    }
}

public class WorldSerialization
{
    public const uint CurrentVersion = 10U;
	public const uint REPrefabVersion = 1;

    public static uint Version    {    get; private set;    }
	public string Checksum { get; private set; }
	public long Timestamp { get; private set; }

	public WorldData world = new WorldData
	{
		size = 4000U,
		maps = new List<MapData>(),
		prefabs = new List<PrefabData>(),
		paths = new List<PathData>()
	};
	
	public REPrefabData rePrefab = new REPrefabData();
	public RMPrefabData rmPrefab = new RMPrefabData();

    public WorldSerialization()
    {
        Version = CurrentVersion;
		Checksum = null;
		Timestamp = 0L;
    }

    [ProtoContract]
    public class WorldData
    {
        [ProtoMember(1)] public uint size = 4000U;
        [ProtoMember(2)] public List<MapData> maps = new List<MapData>();
        [ProtoMember(3)] public List<PrefabData> prefabs = new List<PrefabData>();
        [ProtoMember(4)] public List<PathData> paths = new List<PathData>();
    }
	
	
	[ProtoContract]
	public class REPrefabData : IExtensible
	{
		private IExtension extensionObject;

		[ProtoMember(1)] public ModifierData modifiers = new ModifierData();
		[ProtoMember(3)] public List<PrefabData> prefabs = new List<PrefabData>();
		[ProtoMember(5)] public byte[] circuits = null;
		[ProtoMember(6)] public byte[] emptychunk1 = null; // Use byte[] to store raw data
		[ProtoMember(7)] public byte[] npcs = null;
		[ProtoMember(8)] public byte[] emptychunk3 = null; // Use byte[] to store raw data
		[ProtoMember(9)] public byte[] emptychunk4 = null; // Use byte[] to store raw data
		[ProtoMember(10)] public byte[] buildingchunk = null; // Use byte[] to store raw data
		[ProtoMember(11)] public string checksum;
		[ProtoMember(100)] public List<SocketInfo> sockets = new List<SocketInfo>(); // New socket list field

		IExtension IExtensible.GetExtensionObject(bool createIfMissing)
		{
			return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
		}
	}
	
	[ProtoContract]
	public class RMPrefabData : IExtensible
    {
		private IExtension extensionObject;

		[ProtoMember(1)] public ModifierData modifiers = new ModifierData();
		[ProtoMember(3)] public List<PrefabData> prefabs = new List<PrefabData>();
		[ProtoMember(5)] public byte[] circuits = null;
		[ProtoMember(6)] public byte[] emptychunk1 = null; // Use byte[] to store raw data
		[ProtoMember(7)] public byte[] npcs = null;
		[ProtoMember(8)] public byte[] emptychunk3 = null; // Use byte[] to store raw data
		[ProtoMember(9)] public byte[] emptychunk4 = null; // Use byte[] to store raw data
		[ProtoMember(10)] public byte[] buildingchunk = null; // Use byte[] to store raw data
		[ProtoMember(11)] public string checksum;
		[ProtoMember(100)] public List<SocketInfo> sockets = new List<SocketInfo>(); // New socket list field

		IExtension IExtensible.GetExtensionObject(bool createIfMissing)
		{
			return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
		}
		[ProtoMember(12)] public RMMonument monument;
    }
	
	[ProtoContract]
	public class RMMonument
	{
		[ProtoMember(1)] public VectorData size;
		[ProtoMember(2)] public VectorData extents;
		[ProtoMember(3)] public VectorData offset;
		[ProtoMember(4)] public bool HeightMap = true;
		[ProtoMember(5)] public bool AlphaMap = true;
		[ProtoMember(6)] public bool WaterMap;
		[ProtoMember(7)] public TerrainSplat.Enum SplatMask;
		[ProtoMember(8)] public TerrainBiome.Enum BiomeMask;
		[ProtoMember(9)] public TerrainTopology.Enum TopologyMask;
		[ProtoMember(10)] public byte[] heightmap;
		[ProtoMember(11)] public byte[] splatmap0;
		[ProtoMember(12)] public byte[] splatmap1;
		[ProtoMember(13)] public byte[] alphamap;
		[ProtoMember(14)] public byte[] biomemap;
		[ProtoMember(15)] public byte[] topologymap;
		[ProtoMember(16)] public byte[] watermap;
		[ProtoMember(17)] public byte[] blendmap;
	}

	[Serializable]
    [ProtoContract]
	public class ModifierData
	{
		[ProtoMember(1)] public int size;
		[ProtoMember(2)] public int fade;
		[ProtoMember(3)] public int fill;
		[ProtoMember(4)] public int counter;
		[ProtoMember(5)] public uint id;
	}
	
	[Serializable]
    [ProtoContract]
	public class NPCDataHolder
	{
		[ProtoMember(1)] public List<NPCData> bots = new List<NPCData>();
		
		public NPCDataHolder() { }
        public NPCDataHolder(List<NPCData> bots)
        {
            this.bots = bots;
		}
	}

		[ProtoContract]
		public class MapData
		{
			[ProtoMember(1)] public string name = ""; // Default to empty string
			[ProtoMember(2)] public byte[] data = Array.Empty<byte>(); // Default to empty array
		}

	[Serializable]
    [ProtoContract]
	public class CircuitDataHolder
	{
		[ProtoMember(1)] public List<CircuitData> circuitData = new List<CircuitData>();
		
		public CircuitDataHolder() { }
        public CircuitDataHolder(List<CircuitData> circuitData)
        {
            this.circuitData = circuitData;
		}
	}

	[Serializable]
    [ProtoContract]
    public class CircuitData
	{
		[ProtoMember(1)] public string path;
        [ProtoMember(2)] public VectorData wiring;
		[ProtoMember(3)] public List<Circuit> branchIn = new List<Circuit>();
		[ProtoMember(4)] public List<Circuit> branchOut = new List<Circuit>();
		[ProtoMember(5)] public int cardType;
		[ProtoMember(6)] public int flow1;
		[ProtoMember(7)] public float setting;
		//[ProtoMember(11)] public int manipulator;
		//[ProtoMember(14)] public int flow3;
		
		[ProtoMember(14)] public string cctv;
		[ProtoMember(16)] public int flow2;
		[ProtoMember(17)] public string phone;
		//[ProtoMember(17)] public int flow5;
		
		public Circuit[] connectionsIn;
		public Circuit[] connectionsOut;
		
		public CircuitData() { }
        public CircuitData(string path, Vector3 wiring, List<Circuit> branchIn, List<Circuit> branchOut, int flow1, float setting,int flow2, string cctv, string phone)
        {
            this.path = path;
            this.wiring = wiring;
            this.branchIn = branchIn;
			this.branchOut = branchOut;
			this.flow1 = flow1;
			this.setting = setting;
			this.flow2 = flow2;
			this.cctv = cctv;
			this.phone = phone;
        }
		public CircuitData(string path, Vector3 wiring, Circuit[] connectionsIn, Circuit[] connectionsOut, int flow1, float setting, int flow2, string cctv, string phone)
        {
            this.path = path;
            this.wiring = wiring;
            this.connectionsIn = connectionsIn;
			this.connectionsOut = connectionsOut;
			this.flow1 = flow1;
			this.setting = setting;
			this.flow2 = flow2;
			this.cctv = cctv;
			this.phone = phone;
		}
		
	}
	
	[Serializable]
    [ProtoContract]
    public class NPCData
	{
		[ProtoMember(1)] public int type;
		[ProtoMember(2)] public int respawnMin;
		[ProtoMember(3)] public int respawnMax;
		[ProtoMember(4)] public VectorData scientist;
		[ProtoMember(5)] public string category;
		
		public NPCData() { }
		public NPCData(int type, int respawnMin, int respawnMax, VectorData scientist, string category)
		{
			this.type = type;
			this.respawnMin = respawnMin;
			this.respawnMax = respawnMax;
			this.scientist = scientist;
			this.category = category;
		}
	}
	
	[Serializable]
    [ProtoContract]
    public class Circuit
	{
		[ProtoMember(1)] public string path;
        [ProtoMember(2)] public VectorData wiring;
        [ProtoMember(3)] public int flow1;
        [ProtoMember(4)] public int flow2;
        [ProtoMember(5)] public int fluid1;
		
		public Circuit() { }
        public Circuit(string path, Vector3 wiring, int flow1, int flow2, int fluid1)
        {
            this.path = path;
            this.wiring = wiring;
			this.flow1 = flow1;
			this.flow2 = flow2;
			this.fluid1 = fluid1;

        }
	}
	

    [Serializable]
    [ProtoContract]
    public class PrefabData
    {
        [ProtoMember(1)] public string category;
        [ProtoMember(2)] public uint id;
        [ProtoMember(3)] public VectorData position;
        [ProtoMember(4)] public VectorData rotation;
        [ProtoMember(5)] public VectorData scale;


        public PrefabData() { }
        public PrefabData(string category, uint id, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.category = category;
            this.id = id;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }

    [Serializable]
    [ProtoContract]
    public class PathData
    {
        [ProtoMember(1)] public string name;
        [ProtoMember(2)] public bool spline;
        [ProtoMember(3)] public bool start;
        [ProtoMember(4)] public bool end;
        [ProtoMember(5)] public float width;
        [ProtoMember(6)] public float innerPadding;
        [ProtoMember(7)] public float outerPadding;
        [ProtoMember(8)] public float innerFade;
        [ProtoMember(9)] public float outerFade;
        [ProtoMember(10)] public float randomScale;
        [ProtoMember(11)] public float meshOffset;
        [ProtoMember(12)] public float terrainOffset;
        [ProtoMember(13)] public int splat;
        [ProtoMember(14)] public int topology;
        [ProtoMember(15)] public VectorData[] nodes;
        [ProtoMember(16)] public int hierarchy;
    }

	

    [Serializable]
    [ProtoContract]
    public class VectorData
    {
        [ProtoMember(1)] public float x;
        [ProtoMember(2)] public float y;
        [ProtoMember(3)] public float z;

        public VectorData()        {
        }

        public VectorData(float x, float y, float z)        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static implicit operator VectorData(Vector3 v)        {
            return new VectorData(v.x, v.y, v.z);
        }

        public static implicit operator VectorData(Quaternion q)        {
            return q.eulerAngles;
        }

        public static implicit operator Vector3(VectorData v)        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static implicit operator Quaternion(VectorData v)        {
            return Quaternion.Euler(v);
        }		
    }
	
	public int CalculateCount()
	{
		return this.world.maps.Count + this.world.prefabs.Count + this.world.paths.Count;
	}
	
	/*
	private string Hash()
	{
		Checksum checksum = new Checksum();
		List<PrefabData> prefabs = this.world.prefabs;
		if (prefabs != null)
		{
			for (int i = 0; i < prefabs.Count; i++)
			{
				PrefabData prefabData = prefabs[i];
				checksum.Add(prefabData.id);
				checksum.Add(prefabData.position.x, 3);
				checksum.Add(prefabData.position.y, 3);
				checksum.Add(prefabData.position.z, 3);
				checksum.Add(prefabData.rotation.x, 3);
				checksum.Add(prefabData.rotation.y, 3);
				checksum.Add(prefabData.rotation.z, 3);
				checksum.Add(prefabData.scale.x, 3);
				checksum.Add(prefabData.scale.y, 3);
				checksum.Add(prefabData.scale.z, 3);
			}
		}
		return checksum.MD5();
	}
	
	public void CalculateChecksum()
	{
		this.Checksum = this.Hash();
	}
		*/

    public MapData GetMap(string name)    {
        for (int i = 0; i < world.maps.Count; i++){
            if (world.maps[i].name == name) return world.maps[i];
		}
        return null;
    }
	
    public void AddMap(string name, byte[] data)
    {
        var map = new MapData();

        map.name = name;
        map.data = data;

        world.maps.Add(map);
    }

    public void Save(string fileName)
    {
        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(Version);
					binaryWriter.Write(DateTimeOffset.Now.ToUnixTimeMilliseconds());
					
					Debug.Log("saved Version: " + Version + ", Stream Position: " + fileStream.Position);

                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Compress))
                        Serializer.Serialize(compressionStream, world);
						//Checksum = Hash();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
	
	public void SaveREPrefab(string fileName)
    {
		string checksum;

        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(REPrefabVersion);

                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Compress))
					{
						
                        Serializer.Serialize(compressionStream, rePrefab);
					}
                }
            }
			
				using (var md5 = System.Security.Cryptography.MD5.Create())
						{
							using(var stream = System.IO.File.OpenRead(fileName))
							{
								var hash = md5.ComputeHash(stream);
								checksum = BitConverter.ToString(hash).Replace("-", "").ToLower();
								
								Debug.LogError(checksum);
								rePrefab.checksum = checksum;
							}
						}
						
		using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(REPrefabVersion);

                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Compress))
					{
                        Serializer.Serialize(compressionStream, rePrefab);
					}
                }
            }
			
			
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
	
	public void SaveRMPrefab(string fileName)
    {
		string checksum;

        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(REPrefabVersion);

                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Compress))
					{
						
                        Serializer.Serialize(compressionStream, rmPrefab);
					}
                }
            }
			
				using (var md5 = System.Security.Cryptography.MD5.Create())
						{
							using(var stream = System.IO.File.OpenRead(fileName))
							{
								var hash = md5.ComputeHash(stream);
								checksum = BitConverter.ToString(hash).Replace("-", "").ToLower();
								
								Debug.Log(checksum);
								rmPrefab.checksum = checksum;
							}
						}
						
		using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var binaryWriter = new BinaryWriter(fileStream))
                {
                    binaryWriter.Write(REPrefabVersion);

                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Compress))
					{
                        Serializer.Serialize(compressionStream, rmPrefab);
					}
                }
            }
			
			
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
		
		Debug.Log("saved " + fileName);
    }
	
	
	public static byte[] SerializeTexture(Texture2D texture)
	{
		if (texture == null)
		{
			Debug.LogWarning("SerializeTexture(Texture2D): Texture is null, returning null.");
			return null;
		}

		Debug.Log($"SerializeTexture(Texture2D): Serializing texture '{texture.name}' with resolution {texture.width}x{texture.height}, format: {texture.format}");

		byte[] result = texture.EncodeToPNG();

		if (result == null || result.Length == 0)
		{
			Debug.LogError($"SerializeTexture(Texture2D): Failed to serialize texture '{texture.name}'. Result is null or empty.");
		}
		else
		{
			Debug.Log($"SerializeTexture(Texture2D): Successfully serialized texture '{texture.name}' to PNG, size: {result.Length} bytes.");
		}

		return result;
	}

	/// <summary>
	/// Serializes an R8 RenderTexture into the alpha channel of an RGBA32 texture, returning raw pixel data.
	/// </summary>
	/// <param name="renderTexture">The R8 RenderTexture to serialize.</param>
	/// <returns>A byte array containing the raw RGBA32 pixel data with R8 values in the alpha channel, or null if serialization fails.</returns>
	public static byte[] SerializeR8Texture(RenderTexture renderTexture)
	{
		if (renderTexture == null)
		{
			Debug.LogWarning("SerializeR8Texture(RenderTexture): RenderTexture is null, returning null.");
			return null;
		}

		if (renderTexture.format != RenderTextureFormat.R8)
		{
			Debug.LogWarning($"SerializeR8Texture(RenderTexture): RenderTexture '{renderTexture.name}' format is {renderTexture.format}, expected R8. Attempting to serialize anyway.");
		}

		Debug.Log($"SerializeR8Texture(RenderTexture): Serializing RenderTexture '{renderTexture.name}' with resolution {renderTexture.width}x{renderTexture.height}, format: {renderTexture.format}");

		// Create a temporary R8 Texture2D to read the RenderTexture data
		Texture2D tempR8Texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.R8, false)
		{
			name = "TempR8TextureConversion"
		};
		// Declare tempRgbaTexture outside try block
		Texture2D tempRgbaTexture = null;

		// Store the active RenderTexture and set the target RenderTexture as active
		RenderTexture currentActive = RenderTexture.active;
		RenderTexture.active = renderTexture;

		try
		{
			// Read pixels from the RenderTexture into the R8 Texture2D
			tempR8Texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
			tempR8Texture.Apply();

			// Get the raw R8 pixel data
			byte[] r8Data = tempR8Texture.GetRawTextureData<byte>().ToArray();
			if (r8Data == null || r8Data.Length == 0)
			{
				Debug.LogError($"SerializeR8Texture(RenderTexture): Failed to read R8 data from RenderTexture '{renderTexture.name}'.");
				return null;
			}

			// Create an RGBA32 pixel array and store R8 data in the alpha channel
			Color32[] rgbaPixels = new Color32[r8Data.Length];
			for (int i = 0; i < r8Data.Length; i++)
			{
				rgbaPixels[i] = new Color32(0, 0, 0, r8Data[i]); // R,G,B = 0, A = R8 value
			}

			// Create a temporary RGBA32 Texture2D to hold the final data
			tempRgbaTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false)
			{
				name = "TempRgbaTextureConversion"
			};
			tempRgbaTexture.SetPixels32(rgbaPixels);
			tempRgbaTexture.Apply();

			// Get the raw RGBA32 pixel data
			byte[] result = tempRgbaTexture.GetRawTextureData<byte>().ToArray();

			if (result == null || result.Length == 0)
			{
				Debug.LogError($"SerializeR8Texture(RenderTexture): Failed to serialize RenderTexture '{renderTexture.name}' to RGBA32. Result is null or empty.");
				return null;
			}

			Debug.Log($"SerializeR8Texture(RenderTexture): Successfully serialized RenderTexture '{renderTexture.name}' to RGBA32 raw byte array (R8 in alpha), size: {result.Length} bytes.");
			return result;
		}
		catch (Exception e)
		{
			Debug.LogError($"SerializeR8Texture(RenderTexture): Exception occurred while serializing RenderTexture '{renderTexture.name}': {e.Message}");
			return null;
		}
		finally
		{
			// Clean up
			if (tempR8Texture != null)
				UnityEngine.Object.DestroyImmediate(tempR8Texture);
			if (tempRgbaTexture != null)
				UnityEngine.Object.DestroyImmediate(tempRgbaTexture);
			RenderTexture.active = currentActive;
		}
	}

	public static byte[] SerializeTexture(RenderTexture renderTexture)
	{
		if (renderTexture == null)
		{
			Debug.LogWarning("SerializeTexture(RenderTexture): RenderTexture is null, returning null.");
			return null;
		}

		Debug.Log($"SerializeTexture(RenderTexture): Serializing RenderTexture '{renderTexture.name}' with resolution {renderTexture.width}x{renderTexture.height}, format: {renderTexture.format}");

		// Create a temporary Texture2D to hold the RenderTexture data
		Texture2D tempTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false)
		{
			name = "TempRenderTextureConversion"
		};

		// Store the active RenderTexture and set the target RenderTexture as active
		RenderTexture currentActive = RenderTexture.active;
		RenderTexture.active = renderTexture;

		// Read pixels from the RenderTexture into the Texture2D
		tempTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
		tempTexture.Apply();

		// Serialize the Texture2D to PNG
		byte[] result = tempTexture.EncodeToPNG();

		// Clean up
		UnityEngine.Object.Destroy(tempTexture);
		RenderTexture.active = currentActive;

		if (result == null || result.Length == 0)
		{
			Debug.LogError($"SerializeTexture(RenderTexture): Failed to serialize RenderTexture '{renderTexture.name}'. Result is null or empty.");
		}
		else
		{
			Debug.Log($"SerializeTexture(RenderTexture): Successfully serialized RenderTexture '{renderTexture.name}' to PNG, size: {result.Length} bytes.");
		}

		return result;
	}
	
	public static Texture2D DeserializeTexture(byte[] data, TextureFormat format)
	{
		if (data == null)
		{
			Debug.LogError("Texture data is null.");
			return null;
		}

		// Create a temporary Texture2D to load the PNG data
		Texture2D texture = new Texture2D(2, 2, format, false); // Dummy size, will be resized by LoadImage
		if (!texture.LoadImage(data))
		{
			UnityEngine.Object.Destroy(texture);
			Debug.LogError("Failed to load PNG data into Texture2D.");
			return null;
		}

		// Texture is now loaded with its natural resolution
		return texture;
	}

	public static RenderTexture DeserializeTexture(byte[] data, RenderTextureFormat format)
	{
		if (data == null)
		{
			Debug.LogError("Texture data is null.");
			return null;
		}

		// Create a temporary Texture2D to load the PNG data
		Texture2D tempTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false); // Dummy size, will be resized by LoadImage
		if (!tempTexture.LoadImage(data))
		{
			UnityEngine.Object.Destroy(tempTexture);
			Debug.LogError("Failed to load PNG data into Texture2D.");
			return null;
		}

		// Create a RenderTexture with the natural dimensions of the loaded texture
		RenderTexture renderTexture = new RenderTexture(tempTexture.width, tempTexture.height, 0, format, RenderTextureReadWrite.Linear)
		{
			wrapMode = TextureWrapMode.Clamp,
			enableRandomWrite = true
		};
		renderTexture.Create();

		// Copy the Texture2D pixels to the RenderTexture
		RenderTexture currentActive = RenderTexture.active;
		RenderTexture.active = renderTexture;
		Graphics.Blit(tempTexture, renderTexture);

		// Clean up
		UnityEngine.Object.Destroy(tempTexture);
		RenderTexture.active = currentActive;

		return renderTexture;
	}
	
	//outputs decompressed lz4 binary file protobuftest
	[ConsoleCommand("unpack LZ4")]
	public static void Decompress(string fileName)
	{
		Debug.Log("decompressing...");
		try
		{
			using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
				using (var binaryReader = new BinaryReader(fileStream))
                {
						Version = binaryReader.ReadUInt32();
						binaryReader.ReadUInt64();
						
						using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Decompress))
							{
								MemoryStream destination = new MemoryStream();
								compressionStream.CopyTo(destination);
								var b = new byte[destination.Length];
								b = destination.ToArray();
								File.WriteAllBytes("protobuftest", b);
								Debug.LogError("Actually decompressed");
							}
				}
			}
		}	
		catch (Exception e)
		{
			Debug.LogError(e.Message);
		}
	}

    public void Load(string fileName)
    {
		
		try
        {
		    using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
				
				using (var binaryReader = new BinaryReader(fileStream))
                {
                    Version = binaryReader.ReadUInt32();

                    if (Version != 9)		{
						
						if (Version ==10){
							binaryReader.ReadInt64();
						}
						else{
							Debug.LogError("wrong version:" + Version);
						}
					}
					
					Debug.Log("Loaded Version: " + Version + ", Stream Position: " + fileStream.Position);
					
					using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Decompress))
					{
						world = Serializer.Deserialize<WorldData>(compressionStream);
					}
					
					
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
	
	public void LoadREPrefab(string fileName)
    {
        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
				using (var binaryReader = new BinaryReader(fileStream))
                {
					uint ver = binaryReader.ReadUInt32();
					//Debug.LogError(ver);
					
                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Decompress))
					{
						
						rePrefab = Serializer.Deserialize<REPrefabData>(compressionStream);
						
					}
				}
                
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
	
	public void LoadRMPrefab(string fileName)
    {
        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
				using (var binaryReader = new BinaryReader(fileStream))
                {
					uint ver = binaryReader.ReadUInt32();
					//Debug.LogError(ver);
					
                    using (var compressionStream = new LZ4Stream(fileStream, LZ4StreamMode.Decompress))
					{
						
						rmPrefab = Serializer.Deserialize<RMPrefabData>(compressionStream);
						
					}
				}
                
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
	
	
        // Wrapper class to hold both REPrefabData and List<PathData> for JSON serialization
        [Serializable]
        public class PrefabAndPathData
        {
            public REPrefabData rePrefab;
            public List<PathData> paths;

            public PrefabAndPathData(REPrefabData rePrefab, List<PathData> paths)
            {
                this.rePrefab = rePrefab;
                this.paths = paths;
            }
        }

        // Modified SavePrefabJSON to include path information
        public void SavePrefabJSON(string fileName, List<PathData> paths)
        {
            try
            {
                // Validate inputs
                if (rePrefab == null)
                {
                    Debug.LogError("SavePrefabJSON: rePrefab is null. Initializing to default.");
                    rePrefab = new REPrefabData();
                }
                if (paths == null)
                {
                    Debug.LogWarning("SavePrefabJSON: paths parameter is null. Initializing to empty list.");
                    paths = new List<PathData>();
                }

                // Log data for debugging
                Debug.Log($"SavePrefabJSON: Saving to {fileName}, rePrefab.prefabs.Count={rePrefab.prefabs.Count}, paths.Count={paths.Count}");
                foreach (var path in paths)
                {
                    Debug.Log($"SavePrefabJSON: Path - name={path.name}, nodes.Length={path.nodes?.Length ?? 0}, spline={path.spline}");
                }

                // Create an instance of the wrapper class with the provided paths
                var prefabAndPathData = new PrefabAndPathData(rePrefab, paths);

                // Serialize to JSON with Newtonsoft.Json
                string json = JsonConvert.SerializeObject(prefabAndPathData, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore, // Handle circular references
                    NullValueHandling = NullValueHandling.Include // Include null values
                });

                // Write JSON to file
                File.WriteAllText(fileName, json);
                Debug.Log($"SavePrefabJSON: Saved JSON with {rePrefab.prefabs.Count} prefabs and {paths.Count} paths to {fileName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"SavePrefabJSON: Error saving JSON - {e.Message}");
            }
        }
	
}



