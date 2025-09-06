using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
	using UnityEditor.IMGUI.Controls;
#endif

namespace RustMapEditor.Variables
{
	//console command attribute format
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public sealed class ConsoleCommandAttribute : Attribute
	{
		public string Description { get; }

		public ConsoleCommandAttribute(string description)
		{
			Description = description;
		}
	}
	
	//console variable attribute format
	[AttributeUsage(AttributeTargets.Struct)]
	public class ConsoleVariableAttribute : Attribute
	{
		public string Description { get; }

		public ConsoleVariableAttribute(string description)
		{
			Description = description;
		}
	}
	
	[Serializable]
	public struct WindowState
	{
		public bool isActive;           // Whether the window is active
		public Vector3 position;        // RectTransform position
		public Vector3 scale;           // RectTransform scale

		public WindowState(bool isActive, Vector3 position, Vector3 scale)
		{
			this.isActive = isActive;
			this.position = position;
			this.scale = scale;
		}
	}

	[Serializable]
	public struct MenuState
	{
		public Vector3 scale;           // Menu's RectTransform scale
		public Vector3 position;
	
		public MenuState(Vector3 scale, Vector3 position)
		{
			this.scale = scale;
			this.position = position;
		}
	}
	
	public interface IUndoAction
    {
        string OperationName { get; }
        void Undo();
        void Redo();
        void OnRemoved();
        long EstimateMemoryUsage();
    }
	
	public class TransformUndoAction : IUndoAction
    {
        private readonly RTG.IUndoRedoAction _action;
        public string OperationName { get; }

        public TransformUndoAction(string name, RTG.IUndoRedoAction action)
        {
            OperationName = name;
            _action = action;
        }

        public void Undo()
        {
            _action.Undo();
        }

        public void Redo()
        {
            _action.Redo();
        }

        public void OnRemoved()
        {
            _action.OnRemovedFromUndoRedoStack();
        }

        public long EstimateMemoryUsage()
        {
            // Placeholder: Estimate based on transform data (e.g., position, rotation, scale)
            return 1024; // Assume 1KB per transform action
        }
    }
	
	public class TerrainUndoAction : IUndoAction
	{
		private readonly TerrainUndoState _undoState;
		private readonly TerrainUndoState _redoState;
		public string OperationName { get => _undoState.OperationName; }

		public TerrainUndoAction(TerrainUndoState undoState, TerrainUndoState redoState)
		{
			_undoState = undoState;
			_redoState = redoState;
		}

		public void Undo()
		{
			TerrainUndoManager.ApplyState(_undoState);
		}

		public void Redo()
		{
			TerrainUndoManager.ApplyState(_redoState);
		}

		public void OnRemoved()
		{
			// No cleanup needed
		}

		public long EstimateMemoryUsage()
		{
			return _undoState.EstimateMemoryUsage() + _redoState.EstimateMemoryUsage();
		}
	}
	
	public class TerrainUndoState
    {
        public string OperationName { get; }
        public TerrainUndoManager.TerrainOperationType OperationType { get; }
        public object Data { get; }
        public int StartX { get; }
        public int StartY { get; }
        public int Width { get; }
        public int Height { get; }
        public TerrainManager.TerrainType TerrainType { get; }
        public TerrainManager.LayerType LayerType { get; }
        public int TopologyLayer { get; }

        public TerrainUndoState(
            string name,
            TerrainUndoManager.TerrainOperationType operationType,
            object data,
            int startX,
            int startY,
            int width,
            int height,
            TerrainManager.TerrainType terrainType = TerrainManager.TerrainType.Land,
            TerrainManager.LayerType layerType = TerrainManager.LayerType.Ground,
            int topologyLayer = -1)
        {
            OperationName = name;
            OperationType = operationType;
            Data = data;
            StartX = startX;
            StartY = startY;
            Width = width;
            Height = height;
            TerrainType = terrainType;
            LayerType = layerType;
            TopologyLayer = topologyLayer;
        }

        public long EstimateMemoryUsage()
        {
            long size = 0;
            if (Data is float[,] float2D) size = float2D.Length * sizeof(float);
            else if (Data is float[,,] float3D) size = float3D.Length * sizeof(float);
            else if (Data is bool[,] bool2D) size = bool2D.Length * sizeof(bool);
            else if (Data is int[,] int2D) size = int2D.Length * sizeof(int);
            else if (Data is Color[] colors) size = colors.Length * sizeof(float) * 4;
            return size;
        }

