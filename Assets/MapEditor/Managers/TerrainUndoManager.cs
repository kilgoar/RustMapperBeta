using UnityEngine;
using RustMapEditor.Variables;

public static class TerrainUndoManager
{
	
	private static TerrainUndoState pendingUndoState; // Store undo state temporarily
	
    public enum TerrainOperationType
    {
        HeightMap,
        SplatMap,
        BiomeMap,
        AlphaMap,
        TopologyMap,
        BlendMap,
        TopologyMask
    }

    #region HeightMap
    public static void RegisterHeightMapUndoRedo(string name, TerrainManager.TerrainType terrainType, float[,] heightMapUndoState)
    {
        int resolution = TerrainManager.HeightMapRes;
        RegisterHeightMapUndoRedo(name, terrainType, 0, 0, resolution, resolution, heightMapUndoState);
    }

    public static void RegisterHeightMapUndoRedo(
        string name,
        TerrainManager.TerrainType terrainType,
        int startX,
        int startY,
        int width,
        int height,
        float[,] heightMapData)
    {
        // Capture undo state (before modification)
        float[,] heightMapClone = new float[height, width];
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                heightMapClone[i, j] = heightMapData[i, j];
            }
        }
        var undoState = new TerrainUndoState(
            name,
            TerrainOperationType.HeightMap,
            heightMapClone,
            startX,
            startY,
            width,
            height,
            terrainType: terrainType
        );

        // Capture redo state (after modification)
        float[,] modifiedHeightMap = TerrainManager.GetHeightMap(terrainType);
        float[,] redoClone = new float[height, width];
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                redoClone[i, j] = modifiedHeightMap[startY + i, startX + j];
            }
        }
        var redoState = new TerrainUndoState(
            name,
            TerrainOperationType.HeightMap,
            redoClone,
            startX,
            startY,
            width,
            height,
            terrainType: terrainType
        );

        // Register the undo/redo action
        RegisterState(new TerrainUndoAction(undoState, redoState));
    }
    #endregion

	public static void RegisterSplatMapUndoRedo(string name, float [,,] before, TerrainManager.LayerType layerType = TerrainManager.LayerType.Ground, int topologyLayer = -1)
	{
		int resolution = TerrainManager.SplatMapRes;
		RegisterSplatMapUndoRedo(name, 0, 0, resolution, resolution, before, layerType, topologyLayer);
	}

	public static void RegisterSplatMapUndoRedo(
		string name,
		int startX,
		int startY,
		int width,
		int height,
		float[,,] splatMapData,
		TerrainManager.LayerType layerType = TerrainManager.LayerType.Ground,
		int topologyLayer = -1)
	{
		int numLayers = splatMapData.GetLength(2);
		if (numLayers == 0)
		{
			Debug.LogWarning($"RegisterSplatMapUndoRedo: Splatmap data has zero layers");
			return;
		}

		// Capture undo state (before modification)
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
		var undoState = new TerrainUndoState(
			name,
			TerrainOperationType.SplatMap,
			splatMapClone,
			startX,
			startY,
			width,
			height,
			layerType: layerType,
			topologyLayer: topologyLayer
		);

		// Capture redo state (after modification)
		float[,,] modifiedSplatMap = TerrainManager.GetSplatMap(layerType, topologyLayer);
		float[,,] redoClone = new float[height, width, numLayers];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				for (int k = 0; k < numLayers; k++)
				{
					redoClone[i, j, k] = modifiedSplatMap[startY + i, startX + j, k];
				}
			}
		}
		var redoState = new TerrainUndoState(
			name,
			TerrainOperationType.SplatMap,
			redoClone,
			startX,
			startY,
			width,
			height,
			layerType: layerType,
			topologyLayer: topologyLayer
		);

		// Register the undo/redo action
		RegisterState(new TerrainUndoAction(undoState, redoState));
	}

	public static void RegisterBiomeMapUndoRedo(string name, float[,,] before)
	{
		int resolution = TerrainManager.SplatMapRes;
		RegisterBiomeMapUndoRedo(name, 0, 0, resolution, resolution, before);
	}

	public static void RegisterBiomeMapUndoRedo(
		string name,
		int startX,
		int startY,
		int width,
		int height,
		float[,,] biomeMapData)
	{
		int numLayers = biomeMapData.GetLength(2);
		if (numLayers == 0)
		{
			Debug.LogWarning($"RegisterBiomeMapUndoRedo: Biome map data has zero layers");
			return;
		}

		// Capture undo state (before modification)
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
		var undoState = new TerrainUndoState(
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

		// Capture redo state (after modification)
		float[,,] modifiedBiomeMap = TerrainManager.GetSplatMap(TerrainManager.LayerType.Biome);
		float[,,] redoClone = new float[height, width, numLayers];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				for (int k = 0; k < numLayers; k++)
				{
					redoClone[i, j, k] = modifiedBiomeMap[startY + i, startX + j, k];
				}
			}
		}
		var redoState = new TerrainUndoState(
			name,
			TerrainOperationType.BiomeMap,
			redoClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Biome
		);

		// Register the undo/redo action
		RegisterState(new TerrainUndoAction(undoState, redoState));
	}

	public static void RegisterAlphaMapUndoRedo(string name, bool[,] before)
	{
		int resolution = TerrainManager.AlphaMapRes;
		RegisterAlphaMapUndoRedo(name, 0, 0, resolution, resolution, before);
	}

	public static void RegisterAlphaMapUndoRedo(
		string name,
		int startX,
		int startY,
		int width,
		int height,
		bool[,] alphaMapData)
	{
		// Capture undo state (before modification)
		bool[,] alphaMapClone = new bool[height, width];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				alphaMapClone[i, j] = alphaMapData[i, j];
			}
		}
		var undoState = new TerrainUndoState(
			name,
			TerrainOperationType.AlphaMap,
			alphaMapClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Alpha
		);

		// Capture redo state (after modification)
		bool[,] modifiedAlphaMap = TerrainManager.GetAlphaMap();
		bool[,] redoClone = new bool[height, width];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				redoClone[i, j] = modifiedAlphaMap[startY + i, startX + j];
			}
		}
		var redoState = new TerrainUndoState(
			name,
			TerrainOperationType.AlphaMap,
			redoClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Alpha
		);

		// Register the undo/redo action
		RegisterState(new TerrainUndoAction(undoState, redoState));
	}

	public static void RegisterTopologyMaskUndoRedo(
		string name,
		int startX,
		int startY,
		int width,
		int height,
		int topologyMask,
		int[,] before)
	{
		// Validate input dimensions
		if (width <= 0 || height <= 0 || startX < 0 || startY < 0 ||
			startX + width > TerrainManager.AlphaMapRes || startY + height > TerrainManager.AlphaMapRes)
		{
			Debug.LogWarning($"RegisterTopologyMaskUndoRedo: Invalid region dimensions. startX={startX}, startY={startY}, width={width}, height={height}");
			return;
		}

		if (before == null || before.GetLength(0) != height || before.GetLength(1) != width)
		{
			Debug.LogWarning($"RegisterTopologyMaskUndoRedo: Invalid before data dimensions. Expected [{height}, {width}], got [{before?.GetLength(0)}, {before?.GetLength(1)}]");
			return;
		}

		// Use provided 'before' as undo state
		var undoState = new TerrainUndoState(
			name,
			TerrainOperationType.TopologyMask,
			before,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Topology,
			topologyLayer: -1
		);

		// Capture redo state (after modification)
		TerrainMap<int> modifiedTopologyMap = TopologyData.GetTerrainMap();
		int[,] redoBitmaskData = new int[height, width];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				if (startX + j < modifiedTopologyMap.res && startY + i < modifiedTopologyMap.res)
				{
					redoBitmaskData[i, j] = modifiedTopologyMap[startY + i, startX + j] & topologyMask;
				}
			}
		}
		var redoState = new TerrainUndoState(
			name,
			TerrainOperationType.TopologyMask,
			redoBitmaskData,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Topology,
			topologyLayer: -1
		);

		// Register the undo/redo action
		RegisterState(new TerrainUndoAction(undoState, redoState));
	}

	public static void RegisterTopologyMapUndoRedo(string name, int topologyLayer, bool[,] before)
	{
		int resolution = TerrainManager.AlphaMapRes;
		RegisterTopologyMapUndoRedo(name, topologyLayer, 0, 0, resolution, resolution, before);
	}

	public static void RegisterTopologyMapUndoRedo(
		string name,
		int topologyLayer,
		int startX,
		int startY,
		int width,
		int height,
		bool[,] topologyMapData)
	{
		// Capture undo state (before modification)
		bool[,] topologyMapClone = new bool[height, width];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				topologyMapClone[i, j] = topologyMapData[i, j];
			}
		}
		var undoState = new TerrainUndoState(
			name,
			TerrainOperationType.TopologyMap,
			topologyMapClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Topology,
			topologyLayer: topologyLayer
		);

		// Capture redo state (after modification)
		bool[,] modifiedTopologyMap = TopologyData.GetTopologyBitmap(TerrainTopology.IndexToType(topologyLayer));
		bool[,] redoClone = new bool[height, width];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				redoClone[i, j] = modifiedTopologyMap[startY + i, startX + j];
			}
		}
		var redoState = new TerrainUndoState(
			name,
			TerrainOperationType.TopologyMap,
			redoClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land,
			layerType: TerrainManager.LayerType.Topology,
			topologyLayer: topologyLayer
		);

		// Register the undo/redo action
		RegisterState(new TerrainUndoAction(undoState, redoState));
	}

	public static void RegisterBlendMapUndoRedo(string name, int startX, int startY, int width, int height, Color[] blendMapData)
	{
		// Capture undo state (before modification)
		Color[] blendMapClone = new Color[width * height];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				blendMapClone[i * width + j] = blendMapData[i * width + j];
			}
		}
		var undoState = new TerrainUndoState(
			name,
			TerrainOperationType.BlendMap,
			blendMapClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land
		);

		// Capture redo state (after modification)
		Color[] modifiedBlendMap = TerrainManager.BlendMapTexture.GetPixels(startX, startY, width, height);
		Color[] redoClone = new Color[width * height];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				redoClone[i * width + j] = modifiedBlendMap[i * width + j];
			}
		}
		var redoState = new TerrainUndoState(
			name,
			TerrainOperationType.BlendMap,
			redoClone,
			startX,
			startY,
			width,
			height,
			terrainType: TerrainManager.TerrainType.Land
		);

		// Register the undo/redo action
		RegisterState(new TerrainUndoAction(undoState, redoState));
	}

	private static void RegisterState(TerrainUndoAction action)
	{
		UndoManager.RegisterAction(action);
	}

    public static void ApplyState(TerrainUndoState state)
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
                break;

            case TerrainOperationType.AlphaMap:
                TerrainManager.SetAlphaRegion(
                    (bool[,])state.Data,
                    state.StartX,
                    state.StartY,
                    state.Width,
                    state.Height
                );
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
                TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Topology, state.TopologyLayer);
                TopologyData.UpdateTexture();
                break;

            case TerrainOperationType.TopologyMask:
                int[,] bitmaskData = (int[,])state.Data;
                if (bitmaskData.GetLength(0) != state.Height || bitmaskData.GetLength(1) != state.Width)
                {
                    Debug.LogError($"ApplyState: TopologyMask data dimensions mismatch. Expected [{state.Height}, {state.Width}], got [{bitmaskData.GetLength(0)}, {bitmaskData.GetLength(1)}]");
                    return;
                }
                TopologyData.OverwriteTopologyRegion(bitmaskData, state.StartX, state.StartY, state.Width, state.Height);
                TopologyData.UpdateTexture();
                TerrainManager.Callbacks.InvokeLayerUpdated(TerrainManager.LayerType.Topology, -1);
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

    [ConsoleCommand("Undo terrain change")]
    public static void Undo()
    {
        UndoManager.Undo();
    }

    [ConsoleCommand("Redo terrain change")]
    public static void Redo()
    {
        UndoManager.Redo();
    }

    [ConsoleCommand("Clear terrain undo history")]
    public static void ClearHistory()
    {
        UndoManager.ClearHistory();
    }
}