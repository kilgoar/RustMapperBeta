using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using RustMapEditor.Variables;
using UIRecycleTreeNamespace;
using static AssetManager;

#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;
using UnityEditor;
#endif

using static WorldSerialization;
using EasyRoads3Dv3;

	// Token: 0x02000C8F RID: 3215
	public enum InfrastructureType
	{
		// Token: 0x04004966 RID: 18790
		Road,
		// Token: 0x04004967 RID: 18791
		Power,
		// Token: 0x04004968 RID: 18792
		Trail,
		// Token: 0x04004969 RID: 18793
		Tunnel,
		// Token: 0x0400496A RID: 18794
		UnderwaterLab,
		// Token: 0x0400496B RID: 18795
		Boat,
		// Token: 0x0400496C RID: 18796
		Rail
	}

public static class PathManager
{
    public static ERRoadNetwork _roadNetwork;
    public static int _roadIDCounter = 1;
	public static Transform roadTransform;
	public static GameObject NodePrefab { get; private set; }
	
	
public static void OnBundlesLoaded()
{
	
	AssetManager.SetVolumesCache();
			
			
    if (_roadNetwork == null)
    {
        Debug.LogError("RoadNetwork not initialized in OnBundlesLoaded.");
        return;
    }

    // Get the road types from the network
    ERRoadType[] roadTypes = _roadNetwork.GetRoadTypes();
    if (roadTypes == null || roadTypes.Length == 0)
    {
        Debug.LogError("No road types available in the road network.");
        return;
    }

    // Define asset paths
    string railMaterialPath = "assets/content/structures/train_tracks/models/materials/train_track_procgen.mat";
    string riverMaterialPath = "assets/content/nature/water/materials/river.mat";
    string roadMaterialPath = "assets/content/nature/terrain/procgen_roads/road materials/road_asphalt.mat";
    string circleRoadMaterialPath = "assets/content/nature/terrain/procgen_roads/road materials/road_asphalt_2.mat";

	

    // Load materials
    //Material railMaterial = AssetManager.LoadAsset<Material>(railMaterialPath);
	//Material newMaterial = new Material(railMaterial);
	//ExportMaterialTextures.ExportTextures(railMaterial);
	
	Material riverMaterial = Resources.Load<Material>("Materials/Water");
	Material clearMaterial = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
    clearMaterial.color = new Color(0f, 0f, 0f, 0f);
    Material roadMaterial = AssetManager.LoadAsset<Material>(roadMaterialPath);
    Material circleRoadMaterial = AssetManager.LoadAsset<Material>(circleRoadMaterialPath);

		// Define the EasyRoads3D shader
		Shader erRoadShader = Shader.Find("EasyRoads3D/ER Road");		
		Shader waterShader = Shader.Find("Custom/Rust/Water");
		
		
		if (erRoadShader == null)
		{
			Debug.LogError("EasyRoads3D/ER Road shader not found. Ensure the EasyRoads3D package is correctly installed.");
		}


		if (roadMaterial != null)
		{
			roadMaterial.shader = erRoadShader;
			Debug.Log($"Loaded and assigned ER Road shader to road material at {roadMaterialPath}");
		}
		else
		{
			Debug.LogError($"Failed to load road material at {roadMaterialPath}");
		}

		if (circleRoadMaterial != null)
		{
			circleRoadMaterial.shader = erRoadShader;
			Debug.Log($"Loaded and assigned ER Road shader to circle road material at {circleRoadMaterialPath}");
		}
		else
		{
			Debug.LogError($"Failed to load circle road material at {circleRoadMaterialPath}");
		}

    // Iterate through road types and assign materials based on roadTypeName
    foreach (ERRoadType roadType in roadTypes)
    {
        if (string.IsNullOrEmpty(roadType.roadTypeName))
        {
            Debug.LogWarning("Road type with empty name found. Skipping.");
            continue;
        }

        string typeName = roadType.roadTypeName.ToLower();

        switch (typeName)
        {
            case "rail":
				continue;
				//roadType.roadMaterial = railMaterial;
                break;

            case "river":
                roadType.roadMaterial = riverMaterial;
                Debug.Log($"Assigned river material to road type '{roadType.roadTypeName}'");
                break;

            case "road":
                roadType.roadMaterial = roadMaterial;
                Debug.Log($"Assigned road material to road type '{roadType.roadTypeName}'");
                break;

            case "circle road":
                roadType.roadMaterial = circleRoadMaterial;
                //roadType.layer = 9;
                //roadType.tag = "Path";
                Debug.Log($"Assigned circle road material to road type '{roadType.roadTypeName}'");
                break;

            case "trail":
				roadType.roadMaterial = clearMaterial;
                break;

            case "powerline":
				roadType.roadMaterial = clearMaterial;
                break;

            default:
                Debug.LogWarning($"No matching material configuration for road type '{roadType.roadTypeName}'.");
                break;
        }

        // Update the road type in the network
        roadType.Update();
    }

    Debug.Log("Completed OnBundlesLoaded: Materials assigned to road types.");
}
	
	public static void HideCurrentNodeCollection()
    {
        if (CameraManager.Instance._selectedRoad != null)
        {
			if (CameraManager.Instance._selectedRoad.GetComponent<NodeCollection>() != null){

				CameraManager.Instance._selectedRoad.GetComponent<NodeCollection>().HideNodes();
				Debug.Log($"Hid nodes");
			}
			else	{
				Debug.Log("invalid road");
			}
        }
        else
        {
            Debug.LogWarning("No selected road.");
        }
    }

	//road type name keywords
	//names are "Keyword X" where X is an integer enumerating the paths 
	
	//Powerline   (invisible)
	//Road
	//Width 12  ring road
	//width 10  normal road
	//width 4   trails (invisible)
	
	//River (invisible)
	//Rail
	


    #if UNITY_EDITOR
    public static void Init()
    {
		PathParent = GameObject.FindWithTag("Paths").transform;
		roadTransform = PathParent;
		//roadTransform.SetParent(PathParent, false);
		//roadTransform.position = PathParent.position;
		EditorApplication.update += OnProjectLoad;
		NodePrefab = Resources.Load<GameObject>("Prefabs/NodeSphere");
    }

    private static void OnProjectLoad()
    {
		//_roadNetwork = new ERRoadNetwork();
		//roadTransform.position = PathParent.position;
        EditorApplication.update -= OnProjectLoad;
    }
    #endif

	public static void SetERNetworkPosition()
	{
		// Get the current position of the road network and PathParent
		Vector3 roadNetworkPosition = _roadNetwork.roadNetwork.roadObjectsParent.position;
		Vector3 pathParentPosition = PathParent.position;
		
		// Calculate the difference
		Vector3 offset = pathParentPosition - roadNetworkPosition;
		
		// Translate the road network by the offset
		_roadNetwork.Translate(offset);
	}

    public static void RuntimeInit()
    {
		PathParent = GameObject.FindWithTag("Paths").transform;
		roadTransform = GameObject.FindWithTag("EasyRoads").transform;
		//roadTransform.position = PathParent.position;
		roadTransform.SetParent(PathParent, false);
		_roadNetwork = new ERRoadNetwork();		
		
		//lets create a method that sets the road network to an absolute position by calling translate. it gets its own position and the location of path parent and then finds the difference so it can translate to the path parent
		
		if (_roadNetwork == null){
			Debug.Log("fuck easy roads piece of shit");
			return;
			}
		//SetERNetworkPosition();
		//roadTransform.position = PathParent.position;
		NodePrefab = Resources.Load<GameObject>("Prefabs/NodeSphere");
		
		AssetManager.Callbacks.BundlesLoaded += OnBundlesLoaded;
    }

	public static void ApplyTerrainSmoothing(ERRoad road, float size = 5f, int type = 0)
    {
        if (_roadNetwork == null)
        {
            Debug.LogError("RoadNetwork not initialized.");
            return;
        }

        Terrain terrain = TerrainManager.Land;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found in the scene.");
            return;
        }


            ERModularRoad modularRoad = road.gameObject.GetComponent<ERModularRoad>();
            if (modularRoad == null)
            {
                Debug.LogWarning($"ERModularRoad component not found on road '{road.GetName()}'. Skipping smoothing.");
                return;
            }


            // Smooth step is used internally by TerrainSmooth to alternate between steps
            int smoothStep = 0;

            // Apply smoothing using the EasyRoads3D method
            OCDDDQOQOC.TerrainSmooth(
                terrain: terrain,
                road: modularRoad,
                size: size,
                type: type,
                smoothStep: ref smoothStep
            );

