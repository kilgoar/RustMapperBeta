using UnityEngine;
using System.Collections.Generic;

public class Monument : TerrainPlacement
{
    [SerializeField] public float Radius; // Radius of influence in world units
    [SerializeField] public float Fade;   // Fade distance for blending in world units
	
protected override void ApplyHeightMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions)
{
    if (!HeightMap) { Debug.Log("skip heightmap"); return; }
    if (heightmap == null) { Debug.Log("heightmap not found / invalid"); return; }

    Texture2D heightTexture = heightmap.GetResource();
    Texture2D blendTexture = blendmap.GetResource();

    if (heightTexture != null && heightTexture.isReadable)
    {
        Color pixel = heightTexture.GetPixel(0, 0);
        Debug.Log($"heightTexture Pixel (0,0): R={pixel.r}, G={pixel.g}, B={pixel.b}, A={pixel.a}");
    }

    float radius = Radius == 0f ? extents.x : Radius;
    bool useBlendMap = blendTexture != null;
    float radiusX = useBlendMap ? extents.x : radius;
    float radiusZ = useBlendMap ? extents.z : radius;

    Vector3 position = localToWorld.MultiplyPoint3x4(Vector3.zero);
    Quaternion rotation = localToWorld.rotation;
    Vector3 localXAxis = rotation * Vector3.right;
    Vector3 localZAxis = rotation * Vector3.forward;

    float extentX = Mathf.Abs(Vector3.Dot(new Vector3(radiusX, 0f, 0f), localXAxis)) +
                    Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radiusZ), localXAxis));
    float extentZ = Mathf.Abs(Vector3.Dot(new Vector3(radiusX, 0f, 0f), localZAxis)) +
                    Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radiusZ), localZAxis));

    Vector3[] corners = new Vector3[]
    {
        position + new Vector3(-extentX, 0f, -extentZ),
        position + new Vector3(extentX, 0f, -extentZ),
        position + new Vector3(-extentX, 0f, extentZ),
        position + new Vector3(extentX, 0f, extentZ)
    };

    int[] gridBounds = TerrainManager.WorldCornersToGrid(corners[0], corners[1], corners[2], corners[3]);
    int minX = Mathf.Max(0, gridBounds[0]), minZ = Mathf.Max(0, gridBounds[1]);
    int maxX = Mathf.Min(TerrainManager.HeightMapRes - 1, gridBounds[2]), maxZ = Mathf.Min(TerrainManager.HeightMapRes - 1, gridBounds[3]);
    int width = maxX - minX + 1, height = maxZ - minZ + 1;

    if (width <= 0 || height <= 0) return;

    // Capture current heightmap state for undo
    float[,] heightMap = TerrainManager.GetHeightMap();
    float[,] regionHeightsBefore = new float[height, width];
    for (int z = 0; z < height; z++)
    {
        for (int x = 0; x < width; x++)
        {
            regionHeightsBefore[z, x] = heightMap[z + minZ, x + minX];
        }
    }


    float[,] regionHeights = new float[height, width];
    Vector3 terrainPosition = TerrainManager.Land.transform.position;
    Vector3 terrainSize = TerrainManager.TerrainSize;
    Vector3 rcpSize = new Vector3(1f / terrainSize.x, 1f / terrainSize.y, 1f / terrainSize.z);

    for (int x = minX; x <= maxX; x++)
    {
        for (int z = minZ; z <= maxZ; z++)
        {
            float normX = ((float)x + 0.5f) / TerrainManager.HeightMapRes;
            float normZ = ((float)z + 0.5f) / TerrainManager.HeightMapRes;

            Vector3 worldPos = new Vector3(
                terrainPosition.x + normX * terrainSize.x,
                0f,
                terrainPosition.z + normZ * terrainSize.z
            );
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos) - offset;

            float u = Mathf.Clamp01((localPos.x + extents.x) / size.x);
            float v = Mathf.Clamp01((localPos.z + extents.z) / size.z);
            float fade = useBlendMap
                ? (blendTexture?.GetPixelBilinear(u, v).a ?? 0f)
                : Mathf.InverseLerp(radius, radius - Fade, localPos.Magnitude2D());

            if (fade <= 0f)
            {
                regionHeights[z - minZ, x - minX] = heightMap[z, x];
                continue;
            }

            float combinedHeight = BitUtility.SampleHeightBilinear(heightTexture, u, v);
            float worldHeight = position.y + offset.y + combinedHeight * size.y;
            float normalizedHeight = (worldHeight - terrainPosition.y) * rcpSize.y;

            regionHeights[z - minZ, x - minX] = Mathf.SmoothStep(heightMap[z, x], normalizedHeight, fade);
            dimensions.IncludeRect(new RectInt(x, z, 1, 1));
        }
    }

    TerrainManager.SetHeightMapRegion(regionHeights, minX, minZ, width, height);
	

    // Register undo state
    TerrainUndoManager.RegisterHeightMapUndoRedo(
        "Monument HeightMap",
        TerrainManager.TerrainType.Land,
        minX,
        minZ,
        width,
        height,
        regionHeightsBefore
    );
}
protected override int CheckTolerance(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions)
{
    if (!HeightMap || heightmap == null)
    {
        Debug.LogWarning("Heightmap is not enabled or invalid. Returning 0 tolerance.");
        return 0;
    }

    Texture2D heightTexture = heightmap.GetResource();
    Texture2D blendTexture = blendmap?.GetResource();
    if (heightTexture == null || !heightTexture.isReadable)
    {
        Debug.LogWarning("Height texture is null or not readable. Returning 0 tolerance.");
        return 0;
    }
    if (blendTexture == null || !blendTexture.isReadable)
    {
        Debug.LogWarning("Blend texture is null or not readable. Returning 0 tolerance.");
        return 0;
    }

    // Step 1: Traverse blendmap to find blend base coordinates (alpha between 0.01 and 0.1)
    List<Vector2> blendBaseUVs = new List<Vector2>();
    int width = blendTexture.width;
    int height = blendTexture.height;
    for (int x = 0; x < width; x += Mathf.Max(1, width / 100)) // Stride for performance
    {
        for (int y = 0; y < height; y += Mathf.Max(1, height / 100))
        {
            float u = x / (float)(width - 1);
            float v = y / (float)(height - 1);
            float blendAlpha = blendTexture.GetPixelBilinear(u, v).a;
            if (blendAlpha > 0.01f && blendAlpha <= 0.1f)
            {
                blendBaseUVs.Add(new Vector2(u, v));
            }
        }
    }

    if (blendBaseUVs.Count == 0)
    {
        Debug.LogWarning("No blend base coordinates found with alpha in (0.01, 0.1]. Returning 0 tolerance.");
        return 0;
    }

    // Step 2: Set up bounding box (MATCH ApplyHeightMap/GetWorldCorners LOGIC)
    float radius = Radius == 0f ? extents.x : Radius;
    bool useBlendMap = blendTexture != null;
    float radiusX = useBlendMap ? extents.x : radius;
    float radiusZ = useBlendMap ? extents.z : radius;

    Vector3 position = localToWorld.MultiplyPoint3x4(Vector3.zero);
    Quaternion rotation = localToWorld.rotation;
    Vector3 localXAxis = rotation * Vector3.right;
    Vector3 localZAxis = rotation * Vector3.forward;

    float extentX = Mathf.Abs(Vector3.Dot(new Vector3(radiusX, 0f, 0f), localXAxis)) +
                    Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radiusZ), localXAxis));
    float extentZ = Mathf.Abs(Vector3.Dot(new Vector3(radiusX, 0f, 0f), localZAxis)) +
                    Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radiusZ), localZAxis));

    Vector3[] corners = new Vector3[]
    {
        position + new Vector3(-extentX, 0f, -extentZ),
        position + new Vector3(extentX, 0f, -extentZ),
        position + new Vector3(-extentX, 0f, extentZ),
        position + new Vector3(extentX, 0f, extentZ)
    };

    // Convert to terrain grid coordinates
    int[] gridBounds = TerrainManager.WorldCornersToGrid(corners[0], corners[1], corners[2], corners[3]);
    int minX = Mathf.Max(0, gridBounds[0]), minZ = Mathf.Max(0, gridBounds[1]);
    int maxX = Mathf.Min(TerrainManager.HeightMapRes - 1, gridBounds[2]), maxZ = Mathf.Min(TerrainManager.HeightMapRes - 1, gridBounds[3]);
    int widthGrid = maxX - minX + 1, heightGrid = maxZ - minZ + 1;

    if (widthGrid <= 0 || heightGrid <= 0)
    { 
    
        Debug.LogWarning($"Invalid region size for tolerance check: width={widthGrid}, height={heightGrid}. " +
                         $"Unclamped gridBounds=[{gridBounds[0]},{gridBounds[1]},{gridBounds[2]},{gridBounds[3]}], " +
                         $"extents=({extents.x:F1},{extents.z:F1}), Radius={Radius:F1}, useBlendMap={useBlendMap}. " +
                         $"Position={position}, Rotation={rotation.eulerAngles}. Returning 0 tolerance.");
						 
	
        return 0;
    }

    // Update dimensions (only if valid)
    dimensions.IncludeRect(new RectInt(minX, minZ, widthGrid, heightGrid));

    // Step 3: Compare heightmaps at blend base coordinates
    float[,] heightMap = TerrainManager.GetHeightMap();
    Vector3 terrainPosition = TerrainManager.Land.transform.position;
    Vector3 terrainSize = TerrainManager.TerrainSize;
    Vector3 scale = localToWorld.lossyScale;

    float totalAbsDiff = 0f;
    int sampleCount = 0;

    foreach (Vector2 uv in blendBaseUVs)
    {
        // Convert UV to local position
        Vector3 localPos = new Vector3(
            uv.x * size.x - extents.x,
            0f,
            uv.y * size.z - extents.z
        );

        // Convert to world space
        Vector3 worldPos = localToWorld.MultiplyPoint3x4(localPos);

        // Sample monument height
        float monumentHeight = BitUtility.SampleHeightBilinear(heightTexture, uv.x, uv.y);
        float worldMonumentHeight = position.y + offset.y + monumentHeight * size.y * scale.y;

        // Sample terrain height
        float normX = (worldPos.x - terrainPosition.x) / terrainSize.x;
        float normZ = (worldPos.z - terrainPosition.z) / terrainSize.z;
        int x = Mathf.Clamp(Mathf.FloorToInt(normX * TerrainManager.HeightMapRes), 0, TerrainManager.HeightMapRes - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(normZ * TerrainManager.HeightMapRes), 0, TerrainManager.HeightMapRes - 1);
        float terrainHeight = heightMap[z, x] * terrainSize.y + terrainPosition.y;

        // Calculate absolute difference: |monument - terrain|
        float absDiff = Mathf.Abs(worldMonumentHeight - terrainHeight);
        totalAbsDiff += absDiff;
        sampleCount++;
    }

    // Step 4: Calculate tolerance (0-100)
    if (sampleCount == 0)
    {
        Debug.LogWarning("No valid samples for tolerance check. Returning 0 tolerance.");
        return 0;
    }

    float averageAbsDiff = totalAbsDiff / sampleCount;
    int tolerance = Mathf.RoundToInt(Mathf.Clamp((averageAbsDiff / 25f) * 100f, 0f, 100f));

    //Debug.Log($"Tolerance calculated: {tolerance} (based on {sampleCount} blend base samples, avg abs diff: {averageAbsDiff:F3} units)");
    return tolerance;
}