        public void LogMemoryUsage()
        {
            Debug.Log($"State '{OperationName}' memory usage: {(EstimateMemoryUsage() / (1024f * 1024f)):F2} MB");
        }
    }
	
    public struct Conditions
    {
        public GroundConditions GroundConditions;
        public BiomeConditions BiomeConditions;
        public AlphaConditions AlphaConditions;
        public TopologyConditions TopologyConditions;
        public TerrainConditions TerrainConditions;
        public AreaConditions AreaConditions;
    }
    public struct GroundConditions
    {
        public GroundConditions(TerrainSplat.Enum layer)
        {
            Layer = layer;
            Weight = new float[TerrainSplat.COUNT];
            CheckLayer = new bool[TerrainSplat.COUNT];
        }
        public TerrainSplat.Enum Layer;
        public float[] Weight;
        public bool[] CheckLayer;
    }
    public struct BiomeConditions
    {
        public BiomeConditions(TerrainBiome.Enum layer)
        {
            Layer = layer;
            Weight = new float[TerrainBiome.COUNT];
            CheckLayer = new bool[TerrainBiome.COUNT];
        }
        public TerrainBiome.Enum Layer;
        public float[] Weight;
        public bool[] CheckLayer;
    }
    public struct AlphaConditions
    {
        public AlphaConditions(AlphaTextures texture)
        {
            Texture = texture;
            CheckAlpha = false;
        }
        public AlphaTextures Texture;
        public bool CheckAlpha;
    }
    public struct TopologyConditions
    {
        public TopologyConditions(TerrainTopology.Enum layer)
        {
            Layer = layer;
            Texture = new TopologyTextures[TerrainTopology.COUNT];
            CheckLayer = new bool[TerrainTopology.COUNT];
        }
        public TerrainTopology.Enum Layer;
        public TopologyTextures[] Texture;
        public bool[] CheckLayer;
    }
    public struct TerrainConditions
    {
        public HeightsInfo Heights;
        public bool CheckHeights;
        public SlopesInfo Slopes;
        public bool CheckSlopes;
    }
    public struct AreaConditions
    {
        public AreaManager.Area Area;
        public bool CheckArea;
    }
    
    public enum AlphaTextures
    {
        Visible = 0,
        Invisible = 1,
    }
    public enum TopologyTextures
    {
        Active = 0,
        InActive = 1,
    }
    public struct SlopesInfo
    {
        public bool BlendSlopes;
        public float SlopeBlendLow;
        public float SlopeLow;
        public float SlopeHigh;
        public float SlopeBlendHigh;
    }
	public struct CurvesInfo
    {
        public bool BlendCurves;
        public float CurveBlendLow;
        public float CurveLow;
        public float CurveHigh;
        public float CurveBlendHigh;
    }
    public struct HeightsInfo
    {
        public bool BlendHeights;
        public float HeightBlendLow;
        public float HeightLow;
        public float HeightHigh;
        public float HeightBlendHigh;
    }
    public class Selections
    {
        public enum Objects
        {
            Ground = 1 << 0,
            Biome = 1 << 1,
            Alpha = 1 << 2,
            Topology = 1 << 3,
            Heightmap = 1 << 4,
            Watermap = 1 << 5,
            Prefabs = 1 << 6,
            Paths = 1 << 7,
        }
    }

    public class Layers
    {
        public TerrainSplat.Enum Ground;
        public TerrainBiome.Enum Biome;
        public TerrainTopology.Enum Topologies;
        public TerrainManager.LayerType Layer;
        public AlphaTextures AlphaTexture;
        public TopologyTextures TopologyTexture;
    }
	
	public struct monumentData

	{
		public monumentData(int X, int Y, int Width, int Height)
		{
			x=X;
			y=Y;
			width=Width;
			height=Height;
		}
		public int x,y,width,height;
	}
	
	
	
	public enum ColliderLayer
	{
		All = Physics.AllLayers,
		Prefabs = 1<<3,
		Volumes = 1<<2,
		Paths = 1<<9,
		Land = 1<<10,
		Water = 1<<4,
	}
	
	
	[Serializable]
	public class GeologyPresetCollection
	{
		public GeologyPreset[] geoPresets;
	}
	