            Debug.Log($"Applied terrain smoothing to road '{road.GetName()}' with size={size}, type={type}");
 
    }
	
	public static GameObject GenerateRoadWithNodes(Vector3 startPos, Vector3 endPos, string roadName, int nodeReductionFactor = 1)
	{
		if (_roadNetwork == null)
		{
			Debug.LogError("RoadNetwork not initialized.");
			return null;
		}

		Terrain terrain = TerrainManager.Land;
		if (terrain == null)
		{
			Debug.LogError("No active terrain found.");
			return null;
		}

		WebRoadPreset preset = new WebRoadPreset();
		float terrainSize = terrain.terrainData.size.x;
		int heightMapRes = TerrainManager.HeightMapRes;

		// Generate path using A* algorithm
		uint seed = (uint)UnityEngine.Random.Range(0, 100000);
		int resolution = heightMapRes;
		int[,] costmap = CreateRoadCostmap(seed);
		if (costmap.Length == 0)
		{
			Debug.LogError("Failed to create costmap.");
			return null;
		}

		Point start = GetPoint(startPos, resolution);
		Point end = GetPoint(endPos, resolution);
		List<Point> gridPath = FindPathReversed(start, end, costmap, resolution, 20000000, 0.75f);

		if (gridPath == null || gridPath.Count == 0)
		{
			Debug.LogError($"No valid path found from {startPos} to {endPos}.");
			return null;
		}

		// Convert grid path to world-space path
		List<Vector3> worldPath = new List<Vector3>();
		foreach (Point point in gridPath)
		{
			float uvX = (float)point.x / resolution;
			float uvZ = (float)point.y / resolution;
			Vector3 worldPos = new Vector3(
				uvX * terrainSize,
				0f,
				uvZ * terrainSize
			);
			worldPos = SnapToTerrain(worldPos);
			worldPos -= PathManager.PathParent.position;
			worldPath.Add(worldPos);
		}
		worldPath.Reverse(); // Ensure path goes from start to end

		// Reduce nodes based on nodeReductionFactor
		List<Vector3> reducedPath = new List<Vector3>();
		if (nodeReductionFactor <= 1 || worldPath.Count < 3)
		{
			// Keep all nodes if nodeReductionFactor <= 1 or path is too short
			reducedPath = worldPath;
		}
		else
		{
			// Always include the first node
			reducedPath.Add(worldPath[0]);

			// Add every nodeReductionFactor-th node
			for (int i = nodeReductionFactor; i < worldPath.Count - 1; i += nodeReductionFactor)
			{
				reducedPath.Add(worldPath[i]);
			}

			// Always include the last node
			reducedPath.Add(worldPath[worldPath.Count - 1]);
		}

		// Ensure start and end positions are exact
		reducedPath[0] = startPos - PathManager.PathParent.position;
		reducedPath[reducedPath.Count - 1] = endPos - PathManager.PathParent.position;

		// Create PathData
		PathData pathData = new PathData
		{
			name = string.IsNullOrEmpty(roadName) ? $"Road {_roadIDCounter++}" : roadName,
			nodes = reducedPath.Select(pos => new VectorData { x = pos.x, y = pos.y, z = pos.z }).ToArray(),
			spline = true, // Use spline for smooth roads
			width = 10f,
			innerPadding = 1f,
			outerPadding = 1f,
			innerFade = 2f,
			outerFade = 2f,
			terrainOffset = 0f,
			splat = (int)TerrainSplat.Enum.Gravel,
			topology = (int)TerrainTopology.Enum.Road,
			start = false,
			end = false
		};

		// Spawn the path
		SpawnPath(pathData, fresh: true);
		GameObject roadObject = CurrentMapPaths.Last().gameObject;
		ERRoad road = _roadNetwork.GetRoadByName(pathData.name);
		if (road == null)
		{
			Debug.LogError($"Failed to create ERRoad for '{pathData.name}'.");
			UnityEngine.Object.Destroy(roadObject);
			return null;
		}

		// Update NodeCollection
		NodeCollection nodeCollection = roadObject.GetComponent<NodeCollection>();
		if (nodeCollection == null)
		{
			Debug.LogError($"NodeCollection not found on road '{roadObject.name}'.");
			UnityEngine.Object.Destroy(roadObject);
			return null;
		}
		nodeCollection.pathData = pathData;
		nodeCollection.PopulateNodes();
		nodeCollection.HideNodes();

		Debug.Log($"Generated road '{pathData.name}' from {startPos} to {endPos} with {reducedPath.Count} nodes (reduced from {worldPath.Count} nodes).");
		return roadObject;
	}
	
	public static void GenerateFromNodes(PathData pathData, NodeCollection nodeCollection, float contour = 1f, float nodeDensity = 1f)
	{
		if (_roadNetwork == null)
		{
			Debug.LogError("RoadNetwork not initialized.");
			return;
		}
		
		Terrain terrain = TerrainManager.Land;

		// Validate PathData and NodeCollection
		if (pathData == null || pathData.nodes == null || pathData.nodes.Length != 2)
		{
			Debug.LogError("PathData must contain exactly two nodes (start and end).");
			return;
		}

		if (nodeCollection == null)
		{
			Debug.LogError("NodeCollection reference is null.");
			return;
		}

		float terrainSize = terrain.terrainData.size.x;
		int heightMapRes = TerrainManager.HeightMapRes;

		// Generate path using A* algorithm
		uint seed = (uint)UnityEngine.Random.Range(0, 100000);
		int resolution = heightMapRes;
		int[,] costmap = CreateRoadCostmap(seed);
		if (costmap.Length == 0)
		{
			Debug.LogError("Failed to create costmap.");
			return;
		}

		// Extract start and end positions from PathData
		Vector3 startPos = new Vector3(pathData.nodes[0].x, pathData.nodes[0].y, pathData.nodes[0].z) + PathManager.PathParent.position;
		Vector3 endPos = new Vector3(pathData.nodes[1].x, pathData.nodes[1].y, pathData.nodes[1].z) + PathManager.PathParent.position;

		Point start = GetPoint(startPos, resolution);
		Point end = GetPoint(endPos, resolution);
		List<Point> gridPath = FindPathReversed(start, end, costmap, resolution, 20000000, contour);

		if (gridPath == null || gridPath.Count == 0)
		{
			Debug.LogError($"No valid path found from {startPos} to {endPos}.");
			return;
		}

		// Convert grid path to world-space path
		List<Vector3> worldPath = new List<Vector3>();
		foreach (Point point in gridPath)
		{
			float uvX = (float)point.x / resolution;
			float uvZ = (float)point.y / resolution;
			Vector3 worldPos = new Vector3(
				uvX * terrainSize,
				0f,
				uvZ * terrainSize
			);
			worldPos = SnapToTerrain(worldPos);
			worldPos -= PathManager.PathParent.position;
			worldPath.Add(worldPos);
		}
		worldPath.Reverse(); // Ensure path goes from start to end

		// Reduce nodes based on nodeDensity (0 to 1, where 1 = all nodes, 0 = only start/end)
		List<Vector3> reducedPath = new List<Vector3>();
		if (worldPath.Count < 3 || nodeDensity >= 1f)
		{
			// Keep all nodes if path is too short or nodeDensity >= 1
			reducedPath = worldPath;
		}
		else if (nodeDensity <= 0f)
		{
			// Keep only start and end nodes if nodeDensity <= 0
			reducedPath.Add(worldPath[0]);
			reducedPath.Add(worldPath[worldPath.Count - 1]);
		}
		else
		{
			// Calculate number of nodes to keep based on nodeDensity
			int nodesToKeep = Mathf.Max(2, Mathf.RoundToInt(nodeDensity * worldPath.Count));
			float step = (float)(worldPath.Count - 1) / (nodesToKeep - 1); // Step size between nodes

			reducedPath.Add(worldPath[0]); // Always include the first node
			for (float i = step; i < worldPath.Count - 1; i += step)
			{
				int index = Mathf.RoundToInt(i);
				reducedPath.Add(worldPath[index]);
			}
			reducedPath.Add(worldPath[worldPath.Count - 1]); // Always include the last node
		}

		// Ensure start and end positions are exact
		reducedPath[0] = startPos - PathManager.PathParent.position;
		reducedPath[reducedPath.Count - 1] = endPos - PathManager.PathParent.position;

		// Update PathData with reduced path
		pathData.nodes = reducedPath.Select(pos => new VectorData { x = pos.x, y = pos.y, z = pos.z }).ToArray();

		// Update NodeCollection
		nodeCollection.pathData = pathData;
		nodeCollection.PopulateNodes();
		nodeCollection.HideNodes();

		Debug.Log($"Generated road '{pathData.name}' from {startPos} to {endPos} with {reducedPath.Count} nodes (reduced from {worldPath.Count} nodes).");
	}


private static Vector3 SnapToTerrain(Vector3 pos)
{
	Vector3 terrainSize = TerrainManager.TerrainSize;
	float size = terrainSize.x;
	int heightMapRes = TerrainManager.HeightMapRes;
	
	int xIndex = Mathf.Clamp((int)(pos.x / size * heightMapRes), 0, heightMapRes - 1);
    int zIndex = Mathf.Clamp((int)(pos.z / size * heightMapRes), 0, heightMapRes - 1);
    pos.y = TerrainManager.Height[zIndex, xIndex] * 1000f;
	//Debug.Log(xIndex + "-x " + zIndex + "-z " + pos.y + "-y");
	return pos;
}

// Helper method to get terrain slope
private static float GetTerrainSlope(Vector3 pos, float terrainSize, int heightMapRes)
{
    int xIndex = Mathf.Clamp((int)(pos.x / terrainSize * heightMapRes), 0, heightMapRes - 1);
    int zIndex = Mathf.Clamp((int)(pos.z / terrainSize * heightMapRes), 0, heightMapRes - 1);
    return TerrainManager.Slope[zIndex, xIndex];
}
	
	public static void GenerateWebRoads(List<List<GameObject>> monumentConnectionPoints, WebRoadPreset preset = null)
    {
        preset = preset ?? WebRoadPreset.Default;

        if (monumentConnectionPoints == null || monumentConnectionPoints.Count < 2 || monumentConnectionPoints.Any(list => list == null || list.Count == 0))
        {
            Debug.LogError("Invalid monument connection points provided.");
            return;
        }

        // Select two random connection points from different monuments
        int monumentIndex1 = UnityEngine.Random.Range(0, monumentConnectionPoints.Count);
        int monumentIndex2;
        do
        {
            monumentIndex2 = UnityEngine.Random.Range(0, monumentConnectionPoints.Count);
        } while (monumentIndex2 == monumentIndex1);

        GameObject startPoint = monumentConnectionPoints[monumentIndex1][UnityEngine.Random.Range(0, monumentConnectionPoints[monumentIndex1].Count)];
        GameObject endPoint = monumentConnectionPoints[monumentIndex2][UnityEngine.Random.Range(0, monumentConnectionPoints[monumentIndex2].Count)];

        Vector3 startPos = startPoint.transform.position;
        Vector3 endPos = endPoint.transform.position;

        // Create a new road
        GameObject roadObject = CreatePathAtPosition(startPos);
        if (roadObject == null)
        {
            Debug.LogError("Failed to create road object.");
            return;
        }

        NodeCollection nodeCollection = roadObject.GetComponent<NodeCollection>();
        if (nodeCollection == null)
        {
            Debug.LogError($"NodeCollection not found on road '{roadObject.name}'.");
            return;
        }

        // Initialize path with start node
        List<Vector3> nodes = new List<Vector3> { startPos };
        Vector3 currentPos = startPos;
        int retryCount = 0;

        Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
        if (land == null)
        {
            Debug.LogError("Terrain with tag 'Land' not found.");
            return;
        }

        float terrainSize = land.terrainData.size.x;
        int heightMapRes = TerrainManager.HeightMapRes;

        while (Vector3.Distance(currentPos, endPos) > preset.MaxNodeDistance)
        {
            // Prefer direction toward destination
            Vector3 direction = (endPos - currentPos).normalized;
            float distance = UnityEngine.Random.Range(preset.MinNodeDistance, preset.MaxNodeDistance);
            Vector3 candidatePos = currentPos + direction * distance;

            // Snap to terrain height or waterline
            float terrainHeight = TerrainManager.Height[(int)(candidatePos.z / terrainSize * heightMapRes), (int)(candidatePos.x / terrainSize * heightMapRes)];
            candidatePos.y = Mathf.Max(terrainHeight, preset.WaterlineHeight);

            bool validPosition = false;
            int directionRetries = 0;

            while (!validPosition && directionRetries < preset.MaxRetries)
            {
                // Check slope
                float slope = TerrainManager.Slope[(int)(candidatePos.z / terrainSize * heightMapRes), (int)(candidatePos.x / terrainSize * heightMapRes)];
                if (slope > preset.MaxSlope)
                {
                    directionRetries++;
                    candidatePos = TryNewDirection(currentPos, endPos, directionRetries, preset);
                    continue;
                }

                // Check map edge distance
                if (candidatePos.x < preset.MapEdgeDistance || candidatePos.x > terrainSize - preset.MapEdgeDistance ||
                    candidatePos.z < preset.MapEdgeDistance || candidatePos.z > terrainSize - preset.MapEdgeDistance)
                {
                    directionRetries++;
                    candidatePos = TryNewDirection(currentPos, endPos, directionRetries, preset);
                    continue;
                }

                // Check collisions with roads or prefabs
                if (PrefabManager.sphereCollision(candidatePos, preset.CollisionRadius, (int)ColliderLayer.Paths | (int)ColliderLayer.Prefabs))
                {
                    directionRetries++;
                    candidatePos = TryNewDirection(currentPos, endPos, directionRetries, preset);
                    continue;
                }

                validPosition = true;
            }

            if (!validPosition)
            {
                // Regress to previous node and try a new direction
                if (nodes.Count > 1)
                {
                    nodes.RemoveAt(nodes.Count - 1);
                    currentPos = nodes[nodes.Count - 1];
                    retryCount++;
                    if (retryCount >= preset.MaxRetries)
                    {
                        Debug.LogWarning("Max retries reached. Aborting path generation.");
                        return;
                    }
                    continue;
                }
                else
                {
                    Debug.LogWarning("No valid path found from start node.");
                    return;
                }
            }

            // Add valid node
            nodes.Add(candidatePos);
            currentPos = candidatePos;
            retryCount = 0;

            // Add node to road
            nodeCollection.AddNodeAtPosition(currentPos, new List<GameObject>(), 25f);
        }

        // Connect to destination node
        nodeCollection.AddNodeAtPosition(endPos, new List<GameObject>(), 25f);

        // Update road data
        PathData pathData = nodeCollection.GetComponent<PathDataHolder>().pathData;
        pathData.nodes = nodes.Select(pos => new VectorData { x = pos.x, y = pos.y, z = pos.z }).ToArray();
        PathWindow.Instance?.UpdateData();

        Debug.Log($"Generated road '{roadObject.name}' from {startPos} to {endPos} with {nodes.Count} nodes.");
    }

    private static Vector3 TryNewDirection(Vector3 currentPos, Vector3 endPos, int retryIndex, WebRoadPreset preset)
    {
        // Generate a new direction with slight deviation from the destination direction
        Vector3 baseDirection = (endPos - currentPos).normalized;
        float angleOffset = retryIndex * 30f; // Rotate by 30 degrees per retry
        Vector3 newDirection = Quaternion.Euler(0, angleOffset, 0) * baseDirection;
        float distance = UnityEngine.Random.Range(preset.MinNodeDistance, preset.MaxNodeDistance);
        Vector3 candidatePos = currentPos + newDirection * distance;

        // Snap to terrain height or waterline
        Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
        float terrainSize = land.terrainData.size.x;
        int heightMapRes = TerrainManager.HeightMapRes;
        float terrainHeight = TerrainManager.Height[(int)(candidatePos.z / terrainSize * heightMapRes), (int)(candidatePos.x / terrainSize * heightMapRes)];
        candidatePos.y = Mathf.Max(terrainHeight, preset.WaterlineHeight);

        return candidatePos;
    }
	
 
   public static GameObject SelectPath(GameObject roadObject)
    {
		Debug.Log("selecting road");
        if (roadObject == null)
        {
            Debug.LogError("Road object is null. Cannot populate nodes.");
            return null;
        }


		NodeCollection existingCollection = roadObject.GetComponent<NodeCollection>();
            if (existingCollection != null)
            {
				Debug.Log("node collection found");
                CameraManager.Instance._selectedRoad = roadObject;
            }
        
		else{
			Debug.Log("no existing nodes found");
		}
 
        // Update the ItemsWindow tree
        if (ItemsWindow.Instance != null)
        {
            Node pathNode = ItemsWindow.Instance.tree.FindFirstNodeByDataRecursive(roadObject);
            if (pathNode != null)
            {
                //pathNode.data = roadObject; // Update to reference NodeCollection
                //pathNode.nodes.Clear(); // Clear existing child nodes

                foreach (Transform nodeTransform in existingCollection.GetNodes())
                {
                    if (nodeTransform != null)
                    {
                        Node childNode = new Node(nodeTransform.name) { data = nodeTransform.gameObject };
                        pathNode.nodes.AddWithoutNotify(childNode);
                    }
                }

                ItemsWindow.Instance.tree.Rebuild();
                Debug.Log($"Updated tree for path '{roadObject.name}' with {existingCollection.GetNodes().Count} nodes.");
            }
            else
            {
                Debug.LogWarning($"Could not find tree node for path '{roadObject.name}' to update with NodeCollection.");
            }
        }


        Transform firstNode = existingCollection.GetFirstNode();
        return firstNode != null ? firstNode.gameObject : null;
    }


