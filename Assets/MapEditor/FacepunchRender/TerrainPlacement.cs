using System;
using UnityEngine;

public abstract class TerrainPlacement : PrefabAttribute
{
	
	private Matrix4x4 gizmoLocalToWorld; // Store transformation for gizmo
    private TerrainBounds gizmoDimensions; // Store bounds for gizmo
    private bool shouldDrawGizmo = false; // Flag to enable gizmo drawing

    private void OnDrawGizmos()
    {
        if (!shouldDrawGizmo || gizmoDimensions == null) return;

        // Set gizmo transformation
        Gizmos.matrix = gizmoLocalToWorld;

        // Calculate center and size from gizmoDimensions
        Vector3 center = new Vector3(
            (gizmoDimensions.xMin ) * 0.5f, // X center
            0,                                                   // Y center (base of terrain)
            (gizmoDimensions.yMin ) * 0.5f  // Z center
        );
        Vector3 size = new Vector3(
            gizmoDimensions.xMax - gizmoDimensions.xMin, // Width (X)
            1,                                          // Height (Y, default value)
            gizmoDimensions.yMax - gizmoDimensions.yMin  // Depth (Z)
        );

        // Draw gizmo cube
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);
    }
	
    public void ApplyHeight(Vector3 position, Quaternion rotation, Vector3 scale, TerrainBounds dimensions)
    {
        //if (!ShouldHeight()) return;
		
		Matrix4x4 localToWorld = Matrix4x4.TRS(position, rotation, scale);
        Matrix4x4 worldToLocal = localToWorld.inverse;
		
        // Store for gizmo drawing
        gizmoLocalToWorld = localToWorld;
        gizmoDimensions = dimensions;
        shouldDrawGizmo = true;
		
        ApplyHeightMap(localToWorld, worldToLocal, dimensions);
    }

    public void ApplySplat(Vector3 position, Quaternion rotation, Vector3 scale, TerrainBounds dimensions)
    {
        //if (!ShouldSplat(-1)) return;
        Matrix4x4 localToWorld = Matrix4x4.TRS(position, rotation, scale);
        Matrix4x4 worldToLocal = localToWorld.inverse;
        ApplySplatMap(localToWorld, worldToLocal, dimensions);
    }

    public void ApplyAlpha(Vector3 position, Quaternion rotation, Vector3 scale, TerrainBounds dimensions)
    {
        //if (!ShouldAlpha()) return;
        Matrix4x4 localToWorld = Matrix4x4.TRS(position, rotation, scale);
        Matrix4x4 worldToLocal = localToWorld.inverse;
        ApplyAlphaMap(localToWorld, worldToLocal, dimensions);
    }

    public void ApplyBiome(Vector3 position, Quaternion rotation, Vector3 scale, TerrainBounds dimensions)
    {
        //if (!ShouldBiome(-1)) return;
        Matrix4x4 localToWorld = Matrix4x4.TRS(position, rotation, scale);
        Matrix4x4 worldToLocal = localToWorld.inverse;
        ApplyBiomeMap(localToWorld, worldToLocal, dimensions);
    }

    public void ApplyTopology(Vector3 position, Quaternion rotation, Vector3 scale, TerrainBounds dimensions)
    {
        //if (!ShouldTopology(-1)) return;
        Matrix4x4 localToWorld = Matrix4x4.TRS(position, rotation, scale);
        Matrix4x4 worldToLocal = localToWorld.inverse;
        ApplyTopologyMap(localToWorld, worldToLocal, dimensions);
    }
	
	public void ApplyWater(Vector3 position, Quaternion rotation, Vector3 scale, TerrainBounds dimensions)
    {
        //if (!ShouldTopology(-1)) return;
        Matrix4x4 localToWorld = Matrix4x4.TRS(position, rotation, scale);
        Matrix4x4 worldToLocal = localToWorld.inverse;
        ApplyWaterMap(localToWorld, worldToLocal, dimensions);
    }

    public void Apply(Matrix4x4 localToWorld, Matrix4x4 worldToLocal)
    {
        TerrainBounds dimensions = new TerrainBounds();
        if (ShouldHeight()) ApplyHeightMap(localToWorld, worldToLocal, dimensions);
        if (ShouldSplat(-1)) ApplySplatMap(localToWorld, worldToLocal, dimensions);
        if (ShouldAlpha()) ApplyAlphaMap(localToWorld, worldToLocal, dimensions);
        if (ShouldBiome(-1)) ApplyBiomeMap(localToWorld, worldToLocal, dimensions);
        if (ShouldTopology(-1)) ApplyTopologyMap(localToWorld, worldToLocal, dimensions);
        if (ShouldWater()) ApplyWaterMap(localToWorld, worldToLocal, dimensions);
    }

    protected abstract void ApplyAlphaMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions);
    protected abstract void ApplyBiomeMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions);
    protected abstract void ApplyHeightMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions);
    protected abstract void ApplySplatMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions);
    protected abstract void ApplyTopologyMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions);
    protected abstract void ApplyWaterMap(Matrix4x4 localToWorld, Matrix4x4 worldToLocal, TerrainBounds dimensions);

    protected override Type GetPrefabAttributeType()
    {
        return typeof(TerrainPlacement);
    }