protected override void ApplyAlphaMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions)
{
    if (!AlphaMap) { Debug.Log("skip alpha"); return; }

    bool user = string.IsNullOrEmpty(topologymap.Guid);
    Texture2D alphaTexture = alphamap.GetResource();
    Texture2D blendTexture = blendmap.GetResource();
    if (alphaTexture == null)
    {
        Debug.LogWarning($"No alpha texture available for {this}.");
        return;
    }

    Vector3[] corners = GetWorldCorners(localToWorld);
    int[] gridBounds = TerrainManager.WorldCornersToGrid(corners[0], corners[1], corners[2], corners[3]);
    int minX = Mathf.Max(0, gridBounds[0]), minZ = Mathf.Max(0, gridBounds[1]);
    int maxX = Mathf.Min(TerrainManager.AlphaMapRes - 1, gridBounds[2]), maxZ = Mathf.Min(TerrainManager.AlphaMapRes - 1, gridBounds[3]);
    int width = maxX - minX + 1, height = maxZ - minZ + 1;

    if (width <= 0 || height <= 0) return;

    // Capture current alphamap state for undo
    bool[,] alphaMap = TerrainManager.GetAlphaMap();
    bool[,] regionAlphaBefore = new bool[height, width];
    for (int z = 0; z < height; z++)
    {
        for (int x = 0; x < width; x++)
        {
            regionAlphaBefore[z, x] = alphaMap[z + minZ, x + minX];
        }
    }

    float[,] regionAlpha = new float[height, width];
    float alphaValue = 0f;

    // FIXED: Center sampling to match Unity native (add +0.5f, like HeightMap)
    Vector3 terrainPosition = TerrainManager.Land.transform.position;  // Assume Land for alpha
    Vector3 terrainSize = TerrainManager.TerrainSize;
    for (int x = minX; x <= maxX; x++)
    {
        for (int z = minZ; z <= maxZ; z++)
        {
            float normX = ((float)x + 0.5f) / TerrainManager.AlphaMapRes;
            float normZ = ((float)z + 0.5f) / TerrainManager.AlphaMapRes;
            Vector3 worldPos = new Vector3(
                terrainPosition.x + normX * terrainSize.x,
                0f,
                terrainPosition.z + normZ * terrainSize.z
            );
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos) - offset;
            float u = Mathf.Clamp01((localPos.x + extents.x) / size.x);
            float v = Mathf.Clamp01((localPos.z + extents.z) / size.z);
            float blendAlpha = blendTexture != null ? blendTexture.GetPixelBilinear(u, v).a : 1f;  // Optional: Use for fade if needed

            alphaValue = user ? alphaTexture.GetPixelBilinear(u, v).r : alphaTexture.GetPixelBilinear(u, v).a;
            regionAlpha[z - minZ, x - minX] = alphaValue;  // Multiply by blendAlpha if fading
        }
    }

    TerrainManager.SetAlphaMapRegion(regionAlpha, minX, minZ, width, height);
    
    // Register undo state
    TerrainUndoManager.RegisterAlphaMapUndoRedo(
        "Monument AlphaMap",
        minX,
        minZ,
        width,
        height,
        regionAlphaBefore
    );
    dimensions.IncludeRect(new RectInt(minX, minZ, width, height));
}
	
	protected override void ApplyBiomeMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions)
{
    Texture2D biomeTexture1 = biomemap.GetResource();
    Texture2D blendTexture = blendmap?.cachedInstance ?? blendmap?.GetResource();

    bool useBlendMap = blendTexture != null;

    Vector3[] corners = GetWorldCorners(localToWorld);
    int[] gridBounds = TerrainManager.WorldCornersToGrid(corners[0], corners[1], corners[2], corners[3]);
    int minX = Mathf.Max(0, gridBounds[0]), minZ = Mathf.Max(0, gridBounds[1]);
    int maxX = Mathf.Min(TerrainManager.SplatMapRes - 1, gridBounds[2]), maxZ = Mathf.Min(TerrainManager.SplatMapRes - 1, gridBounds[3]);
    int width = maxX - minX + 1, height = maxZ - minZ + 1;

    if (width <= 0 || height <= 0) return;

    // Capture current biome map state for undo
    float[,,] biomeMap = TerrainManager.GetSplatMap(TerrainManager.LayerType.Biome);
    int biomeCount = TerrainManager.LayerCount(TerrainManager.LayerType.Biome);
    float[,,] regionBiomesBefore = new float[height, width, biomeCount];
    for (int z = 0; z < height; z++)
    {
        for (int x = 0; x < width; x++)
        {
            for (int k = 0; k < biomeCount; k++)
            {
                regionBiomesBefore[z, x, k] = biomeMap[z + minZ, x + minX, k];
            }
        }
    }



    float[,,] regionBiomes = new float[height, width, biomeCount];
    int biomeCountLocal = 5; // Arid, Temperate, Tundra, Arctic, Jungle

    Vector3 terrainPosition = TerrainManager.Land.transform.position;
    Vector3 terrainSize = TerrainManager.TerrainSize;
    for (int x = minX; x <= maxX; x++)
    {
        for (int z = minZ; z <= maxZ; z++)
        {
            float normX = ((float)x + 0.5f) / TerrainManager.SplatMapRes;
            float normZ = ((float)z + 0.5f) / TerrainManager.SplatMapRes;
            Vector3 worldPos = new Vector3(
                terrainPosition.x + normX * terrainSize.x,
                0f,
                terrainPosition.z + normZ * terrainSize.z
            );
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos) - offset;

            float u = Mathf.Clamp01((localPos.x + extents.x) / size.x);
            float v = Mathf.Clamp01((localPos.z + extents.z) / size.z);
            float fade = useBlendMap
                ? BitUtility.SampleAlphaBilinear(blendTexture, u, v)
                : Mathf.InverseLerp(Radius, Radius - Fade, localPos.Magnitude2D());

            Color32 biomeValue1 = biomeTexture1.GetPixelBilinear(u, v);

            float[] biomeValues = new float[biomeCountLocal];
            biomeValues[0] = ShouldBiome(1) ? (biomeValue1.r) : 0f; // Arid
            biomeValues[1] = ShouldBiome(2) ? (biomeValue1.g) : 0f; // Temperate
            biomeValues[2] = ShouldBiome(4) ? (biomeValue1.b) : 0f; // Tundra
            biomeValues[3] = ShouldBiome(8) ? (biomeValue1.a) : 0f; // Arctic
            biomeValues[4] = 0f; // Jungle

            float totalBiomeContribution = 0f;
            for (int i = 0; i < biomeCountLocal; i++)
            {
                totalBiomeContribution += biomeValues[i];
            }

            if (totalBiomeContribution > 0f)
            {
                for (int i = 0; i < biomeCountLocal; i++)
                {
                    biomeValues[i] /= totalBiomeContribution;
                }
            }

            float effectiveFade = fade * totalBiomeContribution;

            regionBiomes[z - minZ, x - minX, 0] = Mathf.Lerp(biomeMap[z, x, 0], biomeValues[0], effectiveFade); // Arid
            regionBiomes[z - minZ, x - minX, 1] = Mathf.Lerp(biomeMap[z, x, 1], biomeValues[1], effectiveFade); // Temperate
            regionBiomes[z - minZ, x - minX, 2] = Mathf.Lerp(biomeMap[z, x, 2], biomeValues[2], effectiveFade); // Tundra
            regionBiomes[z - minZ, x - minX, 3] = Mathf.Lerp(biomeMap[z, x, 3], biomeValues[3], effectiveFade); // Arctic
            regionBiomes[z - minZ, x - minX, 4] = Mathf.Lerp(biomeMap[z, x, 4], biomeValues[4], effectiveFade); // Jungle
        }
    }


    // Normalize biome values
    for (int x = 0; x < width; x++)
    {
        for (int z = 0; z < height; z++)
        {
            float sum = 0f;
            for (int i = 0; i < biomeCount; i++)
            {
                sum += regionBiomes[z, x, i];
            }
            if (sum > 0f)
            {
                for (int i = 0; i < biomeCount; i++)
                {
                    regionBiomes[z, x, i] /= sum;
                }
            }
        }
    }

    TerrainManager.SetBiomeMapRegion(regionBiomes, minX, minZ, width, height);
	// Register undo state
    TerrainUndoManager.RegisterBiomeMapUndoRedo(
        "Monument Biome",
        minX,
        minZ,
        width,
        height,
        regionBiomesBefore
    );
    dimensions.IncludeRect(new RectInt(minX, minZ, width, height));
}