	[ConsoleVariable("settings for randomTerracing")]
	[Serializable]
	public struct TerracingPreset
	{
		public bool flatten, perlinBanks, circular;
		public float weight;
		public int zStart, gateBottom, gateTop, gates, descaleFactor, perlinDensity;
	}
	
	[ConsoleVariable("settings for perlinSimple and perlinRidiculous")]
	[Serializable]
	public struct PerlinPreset
	{
		public int layers, period, scale;
		public bool simple;
	}
	
	[ConsoleVariable("settings for perlinSplat")]
	[Serializable]
	public struct PerlinSplatPreset
	{
		public int scale, splatLayer;
		public TerrainBiome.Enum biomeLayer;
		public float strength;
		public bool invert, paintBiome;
	}
	
	[ConsoleVariable("settings for figuredRippling")]
	[Serializable]
	public struct RipplePreset
	{
		public int size, density;
		public float weight;
	}
	
	[ConsoleVariable("settings for splatCrazing")]
	[Serializable]
	public struct CrazingPreset
	{
		public string title;
		public int zones, minSize, maxSize, splatLayer;
		
	}
	
	[Serializable]
	[ProtoContract]
	public struct BreakerPreset
	{
		[ProtoMember(1)]public string title;
		[ProtoMember(2)]public MonumentData monument;
	}
	
	[ConsoleVariable("settings for ocean")]
	[Serializable]
	public struct OceanPreset
	{
		public string title;
		public int radius, gradient, xOffset, yOffset, s, seafloor;
		public bool perlin;
	}
	//int radius, int gradient, float seafloor, int xOffset, int yOffset, bool perlin, int s
	
	    // Serializable dictionary for socket data
    [Serializable]
	[ProtoContract]
    public class SocketData
    {
        // Directly use Dictionary with Json.NET
        [ProtoMember(1)]public Dictionary<string, List<SocketInfo>> Dictionary = new Dictionary<string, List<SocketInfo>>();

        [ProtoMember(2)]public List<SocketInfo> this[string key]
        {
            get => Dictionary.ContainsKey(key) ? Dictionary[key] : null;
            set => Dictionary[key] = value;
        }
    }
	
	[Serializable]
	[ProtoContract]
	public struct SocketInfo
	{
		[ProtoMember(1)]
		public DungeonBaseSocketType Type;

		[ProtoMember(2)]
		public bool Male;

		[ProtoMember(3)]
		public bool Female;

		[ProtoMember(4)]
		public WorldSerialization.VectorData Position;

		// Store rotation as Euler angles (Vector3)
		[ProtoMember(5)]
		public WorldSerialization.VectorData Rotation;

		// Helper method to create SocketInfo from DungeonBaseSocket
		public static SocketInfo FromDungeonBaseSocket(DungeonBaseSocket socket)
		{
			return new SocketInfo
			{
				Type = socket.Type,
				Male = socket.Male,
				Female = socket.Female,
				Position = socket.transform.localPosition,
				Rotation = socket.transform.rotation.eulerAngles // Convert Quaternion to Euler
			};
		}

	}
	
	[Serializable]
	[ProtoContract]
	public class Colliders
	{
		[ProtoMember(1)]public WorldSerialization.VectorData box = new WorldSerialization.VectorData();
		[ProtoMember(2)]public WorldSerialization.VectorData sphere = new WorldSerialization.VectorData();
		[ProtoMember(3)]public WorldSerialization.VectorData capsule = new WorldSerialization.VectorData();
		
		public Colliders() { }
        public Colliders(Vector3 box, Vector3 sphere, Vector3 capsule)
        {
            this.box = box;
			this.sphere = sphere;
			this.capsule = capsule;
        }
	}
	
	[Serializable]
	[ProtoContract]
	public struct BreakingData
	{
		[ProtoMember(1)]public string name;
		[ProtoMember(2)]public uint id;
		[ProtoMember(3)]public bool ignore;
		[ProtoMember(4)]public int treeID;
		[ProtoMember(5)]public Colliders colliderScales;
		[ProtoMember(6)]public WorldSerialization.PrefabData prefabData;
		[ProtoMember(7)]public string parent;
		[ProtoMember(8)]public string treePath;
		[ProtoMember(9)]public string monument;
		
	}
	