public static void UpdateTerrainHeightmap(ERRoad road, PathData pathData)
{
    if (_roadNetwork == null || TerrainManager.Land == null)
    {
        Debug.LogError("RoadNetwork or terrain not initialized.");
        return;
    }

    Terrain terrain = TerrainManager.Land;
    TerrainData terrainData = terrain.terrainData;
    Vector3 terrainPosition = terrain.transform.position;
    int heightmapRes = TerrainManager.HeightMapRes;

    float outerPadding = Mathf.Max(pathData.outerPadding, 0f);
    float outerFade = Mathf.Max(pathData.outerFade, 0f);
    float innerPadding = Mathf.Max(pathData.innerPadding, 0f);
    float innerFade = Mathf.Max(pathData.innerFade, 0f);
    float terrainOffset = pathData.terrainOffset; // Depth to sink (positive = downward)
    float roadWidth = pathData.width;
    float halfRoadWidth = roadWidth * 0.5f;

    float totalOuterWidth = outerPadding + outerFade;
    float totalInnerWidth = innerPadding + innerFade;
    float totalWidthPerSide = halfRoadWidth + totalInnerWidth + totalOuterWidth;
    float totalWidth = roadWidth + 2f * (totalInnerWidth + totalOuterWidth);

    // Create influence mesh
    GameObject influenceMeshObj = CreateInfluenceMesh(road, pathData);
    if (influenceMeshObj == null)
    {
        Debug.LogError($"Failed to create influence mesh for road '{road.GetName()}'. Aborting heightmap update.");
        return;
    }

    Bounds bounds = influenceMeshObj.GetComponent<MeshCollider>().bounds;
    if (bounds.size == Vector3.zero)
    {
        Debug.LogWarning($"Influence mesh bounds are zero for road '{road.GetName()}'. Using marker bounds.");
        bounds = new Bounds(road.GetMarkerPosition(0), Vector3.one * 10f);
        bounds.Expand(totalWidthPerSide * 2f);
    }

    Vector3 boundsMin = bounds.min - terrainPosition;
    Vector3 boundsMax = bounds.max - terrainPosition;
    float heightmapScale = terrainData.heightmapScale.x;

    int xStartIndex = Mathf.FloorToInt(boundsMin.x / heightmapScale);
    int xEndIndex = Mathf.CeilToInt(boundsMax.x / heightmapScale);
    int zStartIndex = Mathf.FloorToInt(boundsMin.z / heightmapScale);
    int zEndIndex = Mathf.CeilToInt(boundsMax.z / heightmapScale);

    xStartIndex = Mathf.Clamp(xStartIndex, 0, heightmapRes - 1);
    xEndIndex = Mathf.Clamp(xEndIndex, 0, heightmapRes);
    zStartIndex = Mathf.Clamp(zStartIndex, 0, heightmapRes - 1);
    zEndIndex = Mathf.Clamp(zEndIndex, 0, heightmapRes);

    if (xEndIndex <= xStartIndex || zEndIndex <= zStartIndex)
    {
        Debug.LogWarning($"Invalid heightmap region for road '{road.GetName()}': xStart={xStartIndex}, xEnd={xEndIndex}, zStart={zStartIndex}, zEnd={zEndIndex}. Bounds: {bounds}");
        UnityEngine.Object.Destroy(influenceMeshObj);
        return;
    }

    int width = xEndIndex - xStartIndex;
    int height = zEndIndex - zStartIndex;
	float[,] before = TerrainManager.GetHeightMap();
    float[,] heights = TerrainManager.GetHeightMap(xStartIndex, zStartIndex, width, height, TerrainManager.TerrainType.Land);

    const int influenceLayer = 30;
    LayerMask layerMask = (1 << influenceLayer);

    float terrainHeight = terrainData.size.y;
    float raycastHeight = terrainHeight + 100f;
    float raycastDistance = raycastHeight + 510f;

    // Define distances from center (in world units)
    float roadCoreEdge = halfRoadWidth; // End of road core
    float innerFadeStart = roadCoreEdge; // Start of inner fade
    float innerFadeEnd = roadCoreEdge + innerFade; // End of inner fade
    float innerPaddingEnd = innerFadeEnd + innerPadding; // End of inner padding
    float outerPaddingStart = innerPaddingEnd; // Start of outer padding
    float outerPaddingEnd = outerPaddingStart + outerPadding; // End of outer padding
    float outerFadeEnd = outerPaddingEnd + outerFade; // End of outer fade

    for (int i = 0; i < width; i++)
    {
        for (int j = 0; j < height; j++)
        {
            Vector3 worldPos = new Vector3(
                terrainPosition.x + (xStartIndex + i) * heightmapScale,
                terrainPosition.y + raycastHeight,
                terrainPosition.z + (zStartIndex + j) * heightmapScale
            );

            RaycastHit hit;
            if (Physics.Raycast(worldPos, Vector3.down, out hit, raycastDistance, layerMask) && hit.collider.gameObject == influenceMeshObj)
            {
                float u = hit.textureCoord.x;
                float baseHeight = (hit.point.y - terrainPosition.y) / terrainHeight;
                float sunkenHeight = (hit.point.y - terrainPosition.y - terrainOffset) / terrainHeight;

                // Convert UV to distance from center (in world units)
                float distanceFromCenter = Mathf.Abs((u - 0.5f) * totalWidth);

                float targetHeight = baseHeight;
                float blendFactor = 0f;

                if (distanceFromCenter <= roadCoreEdge) // Road core
                {
                    targetHeight = sunkenHeight;
                    blendFactor = 1f;
                }
                else if (distanceFromCenter <= innerFadeEnd) // Inner fade
                {
                    float fadeProgress = (distanceFromCenter - innerFadeStart) / (innerFadeEnd - innerFadeStart);
                    targetHeight = Mathf.Lerp(sunkenHeight, baseHeight, fadeProgress);
                    blendFactor = 1f;
                }
                else if (distanceFromCenter <= innerPaddingEnd) // Inner padding
                {
                    targetHeight = baseHeight;
                    blendFactor = 1f;
                }
                else if (distanceFromCenter <= outerPaddingEnd) // Outer padding
                {
                    targetHeight = baseHeight;
                    blendFactor = 1f;
                }
                else if (distanceFromCenter <= outerFadeEnd) // Outer fade
                {
                    float fadeProgress = (distanceFromCenter - outerPaddingEnd) / (outerFadeEnd - outerPaddingEnd);
                    targetHeight = baseHeight;
                    blendFactor = 1f - fadeProgress; // Fade out to original terrain height
                }

                if (blendFactor > 0f)
                {
                    float newHeight = Mathf.Lerp(heights[j, i], targetHeight, blendFactor);
                    heights[j, i] = Mathf.Clamp01(newHeight);
                }
            }
        }
    }

    terrainData.SetHeights(xStartIndex, zStartIndex, heights);
	TerrainManager.RegisterHeightMapUndoRedo(TerrainManager.TerrainType.Land, $"Update Heightmap for '{road.GetName()}'", before);
    terrain.Flush();
	
	influenceMeshObj.SetActive(false);
    UnityEngine.Object.Destroy(influenceMeshObj);

    Debug.Log($"Updated terrain heightmap for road '{road.GetName()}' with width={roadWidth}, outerPadding={outerPadding}, outerFade={outerFade}, innerPadding={innerPadding}, innerFade={innerFade}, terrainOffset={terrainOffset}");
}


	public static GameObject CreateInfluenceMesh(ERRoad road, PathData pathData)
	{
		ERModularRoad modularRoad = road.gameObject.GetComponent<ERModularRoad>();
		if (modularRoad == null)
		{
			Debug.LogWarning($"ERModularRoad component not found on road '{road.GetName()}'. Using fallback geometry.");
		}

		List<Vector3> roadPoints = new List<Vector3>();
		if (modularRoad != null && modularRoad.soSplinePoints != null && modularRoad.soSplinePoints.Count > 0)
		{
			roadPoints.AddRange(modularRoad.soSplinePoints);
		}
		else
		{
			for (int i = 0; i < road.GetMarkerCount(); i++)
			{
				roadPoints.Add(road.GetMarkerPosition(i));
			}
		}

		if (roadPoints.Count < 2)
		{
			Debug.LogError($"Road '{road.GetName()}' has insufficient points ({roadPoints.Count}) for influence mesh creation.");
			return null;
		}


		List<Vector3> vertices = new List<Vector3>();
		List<Vector2> uvs = new List<Vector2>();
		List<int> triangles = new List<int>();
		float accumulatedLength = 0f;

	 
	float outerPadding = Mathf.Max(pathData.outerPadding, 0f);
	float outerFade = Mathf.Max(pathData.outerFade, 0f);
	float innerPadding = Mathf.Max(pathData.innerPadding, 0f);
	float innerFade = Mathf.Max(pathData.innerFade, 0f);
	float roadWidth = pathData.width;
	float halfRoadWidth = roadWidth * 0.5f;

	float totalOuterWidth = outerPadding + outerFade;
	float totalInnerWidth = innerPadding + innerFade;

	float effectiveOuterWidth = totalOuterWidth;
	float totalWidthPerSide = halfRoadWidth + totalInnerWidth + effectiveOuterWidth;

	// UV proportions
	float totalWidth = roadWidth + 2f * (totalInnerWidth + effectiveOuterWidth);
	float uvOuterFade = outerFade / totalWidth;
	float uvOuterPadding = (outerFade + outerPadding) / totalWidth;
	float uvInnerFade = (effectiveOuterWidth + innerFade) / totalWidth;
	float uvInnerPadding = (effectiveOuterWidth + innerFade + innerPadding) / totalWidth;

	List<int> segmentStartIndices = new List<int>();

	for (int i = 0; i < roadPoints.Count; i++)
	{
		Vector3 point = roadPoints[i];
		Vector3 nextPoint = (i < roadPoints.Count - 1) ? roadPoints[i + 1] : point;
		Vector3 direction = (nextPoint - point).normalized;
		if (direction == Vector3.zero) continue;

		Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;

		// Vertex positions
		Vector3 leftOuterEdge = point - right * totalWidthPerSide;
		Vector3 leftOuterPadding = point - right * (halfRoadWidth + outerPadding);
		Vector3 leftInnerPadding = point - right * (halfRoadWidth - innerPadding);
		Vector3 leftInnerFade = point - right * (halfRoadWidth - innerPadding - innerFade);
		Vector3 rightInnerFade = point + right * (halfRoadWidth - innerPadding - innerFade);
		Vector3 rightInnerPadding = point + right * (halfRoadWidth - innerPadding);
		Vector3 rightOuterPadding = point + right * (halfRoadWidth + outerPadding);
		Vector3 rightOuterEdge = point + right * totalWidthPerSide;

		leftOuterEdge.y = point.y;
		leftOuterPadding.y = point.y;
		leftInnerPadding.y = point.y;
		leftInnerFade.y = point.y;
		rightInnerFade.y = point.y;
		rightInnerPadding.y = point.y;
		rightOuterPadding.y = point.y;
		rightOuterEdge.y = point.y;

		segmentStartIndices.Add(vertices.Count);

		vertices.Add(leftOuterEdge);
		vertices.Add(leftOuterPadding);
		vertices.Add(leftInnerPadding);
		vertices.Add(leftInnerFade);
		vertices.Add(rightInnerFade);
		vertices.Add(rightInnerPadding);
		vertices.Add(rightOuterPadding);
		vertices.Add(rightOuterEdge);

		if (i > 0) accumulatedLength += Vector3.Distance(point, roadPoints[i - 1]);
		uvs.Add(new Vector2(0f, accumulatedLength));           // Left outer edge
		uvs.Add(new Vector2(uvOuterFade, accumulatedLength));  // Left outer padding (or topo edge if outerTopoWidth is used)
		uvs.Add(new Vector2(uvOuterPadding, accumulatedLength)); // Left inner padding
		uvs.Add(new Vector2(uvInnerFade, accumulatedLength));    // Left inner fade
		uvs.Add(new Vector2(1f - uvInnerFade, accumulatedLength)); // Right inner fade
		uvs.Add(new Vector2(1f - uvOuterPadding, accumulatedLength)); // Right inner padding
		uvs.Add(new Vector2(1f - uvOuterFade, accumulatedLength)); // Right outer padding (or topo edge)
		uvs.Add(new Vector2(1f, accumulatedLength));           // Right outer edge
	}

		for (int i = 0; i < segmentStartIndices.Count - 1; i++)
		{
			int baseIndex = segmentStartIndices[i];
			int nextBaseIndex = segmentStartIndices[i + 1];

			triangles.Add(baseIndex);   triangles.Add(nextBaseIndex);  triangles.Add(baseIndex + 1);
			triangles.Add(baseIndex + 1); triangles.Add(nextBaseIndex);  triangles.Add(nextBaseIndex + 1);
			triangles.Add(baseIndex + 1); triangles.Add(nextBaseIndex + 1); triangles.Add(baseIndex + 2);
			triangles.Add(baseIndex + 2); triangles.Add(nextBaseIndex + 1); triangles.Add(nextBaseIndex + 2);
			triangles.Add(baseIndex + 2); triangles.Add(nextBaseIndex + 2); triangles.Add(baseIndex + 3);
			triangles.Add(baseIndex + 3); triangles.Add(nextBaseIndex + 2); triangles.Add(nextBaseIndex + 3);
			triangles.Add(baseIndex + 3); triangles.Add(nextBaseIndex + 3); triangles.Add(baseIndex + 4);
			triangles.Add(baseIndex + 4); triangles.Add(nextBaseIndex + 3); triangles.Add(nextBaseIndex + 4);
			triangles.Add(baseIndex + 4); triangles.Add(nextBaseIndex + 4); triangles.Add(baseIndex + 5);
			triangles.Add(baseIndex + 5); triangles.Add(nextBaseIndex + 4); triangles.Add(nextBaseIndex + 5);
			triangles.Add(baseIndex + 5); triangles.Add(nextBaseIndex + 5); triangles.Add(baseIndex + 6);
			triangles.Add(baseIndex + 6); triangles.Add(nextBaseIndex + 5); triangles.Add(nextBaseIndex + 6);
			triangles.Add(baseIndex + 6); triangles.Add(nextBaseIndex + 6); triangles.Add(baseIndex + 7);
			triangles.Add(baseIndex + 7); triangles.Add(nextBaseIndex + 6); triangles.Add(nextBaseIndex + 7);
		}

		if (vertices.Count == 0 || triangles.Count == 0)
		{
			Debug.LogError($"Influence mesh for '{road.GetName()}' has no valid vertices or triangles.");
			return null;
		}

		GameObject meshObj = new GameObject($"InfluenceMesh_{road.GetName()}");
		meshObj.transform.SetParent(road.gameObject.transform, false);
		Mesh mesh = new Mesh
		{
			name = $"InfluenceMesh_{road.GetName()}",
			vertices = vertices.ToArray(),
			uv = uvs.ToArray(),
			triangles = triangles.ToArray()
		};
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();

		MeshFilter filter = meshObj.AddComponent<MeshFilter>();
		filter.sharedMesh = mesh;
		MeshCollider collider = meshObj.AddComponent<MeshCollider>();
		collider.sharedMesh = mesh;
		meshObj.layer = 30;

		return meshObj;
	}