protected override void ApplySplatMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions)
{
    if ((splatmap0 == null) && (splatmap1 == null)) return;

    bool user = string.IsNullOrEmpty(topologymap.Guid);
    Texture2D splat0Texture = splatmap0.GetResource();
    Texture2D splat1Texture = splatmap1.GetResource();
    Texture2D blendTexture = blendmap.GetResource();

    if (splat0Texture == null && splat1Texture == null)
    {
        Debug.LogWarning($"No splat textures available for {this}.");
        return;
    }

    bool useBlendMap = blendTexture != null;

    Vector3[] corners = GetWorldCorners(localToWorld);
    int[] gridBounds = TerrainManager.WorldCornersToGrid(corners[0], corners[1], corners[2], corners[3]);
    int minX = Mathf.Max(0, gridBounds[0]), minZ = Mathf.Max(0, gridBounds[1]);
    int maxX = Mathf.Min(TerrainManager.SplatMapRes - 1, gridBounds[2]), maxZ = Mathf.Min(TerrainManager.SplatMapRes - 1, gridBounds[3]);
    int width = maxX - minX + 1, height = maxZ - minZ + 1;

    if (width <= 0 || height <= 0) return;

    // Capture current splatmap state for undo
    float[,,] splatMap = TerrainManager.GetSplatMap(TerrainManager.LayerType.Ground);
    int layerCount = TerrainManager.LayerCount(TerrainManager.LayerType.Ground);
    float[,,] regionSplatsBefore = new float[height, width, layerCount];
    for (int z = 0; z < height; z++)
    {
        for (int x = 0; x < width; x++)
        {
            for (int k = 0; k < layerCount; k++)
            {
                regionSplatsBefore[z, x, k] = splatMap[z + minZ, x + minX, k];
            }
        }
    }



    float[,,] regionSplats = new float[height, width, layerCount];
    Debug.Log($"Splat Layer Count: {layerCount}");

    // FIXED: Center sampling (add +0.5f to match Alpha/Height)
    Vector3 terrainPosition = TerrainManager.Land.transform.position;
    Vector3 terrainSize = TerrainManager.TerrainSize;
    for (int x = minX; x <= maxX; x++)
    {
        for (int z = minZ; z <= maxZ; z++)
        {
            float normX = ((float)x + 0.5f) / TerrainManager.SplatMapRes;
            float normZ = ((float)z + 0.5f) / TerrainManager.SplatMapRes;
            Vector3 worldPos = new Vector3(
                terrainPosition.x + normX * terrainSize.x,
                0f,
                terrainPosition.z + normZ * terrainSize.z
            );
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos) - offset;

            float u = Mathf.Clamp01((localPos.x + extents.x) / size.x);
            float v = Mathf.Clamp01((localPos.z + extents.z) / size.z);
            float fade = useBlendMap
                ? BitUtility.SampleAlphaBilinear(blendTexture, u, v)
                : Mathf.InverseLerp(Radius, Radius - Fade, localPos.Magnitude2D());

            Vector4 splat0 = splat0Texture != null ? splat0Texture.GetPixelBilinear(u, v) : Vector4.zero;
            Vector4 splat1 = splat1Texture != null ? splat1Texture.GetPixelBilinear(u, v) : Vector4.zero;

            float[] splatValues = new float[8];
            splatValues[0] = ShouldSplat(1) ? splat0.x : 0f;   // Dirt
            splatValues[1] = ShouldSplat(2) ? splat0.y : 0f;   // Snow
            splatValues[2] = ShouldSplat(4) ? splat0.z : 0f;   // Sand
            splatValues[3] = ShouldSplat(8) ? splat0.w : 0f;   // Rock
            splatValues[4] = ShouldSplat(16) ? splat1.x : 0f;  // Grass
            splatValues[5] = ShouldSplat(32) ? splat1.y : 0f;  // Forest
            splatValues[6] = ShouldSplat(64) ? splat1.z : 0f;  // Stones
            splatValues[7] = ShouldSplat(128) ? splat1.w : 0f; // Gravel

            float totalSplatContribution = 0f;
            for (int i = 0; i < 8; i++) totalSplatContribution += splatValues[i];

            if (totalSplatContribution > 1f && fade > 0f)
            {
                for (int i = 0; i < 8; i++) splatValues[i] /= totalSplatContribution;
            }

            float effectiveFade = fade * totalSplatContribution;

            for (int k = 0; k < Mathf.Min(layerCount, 8); k++)
            {
                regionSplats[z - minZ, x - minX, k] = Mathf.Lerp(splatMap[z, x, k], splatValues[k], effectiveFade);
            }
        }
    }

    // Normalize splat values
    for (int x = 0; x < width; x++)
    {
        for (int z = 0; z < height; z++)
        {
            float sum = 0f;
            for (int k = 0; k < layerCount; k++)
            {
                sum += regionSplats[z, x, k];
            }
            if (sum > 0f)
            {
                for (int k = 0; k < layerCount; k++)
                {
                    regionSplats[z, x, k] /= sum;
                }
            }
        }
    }

    TerrainManager.SetSplatMapRegion(regionSplats, TerrainManager.LayerType.Ground, minX, minZ, width, height);
	    // Register undo state
    TerrainUndoManager.RegisterSplatMapUndoRedo(
        "Monument Splat",
        minX,
        minZ,
        width,
        height,
        regionSplatsBefore
    );
    dimensions.IncludeRect(new RectInt(minX, minZ, width, height));
}
protected override void ApplyTopologyMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions)
{
    bool user = string.IsNullOrEmpty(topologymap.Guid);
    Texture2D topologyTexture = topologymap.GetResource();
    Texture2D blendTexture = blendmap?.GetResource();
    bool useBlendMap = blendTexture != null;

    if (topologyTexture == null)
    {
        Debug.LogWarning($"No topology texture available for {this}.");
        return;
    }

    Vector3[] corners = GetWorldCorners(localToWorld);
    int[] gridBounds = TerrainManager.WorldCornersToGrid(corners[0], corners[1], corners[2], corners[3]);
    int minX = Mathf.Max(0, gridBounds[0]), minZ = Mathf.Max(0, gridBounds[1]);
    int maxX = Mathf.Min(TerrainManager.AlphaMapRes - 1, gridBounds[2]), maxZ = Mathf.Min(TerrainManager.AlphaMapRes - 1, gridBounds[3]);
    int width = maxX - minX + 1, height = maxZ - minZ + 1;

    if (width <= 0 || height <= 0)
    {
        Debug.LogWarning($"Invalid region size for topology map: width={width}, height={height}. Skipping application.");
        return;
    }

    int mask = (int)TopologyMask;
    if (mask == 0)
    {
        Debug.LogWarning("TopologyMask is empty. No layers to apply.");
        return;
    }

    // Capture undo state (before modification)
    TerrainMap<int> topologyMap = TopologyData.GetTerrainMap();
    int[,] undoBitmaskData = new int[height, width];
    for (int i = 0; i < height; i++)
    {
        for (int j = 0; j < width; j++)
        {
            if (minX + j < topologyMap.res && minZ + i < topologyMap.res)
            {
                undoBitmaskData[i, j] = topologyMap[minZ + i, minX + j] & mask;
            }
        }
    }

    // Capture topology state for each active layer
    Dictionary<int, bool[,]> layerBitmaps = new Dictionary<int, bool[,]>();
    List<int> activeLayers = new List<int>();
    for (int layerIndex = 0; layerIndex < TerrainTopology.COUNT; layerIndex++)
    {
        int layer = TerrainTopology.IndexToType(layerIndex);
        if ((mask & layer) != 0)
        {
            layerBitmaps[layer] = new bool[height, width];
            activeLayers.Add(layer);
        }
    }

    if (activeLayers.Count == 0)
    {
        Debug.LogWarning("No valid layers found in TopologyMask.");
        return;
    }

    // FIXED: Center sampling (add +0.5f to match Alpha/Height)
    Vector3 terrainPosition = TerrainManager.Land.transform.position;
    Vector3 terrainSize = TerrainManager.TerrainSize;
    for (int x = minX; x <= maxX; x++)
    {
        for (int z = minZ; z <= maxZ; z++)
        {
            float normX = ((float)x + 0.5f) / TerrainManager.AlphaMapRes;
            float normZ = ((float)z + 0.5f) / TerrainManager.AlphaMapRes;
            Vector3 worldPos = new Vector3(
                terrainPosition.x + normX * terrainSize.x,
                0f,
                terrainPosition.z + normZ * terrainSize.z
            );
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos) - offset;

            float u = Mathf.Clamp01((localPos.x + extents.x) / size.x);
            float v = Mathf.Clamp01((localPos.z + extents.z) / size.z);

            float fade = useBlendMap
                ? BitUtility.SampleAlphaBilinear(blendTexture, u, v)
                : Mathf.InverseLerp(Radius, Radius - Fade, localPos.Magnitude2D());

            if (fade <= 0.5f)
            {
                continue;
            }

            float pixelX = u * (float)(topologyTexture.width - 1);
            float pixelY = v * (float)(topologyTexture.height - 1);
            int px = Mathf.Clamp(Mathf.RoundToInt(pixelX), 1, topologyTexture.width - 2);
            int py = Mathf.Clamp(Mathf.RoundToInt(pixelY), 1, topologyTexture.height - 2);

            Color32 topologySample = topologyTexture.GetPixelData<Color32>(0)[py * topologyTexture.width + px];
            int topologyValue = user ? BitUtility.DecodeUserInt(topologySample) : BitUtility.DecodeInt(topologySample);

            foreach (int layer in activeLayers)
            {
                layerBitmaps[layer][z - minZ, x - minX] = (topologyValue & layer) != 0;
            }

            dimensions.IncludeRect(new RectInt(x, z, 1, 1));
        }
    }

    // Apply topology changes
    foreach (int layer in activeLayers)
    {
        Debug.Log($"Applying topology layer: {(TerrainTopology.Enum)layer} ({layer}) at region ({minX}, {minZ}, {width}, {height})");
        TopologyData.AddTopology(layer, minX, minZ, width, height, layerBitmaps[layer]);
    }

    // Register undo/redo action
    TerrainUndoManager.RegisterTopologyMaskUndoRedo(
        "Monument Topology",
        minX,
        minZ,
        width,
        height,
        mask,
		undoBitmaskData
    );

    Debug.Log("Topology region calculated for layers in TopologyMask");
    TopologyData.UpdateTexture();
}