	[Serializable]
	public struct Bind
	{
		public string bindName; // e.g., "moveForward", "placePrefab"
		public InputActionType actionType; // Button (for press/hold) or Value (for continuous)
		public string primaryInput; // e.g., "<Keyboard>/w", "<Mouse>/leftButton"
		public bool requiresCtrl; // Modifier: Ctrl key
		public bool requiresShift; // Modifier: Shift key
		public bool requiresAlt; // Modifier: Alt key

		public Bind(string name, InputActionType type, string input, bool ctrl = false, bool shift = false, bool alt = false)
		{
			bindName = name;
			actionType = type;
			primaryInput = input;
			requiresCtrl = ctrl;
			requiresShift = shift;
			requiresAlt = alt;
		}
	}
	
	[Serializable]
	[ProtoContract]
	public class MonumentData
	{
		[ProtoMember(1)]public List<CategoryData> category = new List<CategoryData>();
		[ProtoMember(2)]public string monumentName;
	}
	
	[Serializable]
	[ProtoContract]
	public class GreatGreatGrandchildrenData
	{
		[ProtoMember(1)]public BreakingData breakingData = new BreakingData();
		
		public GreatGreatGrandchildrenData(){ }
		public GreatGreatGrandchildrenData(BreakingData breakingData)
		{
			this.breakingData = breakingData;
		}
	}
	
	[Serializable]	
	[ProtoContract]
	public class GreatGrandchildrenData
	{
		[ProtoMember(1)]public BreakingData breakingData = new BreakingData();
		[ProtoMember(2)]public List<GreatGreatGrandchildrenData> greatgreatgrandchild = new List<GreatGreatGrandchildrenData>();
		
		public GreatGrandchildrenData(){ }
		public GreatGrandchildrenData(BreakingData breakingData)
		{
			this.breakingData = breakingData;
		}
	}
	
	[Serializable]
	[ProtoContract]
	public class GrandchildrenData
	{
		[ProtoMember(1)]public BreakingData breakingData = new BreakingData();
		[ProtoMember(2)]public List<GreatGrandchildrenData> greatgrandchild = new List<GreatGrandchildrenData>();
		
		public GrandchildrenData(){ }
		public GrandchildrenData(BreakingData breakingData)
		{
			this.breakingData = breakingData;
		}
	}
	
	[Serializable]
	[ProtoContract]
	public class ChildrenData
	{
		[ProtoMember(1)]public BreakingData breakingData = new BreakingData();
		[ProtoMember(2)]public List<GrandchildrenData> grandchild = new List<GrandchildrenData>();
		
		public ChildrenData(){ }
		public ChildrenData(BreakingData breakingData)
		{
			this.breakingData = breakingData;
		}
	}
	
	[Serializable]
	[ProtoContract]
	public class CategoryData
	{
		[ProtoMember(1)]public BreakingData breakingData = new BreakingData();
		[ProtoMember(2)]public List<ChildrenData> child = new List<ChildrenData>();
		
		public CategoryData(){ }
		public CategoryData(BreakingData breakingData)
		{
			this.breakingData = breakingData;
		}
	}

	public class IconTextures
	{
		public Texture2D gears;
		public Texture2D scrap;
		public Texture2D stop;
		public Texture2D tarp;
		public Texture2D trash;
		public IconTextures(Texture2D gears, Texture2D scrap, Texture2D stop, Texture2D tarp, Texture2D trash)
		{
			this.gears = gears; this.scrap = scrap; this.stop = stop; this.tarp = tarp; this.trash = trash;
		}
	}
	
	#if UNITY_EDITOR
		
	public class BreakingItem : TreeViewItem 
	{
		public BreakingData breakingData;
		
		public BreakingItem(TreeViewItem treeItem, BreakingData breakingData)
		{
			this.displayName = breakingData.name;
			
			this.breakingData = breakingData;
		}
	}
	
	public class BreakerTreeView : TreeView
	{
		public MonumentData monumentFragments;
		public IconTextures icons;
		public List<BreakingData> fragment = new List<BreakingData>();
		