public static void PaintRoadLayers(ERRoad road, WorldSerialization.PathData pathData, float strength = 1f, float outerTopoWidth = 0f, int outerTopology = -1)
{
    Terrain terrain = TerrainManager.Land;
    if (terrain == null) { Debug.LogError("No active terrain found in the scene."); return; }

    ERModularRoad modularRoad = road.gameObject.GetComponent<ERModularRoad>();
    if (modularRoad == null) { Debug.LogError($"ERModularRoad component not found on road '{road.GetName()}'."); return; }

    GameObject influenceMeshObj = CreateSplatTopologyMesh(road, pathData, outerTopoWidth);
    if (influenceMeshObj == null) { Debug.LogError($"Failed to create splat/topology mesh for road '{road.GetName()}'."); return; }

    // Splat and topology maps
    float[,,] groundMap = TerrainManager.GetSplatMap(TerrainManager.LayerType.Ground);
    int splatWidth = groundMap.GetLength(0);
    int splatHeight = groundMap.GetLength(1);
    float splatSizeX = terrain.terrainData.size.x / splatWidth;
    float splatSizeZ = terrain.terrainData.size.z / splatHeight;

    int topologyWidth = Mathf.FloorToInt(splatWidth / TerrainManager.SplatRatio);
    int topologyHeight = Mathf.FloorToInt(splatHeight / TerrainManager.SplatRatio);

    int splatIndex = TerrainSplat.TypeToIndex(pathData.splat);
    int topologyIndex = TerrainTopology.TypeToIndex(pathData.topology);
    int outerTopologyIndex = (outerTopology != -1) ? TerrainTopology.TypeToIndex(outerTopology) : -1;

    if (splatIndex < 0 || splatIndex >= TerrainManager.LayerCount(TerrainManager.LayerType.Ground))
    { Debug.LogError($"Splat index {splatIndex} out of range."); UnityEngine.Object.Destroy(influenceMeshObj); return; }
    if (topologyIndex < 0 || topologyIndex >= TerrainTopology.COUNT)
    { Debug.LogError($"Topology index {topologyIndex} out of range."); UnityEngine.Object.Destroy(influenceMeshObj); return; }
    if (outerTopology != -1 && (outerTopologyIndex < 0 || outerTopologyIndex >= TerrainTopology.COUNT))
    { Debug.LogError($"Outer topology index {outerTopologyIndex} out of range."); UnityEngine.Object.Destroy(influenceMeshObj); return; }

    float[,,] topologyMap = TerrainManager.GetSplatMap(TerrainManager.LayerType.Topology, topologyIndex);
    float[,,] outerTopologyMap = (outerTopology != -1) ? TerrainManager.GetSplatMap(TerrainManager.LayerType.Topology, outerTopologyIndex) : null;

    Vector3 terrainPos = terrain.transform.position;

    // Calculate bounds
    Bounds bounds = influenceMeshObj.GetComponent<MeshCollider>().bounds;
    Vector3 boundsMin = bounds.min - terrainPos;
    Vector3 boundsMax = bounds.max - terrainPos;

    int xStartIndex = Mathf.FloorToInt(boundsMin.x / splatSizeX);
    int xEndIndex = Mathf.CeilToInt(boundsMax.x / splatSizeX);
    int zStartIndex = Mathf.FloorToInt(boundsMin.z / splatSizeZ);
    int zEndIndex = Mathf.CeilToInt(boundsMax.z / splatSizeZ);

    xStartIndex = Mathf.Clamp(xStartIndex, 0, splatWidth - 1);
    xEndIndex = Mathf.Clamp(xEndIndex, 0, splatWidth);
    zStartIndex = Mathf.Clamp(zStartIndex, 0, splatHeight - 1);
    zEndIndex = Mathf.Clamp(zEndIndex, 0, splatHeight);

    if (xEndIndex <= xStartIndex || zEndIndex <= zStartIndex)
    {
        Debug.LogWarning($"Invalid splat map region for road '{road.GetName()}': xStart={xStartIndex}, xEnd={xEndIndex}, zStart={zStartIndex}, zEnd={zEndIndex}");
        UnityEngine.Object.Destroy(influenceMeshObj);
        return;
    }

    int width = xEndIndex - xStartIndex;
    int height = zEndIndex - zStartIndex;

    // Register undo for splat map
    float[,,] splatMapBefore = new float[height, width, groundMap.GetLength(2)];
    for (int i = 0; i < height; i++)
    {
        for (int j = 0; j < width; j++)
        {
            for (int k = 0; k < groundMap.GetLength(2); k++)
            {
                splatMapBefore[i, j, k] = groundMap[zStartIndex + i, xStartIndex + j, k];
            }
        }
    }
    

    // Register undo for internal topology map
    int topoStartX = Mathf.FloorToInt(xStartIndex / TerrainManager.SplatRatio);
    int topoStartZ = Mathf.FloorToInt(zStartIndex / TerrainManager.SplatRatio);
    int topoWidth = Mathf.Min(Mathf.CeilToInt((float)width / TerrainManager.SplatRatio), topologyWidth - topoStartX);
    int topoHeight = Mathf.Min(Mathf.CeilToInt((float)height / TerrainManager.SplatRatio), topologyHeight - topoStartZ);
	bool[,] topologyMapBefore=null;
	bool[,] outerTopologyMapBefore=null;
    
    if (topoWidth > 0 && topoHeight > 0)
    {
        topologyMapBefore = TopologyData.GetTopology(TerrainTopology.IndexToType(topologyIndex), topoStartX, topoStartZ, topoWidth, topoHeight);

    }

    // Register undo for outer topology map if applicable
    if (outerTopology != -1 && outerTopologyMap != null)
    {
        outerTopologyMapBefore = TopologyData.GetTopology(TerrainTopology.IndexToType(outerTopologyIndex), topoStartX, topoStartZ, topoWidth, topoHeight);
    }

    const int influenceLayer = 30;
    LayerMask layerMask = 1 << influenceLayer;
    float rayHeight = terrain.terrainData.size.y + 100f;
    float raycastDistance = rayHeight + 510f;

    float roadWidth = pathData.width;
    float halfRoadWidth = roadWidth * 0.5f;
    float blendWidth = 1f;
    float totalWidth = roadWidth + 2f * (blendWidth + outerTopoWidth);

    // Define zones
    float coreEdge = halfRoadWidth;
    float blendEdge = halfRoadWidth + blendWidth;
    float outerEdge = halfRoadWidth + blendWidth + outerTopoWidth;

    for (int i = 0; i < width; i++)
    {
        for (int j = 0; j < height; j++)
        {
            Vector3 rayOrigin = new Vector3(
                terrainPos.x + (xStartIndex + i) * splatSizeX + splatSizeX * 0.5f,
                terrainPos.y + rayHeight,
                terrainPos.z + (zStartIndex + j) * splatSizeZ + splatSizeZ * 0.5f
            );

            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, raycastDistance, layerMask))
            {
                float u = hit.textureCoord.x;
                float distanceFromCenter = Mathf.Abs((u - 0.5f) * totalWidth);

                // Core section (splat and internal topology)
                if (distanceFromCenter <= blendEdge)
                {
                    // Splat painting
                    float splatStrength = strength;
                    if (distanceFromCenter > coreEdge)
                    {
                        float fadeProgress = .5f;
                        splatStrength *= fadeProgress;
                    }

                    if (splatStrength > 0f)
                    {
                        int x = zStartIndex + j;
                        int z = xStartIndex + i;
                        float currentGround = groundMap[x, z, splatIndex];
                        float newGroundValue = Mathf.Lerp(currentGround, 1f, splatStrength);
                        groundMap[x, z, splatIndex] = newGroundValue;

                        int groundLayerCount = TerrainManager.LayerCount(TerrainManager.LayerType.Ground);
                        float totalOtherGround = 0f;
                        for (int k = 0; k < groundLayerCount; k++)
                            if (k != splatIndex) totalOtherGround += groundMap[x, z, k];

                        if (totalOtherGround > 0f)
                        {
                            float scale = (1f - newGroundValue) / totalOtherGround;
                            for (int k = 0; k < groundLayerCount; k++)
                                if (k != splatIndex) groundMap[x, z, k] *= scale;
                        }
                    }

                    // Internal topology
                    if (distanceFromCenter <= coreEdge)
                    {
                        int topoX = Mathf.FloorToInt((zStartIndex + j) / TerrainManager.SplatRatio);
                        int topoZ = Mathf.FloorToInt((xStartIndex + i) / TerrainManager.SplatRatio);
                        if (topoX >= 0 && topoX < topologyWidth && topoZ >= 0 && topoZ < topologyHeight)
                        {
                            topologyMap[topoX, topoZ, 0] = 1f;
                            topologyMap[topoX, topoZ, 1] = 0f;
                        }
                    }
                }
                // Outer topology section
                else if (outerTopoWidth > 0f && outerTopology != -1 && distanceFromCenter <= outerEdge)
                {
                    int topoX = Mathf.FloorToInt((zStartIndex + j) / TerrainManager.SplatRatio);
                    int topoZ = Mathf.FloorToInt((xStartIndex + i) / TerrainManager.SplatRatio);
                    if (topoX >= 0 && topoX < topologyWidth && topoZ >= 0 && topoZ < topologyHeight)
                    {
                        outerTopologyMap[topoX, topoZ, 0] = 1f;
                        outerTopologyMap[topoX, topoZ, 1] = 0f;
                    }
                }
            }
        }
    }

    // Apply changes
    TerrainManager.SetSplatMap(groundMap, TerrainManager.LayerType.Ground);
	TerrainUndoManager.RegisterSplatMapUndoRedo($"Paint Road Splat '{road.GetName()}'", xStartIndex, zStartIndex, width, height, splatMapBefore);	
	
    TerrainManager.SetSplatMap(topologyMap, TerrainManager.LayerType.Topology, topologyIndex);
	
	if(topologyMapBefore!=null){
		TerrainUndoManager.RegisterTopologyMapUndoRedo($"Paint Road Topology '{road.GetName()}'", topologyIndex, topoStartX, topoStartZ, topoWidth, topoHeight, topologyMapBefore);
	}	
	if(outerTopologyMapBefore!=null){
		TerrainUndoManager.RegisterTopologyMapUndoRedo($"Paint Road Outer Topology '{road.GetName()}'", outerTopologyIndex, topoStartX, topoStartZ, topoWidth, topoHeight, outerTopologyMapBefore);
	}

    bool[,] topologyBitmap = TerrainManager.ConvertSplatToBitmap(topologyMap);
    bool[,] downscaledBitmap = TerrainManager.DownscaleBitmap(topologyBitmap);
    TopologyData.SetTopology(TerrainTopology.IndexToType(topologyIndex), 0, 0, downscaledBitmap.GetLength(0), downscaledBitmap.GetLength(1), downscaledBitmap);

    if (outerTopology != -1 && outerTopologyMap != null)
    {
        TerrainManager.SetSplatMap(outerTopologyMap, TerrainManager.LayerType.Topology, outerTopologyIndex);
        bool[,] outerTopologyBitmap = TerrainManager.ConvertSplatToBitmap(outerTopologyMap);
        bool[,] outerDownscaledBitmap = TerrainManager.DownscaleBitmap(outerTopologyBitmap);
        TopologyData.SetTopology(TerrainTopology.IndexToType(outerTopologyIndex), 0, 0, outerDownscaledBitmap.GetLength(0), outerDownscaledBitmap.GetLength(1), outerDownscaledBitmap);
        TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Topology, outerTopologyIndex);
    }

    TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Ground, 0);
    TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Topology, topologyIndex);

    influenceMeshObj.SetActive(false);
    UnityEngine.Object.Destroy(influenceMeshObj);
    
    TopologyData.UpdateTexture();
    Debug.Log($"Painted road layers for '{road.GetName()}': splat={pathData.splat} (index {splatIndex}), topology={pathData.topology} (index {topologyIndex}), outerTopoWidth={outerTopoWidth}, outerTopology={(outerTopology != -1 ? outerTopology.ToString() : "None")}, strength={strength}");
}

    private static void DestroySplatMeshes(List<GameObject> splatMeshList)
    {
        if (splatMeshList != null)
        {
            foreach (GameObject obj in splatMeshList)
            {
                if (obj != null) UnityEngine.Object.Destroy(obj);
            }
        }
    }

	
	public static void SpawnPath(PathData pathData, bool fresh = false)
	{
		Debug.Log("spawning path...");
		if (_roadNetwork == null)
		{
			Debug.LogError("RoadNetwork not initialized.");
			return;
		}

		Vector3 offset = PathParent.transform.position;
		Vector3[] markers = pathData.nodes.Select(v => new Vector3(v.x, v.y, v.z) + offset).ToArray();
		string roadName = pathData.name;

		// Create the road object
		ERRoad newRoad = _roadNetwork.CreateRoad(roadName, markers);
		if (newRoad == null)
		{
			Debug.LogError($"Failed to create road '{roadName}'.");
			return;
		}

		// Configure the road
		Debug.Log("attempting to configure road");
		if(!fresh){
			Debug.Log("triggering configure road");
			ConfigureRoad(newRoad, pathData);
		}
		else{
			Debug.Log("triggering configure new road");
			ConfigureNewRoad(newRoad, pathData);
		}

		// Set up the GameObject hierarchy
		GameObject roadObject = newRoad.gameObject;
		roadObject.transform.SetParent(roadTransform, false);

		// Add PathDataHolder
		//PathDataHolder dataHolder = roadObject.AddComponent<PathDataHolder>();
		//dataHolder.pathData = pathData;
		
	    // Add and initialize NodeCollection to populate nodes
		NodeCollection nodeCollection = roadObject.AddComponent<NodeCollection>();
		nodeCollection.Initialize(newRoad); // Initialize with the ERRoad
		nodeCollection.pathData = pathData; // Set the pathData
		nodeCollection.PopulateNodes(); // Populate the nodes
		
		if(!fresh){
		nodeCollection.HideNodes(); // Hide the nodes
		}
		
		newRoad.Refresh();
		
		NetworkManager.Register(roadObject);
		roadObject.tag = "Path";
		roadObject.SetLayerRecursively(9); // Paths layer
	}
	
	
	//reverted method
	/*
    public static void SpawnPath(PathData pathData)
    {
        Vector3 averageLocation = Vector3.zero;
        for (int j = 0; j < pathData.nodes.Length; j++)
            averageLocation += pathData.nodes[j];

        averageLocation /= pathData.nodes.Length;
        GameObject newObject = GameObject.Instantiate(PrefabManager.DefaultCubeVolume, averageLocation + PathParent.position, Quaternion.identity, PathParent);
        newObject.name = pathData.name;

        var pathNodes = new List<GameObject>();
        for (int j = 0; j < pathData.nodes.Length; j++)
        {
            GameObject newNode = GameObject.Instantiate(PrefabManager.DefaultCubeVolume, newObject.transform);
            newNode.transform.position = pathData.nodes[j] + PathParent.position;
            pathNodes.Add(newNode);
        }
        newObject.GetComponent<PathDataHolder>().pathData = pathData;
    }
	*/
	
	public static GameObject CreatePathAtPosition(Vector3 startPosition)
    {
        if (_roadNetwork == null)
        {
            Debug.LogError("RoadNetwork not initialized.");
            return null;
        }

        // Define two initial nodes
		//Vector3 offset = PathParent.transform.position;
        //Vector3 firstNodePosition = startPosition - offset;
		//Debug.Log("first node position" + firstNodePosition);
        //Vector3 secondNodePosition = startPosition + (Vector3.right * 10f) - offset; // Default offset along X-axis
		//Debug.Log("second node position" + secondNodePosition);

        // Build newPathData from PathWindow UI fields
        PathData newPathData = new PathData
        {
            width = float.TryParse(PathWindow.Instance.widthField.text, out float width) ? width : 10f,
            innerPadding = float.TryParse(PathWindow.Instance.innerPaddingField.text, out float innerPadding) ? innerPadding : 1f,
            outerPadding = float.TryParse(PathWindow.Instance.outerPaddingField.text, out float outerPadding) ? outerPadding : 1f,
            innerFade = float.TryParse(PathWindow.Instance.innerFadeField.text, out float innerFade) ? innerFade : 1f,
            outerFade = float.TryParse(PathWindow.Instance.outerFadeField.text, out float outerFade) ? outerFade : 8f,
            splat = (int)PathWindow.Instance.splatEnums[PathWindow.Instance.splatDropdown.value],
            topology = (int)PathWindow.Instance.topologyEnums[PathWindow.Instance.topologyDropdown.value],
            spline = false, // Default, could add a UI toggle if needed
            terrainOffset = 0f, // Default, could add a UI field if needed
            start = false, // Default
            end = false, // Default
            nodes = new VectorData[]
            {
                //new VectorData { x = firstNodePosition.x, y = firstNodePosition.y, z = firstNodePosition.z },
                //new VectorData { x = secondNodePosition.x, y = firstNodePosition.y, z = secondNodePosition.z }
            }
        };
		
        // Determine road type and name from template or defaults
        string roadTypePrefix = InferRoadTypePrefix(newPathData);
        newPathData.name = $"{roadTypePrefix} {_roadIDCounter++}";

        // Spawn the path
        SpawnPath(newPathData, true);
        GameObject newRoadObject = CurrentMapPaths.Last().gameObject;
        //Debug.Log($"Created new road '{newPathData.name}' with nodes at {firstNodePosition} and {secondNodePosition}");

        return newRoadObject;
    }
	
	public static GameObject CreateRoadGap(Vector3 startPosition, Vector3 endPosition)
	{
		if (_roadNetwork == null)
		{
			Debug.LogError("RoadNetwork not initialized.");
			return null;
		}

		// Build newPathData from PathWindow UI fields or defaults
		PathData newPathData = new PathData
		{
                width = 10f,
				innerPadding = 1f,
				outerPadding = 1f,
				innerFade = 1f,
				outerFade = 8f,
                splat = (int)TerrainSplat.Enum.Gravel,			
                topology = (int)TerrainTopology.Enum.Road,
			
			nodes = new VectorData[]
			{
				new VectorData { x = startPosition.x, y = startPosition.y, z = startPosition.z },
				new VectorData { x = endPosition.x, y = endPosition.y, z = endPosition.z }
			}
		};

		// Determine road type and name
		string roadTypePrefix = "Road";
		newPathData.name = $"{roadTypePrefix} {_roadIDCounter++}";

		// Spawn the path
		SpawnPath(newPathData, true);
		GameObject newRoadObject = CurrentMapPaths.Last().gameObject;
		Debug.Log($"Created new road '{newPathData.name}' with nodes at {startPosition} and {endPosition}");

		return newRoadObject;
	}
	
	private static string InferRoadTypePrefix(PathData pathData)
	{
		// Log for debugging
		Debug.Log($"InferRoadTypePrefix: topology={pathData.topology}, width={pathData.width}, splat={pathData.splat}");

		// Check topology using TerrainTopology.TypeToIndex for consistency
		int riverIndex = ((int)TerrainTopology.Enum.River);
		int railIndex = ((int)TerrainTopology.Enum.Rail);
		int roadIndex = ((int)TerrainTopology.Enum.Road);

		Debug.Log($"river index={riverIndex}, railIndex={railIndex}, roadIndex={roadIndex}");

		if (pathData.topology == riverIndex)
		{
			return "River";
		}
		else if (pathData.width == 0f)
		{
			return "Powerline";
		}
		else if (pathData.topology == railIndex)
		{
			return "Rail";
		}
		else if (pathData.topology == roadIndex)
		{
			if (pathData.width == 4f)
				return "Road"; // Trails are named as road
			else if (pathData.width == 12f)
				return "Road"; // CircleRoad named as road
			else
				return "Road"; // Default road
		}
		
		Debug.LogError("road type defaulted");
		return "Road";
	}



    public static void ReconfigureRoad(ERRoad road, PathData pathData)
    {
        if (road == null || pathData == null)
        {
            Debug.LogError("Road or PathData is null in ReconfigureRoad.");
            return;
        }

        road.SetWidth(pathData.width);

        ERModularRoad modularRoad = road.gameObject.GetComponent<ERModularRoad>();
        if (modularRoad == null)
        {
            Debug.LogError($"ERModularRoad component not found on road '{pathData.name}'.");
            return;
        }

		

        modularRoad.roadWidth = pathData.width;
        modularRoad.indent = pathData.innerPadding;
        modularRoad.surrounding = pathData.outerPadding;
        modularRoad.fadeInDistance = pathData.innerFade;
        modularRoad.fadeOutDistance = pathData.outerFade;
        modularRoad.splatIndex = pathData.splat;

        // Refresh the road to apply changes
        road.Refresh();
    }

    public static void ConfigureNewRoad(ERRoad road, PathData pathData)
    {
        if (road == null || pathData == null)
        {
            Debug.LogError("Road or PathData is null in ConfigureNewRoad.");
            return;
        }

        if (PathWindow.Instance == null)
        {
            Debug.LogError("PathWindow.Instance is null. Cannot access RoadType for new road configuration.");
            return;
        }


        RoadType selectedRoadType = InferRoadType(pathData);		
        road.SetName(pathData.name);
        road.SetWidth(pathData.width);
        road.SetMarkerControlType(0, pathData.spline ? ERMarkerControlType.Spline : ERMarkerControlType.StraightXZ);
        road.ClosedTrack(false);
		


        // Get the ERModularRoad component
        ERModularRoad modularRoad = road.gameObject.GetComponent<ERModularRoad>();
        if (modularRoad == null)
        {
            Debug.LogError($"ERModularRoad component not found on road '{pathData.name}'.");
            return;
        }

        // Apply all settings from PathData directly
        modularRoad.roadWidth = pathData.width;
        modularRoad.indent = pathData.innerPadding;
        modularRoad.surrounding = pathData.outerPadding;
        modularRoad.fadeInDistance = pathData.innerFade;
        modularRoad.fadeOutDistance = pathData.outerFade;
        modularRoad.splatIndex = pathData.splat;
        modularRoad.terrainContoursOffset = pathData.terrainOffset;
        modularRoad.startConnectionFlag = pathData.start;
        modularRoad.endConnectionFlag = pathData.end;

        // Get available road types from the network
        ERRoadType[] roadTypes = _roadNetwork.GetRoadTypes();

        int roadTypeIndex = 0;
        // Configure visibility and lanes based on the explicit RoadType from the dropdown
        switch (selectedRoadType)
        {
            case RoadType.Powerline:
				roadTypeIndex = 0;
				break;
            case RoadType.Trail: 
				roadTypeIndex = 3;
                break;
            case RoadType.Road:                
				roadTypeIndex = 1;
                break;
            case RoadType.CircleRoad:
				roadTypeIndex = 2;
                break;
            case RoadType.Rail:
					SideObject sideObject = _roadNetwork.GetSideObjectByName("railroad");
				    if (sideObject != null)
					{
						road.SideObjectSetActive(sideObject, true);
					}
					else {
						Debug.Log("fuck your feelings the side object isn't there");
					}
				roadTypeIndex = 4;
                break;
			case RoadType.River:
				roadTypeIndex = 5;
				break;
        }

        // Apply the road type (visible or invisible)
        if (roadTypes != null && roadTypes.Length > roadTypeIndex)
        {
            road.SetRoadType(roadTypes[roadTypeIndex]);
        }
		
		road.SetTerrainDeformation(true);

        // Ensure width is set again after road type (some road types may override it)
        road.SetWidth(pathData.width);
        modularRoad.roadWidth = pathData.width;
        // Refresh the road to apply changes
        road.Refresh();
		
		

        Debug.Log($"Configured new road '{pathData.name}' with width={pathData.width}, splat={pathData.splat}, topology={pathData.topology}, roadType={selectedRoadType}");
    }
	
    public static void ConfigureRoad(ERRoad road, PathData pathData)
    {

        road.SetName(pathData.name);
        road.SetWidth(pathData.width);
		
        Debug.Log(pathData.spline + " spline control type on path " + pathData.name);
		road.SetMarkerControlType(0, pathData.spline ? ERMarkerControlType.Spline : ERMarkerControlType.StraightXZ);
		
		
        road.ClosedTrack(false);

        ERModularRoad modularRoad = road.gameObject.GetComponent<ERModularRoad>();
        if (modularRoad == null)
        {
            Debug.LogError($"ERModularRoad component not found on road '{pathData.name}'.");
            return;
        }

        ERRoadType[] roadTypes = _roadNetwork.GetRoadTypes();
        string[] nameParts = pathData.name.Split(' ');
        string roadTypePrefix = nameParts[0].ToLower();
		
		RoadType selectedRoadType = InferRoadType(pathData);
		int roadTypeIndex = 0;
		
		road.SetWidth(pathData.width);
        modularRoad.roadWidth = pathData.width;
        modularRoad.indent = pathData.innerPadding;
        modularRoad.surrounding = pathData.outerPadding;
        modularRoad.fadeInDistance = pathData.innerFade;
        modularRoad.fadeOutDistance = pathData.outerFade;
        modularRoad.terrainContoursOffset = pathData.terrainOffset;
        modularRoad.splatIndex = pathData.splat;
        modularRoad.startConnectionFlag = pathData.start;
        modularRoad.endConnectionFlag = pathData.end;
		
        // Configure visibility and lanes based on the explicit RoadType from the dropdown
        switch (selectedRoadType)
        {
            case RoadType.Powerline:
				roadTypeIndex = 0;
				break;
            case RoadType.Trail: 
				roadTypeIndex = 3;
                break;
            case RoadType.Road:                
				roadTypeIndex = 1;
                break;
            case RoadType.CircleRoad:
				roadTypeIndex = 2;
                break;
            case RoadType.Rail:
				roadTypeIndex = 4;
				SideObject sideObject = _roadNetwork.GetSideObjectByName("railroad");
				    if (sideObject != null)
					{
						bool isActive = road.GetSideObjectActiveState(sideObject);
						Debug.Log($"Side object 'railroad' active state for road '{road.GetName()}': {isActive}");
					}
					else {
						Debug.Log("fuck your feelings the side object isn't there");
					}
                break;
			case RoadType.River:
				roadTypeIndex = 5;
				break;
        }

        // Apply the road type (visible or invisible)
        if (roadTypes != null && roadTypes.Length > roadTypeIndex)
        {
            road.SetRoadType(roadTypes[roadTypeIndex]);
        }
		

		
		road.Refresh();
    }
	
	public static RoadType InferRoadType(PathData pathData)
    {
        string[] nameParts = pathData.name.Split(' ');
        string prefix = nameParts[0].ToLower();

        switch (prefix)
        {
            case "river":
                return RoadType.River;
            case "powerline":
                return RoadType.Powerline;
            case "rail":
                return RoadType.Rail;
            case "road":
                if (pathData.width == 4f)
                    return RoadType.Trail;
                else if (pathData.width == 12f)
                    return RoadType.CircleRoad;
                else
                    return RoadType.Road;
            default:
                if (pathData.width == 4f)
                    return RoadType.Trail;
                else if (pathData.width == 12f)
                    return RoadType.CircleRoad;
                else
                    return RoadType.Road;
        }
	}

	    public struct Point
    {
        public int x;
        public int y;

        public Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static Point operator +(Point a, Point b) => new Point(a.x + b.x, a.y + b.y);
        public static bool operator ==(Point a, Point b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Point a, Point b) => !(a == b);
        public override bool Equals(object obj) => obj is Point other && this == other;
        public override int GetHashCode() => (x, y).GetHashCode();
    }

    public class HeuristicNode
    {
        public Point point;
        public int cost;
        public int heuristic;
        public HeuristicNode previous;

        public HeuristicNode(Point point, int cost, int heuristic, HeuristicNode previous = null)
        {
            this.point = point;
            this.cost = cost;
            this.heuristic = heuristic;
            this.previous = previous;
        }
    }

    public static void GeneratePath(Vector3 startPos, Vector3 endPos, string pathName = "Road", bool spline = true, int maxDepth = 10000)
    {
        if (_roadNetwork == null)
        {
            Debug.LogError("RoadNetwork not initialized.");
            return;
        }

        Terrain terrain = TerrainManager.Land;
        if (terrain == null)
        {
            Debug.LogError("No active terrain found.");
            return;
        }

		Debug.Log("creating costmap");
        // Initialize costmap
        uint seed = (uint)UnityEngine.Random.Range(0, 100000);
        int resolution = TerrainManager.HeightMapRes; // Assume square costmap
        int[,] costmap = CreateRoadCostmap(seed);
		Debug.Log("costmap created successfully");

		/*
        // Convert world positions to grid coordinates
        Point start = GetPoint(startPos, resolution);
        Point end = GetPoint(endPos, resolution);

		
        // Find path in grid space
        List<Point> gridPath = FindPathReversed(start, end, costmap, resolution, maxDepth);

        if (gridPath == null || gridPath.Count == 0)
        {
            Debug.LogWarning($"No path found from {startPos} to {endPos}");
            return;
        }

        // Convert grid path to world-space path
        List<Vector3> worldPath = new List<Vector3>();
        float terrainSize = terrain.terrainData.size.x;
        foreach (Point point in gridPath)
        {
            // Convert grid to normalized UV [0, 1]
            float uvX = (float)point.x / resolution;
            float uvZ = (float)point.y / resolution;

            // Convert UV to world-space
            Vector3 worldPos = new Vector3(
                uvX * terrainSize,
                0f,
                uvZ * terrainSize
            );

            // Set height to terrain surface
            float height = terrain.terrainData.GetHeight(
                Mathf.FloorToInt(uvX * terrain.terrainData.heightmapResolution),
                Mathf.FloorToInt(uvZ * terrain.terrainData.heightmapResolution)
            );
            worldPath.Add(new Vector3(worldPos.x, height, worldPos.z));
        }

        // Reverse path to go from start to end
        worldPath.Reverse();

		Debug.Log(worldPath.Count + " nodes on path... creating path");
        // Create PathData
        PathData pathData = new PathData
        {
            name = $"{pathName} {_roadIDCounter++}",
            nodes = worldPath.Select(pos => new VectorData { x = pos.x, y = pos.y, z = pos.z }).ToArray(),
            spline = spline,
            width = 10f,
            innerPadding = 1f,
            outerPadding = 1f,
            innerFade = 2f,
            outerFade = 2f,
            terrainOffset = 0f,
            splat = (int)TerrainSplat.Enum.Dirt,
            topology = (int)TerrainTopology.Enum.Road,
            start = false,
            end = false
        };

        // Spawn the path
        SpawnPath(pathData);

        // Apply terrain modifications
        ERRoad road = _roadNetwork.GetRoadByName(pathData.name);
        if (road != null)
        {
            UpdateTerrainHeightmap(road, pathData);
            PaintRoadLayers(road, pathData);
            ApplyTerrainSmoothing(road);
        }

        Debug.Log($"Generated path '{pathData.name}' from {startPos} to {endPos} with {worldPath.Count} nodes.");
		*/
    }
	
