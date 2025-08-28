/*using System;
using UnityEngine;
using static TerrainManager; // For TerrainManager

// Token: 0x02000C0B RID: 3083
[Serializable]
public class SpawnFilter
{
    // Token: 0x06005454 RID: 21588 RVA: 0x001D17DE File Offset: 0x001CF9DE
    public bool Test(Vector3 worldPos)
    {
        return this.GetFactor(worldPos, true, 0f) > 0.5f;
    }

    // Token: 0x06005455 RID: 21589 RVA: 0x001D17F4 File Offset: 0x001CF9F4
    public bool Test(float normX, float normZ)
    {
        return this.GetFactor(normX, normZ, true, 0f) > 0.5f;
    }

    // Token: 0x06005456 RID: 21590 RVA: 0x001D180C File Offset: 0x001CFA0C
    public float GetFactor(Vector3 worldPos, bool checkPlacementMap = true, float checkTopologyRadius = 0f)
    {
        Vector3 uv = TerrainManager.WorldToTerrainUV(worldPos);
        return this.GetFactor(uv.x, uv.z, checkPlacementMap, checkTopologyRadius);
    }

    // Token: 0x06005457 RID: 21591 RVA: 0x001D183C File Offset: 0x001CFA3C
    public float GetFactor(float normX, float normZ, bool checkPlacementMap = true, float checkTopologyRadius = 0f)
    {
        int splatType = (int)this.SplatType;
        int biomeType = (int)this.BiomeType;
        int topologyAny = (int)this.TopologyAny;
        int topologyAll = (int)this.TopologyAll;
        int topologyNot = (int)this.TopologyNot;

        // Topology checks
        if (topologyAny == 0)
        {
            Debug.LogError("Empty topology filter is invalid.");
        }
        else if (topologyAny != -1 || topologyAll != 0 || topologyNot != 0)
        {
            int gridX = Mathf.FloorToInt(normX * TerrainManager.SplatMapRes);
            int gridZ = Mathf.FloorToInt(normZ * TerrainManager.SplatMapRes);
            gridX = Mathf.Clamp(gridX, 0, TerrainManager.SplatMapRes - 1);
            gridZ = Mathf.Clamp(gridZ, 0, TerrainManager.SplatMapRes - 1);

            int topology = 0;
            if (checkTopologyRadius > 0f)
            {
                int gridRadius = Mathf.CeilToInt(checkTopologyRadius / TerrainManager.SplatSize);
                bool[,] topologyMap = TerrainManager.GetTopologyBitview(0, gridX - gridRadius, gridZ - gridRadius, gridRadius * 2 + 1, gridRadius * 2 + 1);
                for (int tz = 0; tz < topologyMap.GetLength(0); tz++)
                {
                    for (int tx = 0; tx < topologyMap.GetLength(1); tx++)
                    {
                        if (topologyMap[tx, tz])
                            topology |= TerrainTopology.IndexToType(tx + gridX - gridRadius);
                    }
                }
            }
            else
            {
                bool[,] topologyMap = TerrainManager.GetTopologyBitview(0, gridX, gridZ, 1, 1);
                if (topologyMap[0, 0])
                    topology = TerrainTopology.IndexToType(gridX); // Adjust topology index as needed
            }

            if (topologyAny != -1 && (topology & topologyAny) == 0)
            {
                return 0f;
            }
            if (topologyNot != 0 && (topology & topologyNot) != 0)
            {
                return 0f;
            }
            if (topologyAll != 0 && (topology & topologyAll) != topologyAll)
            {
                return 0f;
            }
        }

        // Biome checks
        if (biomeType == 0)
        {
            Debug.LogError("Empty biome filter is invalid.");
        }
        else if (biomeType != -1)
        {
            float[,] biomeMap = TerrainManager.GetBiomeMap();
            int gridX = Mathf.FloorToInt(normX * TerrainManager.SplatMapRes);
            int gridZ = Mathf.FloorToInt(normZ * TerrainManager.SplatMapRes);
            gridX = Mathf.Clamp(gridX, 0, TerrainManager.SplatMapRes - 1);
            gridZ = Mathf.Clamp(gridZ, 0, TerrainManager.SplatMapRes - 1);
            if ((Mathf.RoundToInt(biomeMap[gridZ, gridX]) & biomeType) == 0)
            {
                return 0f;
            }
        }

        // Splat checks
        if (splatType == 0)
        {
            Debug.LogError("Empty splat filter is invalid.");
        }
        else if (splatType != -1)
        {
            float[,,] splatMap = TerrainManager.GetSplatMap(TerrainManager.LayerType.Ground);
            int gridX = Mathf.FloorToInt(normX * TerrainManager.SplatMapRes);
            int gridZ = Mathf.FloorToInt(normZ * TerrainManager.SplatMapRes);
            gridX = Mathf.Clamp(gridX, 0, TerrainManager.SplatMapRes - 1);
            gridZ = Mathf.Clamp(gridZ, 0, TerrainManager.SplatMapRes - 1);
            return splatMap[gridZ, gridX, TerrainSplat.TypeToIndex((TerrainSplat.Enum)splatType)];
        }

        return 1f;
    }

    // Token: 0x04004726 RID: 18214

    public TerrainSplat.Enum SplatType = (TerrainSplat.Enum)(-1);

    // Token: 0x04004727 RID: 18215

    public TerrainBiome.Enum BiomeType = (TerrainBiome.Enum)(-1);

    // Token: 0x04004728 RID: 18216

    public TerrainTopology.Enum TopologyAny = (TerrainTopology.Enum)(-1);

    // Token: 0x04004729 RID: 18217

    public TerrainTopology.Enum TopologyAll;

    // Token: 0x0400472A RID: 18218

    public TerrainTopology.Enum TopologyNot;
}
*/