		public IList<int> ChildList(int ID)
		{
			IList<int> IDlist = new List<int>();
			TreeViewItem parent =  this.FindItem(ID, rootItem);
			
			if (parent.hasChildren)
			{
				
				List<TreeViewItem> childList =  parent.children;
				foreach (TreeViewItem item in this.FindItem(ID, rootItem).children)
				{
					IDlist.Add(item.id);
				}
			}
			else
			{
				IDlist.Add(parent.id);
			}
			return IDlist;
		}
		
		public void ClearSelection()
		{
			IList<int> IDlist = new List<int>();
			this.SetSelection(IDlist);
		}
		
		public void ConcatSelection(IList<int> newSelection)
		{
			this.SetSelection(this.GetSelection().Concat(newSelection).ToList());
		}
		
		public void LoadFragments(MonumentData fragments)
		{
			monumentFragments = fragments;
			Reload();
		}
		
		public void LoadIcons(IconTextures iconLoader)
		{
			icons = iconLoader;
		}
		
		public void Update()
		{	
			if (monumentFragments != null)
			{
					for (int i = 0; i < monumentFragments.category.Count; i++) 
					{
						
						monumentFragments.category[i].breakingData = fragment[monumentFragments.category[i].breakingData.treeID];
						
						for (int j = 0; j <monumentFragments.category[i].child.Count; j++)
						{
							monumentFragments.category[i].child[j].breakingData = fragment[monumentFragments.category[i].child[j].breakingData.treeID];
								
							for (int k = 0; k <monumentFragments.category[i].child[j].grandchild.Count; k++)
							{
								monumentFragments.category[i].child[j].grandchild[k].breakingData = fragment[monumentFragments.category[i].child[j].grandchild[k].breakingData.treeID];
								
								for (int m = 0; m <monumentFragments.category[i].child[j].grandchild[k].greatgrandchild.Count; m++)
								{
									monumentFragments.category[i].child[j].grandchild[k].greatgrandchild[m].breakingData = fragment[monumentFragments.category[i].child[j].grandchild[k].greatgrandchild[m].breakingData.treeID];
								}
							}
						}
					}
			}
			Reload();
		}
		
		public BreakerTreeView(TreeViewState treeViewState)
			: base(treeViewState)
		{
			Reload();
		}
		
		
		
		protected override TreeViewItem BuildRoot ()
		{
			var root = new TreeViewItem {id = 0, depth = -1, displayName = "Root"};
			int idCount = 0;
			fragment = new List<BreakingData>();
			
			BreakingItem childTree, grandchildTree, greatgrandchildTree, greatgreatgrandchildTree;
			if (monumentFragments != null)
			{
					for (int i = 0; i < monumentFragments.category.Count; i++) 
					{
						monumentFragments.category[i].breakingData.treeID = idCount;						
						childTree = new BreakingItem (new TreeViewItem {id = idCount, displayName = monumentFragments.category[i].breakingData.name}, monumentFragments.category[i].breakingData);
						fragment.Add(monumentFragments.category[i].breakingData);
						childTree.id = idCount;
						childTree.icon = PrefabManager.GetIcon(monumentFragments.category[i].breakingData, icons);
						childTree.displayName = monumentFragments.category[i].breakingData.name;
						root.AddChild (childTree);
						idCount++;
						
						for (int j = 0; j <monumentFragments.category[i].child.Count; j++)
						{
							monumentFragments.category[i].child[j].breakingData.treeID = idCount;
							grandchildTree = new BreakingItem (new TreeViewItem {id = idCount, displayName = monumentFragments.category[i].child[j].breakingData.name}, monumentFragments.category[i].child[j].breakingData);
							fragment.Add(monumentFragments.category[i].child[j].breakingData);
							grandchildTree.id = idCount;
							grandchildTree.icon = PrefabManager.GetIcon(monumentFragments.category[i].child[j].breakingData, icons);
							childTree.AddChild(grandchildTree);
							idCount++;
							
							for (int k = 0; k <monumentFragments.category[i].child[j].grandchild.Count; k++)
							{
								monumentFragments.category[i].child[j].grandchild[k].breakingData.treeID = idCount;
								greatgrandchildTree = new BreakingItem(new TreeViewItem {id = idCount, displayName = monumentFragments.category[i].child[j].grandchild[k].breakingData.name}, monumentFragments.category[i].child[j].grandchild[k].breakingData);
								fragment.Add(monumentFragments.category[i].child[j].grandchild[k].breakingData);
								greatgrandchildTree.id = idCount;
								greatgrandchildTree.icon = PrefabManager.GetIcon(monumentFragments.category[i].child[j].grandchild[k].breakingData, icons);
								grandchildTree.AddChild(greatgrandchildTree);
								idCount++;
								
								for (int m = 0; m <monumentFragments.category[i].child[j].grandchild[k].greatgrandchild.Count; m++)
								{
									monumentFragments.category[i].child[j].grandchild[k].greatgrandchild[m].breakingData.treeID = idCount;
									greatgreatgrandchildTree = new BreakingItem(new TreeViewItem {id = idCount, displayName = monumentFragments.category[i].child[j].grandchild[k].greatgrandchild[m].breakingData.name}, monumentFragments.category[i].child[j].grandchild[k].greatgrandchild[m].breakingData);
									fragment.Add(monumentFragments.category[i].child[j].grandchild[k].greatgrandchild[m].breakingData);
									greatgreatgrandchildTree.id = idCount;
									greatgreatgrandchildTree.icon = PrefabManager.GetIcon(monumentFragments.category[i].child[j].grandchild[k].greatgrandchild[m].breakingData, icons);
									greatgrandchildTree.AddChild(greatgreatgrandchildTree);
									idCount++;
								}
							}
							
						}
						
					}
			}
			else
			{
				root.AddChild(new TreeViewItem   { id = 1, displayName = " " });
			}
			
			SetupDepthsFromParentsAndChildren(root);

			return root;
		}
	}
	
