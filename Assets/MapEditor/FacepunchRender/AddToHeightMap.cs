using System;
using UnityEngine;

public class AddToHeightMap : MonoBehaviour
{
    public void Apply()
    {
        Collider component = GetComponent<Collider>();
        if (component == null)
        {
            Debug.LogError("No Collider component found on this GameObject.");
            return;
        }

        if (TerrainManager.Land == null || TerrainManager.Land.transform == null)
        {
            Debug.LogError("TerrainManager.Land or its transform is null.");
            return;
        }

        Bounds bounds = component.bounds;
        Vector3 terrainPosition = TerrainManager.Land.transform.position;
        Vector3 terrainSize = TerrainManager.TerrainSize;

        Debug.Log($"Bounds: min={bounds.min}, max={bounds.max}");
        Debug.Log($"Terrain Position: {terrainPosition}, Terrain Size: {terrainSize}, HeightMap Resolution: {TerrainManager.HeightMapRes}");

        int xMin = Mathf.FloorToInt(((bounds.min.x - terrainPosition.x ) / terrainSize.x) * TerrainManager.HeightMapRes);
        int xMax = Mathf.FloorToInt(((bounds.max.x - terrainPosition.x  ) / terrainSize.x) * TerrainManager.HeightMapRes);
        int zMin = Mathf.FloorToInt(((bounds.min.z - terrainPosition.z ) / terrainSize.z) * TerrainManager.HeightMapRes);
        int zMax = Mathf.FloorToInt(((bounds.max.z - terrainPosition.z ) / terrainSize.z) * TerrainManager.HeightMapRes);

        xMin = Mathf.Clamp(xMin, 0, TerrainManager.HeightMapRes - 1);
        xMax = Mathf.Clamp(xMax, 0, TerrainManager.HeightMapRes - 1);
        zMin = Mathf.Clamp(zMin, 0, TerrainManager.HeightMapRes - 1);
        zMax = Mathf.Clamp(zMax, 0, TerrainManager.HeightMapRes - 1);

        Debug.Log($"Clamped Indices: xMin={xMin}, xMax={xMax}, zMin={zMin}, zMax={zMax}");

        int width = xMax - xMin + 1;
        int height = zMax - zMin + 1;
        float[,] regionHeights = new float[height, width];
        bool hasChanges = false;

        float[,] currentHeightMap = TerrainManager.GetHeightMap();

        for (int i = zMin; i <= zMax; i++)
        {
            float normZ = ((float)i) / TerrainManager.HeightMapRes;

            for (int j = xMin; j <= xMax; j++)
            {
                float normX = ((float)j) / TerrainManager.HeightMapRes;

                Vector3 origin = new Vector3(
                    terrainPosition.x + normX * terrainSize.x,
                    bounds.max.y,
                    terrainPosition.z + normZ * terrainSize.z
                );

                Ray ray = new Ray(origin, Vector3.down);
                RaycastHit raycastHit;

                // --- Start of Debug Ray Addition ---
                // Draw the ray in the Scene view for debugging (green if it hits, red if it misses)
                if (component.Raycast(ray, out raycastHit, bounds.size.y))
                {
                    // Draw green ray from origin to the hit point
                    Debug.DrawRay(origin, Vector3.down * raycastHit.distance, Color.green, 5f);
                    float newHeight = (raycastHit.point.y - terrainPosition.y) / terrainSize.y;
                    float currentHeight = currentHeightMap[i, j];

                    if (newHeight > currentHeight)
                    {
                        regionHeights[i - zMin, j - xMin] = newHeight;
                        hasChanges = true;
                    }
                    else
                    {
                        regionHeights[i - zMin, j - xMin] = currentHeight;
                    }
                }
                else
                {
                    // Draw red ray for the full distance if no hit
                    Debug.DrawRay(origin, Vector3.down * bounds.size.y, Color.red, 5f);
                    regionHeights[i - zMin, j - xMin] = currentHeightMap[i, j];
                }
                // --- End of Debug Ray Addition ---
            }
        }

        if (hasChanges)
        {
            try
            {
                TerrainManager.SetHeightMapRegion(regionHeights, xMin, zMin, width, height);
                Debug.Log($"Updated heightmap region: x={xMin}, z={zMin}, width={width}, height={height}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to update heightmap region: {ex.Message}");
            }
        }
        else
        {
            Debug.Log("No heightmap changes needed.");
        }

        Debug.Log("Heightmap application completed.");
    }
}