public static int[,] CreateRoadCostmap(uint seed = 41243143u)
{
    int resolution = TerrainManager.HeightMapRes;

    // Terrain data validation
    TerrainManager.SyncTerrainResolutions();
    
    if (TerrainManager.Slope == null || 
        TerrainManager.Slope.GetLength(0) != TerrainManager.HeightMapRes || 
        TerrainManager.Slope.GetLength(1) != TerrainManager.HeightMapRes)
    {
        Debug.LogError($"Slope map is null or has incorrect dimensions: Expected {TerrainManager.HeightMapRes}x{TerrainManager.HeightMapRes}, Got {(TerrainManager.Slope != null ? $"{TerrainManager.Slope.GetLength(0)}x{TerrainManager.Slope.GetLength(1)}" : "null")}");
        return new int[0, 0];
    }

    if (TerrainManager.SplatMapRes <= 0)
    {
        Debug.LogError($"SplatMapRes is zero, preventing valid topology sampling.");
        return new int[0, 0];
    }

    int[,] costmap = new int[resolution, resolution];
    Color[] maskPixels = new Color[resolution * resolution]; // Array for mask texture pixels
    int resRatio = TerrainManager.HeightMapRes / TerrainManager.SplatMapRes;
	float highestCost=0f;

    // Pre-fetch topology map for the entire terrain
    bool[,] topologyMap = TopologyData.GetTopology(
        (int)TerrainTopology.Enum.Road,
        0, 0,
        TerrainManager.SplatMapRes, TerrainManager.SplatMapRes
    );

    if (topologyMap == null || topologyMap.GetLength(0) == 0 || topologyMap.GetLength(1) == 0)
    {
        Debug.LogError("Failed to fetch topology map for the entire terrain.");
        return new int[0, 0];
    }

    Debug.Log("Costmap vars initialized");

    for (int i = 0; i < resolution; i++)
    {
        for (int j = 0; j < resolution; j++)
        {
            int gridX = j;
            int gridZ = i;

			/*
            // Ensure grid coordinates are within bounds
            if (gridX >= TerrainManager.Slope.GetLength(1) || gridZ >= TerrainManager.Slope.GetLength(0))
            {
                Debug.LogWarning($"Grid coordinates out of bounds: ({gridX}, {gridZ}) for Slope map of size {TerrainManager.Slope.GetLength(1)}x{TerrainManager.Slope.GetLength(0)}");
                costmap[j, i] = int.MaxValue;
                maskPixels[i * resolution + j] = Color.red; // Impassable: Red
                continue;
            }
			*/

            // Random cost variation
            int randomCost = SeedRandom.Range(ref seed, 100, 200);

            // Get slope
            float slope = TerrainManager.Slope[gridZ, gridX];
			/*
            if (float.IsNaN(slope) || float.IsInfinity(slope))
            {
                costmap[j, i] = int.MaxValue;
                maskPixels[i * resolution + j] = Color.red; // Impassable: Red
                continue;
            }
			*/
			
            // Check topology using pre-fetched map
            int topoX = gridX / resRatio;
            int topoZ = gridZ / resRatio;
            //bool hasRoadTopology = topoX < topologyMap.GetLength(1) && topoZ < topologyMap.GetLength(0) && topologyMap[topoZ, topoX];
			
            // Assign costs and colors
            if (slope > 20f )
            {
                costmap[j, i] = int.MaxValue;
                maskPixels[i * resolution + j] = Color.red; // Impassable: Red
            }
            else
            {
                int cost = 1 + (int)(slope * slope * 10f) + randomCost;
                costmap[j, i] = cost;
				
				// Update highest cost
                if (cost > highestCost)
                {
                    highestCost = cost;
                }

                // Normalize cost for color gradient (assuming max cost for gradient is 5000)
                float normalizedCost = Mathf.Clamp01((float)cost / 5000f);
                Color passableColor = new Color(0, 0, 0); 
                maskPixels[i * resolution + j] = Color.Lerp(passableColor, Color.red, normalizedCost);
            }
        }
    }

    // Apply the mask texture
    if (TerrainManager.MaskTexture != null)
    {
        TerrainManager.MaskTexture.SetPixels(maskPixels);
        TerrainManager.MaskTexture.Apply();
        Debug.Log("Costmap visualization applied to MaskTexture");
    }
    else
    {
        Debug.LogError("MaskTexture is null, cannot apply costmap visualization");
    }

    Debug.Log("Costmap generation completed with cost as high as: " + highestCost);
    return costmap;
}

  
private static List<Point> FindPathReversed(Point start, Point end, int[,] costmap, int resolution, int maxDepth, float costStrength)
{
    float terrainSize = TerrainManager.Land.terrainData.size.x;
    int heightMapRes = TerrainManager.HeightMapRes;
    WebRoadPreset preset = new WebRoadPreset();

    int[,] visited = new int[resolution, resolution];
    var heap = new PriorityQueue<HeuristicNode>((a, b) => (a.cost + a.heuristic).CompareTo(b.cost + b.heuristic));

    List<(Point from, Point to)> failedAttempts = new List<(Point, Point)>();

    // Scale the starting cost
    int startRawCost = costmap[start.x, start.y];
    if (startRawCost == int.MaxValue)
    {
        return null; // Immediate failure if start is impassable
    }
    int startCost = (int)(startRawCost * costStrength);

    heap.Enqueue(new HeuristicNode(start, startCost, Heuristic(start, end)));
    visited[start.x, start.y] = startCost;

    Point[] neighbors = new Point[]
    {
        new Point(0, 1), new Point(-1, 0), new Point(1, 0), new Point(0, -1),
        new Point(-1, 1), new Point(1, 1), new Point(-1, -1), new Point(1, -1)
    };

    while (heap.Count > 0 && maxDepth-- > 0)
    {
        HeuristicNode node = heap.Dequeue();
        if (node.point == end)
        {
            List<Point> path = new List<Point>();
            for (HeuristicNode current = node; current != null; current = current.previous)
            {
                path.Add(current.point);
            }
            return path;
        }

        foreach (Point neighborOffset in neighbors)
        {
            Point neighbor = node.point + neighborOffset;
            if (neighbor.x >= 0 && neighbor.x < resolution && neighbor.y >= 0 && neighbor.y < resolution)
            {
                int neighborRawCost = costmap[neighbor.x, neighbor.y];
                if (neighborRawCost != int.MaxValue)
                {
                    int neighborCost = (int)(neighborRawCost * costStrength);
                    int totalCost = node.cost + neighborCost;
                    int visitedCost = visited[neighbor.x, neighbor.y];
                    if (visitedCost == 0 || totalCost < visitedCost)
                    {
                        heap.Enqueue(new HeuristicNode(neighbor, totalCost, Heuristic(neighbor, end), node));
                        visited[neighbor.x, neighbor.y] = totalCost;
                    }
                }
                else
                {
                    visited[neighbor.x, neighbor.y] = -1; // Mark impassable
                }
            }
        }
    }

    return null;
}

    private static int Heuristic(Point a, Point b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)); // Chebyshev distance
    }

    private static Point GetPoint(Vector3 worldPos, int resolution)
    {
        float terrainSize = TerrainManager.Land.terrainData.size.x;
        return new Point(
            Mathf.Clamp((int)(worldPos.x / terrainSize * resolution), 0, resolution - 1),
            Mathf.Clamp((int)(worldPos.z / terrainSize * resolution), 0, resolution - 1)
        );
    }

    // Simple priority queue implementation
    private class PriorityQueue<T>
    {
        private readonly List<T> items = new List<T>();
        private readonly Comparison<T> comparison;

        public PriorityQueue(Comparison<T> comparison)
        {
            this.comparison = comparison;
        }

        public int Count => items.Count;

        public void Enqueue(T item)
        {
            items.Add(item);
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (comparison(items[parent], items[i]) <= 0) break;
                (items[i], items[parent]) = (items[parent], items[i]);
                i = parent;
            }
        }

        public T Dequeue()
        {
            if (items.Count == 0) throw new InvalidOperationException("Queue is empty");
            T result = items[0];
            items[0] = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                int smallest = i;
                if (left < items.Count && comparison(items[left], items[smallest]) < 0)
                    smallest = left;
                if (right < items.Count && comparison(items[right], items[smallest]) < 0)
                    smallest = right;
                if (smallest == i) break;
                (items[i], items[smallest]) = (items[smallest], items[i]);
                i = smallest;
            }
            return result;
        }
    }

    // Simple random number generator for seed-based randomness
    private static class SeedRandom
    {
        public static int Range(ref uint seed, int min, int max)
        {
            seed = (seed * 1664525u + 1013904223u);
            return min + (int)(seed % (uint)(max - min));
        }
    }
	
