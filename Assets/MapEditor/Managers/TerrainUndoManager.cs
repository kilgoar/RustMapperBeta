using UnityEngine;
using System.Collections.Generic;
using RustMapEditor.Variables;

public static class TerrainUndoManager
{
    private static List<TerrainUndoState> undoStack = new List<TerrainUndoState>();
    private static List<TerrainUndoState> redoStack = new List<TerrainUndoState>();
    private static int maxStates = 512;
    private static long totalMemoryUsage = 0;

    public enum TerrainOperationType
    {
        HeightMap,
        SplatMap,
        BiomeMap,
        AlphaMap,
        TopologyMap,
        BlendMap
    }

    // HeightMap Undo
    public static void RegisterHeightMapUndo(string name, TerrainManager.TerrainType terrainType)
    {
        float[,] heightMapData = TerrainManager.GetHeightMap(terrainType);
        int resolution = TerrainManager.HeightMapRes;
        RegisterHeightMapUndo(name, terrainType, 0, 0, resolution, resolution, heightMapData);
    }

    public static void RegisterHeightMapUndo(
        string name,
        TerrainManager.TerrainType terrainType,
        int startX,
        int startY,
        int width,
        int height,
        float[,] heightMapData)
		{

			float[,] heightMapClone = new float[height, width];
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					heightMapClone[i, j] = heightMapData[i, j];
				}
			}
			
			var state = new TerrainUndoState(
				name,
				TerrainOperationType.HeightMap,
				heightMapClone,
				startX,
				startY,
				width,
				height,
				terrainType: terrainType
			);
			
			RegisterState(state);
		}

    // SplatMap Undo
    public static void RegisterSplatMapUndo(string name, TerrainManager.LayerType layerType = TerrainManager.LayerType.Ground, int topologyLayer = -1)
    {
        float[,,] splatMapData = TerrainManager.GetSplatMap(layerType, topologyLayer);
        int resolution = TerrainManager.SplatMapRes;
        RegisterSplatMapUndo(name, 0, 0, resolution, resolution, splatMapData);
    }