	#endif
	
	[Serializable]
	public struct FragmentPair
	{
		public string fragment;
		public uint id;
		
		public FragmentPair(string fragment,uint id)
		{
			this.fragment = fragment;
			this.id = id;
		}
	}
	
	[Serializable]
	public class FragmentLookup
	{
		public List<FragmentPair> fragmentPairs = new List<FragmentPair>();
		public Dictionary<string,uint> fragmentNamelist = new Dictionary<string,uint>();
		
		public void LoadPairList(List<FragmentPair> fragmentPairs)
		{
			this.fragmentPairs = fragmentPairs;
		}
		
		public void Deserialize()
		{
			this.fragmentNamelist = SettingsManager.ListToDict(this.fragmentPairs);
		}
		
		public void Serialize()
		{
			this.fragmentPairs = SettingsManager.DictToList(this.fragmentNamelist);
		}
		
	}
	
	[Serializable]
	public struct ReplacerPreset
	{

				public uint prefabID0;
				public uint prefabID1;
				public uint prefabID2;
				public uint prefabID3;
				public uint prefabID4;
				public uint prefabID5;
				public uint prefabID6;
				public uint prefabID7;				
				public uint prefabID8;
				public uint prefabID9;
				public uint prefabID10;
				public uint prefabID11;
				public uint prefabID12;
				public uint prefabID13;
				public uint prefabID14;
				public uint prefabID15;
				public uint prefabID16;
				
				public uint replaceID0;
				public uint replaceID1;
				public uint replaceID2;
				public uint replaceID3;
				public uint replaceID4;
				public uint replaceID5;
				public uint replaceID6;
				public uint replaceID7;				
				public uint replaceID8;
				public uint replaceID9;
				public uint replaceID10;
				public uint replaceID11;
				public uint replaceID12;
				public uint replaceID13;
				public uint replaceID14;
				public uint replaceID15;
				public uint replaceID16;
				
				public bool rotateToTerrain, rotateToX, rotateToY, rotateToZ;
				public string title;
				public bool scale;
				public Vector3 scaling;
	}

	[Serializable]
	public class GeologyItem
	{
		public string customPrefab;
		public uint prefabID;
		public int emphasis;
		public bool custom;
		public GeologyItem Clone()		{
			
			return new GeologyItem			{
				custom = this.custom,
				customPrefab = this.customPrefab,
				prefabID = this.prefabID,
				emphasis = this.emphasis
			};
		}
		
		public GeologyItem(uint prefabID)		{
			this.prefabID = prefabID;
		}
		