public static Dictionary<Vector3, List<Vector3>> RetrieveRoadPositions()
{
    int resolution = TerrainManager.HeightMapRes;
    Dictionary<Vector3, List<Vector3>> roadPositions = new Dictionary<Vector3, List<Vector3>>();
    int totalConnections = 0;
    int roadConnections = 0;

    foreach (PrefabDataHolder prefabHolder in PrefabManager.CurrentMapPrefabs)
    {
        Debug.Log($"Processing prefab at position: {prefabHolder.prefabData.position}, Connections: {prefabHolder.connections.Count}");
        
        // Filter for Road infrastructure type connections
        foreach (TerrainPathConnect connection in prefabHolder.connections)
        {
            totalConnections++;
            if (connection.Type == InfrastructureType.Road)
            {
                roadConnections++;

                Vector3 connectPosition = connection.gameObject.transform.position;


                // Add pathPoint to the list for this position
                if (!roadPositions.ContainsKey(prefabHolder.gameObject.transform.position))
                {
                    roadPositions[prefabHolder.gameObject.transform.position] = new List<Vector3>();
                    Debug.Log($"New position added: {prefabHolder.gameObject.transform.position}");
                }
				roadPositions[prefabHolder.gameObject.transform.position].Add(connectPosition);
            }
        }
    }

    Debug.Log($"Total connections processed: {totalConnections}, Road connections: {roadConnections}");
    Debug.Log($"Unique positions with road points: {roadPositions.Count}");
    foreach (var kvp in roadPositions)
    {
        Debug.Log($"Position {kvp.Key} has {kvp.Value.Count} vectors ");
    }

    return roadPositions;
}

	public static GameObject CreateSplatTopologyMesh(ERRoad road, PathData pathData, float outerTopoWidth)
	{
		ERModularRoad modularRoad = road.gameObject.GetComponent<ERModularRoad>();
		if (modularRoad == null)
		{
			Debug.LogWarning($"ERModularRoad component not found on road '{road.GetName()}'. Using fallback geometry.");
		}

		// Gather road points
		List<Vector3> roadPoints = new List<Vector3>();
		if (modularRoad != null && modularRoad.soSplinePoints != null && modularRoad.soSplinePoints.Count > 0)
		{
			roadPoints.AddRange(modularRoad.soSplinePoints);
		}
		else
		{
			for (int i = 0; i < road.GetMarkerCount(); i++)
			{
				roadPoints.Add(road.GetMarkerPosition(i));
			}
		}

		if (roadPoints.Count < 2)
		{
			Debug.LogError($"Road '{road.GetName()}' has insufficient points ({roadPoints.Count}) for splat/topology mesh creation.");
			return null;
		}

		// Mesh data
		List<Vector3> vertices = new List<Vector3>();
		List<Vector2> uvs = new List<Vector2>();
		List<int> triangles = new List<int>();
		float accumulatedLength = 0f;

		// Dimensions
		float roadWidth = pathData.width;
		float halfRoadWidth = roadWidth * 0.5f;
		float blendWidth = 1f; 
		float totalWidthPerSide = halfRoadWidth + blendWidth + outerTopoWidth;
		float totalWidth = roadWidth + 2f * (blendWidth + outerTopoWidth);

		// UV proportions
		float uvBlendStart = (halfRoadWidth) / totalWidth;
		float uvBlendEnd = (halfRoadWidth + blendWidth) / totalWidth;
		float uvOuterEnd = 1f;

		List<int> segmentStartIndices = new List<int>();

		for (int i = 0; i < roadPoints.Count; i++)
		{
			Vector3 point = roadPoints[i];
			Vector3 nextPoint = (i < roadPoints.Count - 1) ? roadPoints[i + 1] : point;
			Vector3 direction = (nextPoint - point).normalized;
			if (direction == Vector3.zero) continue;

			Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;

			// Vertex positions for three sections per side
			Vector3 leftOuterEdge = point - right * totalWidthPerSide;
			Vector3 leftBlendEdge = point - right * (halfRoadWidth + blendWidth);
			Vector3 leftCoreEdge = point - right * halfRoadWidth;
			Vector3 rightCoreEdge = point + right * halfRoadWidth;
			Vector3 rightBlendEdge = point + right * (halfRoadWidth + blendWidth);
			Vector3 rightOuterEdge = point + right * totalWidthPerSide;

			// Set Y to match road height
			leftOuterEdge.y = point.y;
			leftBlendEdge.y = point.y;
			leftCoreEdge.y = point.y;
			rightCoreEdge.y = point.y;
			rightBlendEdge.y = point.y;
			rightOuterEdge.y = point.y;

			segmentStartIndices.Add(vertices.Count);

			// Add vertices
			vertices.Add(leftOuterEdge);  // 0: Left outer edge
			vertices.Add(leftBlendEdge);  // 1: Left blend edge
			vertices.Add(leftCoreEdge);   // 2: Left core edge
			vertices.Add(rightCoreEdge);  // 3: Right core edge
			vertices.Add(rightBlendEdge); // 4: Right blend edge
			vertices.Add(rightOuterEdge); // 5: Right outer edge

			// UVs
			if (i > 0) accumulatedLength += Vector3.Distance(point, roadPoints[i - 1]);
			uvs.Add(new Vector2(0f, accumulatedLength));           // Left outer edge
			uvs.Add(new Vector2(uvBlendStart, accumulatedLength)); // Left blend edge
			uvs.Add(new Vector2(uvBlendEnd, accumulatedLength));   // Left core edge
			uvs.Add(new Vector2(1f - uvBlendEnd, accumulatedLength)); // Right core edge
			uvs.Add(new Vector2(1f - uvBlendStart, accumulatedLength)); // Right blend edge
			uvs.Add(new Vector2(1f, accumulatedLength));           // Right outer edge
		}

		// Generate triangles
		for (int i = 0; i < segmentStartIndices.Count - 1; i++)
		{
			int baseIndex = segmentStartIndices[i];
			int nextBaseIndex = segmentStartIndices[i + 1];

			triangles.Add(baseIndex);   triangles.Add(nextBaseIndex);  triangles.Add(baseIndex + 1);
			triangles.Add(baseIndex + 1); triangles.Add(nextBaseIndex);  triangles.Add(nextBaseIndex + 1);
			triangles.Add(baseIndex + 1); triangles.Add(nextBaseIndex + 1); triangles.Add(baseIndex + 2);
			triangles.Add(baseIndex + 2); triangles.Add(nextBaseIndex + 1); triangles.Add(nextBaseIndex + 2);
			triangles.Add(baseIndex + 2); triangles.Add(nextBaseIndex + 2); triangles.Add(baseIndex + 3);
			triangles.Add(baseIndex + 3); triangles.Add(nextBaseIndex + 2); triangles.Add(nextBaseIndex + 3);
			triangles.Add(baseIndex + 3); triangles.Add(nextBaseIndex + 3); triangles.Add(baseIndex + 4);
			triangles.Add(baseIndex + 4); triangles.Add(nextBaseIndex + 3); triangles.Add(nextBaseIndex + 4);
			triangles.Add(baseIndex + 4); triangles.Add(nextBaseIndex + 4); triangles.Add(baseIndex + 5);
			triangles.Add(baseIndex + 5); triangles.Add(nextBaseIndex + 4); triangles.Add(nextBaseIndex + 5);
		}

		if (vertices.Count == 0 || triangles.Count == 0)
		{
			Debug.LogError($"Splat/topology mesh for '{road.GetName()}' has no valid vertices or triangles.");
			return null;
		}

		// Create mesh object
		GameObject meshObj = new GameObject($"SplatTopologyMesh_{road.GetName()}");
		meshObj.transform.SetParent(road.gameObject.transform, false);
		Mesh mesh = new Mesh
		{
			name = $"SplatTopologyMesh_{road.GetName()}",
			vertices = vertices.ToArray(),
			uv = uvs.ToArray(),
			triangles = triangles.ToArray()
		};
		mesh.RecalculateNormals();
		mesh.RecalculateBounds();

		MeshFilter filter = meshObj.AddComponent<MeshFilter>();
		filter.sharedMesh = mesh;
		MeshCollider collider = meshObj.AddComponent<MeshCollider>();
		collider.sharedMesh = mesh;
		meshObj.layer = 30; // Influence layer

		Debug.Log($"Created splat/topology mesh for '{road.GetName()}': {vertices.Count} vertices, bounds: {mesh.bounds}, outerTopoWidth: {outerTopoWidth}");
		return meshObj;
	}

    public static void RotatePaths(bool CW)
    {
        if (_roadNetwork != null)
        {
            PathParent.transform.Rotate(0, CW ? 90f : -90f, 0, Space.World);
            foreach (ERRoad road in _roadNetwork.GetRoadObjects())
            {
                Vector3[] newMarkerPositions = road.GetMarkerPositions().Select(p => PathParent.rotation * (p - PathParent.position) + PathParent.position).ToArray();
                road.SetMarkerPositions(newMarkerPositions);
                road.Refresh();
            }
        }
        else
        {
            Debug.LogWarning("RoadNetwork is not initialized. Cannot rotate paths.");
        }
    }

    #if UNITY_EDITOR
    public static void SpawnPaths(WorldSerialization.PathData[] paths, int progressID)
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(Coroutines.SpawnPaths(paths, progressID));
    }

    public static void DeletePaths(NodeCollection[] paths, int delPath = 0)
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(Coroutines.DeletePaths(paths, delPath));
    }
    #else
    public static void SpawnPaths(WorldSerialization.PathData[] paths, int progressID = 0)
    {
        CoroutineManager.Instance.StartRuntimeCoroutine(Coroutines.SpawnPaths(paths, progressID));
    }

    public static void DeletePaths(NodeCollection[] paths, int delPath = 0)
    {
        CoroutineManager.Instance.StartRuntimeCoroutine(Coroutines.DeletePaths(paths, delPath));
    }
    #endif

    private static class Coroutines
    {
        public static IEnumerator SpawnPaths(WorldSerialization.PathData[] paths, int progressID = 0)
        {
			//SetERNetworkPosition();
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            for (int i = 0; i < paths.Length; i++)
            {
                if (sw.Elapsed.TotalSeconds > 0.1f)
                {
                    yield return null;
                    #if UNITY_EDITOR
                    Progress.Report(progressID, (float)i / paths.Length, "Spawning Paths: " + i + " / " + paths.Length);
                    #endif
                    sw.Restart();
                }
                SpawnPath(paths[i]);
            }
            #if UNITY_EDITOR
            Progress.Report(progressID, 0.99f, "Spawned " + paths.Length + " paths.");
            Progress.Finish(progressID, Progress.Status.Succeeded);
            #endif
        }

        public static IEnumerator DeletePaths(NodeCollection[] paths, int progressID = 0)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            #if UNITY_EDITOR
            if (progressID == 0)
                progressID = Progress.Start("Delete Paths", null, Progress.Options.Sticky);
            #endif

            for (int i = 0; i < paths.Length; i++)
            {
                if (sw.Elapsed.TotalSeconds > 0.1f)
                {
                    yield return null;
                    #if UNITY_EDITOR
                    Progress.Report(progressID, (float)i / paths.Length, "Deleting Paths: " + i + " / " + paths.Length);
                    #endif
                    sw.Restart();
                }
				//Debug.LogError(paths[i].gameObject.name);
                ERRoad roadToDelete = _roadNetwork.GetRoadByName(paths[i].pathData.name);

                if (roadToDelete != null)
                {
                    roadToDelete.Destroy();
                }
                else
                {
                    Debug.LogWarning($"Could not find road named {paths[i].gameObject.name} to delete.");
                }

                
            }

            #if UNITY_EDITOR
            Progress.Report(progressID, 0.99f, "Deleted " + paths.Length + " paths.");
            Progress.Finish(progressID, Progress.Status.Succeeded);
            #endif
        }
    }
	


    public static Transform PathParent { get; private set; }
    public static NodeCollection[] CurrentMapPaths { get => PathParent.GetComponentsInChildren<NodeCollection>(); }
}
#if UNITY_EDITOR
public static class ExportMaterialTextures
{
    public static void ExportTextures(Material material, string outputFolderPath = "Assets/Resources/Textures")
    {
        if (material == null)
        {
            Debug.LogError("Material is null!");
            return;
        }

        // Define the output directory (relative to project for AssetDatabase, absolute for file system)
        string outputDirectory = Path.Combine(Application.dataPath, outputFolderPath.Substring("Assets/".Length));

        // Ensure the output directory exists
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        // Ensure the folder exists in Unity's AssetDatabase
        if (!AssetDatabase.IsValidFolder(outputFolderPath))
        {
            string parentFolder = Path.GetDirectoryName(outputFolderPath).Replace("\\", "/");
            string newFolderName = Path.GetFileName(outputFolderPath);
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }

        // Get all texture properties from the material
        Shader shader = material.shader;
        bool hasTextures = false;

        for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                Texture texture = material.GetTexture(propertyName);

                // Skip if texture is null
                if (texture == null)
                {
                    Debug.Log($"Skipping {propertyName}: Texture is null.");
                    continue;
                }

                // Convert to Texture2D (if possible)
                Texture2D texture2D = texture as Texture2D;
                if (texture2D == null)
                {
                    texture2D = ConvertToTexture2D(texture);
                }

                if (texture2D != null)
                {
                    // Try to make texture readable
                    string assetPath = AssetDatabase.GetAssetPath(texture2D);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (importer != null)
                        {
                            if (!importer.isReadable)
                            {
                                importer.isReadable = true;
                                try
                                {
                                    importer.SaveAndReimport();
                                    Debug.Log($"Made texture '{texture2D.name}' readable at: {assetPath}");
                                }
                                catch (System.Exception e)
                                {
                                    Debug.LogWarning($"Failed to reimport texture '{texture2D.name}' at {assetPath}: {e.Message}");
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"No TextureImporter found for '{texture2D.name}' at {assetPath}. It may not be a project asset.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No asset path found for texture '{texture2D.name}'. It may be a built-in or generated texture.");
                    }

                    // Double-check readability
                    if (!texture2D.isReadable)
                    {
                        Debug.LogWarning($"Texture '{texture2D.name}' is still not readable after import attempt. Attempting to create a readable copy.");
                        texture2D = CreateReadableTextureCopy(texture2D);
                    }

                    if (texture2D != null && texture2D.isReadable)
                    {
                        // Encode to PNG
                        byte[] bytes = texture2D.EncodeToPNG();

                        // Generate a unique filename
                        string textureName = texture.name.Replace("/", "_");
                        if (string.IsNullOrEmpty(textureName))
                            textureName = $"{propertyName}_Texture";
                        string pngPath = Path.Combine(outputDirectory, $"{textureName}.png");

                        // Ensure unique filename
                        pngPath = GetUniqueFilePath(pngPath);

                        // Save to disk
                        File.WriteAllBytes(pngPath, bytes);
                        Debug.Log($"Texture saved at: {pngPath}");
                        hasTextures = true;

                        // Refresh AssetDatabase to make the PNG visible in Unity
                        string relativePath = "Assets" + pngPath.Substring(Application.dataPath.Length);
                        AssetDatabase.ImportAsset(relativePath);
                    }
                    else
                    {
                        Debug.LogWarning($"Skipping {propertyName}: Texture '{texture.name}' could not be made readable.");
                    }
                }
                else
                {
                    Debug.LogWarning($"Skipping {propertyName}: Could not convert texture '{texture.name}' to Texture2D.");
                }
            }
        }