public static void RegisterSplatMapUndo(
    string name,
    int startX,
    int startY,
    int width,
    int height,
    float[,,] splatMapData)
	{

		int numLayers = splatMapData.GetLength(2);
		if (numLayers == 0)
		{
			Debug.LogWarning($"RegisterSplatMapUndo: Splatmap data has zero layers");
			return;
		}

		// Clone the splatmap array
		float[,,] splatMapClone = new float[height, width, numLayers];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				for (int k = 0; k < numLayers; k++)
				{
					splatMapClone[i, j, k] = splatMapData[i, j, k];
				}
			}
		}

		var state = new TerrainUndoState(
			name,
			TerrainOperationType.SplatMap,
			splatMapClone,
			startX,
			startY,
			width,
			height
		);
		RegisterState(state);
	}

    // BiomeMap Undo
    public static void RegisterBiomeMapUndo(string name)
    {
        float[,,] biomeMapData = TerrainManager.GetSplatMap(TerrainManager.LayerType.Biome);
        int resolution = TerrainManager.SplatMapRes;
        RegisterBiomeMapUndo(name, 0, 0, resolution, resolution, biomeMapData);
    }

	public static void RegisterBiomeMapUndo(
		string name,
		int startX,
		int startY,
		int width,
		int height,
		float[,,] biomeMapData)
	{
		int resolution = TerrainManager.SplatMapRes;

		int numLayers = biomeMapData.GetLength(2);
		if (numLayers == 0)
		{
			Debug.LogWarning($"RegisterBiomeMapUndo: Biome map data has zero layers");
			return;
		}

		// Clone the biome map array
		float[,,] biomeMapClone = new float[height, width, numLayers];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				for (int k = 0; k < numLayers; k++)
				{
					biomeMapClone[i, j, k] = biomeMapData[i, j, k];
				}
			}
		}

		var state = new TerrainUndoState(
			name,
			TerrainOperationType.BiomeMap,
			biomeMapClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Biome
		);
		RegisterState(state);
	}
	
    // AlphaMap Undo
    public static void RegisterAlphaMapUndo(string name)
    {
        bool[,] alphaMapData = TerrainManager.GetAlphaMap();
        int resolution = TerrainManager.AlphaMapRes;
        RegisterAlphaMapUndo(name, 0, 0, resolution, resolution, alphaMapData);
    }

    public static void RegisterAlphaMapUndo(
        string name,
        int startX,
        int startY,
        int width,
        int height,
        bool[,] alphaMapData)
    {
        var state = new TerrainUndoState(
            name,
            TerrainOperationType.AlphaMap,
            alphaMapData,
            startX,
            startY,
            width,
            height,
            terrainType: TerrainManager.TerrainType.Land,
            layerType: TerrainManager.LayerType.Alpha
        );
        RegisterState(state);
    }

    // TopologyMap Undo
    public static void RegisterTopologyMapUndo(string name, int topologyLayer)
    {
        bool[,] topologyMapData = TopologyData.GetTopologyBitmap(TerrainTopology.IndexToType(topologyLayer));
        int resolution = TerrainManager.AlphaMapRes;
        RegisterTopologyMapUndo(name, topologyLayer, 0, 0, resolution, resolution, topologyMapData);
    }

    public static void RegisterTopologyMapUndo(
        string name,
        int topologyLayer,
        int startX,
        int startY,
        int width,
        int height,
        bool[,] topologyMapData)
    {
        var state = new TerrainUndoState(
            name,
            TerrainOperationType.TopologyMap,
            topologyMapData,
            startX,
            startY,
            width,
            height,
            terrainType: TerrainManager.TerrainType.Land,
            layerType: TerrainManager.LayerType.Topology,
            topologyLayer: topologyLayer
        );
        RegisterState(state);
    }

    // BlendMap Undo
    public static void RegisterBlendMapUndo(string name, int startX, int startY, int width, int height, Color[] blendMapData)
    {
        var state = new TerrainUndoState(
            name,
            TerrainOperationType.BlendMap,
            blendMapData,
            startX,
            startY,
            width,
            height,
            terrainType: TerrainManager.TerrainType.Land
        );
        RegisterState(state);
    }

    private static void RegisterState(TerrainUndoState state)
    {
        state.LogMemoryUsage();
        totalMemoryUsage += state.EstimateMemoryUsage();
        //Debug.Log($"Total undo/redo memory usage: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");

        undoStack.Add(state);
        redoStack.Clear();

        while (undoStack.Count > maxStates)
        {
            var oldestState = undoStack[0];
            totalMemoryUsage -= oldestState.EstimateMemoryUsage();
            undoStack.RemoveAt(0);
            //Debug.Log($"Removed oldest state '{oldestState.OperationName}' to enforce maxStates={maxStates}. New total: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");
        }
    }

    [ConsoleCommand("Undo terrain change")]
    public static void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("No actions to undo.");
            return;
        }

        var state = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);
        redoStack.Add(state);

        ApplyState(state);
        totalMemoryUsage -= state.EstimateMemoryUsage();
        Debug.Log($"Undid action: '{state.OperationName}' (Type: {state.OperationType}). Total memory: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");
    }

    [ConsoleCommand("Redo terrain change")]
    public static void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.Log("No actions to redo.");
            return;
        }

        var state = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);
        undoStack.Add(state);

        ApplyState(state);
        totalMemoryUsage += state.EstimateMemoryUsage();
        Debug.Log($"Redid action: '{state.OperationName}' (Type: {state.OperationType}). Total memory: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");
    }

    private static void ApplyState(TerrainUndoState state)
    {
        switch (state.OperationType)
        {
            case TerrainOperationType.HeightMap:
                TerrainManager.SetHeightMapRegion(
                    (float[,])state.Data,
                    state.StartX,
                    state.StartY,
                    state.Width,
                    state.Height,
                    state.TerrainType
                );
                TerrainManager.SyncHeightTextures();
                TerrainManager.Callbacks.InvokeHeightMapUpdated(state.TerrainType);
                break;

            case TerrainOperationType.SplatMap:
                TerrainManager.SetSplatMapRegion(
                    (float[,,])state.Data,
                    state.LayerType,
                    state.StartX,
                    state.StartY,
                    state.Width,
                    state.Height,
                    state.TopologyLayer
                );
                //TerrainManager.SyncSplatTexture();
                TerrainManager.Callbacks.InvokeLayerUpdated(state.LayerType, state.TopologyLayer);
                break;

            case TerrainOperationType.BiomeMap:
                TerrainManager.SetBiomeMap(
                    (float[,,])state.Data,
                    state.StartX,
                    state.StartY,
                    state.Width,
                    state.Height
                );
                //TerrainManager.SyncBiomeTexture();
                //TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Biome, -1);
                break;

            case TerrainOperationType.AlphaMap:
                TerrainManager.SetAlphaRegion(
                    (bool[,])state.Data,
                    state.StartX,
                    state.StartY,
                    state.Width,
                    state.Height
                );
                //TerrainManager.SyncAlphaTexture();
                TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Alpha, -1);
                break;

            case TerrainOperationType.TopologyMap:
                TopologyData.SetTopology(
                    TerrainTopology.IndexToType(state.TopologyLayer),
                    state.StartX,
                    state.StartY,
                    state.Width,
                    state.Height,
                    (bool[,])state.Data
                );
                TopologyData.UpdateTexture();
                TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Topology, state.TopologyLayer);
                break;

            case TerrainOperationType.BlendMap:
                TerrainManager.BlendMapTexture.SetPixels(
                    state.StartX,
                    state.StartY,
                    state.Width,
                    state.Height,
                    (Color[])state.Data
                );
                TerrainManager.BlendMapTexture.Apply();
                TerrainManager.Land.Flush();
                TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Blend, -1);
                break;
        }
    }

    [ConsoleCommand("Clear terrain undo history")]
    public static void ClearHistory()
    {
        undoStack.Clear();
        redoStack.Clear();
        totalMemoryUsage = 0;
        Debug.Log("Cleared undo/redo history.");
    }
}