		public GeologyItem(GeologyItem geoItem)		{
			this.prefabID = geoItem.prefabID;
			this.custom =  geoItem.custom;
			this.emphasis = geoItem.emphasis;
			this.customPrefab = geoItem.customPrefab;
		}
		
		public GeologyItem()		{
		}
	}
	
	[Serializable]
	public class GeologyCollisions
	{
		public bool minMax;
		public float radius;
		public ColliderLayer layer;
		
		public GeologyCollisions(GeologyCollisions geoCollisions)
		{
			this.minMax = geoCollisions.minMax;
			this.radius = geoCollisions.radius;
			this.layer = geoCollisions.layer;
		}
		public GeologyCollisions()
		{		}
	}
	
	[Serializable]
	public struct Favorites
	{
		public List<string> favoriteCustoms;

	}
	
	[Serializable]
	public class GeologyMacroWrapper
	{
		public List<string> macroList;
	}
	
	[Serializable]
	public class WebRoadPreset
	{
		public float WaterlineHeight = 0.501f;
		public float MinNodeDistance = 15f;
		public float MaxNodeDistance = 20f;
		public float MaxSlope = 25f;
		public float MapEdgeDistance = 30f;
		public float CollisionRadius = 10f;
		public int MaxRetries = 5;

		public static readonly WebRoadPreset Default = new WebRoadPreset
		{
			WaterlineHeight = 0.501f,
			MinNodeDistance = 15f,
			MaxNodeDistance = 20f,
			MaxSlope = 25f,
			MapEdgeDistance = 30f,
			CollisionRadius = 10f,
			MaxRetries = 5
		};
	}
		
	[Serializable] 
	public struct Blacklist{
		public List<ItemSettings> blacklistItems;
	}
	
	[Serializable]
	public struct ItemSettings{
		public string path;
		public bool hidden;
		public bool blacklisted;
	}
		
	[Serializable]
	public struct GeologyPreset
	{
				
				public List<GeologyItem> geologyItems;
				public List<GeologyCollisions> geologyCollisions;
				public GeologyCollisions newCollisions;
				public string filename;
				public string title;

				
				public int density, frequency, floor, ceiling, biomeIndex, seed, spawns;
				
				public TerrainBiome.Enum biomeLayer;
				
				
				public ColliderLayer colliderLayer, closeColliderLayer;
				public bool avoidTopo, flipping, tilting, normalizeX, normalizeY, normalizeZ, biomeExclusive, cliffTest, overlap, closeOverlap, temperate, arid, arctic, tundra, jungle, road, monument, dither, useSeed, slopeRange, curveRange, heightRange; 
				public bool hAscend, hDescend, sAscend, sDescend, cAscend,cDescend;
				public bool featureMenu, rotationMenu, scaleMenu, placementMenu, collisionMenu, presetMenu, jitterMenu, preview;
				
				public Vector3 scalesLow, scalesHigh, rotationsLow, rotationsHigh, jitterLow, jitterHigh, slideHigh, slideLow;
				public float zOffset, colliderDistance, closeColliderDistance, balance;
				public float slopeLow, slopeHigh; //legacy
				public HeightSelector heights;
				public Topologies topologies;
				

				
			// Default constructor with improved initialization
			public GeologyPreset(string title) : this()
			{
				this.title = title ?? "DefaultPreset"; // Ensure title is not null
				this.filename = title ?? "DefaultPreset"; // Set filename to match title
				this.geologyItems = new List<GeologyItem>(); // Initialize empty list
				this.geologyCollisions = new List<GeologyCollisions>(); // Initialize empty list
				this.newCollisions = new GeologyCollisions(); // Initialize with default values

				this.heights = new HeightSelector
				{
					slopeLow = 0f,
					slopeHigh = 90f,
					heightMin = 0f,
					heightMax = 1000f,
					curveMin = 0f,
					curveMax = 1f,
					slopeWeight = 1f,
					curveWeight = 1f
				}; // Reasonable defaults for height and slope selection
			}
				
	}
	
	public class GeologySpawner{
		public int i,j;  //map location
		public float height, slope, curve; //sorting values
	}