        AssetDatabase.Refresh();
        if (hasTextures)
        {
            Debug.Log($"Texture export complete for material: {material.name}");
        }
        else
        {
            Debug.Log($"No valid textures found for material: {material.name}");
        }
    }

    // Convert non-Texture2D textures (e.g., RenderTexture) to Texture2D
    private static Texture2D ConvertToTexture2D(Texture texture)
    {
        if (texture is Texture2D tex2D)
            return tex2D;

        if (texture is RenderTexture renderTexture)
        {
            Texture2D tempTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            tempTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            tempTexture.Apply();
            RenderTexture.active = null;
            return tempTexture;
        }

        return null; // Unsupported texture type
    }

    // Create a readable copy of a Texture2D
    private static Texture2D CreateReadableTextureCopy(Texture2D source)
    {
        if (source == null)
            return null;

        try
        {
            // Create a temporary RenderTexture
            RenderTexture tempRT = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, tempRT);

            // Create a new readable Texture2D
            Texture2D readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            RenderTexture.active = tempRT;
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(tempRT);

            return readableTexture;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to create readable copy of texture '{source.name}': {e.Message}");
            return null;
        }
    }

    // Ensure unique file path to avoid overwriting
    private static string GetUniqueFilePath(string path)
    {
        string directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        int counter = 1;
        string newPath = path;

        while (File.Exists(newPath))
        {
            newPath = Path.Combine(directory, $"{fileName}_{counter}{extension}");
            counter++;
        }

        return newPath;
    }
	

	
}
#endif