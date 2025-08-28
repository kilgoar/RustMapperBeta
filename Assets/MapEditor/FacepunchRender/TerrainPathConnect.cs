using System;
using UnityEngine;

// Token: 0x02000C46 RID: 3142
public class TerrainPathConnect : MonoBehaviour
{
	// Token: 0x06005544 RID: 21828 RVA: 0x001D5B09 File Offset: 0x001D3D09
	public PathManager.Point GetPathFinderPoint(int res)
	{
		return GetPoint(base.transform.position, res);
	}
	

	public static PathManager.Point GetPoint(Vector3 worldPos, int res)
	{
		Vector3 normalized = TerrainManager.WorldToTerrainUV(worldPos);
		return new PathManager.Point
		{
			x = Mathf.Clamp((int)(normalized.x * (float)res), 0, res - 1),
			y = Mathf.Clamp((int)(normalized.z * (float)res), 0, res - 1)
		};
	}


	// Token: 0x0400483C RID: 18492
	public InfrastructureType Type;
}