	[Serializable][System.Flags]
		public enum Topologies
		{
			Field = 1 << 0,
			Cliff = 1 << 1,
			Summit = 1 << 2,
			Beachside = 1 << 3,
			Beach = 1 << 4,
			Forest = 1 << 5,
			Forestside = 1 << 6,
			Ocean = 1 << 7,
			Oceanside = 1 << 8,
			Decor = 1 << 9,
			Monument = 1 << 10,
			Road = 1 << 11,
			Roadside = 1 << 12,
			Swamp = 1 << 13,
			River = 1 << 14,
			Riverside = 1 << 15,
			Lake = 1 << 16,
			Lakeside = 1 << 17,
			Offshore = 1 << 18,
			Powerline = 1 << 19,
			Runway = 1 << 20,
			Building = 1 << 21,
			Cliffside = 1 << 22,
			Mountain = 1 << 23,
			Clutter = 1 << 24,
			Alt = 1 << 25,
			Tier0 = 1 << 26,
			Tier1 = 1 << 27,
			Tier2 = 1 << 28,
			Mainland = 1 << 29,
			Hilltop = 1 << 30,
		}
	
	
	[Serializable]
	public struct HeightSelector
	{
		public float slopeLow, slopeHigh, heightMin, heightMax, curveMin, curveMax, slopeWeight, curveWeight;
	}
	
	

	[Serializable]
	public struct RustCityPreset
	{
		public string title;
		public int size, alley, street, start;
		public float flatness;
		public float zOff;
		public int x, y;
	}
	
	[Serializable]
	public struct FilePreset
	{
		    public string rustDirectory;
			public float prefabRenderDistance;
			public float pathRenderDistance;
			public float waterTransparency;
			public bool loadbundleonlaunch;
			public bool terrainTextureSet;
			public int loadBatch;
			public int newSize;
			public float newHeight;
			public TerrainBiome.Enum newBiome;
			public TerrainSplat.Enum newSplat;
			public string startupSkin;
			public List<string> recentFiles;
	}
	
/*
	//this is our original terrain undo state 
	public struct TerrainUndoState
{
    public string OperationName { get; private set; }
    public TerrainUndoManager.TerrainOperationType OperationType { get; private set; }
    public object Data { get; private set; }
    public int StartX { get; private set; }
    public int StartY { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public TerrainManager.TerrainType TerrainType { get; private set; }
    public TerrainManager.LayerType LayerType { get; private set; }
    public int TopologyLayer { get; private set; }

    // Constructor for HeightMap, AlphaMap, TopologyMap
    public TerrainUndoState(string name, TerrainUndoManager.TerrainOperationType operationType, object data,
        int startX, int startY, int width, int height,
        TerrainManager.TerrainType terrainType = TerrainManager.TerrainType.Land,
        TerrainManager.LayerType layerType = TerrainManager.LayerType.Ground,
        int topologyLayer = -1)
    {
        OperationName = name;
        OperationType = operationType;
        Data = data;
        StartX = startX;
        StartY = startY;
        Width = width;
        Height = height;
        TerrainType = terrainType;
        LayerType = layerType;
        TopologyLayer = topologyLayer;
    }

    public void LogMemoryUsage()
    {
        //Debug.Log($"Registered undo state '{OperationName}' (Type: {OperationType}, Size: {EstimateMemoryUsage() / 1024f:F2} KB)");
    }

    public long EstimateMemoryUsage()
    {
        long size = 0;
        if (Data is float[,]) size = ((float[,])Data).Length * sizeof(float);
        else if (Data is float[,,]) size = ((float[,,])Data).Length * sizeof(float);
        else if (Data is bool[,]) size = ((bool[,])Data).Length * sizeof(bool);
        else if (Data is Color[]) size = ((Color[])Data).Length * sizeof(float) * 4; // 4 floats per Color
        return size;
    }
}

	*/
	
    public class PrefabExport
    {
        public int PrefabNumber
        {
            get; set;
        }
        public uint PrefabID
        {
            get; set;
        }
        public string PrefabPath
        {
            get; set;
        }
        public string PrefabPosition
        {
            get; set;
        }
        public string PrefabScale
        {
            get; set;
        }
        public string PrefabRotation
        {
            get; set;
        }
    }
	
	public struct Point
	{
		public Point(int x, int y)
		{
			X=x;
			Y=y;
		}
		public int X;
		public int Y;
	}
	
	
}