public virtual bool ShouldAlpha()
{
    bool hasAlphaMap = alphamap != null;
    bool shouldUseAlpha = hasAlphaMap && AlphaMap;

    Debug.Log($"ShouldAlpha: alphamap exists: {hasAlphaMap}, AlphaMap enabled: {AlphaMap}, Result: {shouldUseAlpha}");

    return shouldUseAlpha;
}

public virtual bool ShouldBiome(int mask = -1)
{
    bool hasBiomeMap = biomemap != null;
    bool biomeMaskMatch = (BiomeMask & (TerrainBiome.Enum)mask) > (TerrainBiome.Enum)0;
    bool shouldUseBiome = hasBiomeMap && biomeMaskMatch;

    Debug.Log($"ShouldBiome: biomemap exists: {hasBiomeMap}, BiomeMask match: {biomeMaskMatch} (mask: {mask}), Result: {shouldUseBiome}");

    return shouldUseBiome;
}

public virtual bool ShouldHeight()
{
    bool hasHeightMap = heightmap != null;
    bool shouldUseHeight = hasHeightMap && HeightMap;

    Debug.Log($"ShouldHeight: heightmap exists: {hasHeightMap}, HeightMap enabled: {HeightMap}, Result: {shouldUseHeight}");

    return shouldUseHeight;
}

public virtual bool ShouldSplat(int mask = -1)
{
    bool hasSplatMap0 = splatmap0 != null;
    bool hasSplatMap1 = splatmap1 != null;
    bool splatMaskMatch = (SplatMask & (TerrainSplat.Enum)mask) > (TerrainSplat.Enum)0;
    bool shouldUseSplat = hasSplatMap1 && hasSplatMap0 && splatMaskMatch;

    //Debug.Log($"ShouldSplat: splatmap0 exists: {hasSplatMap0}, splatmap1 exists: {hasSplatMap1}, SplatMask match: {splatMaskMatch} (mask: {mask}), Result: {shouldUseSplat}");

    return shouldUseSplat;
}

public virtual bool ShouldTopology(int mask = -1)
{
    bool hasTopologyMap = topologymap != null;
    bool topologyMaskMatch = (TopologyMask & (TerrainTopology.Enum)mask) > (TerrainTopology.Enum)0;
    bool shouldUseTopology = hasTopologyMap && topologyMaskMatch;

    Debug.Log($"ShouldTopology: topologymap exists: {hasTopologyMap}, TopologyMask match: {topologyMaskMatch} (mask: {mask}), Result: {shouldUseTopology}");

    return shouldUseTopology;
}

public virtual bool ShouldWater()
{
    bool hasWaterMap = watermap != null;
    bool shouldUseWater = hasWaterMap && WaterMap;

    Debug.Log($"ShouldWater: watermap exists: {hasWaterMap},WaterMap enabled: {WaterMap}, Result: {shouldUseWater}");

    return shouldUseWater;
}

    public Vector3 size = Vector3.zero;
    public Vector3 extents = Vector3.zero;
    public Vector3 offset = Vector3.zero;
    public bool HeightMap = true;
    public bool AlphaMap = true;
    public bool WaterMap;
	
    public TerrainSplat.Enum SplatMask;
    public TerrainBiome.Enum BiomeMask;
    public TerrainTopology.Enum TopologyMask;
	
    public Texture2DRef heightmap;
    public Texture2DRef splatmap0;
    public Texture2DRef splatmap1;
    public Texture2DRef alphamap;
    public Texture2DRef biomemap;
    public Texture2DRef topologymap;
    public Texture2DRef watermap;
    public Texture2DRef blendmap;
}