protected override void ApplyWaterMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions)
{
    if (!WaterMap) { Debug.Log("skip water"); return; }

    bool user = string.IsNullOrEmpty(topologymap.Guid);
    if (user) return;

    Texture2D waterTexture = watermap.GetResource();
    if (waterTexture == null)
    {
        Debug.LogWarning($"No water heightmap texture available for {this}.");
        return;
    }

    Texture2D blendTexture = blendmap.GetResource();
    bool useBlendMap = blendTexture != null;

    float radius = Radius == 0f ? extents.x : Radius;
    Vector3 position = localToWorld.MultiplyPoint3x4(Vector3.zero);

    Quaternion rotation = localToWorld.rotation;
    Vector3 localXAxis = rotation * Vector3.right;
    Vector3 localZAxis = rotation * Vector3.forward;

    float extentX = Mathf.Abs(Vector3.Dot(new Vector3(radius, 0f, 0f), localXAxis)) +
                    Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radius), localXAxis));
    float extentZ = Mathf.Abs(Vector3.Dot(new Vector3(radius, 0f, 0f), localZAxis)) +
                    Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radius), localZAxis));

    Vector3[] corners = new Vector3[]
    {
        position + new Vector3(-extentX, 0f, -extentZ),
        position + new Vector3(extentX, 0f, -extentZ),
        position + new Vector3(-extentX, 0f, extentZ),
        position + new Vector3(extentX, 0f, extentZ)
    };

    int[] gridBounds = TerrainManager.WorldCornersToGrid(corners[0], corners[1], corners[2], corners[3]);
    int minX = Mathf.Max(0, gridBounds[0]), minZ = Mathf.Max(0, gridBounds[1]);
    int maxX = Mathf.Min(TerrainManager.HeightMapRes - 1, gridBounds[2]), maxZ = Mathf.Min(TerrainManager.HeightMapRes - 1, gridBounds[3]);
    int width = maxX - minX + 1, height = maxZ - minZ + 1;

    if (width <= 0 || height <= 0)
    {
        Debug.LogWarning($"Invalid region size for water map: width={width}, height={height}. Skipping application.");
        return;
    }

    // Capture current water heightmap state for undo
    float[,] waterMapData = TerrainManager.GetWaterHeightMap();
    float[,] regionWaterBefore = new float[height, width];
    for (int z = 0; z < height; z++)
    {
        for (int x = 0; x < width; x++)
        {
            regionWaterBefore[z, x] = waterMapData[z + minZ, x + minX];
        }
    }

    float[,] regionWater = new float[height, width];
    int modifiedPixels = 0;

    Vector3 terrainPosition = TerrainManager.Water.transform.position;
    Vector3 terrainSize = TerrainManager.TerrainSize;
    Vector3 rcpSize = new Vector3(1f / terrainSize.x, 1f / terrainSize.y, 1f / terrainSize.z);

    for (int x = minX; x <= maxX; x++)
    {
        for (int z = minZ; z <= maxZ; z++)
        {
            float normX = ((float)x + 0.5f) / TerrainManager.HeightMapRes;
            float normZ = ((float)z + 0.5f) / TerrainManager.HeightMapRes;

            Vector3 worldPos = new Vector3(
                terrainPosition.x + normX * terrainSize.x,
                0f,
                terrainPosition.z + normZ * terrainSize.z
            );
            Vector3 localPos = worldToLocal.MultiplyPoint3x4(worldPos) - offset;

            float u = Mathf.Clamp01((localPos.x + extents.x) / size.x);
            float v = Mathf.Clamp01((localPos.z + extents.z) / size.z);

            float combinedHeight = BitUtility.SampleHeightBilinear(waterTexture, u, v);
            float worldHeight = position.y + offset.y + combinedHeight * size.y;
            float normalizedHeight = (worldHeight - terrainPosition.y) * rcpSize.y;

            float fade = useBlendMap ? (blendTexture?.GetPixelBilinear(u, v).a ?? 0f) : 1f;
            if (fade <= 0f)
            {
                regionWater[z - minZ, x - minX] = waterMapData[z, x];
                continue;
            }

            regionWater[z - minZ, x - minX] = Mathf.SmoothStep(waterMapData[z, x], normalizedHeight, fade);
            if (normalizedHeight > 0f) modifiedPixels++;
            dimensions.IncludeRect(new RectInt(x, z, 1, 1));
        }
    }

    TerrainManager.SetHeightMapRegion(regionWater, minX, minZ, width, height, TerrainManager.TerrainType.Water);
    dimensions.IncludeRect(new RectInt(minX, minZ, width, height));
	
	    // Register undo state
    TerrainUndoManager.RegisterHeightMapUndoRedo(
        "Apply WaterMap",
        TerrainManager.TerrainType.Water,
        minX,
        minZ,
        width,
        height,
		regionWaterBefore
    );

    Debug.Log($"ApplyWaterMap: Applied {modifiedPixels} non-zero height pixels for water map in region ({minX}, {minZ}, {width}, {height}).");
}

	private Vector3[] GetWorldCorners(Matrix4x4 localToWorld)
	{
		float radius = Radius == 0f ? extents.x : Radius;
		bool useBlendMap = blendmap != null;
		float radiusX = useBlendMap ? extents.x : radius;
		float radiusZ = useBlendMap ? extents.z : radius;
		Vector3 position = localToWorld.MultiplyPoint3x4(Vector3.zero);

		Quaternion rotation = localToWorld.rotation;
		Vector3 localXAxis = rotation * Vector3.right;
		Vector3 localZAxis = rotation * Vector3.forward;

		float extentX = Mathf.Abs(Vector3.Dot(new Vector3(radiusX, 0f, 0f), localXAxis)) +
						Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radiusZ), localXAxis));
		float extentZ = Mathf.Abs(Vector3.Dot(new Vector3(radiusX, 0f, 0f), localZAxis)) +
						Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, radiusZ), localZAxis));

		return new Vector3[]
		{
			position + new Vector3(-extentX, 0f, -extentZ),
			position + new Vector3(extentX, 0f, -extentZ),
			position + new Vector3(-extentX, 0f, extentZ),
			position + new Vector3(extentX, 0f, extentZ)
		};
	}

    private void GenerateCliffSplat(Vector3 worldPos, float[,,] splatMap, int z, int x)
    {
    }

    private void GenerateCliffTopology(Vector3 worldPos, float[,,] topologyMap, int z, int x, int layer)
    {
    }
	

	
}

public static class Vector3Extensions
{
    public static float Magnitude2D(this Vector3 v)
    {
        return Mathf.Sqrt(v.x * v.x + v.z * v.z);
    }
}