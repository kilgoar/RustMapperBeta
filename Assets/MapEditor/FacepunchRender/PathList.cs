/*using System;
using System.Collections.Generic;
using UnityEngine;
using static PrefabManager;
using static WorldSerialization;

// Token: 0x02000BF7 RID: 3063
public class PathList
{
	// Token: 0x06005420 RID: 21536 RVA: 0x001CEC31 File Offset: 0x001CCE31
	public PathList(string name, Vector3[] points)
	{
		this.Name = name;
		this.Path = new PathInterpolator(points);
	}

	// Token: 0x06005421 RID: 21537 RVA: 0x001CEC4C File Offset: 0x001CCE4C
	private void SpawnObjectsNeighborAligned(ref uint seed, PrefabData[] prefabs, List<Vector3> positions, SpawnFilter filter = null)
	{
		if (positions.Count < 2)
		{
			return;
		}
		List<PrefabData> list = Pool.Get<List<PrefabData>>();
		for (int i = 0; i < positions.Count; i++)
		{
			int index = Mathf.Max(i - 1, 0);
			int index2 = Mathf.Min(i + 1, positions.Count - 1);
			Vector3 position = positions[i];
			Quaternion rotation = Quaternion.LookRotation((positions[index2] - positions[index]).XZ3D());
			PrefabData prefab;
			this.SpawnObject(ref seed, prefabs, position, rotation, list, out prefab, positions.Count, i, filter);
			if (prefab != null)
			{
				list.Add(prefab);
			}
		}
		Pool.FreeUnmanaged<PrefabData>(ref list);
	}

	// Token: 0x06005422 RID: 21538 RVA: 0x001CECEC File Offset: 0x001CCEEC
	private bool SpawnObject(ref uint seed, PrefabData[] prefabs, Vector3 position, Quaternion rotation, SpawnFilter filter = null)
	{
		PrefabData random = prefabs.GetRandom(ref seed);
		Vector3 position2 = position;
		Quaternion quaternion = rotation;
		Vector3 localScale = random.Object.transform.localScale;
		random.ApplyDecorComponents(ref position2, ref quaternion, ref localScale);
		if (!random.ApplyTerrainAnchors(ref position2, quaternion, localScale, filter))
		{
			return false;
		}
		World.AddPrefab(this.Name, random, position2, quaternion, localScale);
		return true;
	}

	// Token: 0x06005423 RID: 21539 RVA: 0x001CED44 File Offset: 0x001CCF44
	private bool SpawnObject(ref uint seed, PrefabData[] prefabs, Vector3 position, Quaternion rotation, List<PrefabData> previousSpawns, out PrefabData spawned, int pathLength, int index, SpawnFilter filter = null)
	{
		spawned = null;
		PrefabData random = prefabs.GetRandom(ref seed);
		random.ApplySequenceReplacement(previousSpawns, ref random, prefabs, pathLength, index, position);
		Vector3 position2 = position;
		Quaternion quaternion = rotation;
		Vector3 localScale = random.Object.transform.localScale;
		random.ApplyDecorComponents(ref position2, ref quaternion, ref localScale);
		if (!random.ApplyTerrainAnchors(ref position2, quaternion, localScale, filter))
		{
			return false;
		}
		World.AddPrefab(this.Name, random, position2, quaternion, localScale);
		spawned = random;
		return true;
	}

	// Token: 0x06005424 RID: 21540 RVA: 0x001CEDB4 File Offset: 0x001CCFB4
	private bool CheckObjects(PrefabData[] prefabs, Vector3 position, Quaternion rotation, SpawnFilter filter = null)
	{
		foreach (PrefabData prefab in prefabs)
		{
			Vector3 vector = position;
			Vector3 localScale = prefab.Object.transform.localScale;
			if (!prefab.ApplyTerrainAnchors(ref vector, rotation, localScale, filter))
			{
				return false;
			}
		}
		return true;
	}

	// Token: 0x06005425 RID: 21541 RVA: 0x001CEDF8 File Offset: 0x001CCFF8
	private void SpawnObject(ref uint seed, PrefabData[] prefabs, Vector3 pos, Vector3 dir, PathList.BasicObject obj)
	{
		if (!obj.AlignToNormal)
		{
			dir = dir.XZ3D().normalized;
		}
		SpawnFilter filter = obj.Filter;
		Vector3 a = (this.Width * 0.5f + obj.Offset) * (PathList.rot90 * dir);
		for (int i = 0; i < PathList.placements.Length; i++)
		{
			if ((obj.Placement != PathList.Placement.Center || i == 0) && (obj.Placement != PathList.Placement.Side || i != 0))
			{
				Vector3 vector = pos + PathList.placements[i] * a;
				if (obj.HeightToTerrain)
				{
					vector.y = TerrainMeta.HeightMap.GetHeight(vector);
				}
				if (filter.Test(vector))
				{
					Quaternion rotation = (i == 2) ? Quaternion.LookRotation(PathList.rot180 * dir) : Quaternion.LookRotation(dir);
					if (this.SpawnObject(ref seed, prefabs, vector, rotation, filter))
					{
						break;
					}
				}
			}
		}
	}

	// Token: 0x06005426 RID: 21542 RVA: 0x001CEEE8 File Offset: 0x001CD0E8
	private bool CheckObjects(PrefabData[] prefabs, Vector3 pos, Vector3 dir, PathList.BasicObject obj)
	{
		if (!obj.AlignToNormal)
		{
			dir = dir.XZ3D().normalized;
		}
		SpawnFilter filter = obj.Filter;
		Vector3 a = (this.Width * 0.5f + obj.Offset) * (PathList.rot90 * dir);
		for (int i = 0; i < PathList.placements.Length; i++)
		{
			if ((obj.Placement != PathList.Placement.Center || i == 0) && (obj.Placement != PathList.Placement.Side || i != 0))
			{
				Vector3 vector = pos + PathList.placements[i] * a;
				if (obj.HeightToTerrain)
				{
					vector.y = TerrainMeta.HeightMap.GetHeight(vector);
				}
				if (filter.Test(vector))
				{
					Quaternion rotation = (i == 2) ? Quaternion.LookRotation(PathList.rot180 * dir) : Quaternion.LookRotation(dir);
					if (this.CheckObjects(prefabs, vector, rotation, filter))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	// Token: 0x06005427 RID: 21543 RVA: 0x001CEFD8 File Offset: 0x001CD1D8
	public void SpawnSide(ref uint seed, PathList.SideObject obj)
	{
		if (string.IsNullOrEmpty(obj.Folder))
		{
			return;
		}
		PrefabData[] array = PrefabManager.LoadFolder("assets/bundled/prefabs/autospawn/" + obj.Folder, null, null, true, true);
		if (array == null || array.Length == 0)
		{
			Debug.LogError("Empty decor folder: " + obj.Folder);
			return;
		}
		PathList.Side side = obj.Side;
		SpawnFilter filter = obj.Filter;
		float density = obj.Density;
		float distance = obj.Distance;
		float num = this.Width * 0.5f + obj.Offset;
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		float[] array2 = new float[]
		{
			-num,
			num
		};
		int num2 = 0;
		Vector3 b = this.Path.GetStartPoint();
		List<Vector3> list = new List<Vector3>();
		float num3 = distance * 0.25f;
		float num4 = distance * 0.5f;
		float num5 = this.Path.StartOffset + num4;
		float num6 = this.Path.Length - this.Path.EndOffset - num4;
		for (float num7 = num5; num7 <= num6; num7 += num3)
		{
			Vector3 vector = this.Spline ? this.Path.GetPointCubicHermite(num7) : this.Path.GetPoint(num7);
			if ((vector - b).magnitude >= distance)
			{
				Vector3 tangent = this.Path.GetTangent(num7);
				Vector3 vector2 = PathList.rot90 * tangent;
				for (int i = 0; i < array2.Length; i++)
				{
					int num8 = (num2 + i) % array2.Length;
					if ((side != PathList.Side.Left || num8 == 0) && (side != PathList.Side.Right || num8 == 1))
					{
						float num9 = array2[num8];
						Vector3 vector3 = vector;
						vector3.x += vector2.x * num9;
						vector3.z += vector2.z * num9;
						float normX = TerrainMeta.NormalizeX(vector3.x);
						float normZ = TerrainMeta.NormalizeZ(vector3.z);
						if (filter.GetFactor(normX, normZ, true, 0f) >= SeedRandom.Value(ref seed))
						{
							if (density >= SeedRandom.Value(ref seed))
							{
								vector3.y = heightMap.GetHeight(normX, normZ);
								if (obj.Alignment == PathList.Alignment.None)
								{
									if (!this.SpawnObject(ref seed, array, vector3, Quaternion.LookRotation(Vector3.zero), filter))
									{
										goto IL_28A;
									}
								}
								else if (obj.Alignment == PathList.Alignment.Forward)
								{
									if (!this.SpawnObject(ref seed, array, vector3, Quaternion.LookRotation(tangent * num9), filter))
									{
										goto IL_28A;
									}
								}
								else if (obj.Alignment == PathList.Alignment.Inward)
								{
									if (!this.SpawnObject(ref seed, array, vector3, Quaternion.LookRotation(tangent * num9) * PathList.rot270, filter))
									{
										goto IL_28A;
									}
								}
								else
								{
									list.Add(vector3);
								}
							}
							num2 = num8;
							b = vector;
							if (side == PathList.Side.Any)
							{
								break;
							}
						}
					}
					IL_28A:;
				}
			}
		}
		if (list.Count > 0)
		{
			this.SpawnObjectsNeighborAligned(ref seed, array, list, filter);
		}
	}

	// Token: 0x06005428 RID: 21544 RVA: 0x001CF2A8 File Offset: 0x001CD4A8
	public void SpawnAlong(ref uint seed, PathList.PathObject obj)
	{
		if (string.IsNullOrEmpty(obj.Folder))
		{
			return;
		}
		PrefabData[] array = PrefabManager.LoadFolder("assets/bundled/prefabs/autospawn/" + obj.Folder, null, null, true, true);
		if (array == null || array.Length == 0)
		{
			Debug.LogError("Empty decor folder: " + obj.Folder);
			return;
		}
		SpawnFilter filter = obj.Filter;
		float density = obj.Density;
		float distance = obj.Distance;
		float dithering = obj.Dithering;
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		Vector3 b = this.Path.GetStartPoint();
		List<Vector3> list = new List<Vector3>();
		float num = distance * 0.25f;
		float num2 = distance * 0.5f;
		float num3 = this.Path.StartOffset + num2;
		float num4 = this.Path.Length - this.Path.EndOffset - num2;
		for (float num5 = num3; num5 <= num4; num5 += num)
		{
			Vector3 vector = this.Spline ? this.Path.GetPointCubicHermite(num5) : this.Path.GetPoint(num5);
			if ((vector - b).magnitude >= distance)
			{
				Vector3 tangent = this.Path.GetTangent(num5);
				Vector3 forward = PathList.rot90 * tangent;
				Vector3 vector2 = vector;
				vector2.x += SeedRandom.Range(ref seed, -dithering, dithering);
				vector2.z += SeedRandom.Range(ref seed, -dithering, dithering);
				float normX = TerrainMeta.NormalizeX(vector2.x);
				float normZ = TerrainMeta.NormalizeZ(vector2.z);
				if (filter.GetFactor(normX, normZ, true, 0f) >= SeedRandom.Value(ref seed))
				{
					if (density >= SeedRandom.Value(ref seed))
					{
						vector2.y = heightMap.GetHeight(normX, normZ);
						if (obj.Alignment == PathList.Alignment.None)
						{
							if (!this.SpawnObject(ref seed, array, vector2, Quaternion.identity, filter))
							{
								goto IL_204;
							}
						}
						else if (obj.Alignment == PathList.Alignment.Forward)
						{
							if (!this.SpawnObject(ref seed, array, vector2, Quaternion.LookRotation(tangent), filter))
							{
								goto IL_204;
							}
						}
						else if (obj.Alignment == PathList.Alignment.Inward)
						{
							if (!this.SpawnObject(ref seed, array, vector2, Quaternion.LookRotation(forward), filter))
							{
								goto IL_204;
							}
						}
						else
						{
							list.Add(vector2);
						}
					}
					b = vector;
				}
			}
			IL_204:;
		}
		if (list.Count > 0)
		{
			this.SpawnObjectsNeighborAligned(ref seed, array, list, filter);
		}
	}

	// Token: 0x06005429 RID: 21545 RVA: 0x001CF4E0 File Offset: 0x001CD6E0
	public void SpawnBridge(ref uint seed, PathList.BridgeObject obj)
	{
		if (string.IsNullOrEmpty(obj.Folder))
		{
			return;
		}
		PrefabData[] array = PrefabManager.LoadFolder("assets/bundled/prefabs/autospawn/" + obj.Folder, null, null, true, true);
		if (array == null || array.Length == 0)
		{
			Debug.LogError("Empty decor folder: " + obj.Folder);
			return;
		}
		Vector3 startPoint = this.Path.GetStartPoint();
		Vector3 a = this.Path.GetEndPoint() - startPoint;
		float magnitude = a.magnitude;
		Vector3 vector = a / magnitude;
		float num = magnitude / obj.Distance;
		int num2 = Mathf.RoundToInt(num);
		float num3 = 0.5f * (num - (float)num2);
		Vector3 vector2 = obj.Distance * vector;
		Vector3 vector3 = startPoint + (0.5f + num3) * vector2;
		Quaternion rotation = Quaternion.LookRotation(vector);
		for (int i = 0; i < num2; i++)
		{
			float num4 = WaterLevel.GetWaterOrTerrainSurface(vector3, false, false, null) - 1f;
			if (vector3.y > num4)
			{
				this.SpawnObject(ref seed, array, vector3, rotation, null);
			}
			vector3 += vector2;
		}
	}
	
	// Token: 0x0600542A RID: 21546 RVA: 0x001CF5F8 File Offset: 0x001CD7F8
	public void SpawnStart(ref uint seed, PathList.BasicObject obj)
	{
		if (!this.Start)
		{
			return;
		}
		if (string.IsNullOrEmpty(obj.Folder))
		{
			return;
		}
		PrefabData[] array = PrefabData.Load("assets/bundled/prefabs/autospawn/" + obj.Folder, null, null, true, true);
		if (array == null || array.Length == 0)
		{
			Debug.LogError("Empty decor folder: " + obj.Folder);
			return;
		}
		Vector3 startPoint = this.Path.GetStartPoint();
		Vector3 startTangent = this.Path.GetStartTangent();
		this.SpawnObject(ref seed, array, startPoint, startTangent, obj);
	}

	// Token: 0x0600542B RID: 21547 RVA: 0x001CF678 File Offset: 0x001CD878
	public void SpawnEnd(ref uint seed, PathList.BasicObject obj)
	{
		if (!this.End)
		{
			return;
		}
		if (string.IsNullOrEmpty(obj.Folder))
		{
			return;
		}
		PrefabData[] array = PrefabManager.LoadFolder("assets/bundled/prefabs/autospawn/" + obj.Folder, null, null, true, true);
		if (array == null || array.Length == 0)
		{
			Debug.LogError("Empty decor folder: " + obj.Folder);
			return;
		}
		Vector3 endPoint = this.Path.GetEndPoint();
		Vector3 dir = -this.Path.GetEndTangent();
		this.SpawnObject(ref seed, array, endPoint, dir, obj);
	}

	// Token: 0x0600542C RID: 21548 RVA: 0x001CF6FC File Offset: 0x001CD8FC
	public void TrimStart(PathList.BasicObject obj)
	{
		if (!this.Start)
		{
			return;
		}
		if (string.IsNullOrEmpty(obj.Folder))
		{
			return;
		}
		PrefabData[] array = PrefabManager.LoadFolder("assets/bundled/prefabs/autospawn/" + obj.Folder, null, null, true, true);
		if (array == null || array.Length == 0)
		{
			Debug.LogError("Empty decor folder: " + obj.Folder);
			return;
		}
		Vector3[] points = this.Path.Points;
		Vector3[] tangents = this.Path.Tangents;
		int num = points.Length / 4;
		for (int i = 0; i < num; i++)
		{
			Vector3 pos = points[this.Path.MinIndex + i];
			Vector3 dir = tangents[this.Path.MinIndex + i];
			if (this.CheckObjects(array, pos, dir, obj))
			{
				this.Path.MinIndex += i;
				return;
			}
		}
	}

	// Token: 0x0600542D RID: 21549 RVA: 0x001CF7D4 File Offset: 0x001CD9D4
	public void TrimEnd(PathList.BasicObject obj)
	{
		if (!this.End)
		{
			return;
		}
		if (string.IsNullOrEmpty(obj.Folder))
		{
			return;
		}
		PrefabData[] array = PrefabManager.LoadFolder("assets/bundled/prefabs/autospawn/" + obj.Folder, null, null, true, true);
		if (array == null || array.Length == 0)
		{
			Debug.LogError("Empty decor folder: " + obj.Folder);
			return;
		}
		Vector3[] points = this.Path.Points;
		Vector3[] tangents = this.Path.Tangents;
		int num = points.Length / 4;
		for (int i = 0; i < num; i++)
		{
			Vector3 pos = points[this.Path.MaxIndex - i];
			Vector3 dir = -tangents[this.Path.MaxIndex - i];
			if (this.CheckObjects(array, pos, dir, obj))
			{
				this.Path.MaxIndex -= i;
				return;
			}
		}
	}

	// Token: 0x0600542E RID: 21550 RVA: 0x001CF8B4 File Offset: 0x001CDAB4
	public void TrimTopology(int topology)
	{
		Vector3[] points = this.Path.Points;
		int num = points.Length / 4;
		for (int i = 0; i < num; i++)
		{
			Vector3 worldPos = points[this.Path.MinIndex + i];
			if (!TerrainMeta.TopologyMap.GetTopology(worldPos, topology))
			{
				this.Path.MinIndex += i;
				break;
			}
		}
		for (int j = 0; j < num; j++)
		{
			Vector3 worldPos2 = points[this.Path.MaxIndex - j];
			if (!TerrainMeta.TopologyMap.GetTopology(worldPos2, topology))
			{
				this.Path.MaxIndex -= j;
				return;
			}
		}
	}

	// Token: 0x0600542F RID: 21551 RVA: 0x001CF960 File Offset: 0x001CDB60
	public void ResetTrims()
	{
		this.Path.MinIndex = this.Path.DefaultMinIndex;
		this.Path.MaxIndex = this.Path.DefaultMaxIndex;
	}

	// Token: 0x06005430 RID: 21552 RVA: 0x001CF990 File Offset: 0x001CDB90
	public void AdjustTerrainHeight(float intensity = 1f, float fade = 1f, bool scaleWidthWithLength = false)
	{
		this.AdjustTerrainHeight((float xn, float zn) => intensity, (float xn, float zn) => fade, scaleWidthWithLength);
	}

	// Token: 0x06005431 RID: 21553 RVA: 0x001CF9D0 File Offset: 0x001CDBD0
	public void AdjustTerrainHeight(Func<float, float, float> intensity, Func<float, float, float> fade, bool scaleWidthWithLength = false)
	{
		TerrainHeightMap heightmap = TerrainMeta.HeightMap;
		TerrainTopologyMap topologyMap = TerrainMeta.TopologyMap;
		float num = 1f;
		float randomScale = this.RandomScale;
		float outerPadding = this.OuterPadding;
		float innerPadding = this.InnerPadding;
		float outerFade = this.OuterFade;
		float innerFade = this.InnerFade;
		float terrainOffset = this.TerrainOffset;
		float num2 = this.Width * 0.5f;
		Vector3 startPoint = this.Path.GetStartPoint();
		Vector3 endPoint = this.Path.GetEndPoint();
		Vector3 startTangent = this.Path.GetStartTangent();
		Vector3 normalized = startTangent.XZ3D().normalized;
		Vector3 a = PathList.rot90 * normalized;
		Vector3 vector = startPoint;
		Vector3 vector2 = startTangent;
		Line prev_line = new Line(startPoint, startPoint + startTangent * num);
		Vector3 v = startPoint - a * (num2 + outerPadding + outerFade);
		Vector3 v2 = startPoint + a * (num2 + outerPadding + outerFade);
		Vector3 vector3 = vector;
		Vector3 v3 = vector2;
		Line cur_line = prev_line;
		float num3 = this.Path.Length + num;
		float d;
		for (d = 0f; d < num3; d += num)
		{
			Vector3 vector4 = this.Spline ? this.Path.GetPointCubicHermite(d + num) : this.Path.GetPoint(d + num);
			Vector3 tangent = this.Path.GetTangent(d + num);
			Line next_line = new Line(vector4, vector4 + tangent * num);
			float opacity = 1f;
			float radius = PathList.GetRadius(d, this.Path.Length, num2, randomScale, scaleWidthWithLength);
			float depth = PathList.GetDepth(d, this.Path.Length, terrainOffset, randomScale, scaleWidthWithLength);
			float offset01 = depth * TerrainMeta.OneOverSize.y;
			if (!this.Path.Circular)
			{
				float a2 = (startPoint - vector3).Magnitude2D();
				float b = (endPoint - vector3).Magnitude2D();
				opacity = Mathf.InverseLerp(0f, num2, Mathf.Min(a2, b));
			}
			normalized = v3.XZ3D().normalized;
			a = PathList.rot90 * normalized;
			Vector3 vector5 = vector3 - a * (radius + outerPadding + outerFade);
			Vector3 vector6 = vector3 + a * (radius + outerPadding + outerFade);
			float yn = TerrainMeta.NormalizeY((vector3.y + vector.y) * 0.5f);
			heightmap.ForEach(v, v2, vector5, vector6, delegate(int x, int z)
			{
				float num4 = heightmap.Coordinate(x);
				float num5 = heightmap.Coordinate(z);
				Vector3 vector7 = TerrainMeta.Denormalize(new Vector3(num4, yn, num5));
				Vector3 vector8 = prev_line.ClosestPoint2D(vector7);
				Vector3 vector9 = cur_line.ClosestPoint2D(vector7);
				Vector3 vector10 = next_line.ClosestPoint2D(vector7);
				float num6 = (vector7 - vector8).Magnitude2D();
				float num7 = (vector7 - vector9).Magnitude2D();
				float num8 = (vector7 - vector10).Magnitude2D();
				float value = num7;
				Vector3 vector11 = vector9;
				if (num7 > num6 || num7 > num8)
				{
					if (num6 <= num8)
					{
						value = num6;
						vector11 = vector8;
					}
					else
					{
						value = num8;
						vector11 = vector10;
					}
				}
				float num9 = Mathf.InverseLerp(radius + outerPadding + outerFade * fade(num4, num5), radius + outerPadding, value);
				float num10 = intensity(num4, num5) * opacity * num9;
				if (num10 > 0f)
				{
					float num11 = scaleWidthWithLength ? Mathf.Lerp(0.3f, 1f, d / 1000f) : 1f;
					float t = Mathf.InverseLerp(radius - innerPadding * num11, radius - innerPadding * num11 - innerFade * num11, value);
					float num12 = TerrainMeta.NormalizeY(vector11.y);
					float num13 = Mathf.SmoothStep(0f, offset01, t);
					heightmap.SetHeight(x, z, num12 + num13, num10);
				}
			});
			vector = vector3;
			v = vector5;
			v2 = vector6;
			prev_line = cur_line;
			vector3 = vector4;
			v3 = tangent;
			cur_line = next_line;
		}
	}

	// Token: 0x06005432 RID: 21554 RVA: 0x001CFDAC File Offset: 0x001CDFAC
	public void AdjustTerrainTexture(bool scaleWidthWithLength = false)
	{
		if (this.Splat == 0)
		{
			return;
		}
		TerrainSplatMap splatmap = TerrainMeta.SplatMap;
		float num = 1f;
		float randomScale = this.RandomScale;
		float outerPadding = this.OuterPadding;
		float innerPadding = this.InnerPadding;
		float num2 = this.Width * 0.5f;
		Vector3 startPoint = this.Path.GetStartPoint();
		Vector3 endPoint = this.Path.GetEndPoint();
		Vector3 vector = this.Path.GetStartTangent();
		Vector3 normalized = vector.XZ3D().normalized;
		Vector3 a = PathList.rot90 * normalized;
		Vector3 v = startPoint - a * (num2 + outerPadding);
		Vector3 v2 = startPoint + a * (num2 + outerPadding);
		float num3 = this.Path.Length + num;
		for (float num4 = 0f; num4 < num3; num4 += num)
		{
			Vector3 vector2 = this.Spline ? this.Path.GetPointCubicHermite(num4) : this.Path.GetPoint(num4);
			float opacity = 1f;
			float radius = PathList.GetRadius(num4, this.Path.Length, num2, randomScale, scaleWidthWithLength);
			if (!this.Path.Circular)
			{
				float a2 = (startPoint - vector2).Magnitude2D();
				float b = (endPoint - vector2).Magnitude2D();
				opacity = Mathf.InverseLerp(0f, num2, Mathf.Min(a2, b));
			}
			vector = this.Path.GetTangent(num4);
			normalized = vector.XZ3D().normalized;
			a = PathList.rot90 * normalized;
			Ray ray = new Ray(vector2, vector);
			Vector3 vector3 = vector2 - a * (radius + outerPadding);
			Vector3 vector4 = vector2 + a * (radius + outerPadding);
			float yn = TerrainMeta.NormalizeY(vector2.y);
			splatmap.ForEach(v, v2, vector3, vector4, delegate(int x, int z)
			{
				float x2 = splatmap.Coordinate(x);
				float z2 = splatmap.Coordinate(z);
				Vector3 vector5 = TerrainMeta.Denormalize(new Vector3(x2, yn, z2));
				Vector3 b2 = ray.ClosestPoint(vector5);
				float value = (vector5 - b2).Magnitude2D();
				float num5 = Mathf.InverseLerp(radius + outerPadding, radius - innerPadding, value);
				splatmap.SetSplat(x, z, this.Splat, num5 * opacity);
			});
			v = vector3;
			v2 = vector4;
		}
	}

	public void AdjustTerrainWaterFlow(bool scaleWidthWithLength = false)
	{
		TerrainWaterFlowMap flowMap = TerrainMeta.WaterFlowMap;
		float num = 1f;
		float randomScale = this.RandomScale;
		float outerPadding = this.OuterPadding;
		float num2 = this.Width * 0.5f;
		Vector3 startPoint = this.Path.GetStartPoint();
		Vector3 dir = this.Path.GetStartTangent();
		Vector3 normalized = dir.XZ3D().normalized;
		Vector3 a = PathList.rot90 * normalized;
		Vector3 vector = startPoint - a * (num2 + outerPadding);
		Vector3 vector2 = startPoint + a * (num2 + outerPadding);
		float num3 = this.Path.Length + num;
		for (float num4 = 0f; num4 < num3; num4 += num)
		{
			object obj = this.Spline ? this.Path.GetPointCubicHermite(num4) : this.Path.GetPoint(num4);
			float radius = PathList.GetRadius(num4, this.Path.Length, num2, randomScale, scaleWidthWithLength);
			dir = this.Path.GetTangent(num4);
			normalized = dir.XZ3D().normalized;
			a = PathList.rot90 * normalized;
			object a2 = obj;
			Vector3 vector3 = a2 - a * (radius + outerPadding);
			Vector3 vector4 = a2 + a * (radius + outerPadding);
			TerrainMap flowMap2 = flowMap;
			Vector3 v = vector;
			Vector3 v2 = vector2;
			Vector3 v3 = vector3;
			Vector3 v4 = vector4;
			Action<int, int> action = delegate(int x, int z)
			{
				float normX = flowMap.Coordinate(x);
				float normZ = flowMap.Coordinate(z);
				flowMap.SetFlowDirection(normX, normZ, dir);
			};
			flowMap2.ForEach(v, v2, v3, v4, action);
			vector = vector3;
			vector2 = vector4;
		}
	}

	// Token: 0x06005434 RID: 21556 RVA: 0x001D01C0 File Offset: 0x001CE3C0
	public void AdjustTerrainTopology(bool scaleWidthWithLength = false)
	{
		if (this.Topology == 0)
		{
			return;
		}
		TerrainTopologyMap topomap = TerrainMeta.TopologyMap;
		float num = 1f;
		float randomScale = this.RandomScale;
		float outerPadding = this.OuterPadding;
		float innerPadding = this.InnerPadding;
		float num2 = this.Width * 0.5f;
		Vector3 startPoint = this.Path.GetStartPoint();
		Vector3 endPoint = this.Path.GetEndPoint();
		Vector3 vector = this.Path.GetStartTangent();
		Vector3 normalized = vector.XZ3D().normalized;
		Vector3 a = PathList.rot90 * normalized;
		Vector3 v = startPoint - a * (num2 + outerPadding);
		Vector3 v2 = startPoint + a * (num2 + outerPadding);
		float num3 = this.Path.Length + num;
		for (float num4 = 0f; num4 < num3; num4 += num)
		{
			Vector3 vector2 = this.Spline ? this.Path.GetPointCubicHermite(num4) : this.Path.GetPoint(num4);
			float opacity = 1f;
			float radius = PathList.GetRadius(num4, this.Path.Length, num2, randomScale, scaleWidthWithLength);
			if (!this.Path.Circular)
			{
				float a2 = (startPoint - vector2).Magnitude2D();
				float b = (endPoint - vector2).Magnitude2D();
				opacity = Mathf.InverseLerp(0f, num2, Mathf.Min(a2, b));
			}
			vector = this.Path.GetTangent(num4);
			normalized = vector.XZ3D().normalized;
			a = PathList.rot90 * normalized;
			Ray ray = new Ray(vector2, vector);
			Vector3 vector3 = vector2 - a * (radius + outerPadding);
			Vector3 vector4 = vector2 + a * (radius + outerPadding);
			float yn = TerrainMeta.NormalizeY(vector2.y);
			topomap.ForEach(v, v2, vector3, vector4, delegate(int x, int z)
			{
				float x2 = topomap.Coordinate(x);
				float z2 = topomap.Coordinate(z);
				Vector3 vector5 = TerrainMeta.Denormalize(new Vector3(x2, yn, z2));
				Vector3 b2 = ray.ClosestPoint(vector5);
				float value = (vector5 - b2).Magnitude2D();
				if (Mathf.InverseLerp(radius + outerPadding, radius - innerPadding, value) * opacity > 0.3f)
				{
					topomap.AddTopology(x, z, this.Topology);
				}
			});
			v = vector3;
			v2 = vector4;
		}
	}

	// Token: 0x06005435 RID: 21557 RVA: 0x001D0430 File Offset: 0x001CE630
	public void AdjustPlacementMap(float width)
	{
		TerrainPlacementMap placementmap = TerrainMeta.PlacementMap;
		float num = 1f;
		float radius = width * 0.5f;
		Vector3 startPoint = this.Path.GetStartPoint();
		this.Path.GetEndPoint();
		Vector3 vector = this.Path.GetStartTangent();
		Vector3 normalized = vector.XZ3D().normalized;
		Vector3 a = PathList.rot90 * normalized;
		Vector3 v = startPoint - a * radius;
		Vector3 v2 = startPoint + a * radius;
		float num2 = this.Path.Length + num;
		for (float num3 = 0f; num3 < num2; num3 += num)
		{
			Vector3 vector2 = this.Spline ? this.Path.GetPointCubicHermite(num3) : this.Path.GetPoint(num3);
			vector = this.Path.GetTangent(num3);
			normalized = vector.XZ3D().normalized;
			a = PathList.rot90 * normalized;
			Ray ray = new Ray(vector2, vector);
			Vector3 vector3 = vector2 - a * radius;
			Vector3 vector4 = vector2 + a * radius;
			float yn = TerrainMeta.NormalizeY(vector2.y);
			placementmap.ForEach(v, v2, vector3, vector4, delegate(int x, int z)
			{
				float x2 = placementmap.Coordinate(x);
				float z2 = placementmap.Coordinate(z);
				Vector3 vector5 = TerrainMeta.Denormalize(new Vector3(x2, yn, z2));
				Vector3 b = ray.ClosestPoint(vector5);
				if ((vector5 - b).Magnitude2D() <= radius)
				{
					placementmap.SetBlocked(x, z);
				}
			});
			v = vector3;
			v2 = vector4;
		}
	}
/*
	// Token: 0x06005436 RID: 21558 RVA: 0x001D05DC File Offset: 0x001CE7DC
	public List<PathList.MeshObject> CreateMesh(Mesh[] meshes, float normalSmoothing, bool snapToTerrain, bool snapStartToTerrain, bool snapEndToTerrain, bool scaleWidthWithLength = false, bool topAligned = false, int roundVertices = 0)
	{
		MeshCache.Data[] array = new MeshCache.Data[meshes.Length];
		MeshData[] array2 = new MeshData[meshes.Length];
		for (int i = 0; i < meshes.Length; i++)
		{
			array[i] = MeshCache.Get(meshes[i]);
			array2[i] = new MeshData();
		}
		MeshData[] array3 = array2;
		for (int j = 0; j < array3.Length; j++)
		{
			array3[j].AllocMinimal();
		}
		Bounds bounds = meshes[meshes.Length - 1].bounds;
		Vector3 min = bounds.min;
		Vector3 size = bounds.size;
		float num = this.Width / bounds.size.x;
		List<PathList.MeshObject> list = new List<PathList.MeshObject>();
		int num2 = (int)(this.Path.Length / (num * bounds.size.z));
		int num3 = 5;
		float num4 = this.Path.Length / (float)num2;
		float randomScale = this.RandomScale;
		float meshOffset = this.MeshOffset;
		float baseRadius = this.Width * 0.5f;
		int num5 = array[0].vertices.Length;
		int num6 = array[0].triangles.Length;
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		for (int k = 0; k < num2; k += num3)
		{
			float distance = (float)k * num4 + 0.5f * (float)num3 * num4;
			Vector3 vector = this.Spline ? this.Path.GetPointCubicHermite(distance) : this.Path.GetPoint(distance);
			int num7 = 0;
			while (num7 < num3 && k + num7 < num2)
			{
				float num8 = (float)(k + num7) * num4;
				for (int l = 0; l < meshes.Length; l++)
				{
					MeshCache.Data data = array[l];
					MeshData meshData = array2[l];
					int count = meshData.vertices.Count;
					for (int m = 0; m < data.vertices.Length; m++)
					{
						Vector2 item = data.uv[m];
						Vector3 vector2 = data.vertices[m];
						Vector3 vector3 = data.normals[m];
						Vector4 vector4 = data.tangents[m];
						float t = (vector2.x - min.x) / size.x;
						float num9 = vector2.y - min.y;
						if (topAligned)
						{
							num9 -= size.y;
						}
						float num10 = (vector2.z - min.z) / size.z;
						float num11 = num8 + num10 * num4;
						object obj = this.Spline ? this.Path.GetPointCubicHermite(num11) : this.Path.GetPoint(num11);
						Vector3 tangent = this.Path.GetTangent(num11);
						Vector3 normalized = tangent.XZ3D().normalized;
						Vector3 vector5 = PathList.rot90 * normalized;
						Vector3 vector6 = Vector3.Cross(tangent, vector5);
						Quaternion rotation = Quaternion.LookRotation(normalized, vector6);
						float radius = PathList.GetRadius(num11, this.Path.Length, baseRadius, randomScale, scaleWidthWithLength);
						object a = obj;
						Vector3 vector7 = a - vector5 * radius;
						Vector3 vector8 = a + vector5 * radius;
						if (snapToTerrain)
						{
							vector7.y = heightMap.GetHeight(vector7);
							vector8.y = heightMap.GetHeight(vector8);
						}
						vector7 += vector6 * meshOffset;
						vector8 += vector6 * meshOffset;
						vector2 = Vector3.Lerp(vector7, vector8, t);
						if ((snapStartToTerrain && num11 < 0.1f) || (snapEndToTerrain && num11 > this.Path.Length - 0.1f))
						{
							vector2.y = heightMap.GetHeight(vector2);
						}
						else
						{
							vector2.y += num9;
						}
						vector2 -= vector;
						vector3 = rotation * vector3;
						Vector3 vector9 = new Vector3(vector4.x, vector4.y, vector4.z);
						vector9 = rotation * vector9;
						vector4.Set(vector9.x, vector9.y, vector9.z, vector4.w);
						if (normalSmoothing > 0f)
						{
							vector3 = Vector3.Slerp(vector3, Vector3.up, normalSmoothing);
						}
						if (roundVertices > 0)
						{
							vector2.x = (float)Math.Round((double)vector2.x, roundVertices);
							vector2.y = (float)Math.Round((double)vector2.y, roundVertices);
							vector2.z = (float)Math.Round((double)vector2.z, roundVertices);
						}
						meshData.vertices.Add(vector2);
						meshData.normals.Add(vector3);
						meshData.tangents.Add(vector4);
						meshData.uv.Add(item);
					}
					for (int n = 0; n < data.triangles.Length; n++)
					{
						int num12 = data.triangles[n];
						meshData.triangles.Add(count + num12);
					}
				}
				num7++;
			}
			list.Add(new PathList.MeshObject(vector, array2));
			array3 = array2;
			for (int j = 0; j < array3.Length; j++)
			{
				array3[j].Clear();
			}
		}
		array3 = array2;
		for (int j = 0; j < array3.Length; j++)
		{
			array3[j].Free();
		}
		return list;
	}

	// Token: 0x06005437 RID: 21559 RVA: 0x001D0B0C File Offset: 0x001CED0C
	public List<PathList.MeshObject> CreateMeshRiverInterior(Mesh[] meshes, bool snapToTerrain, bool snapStartToTerrain, bool snapEndToTerrain, Bounds bounds, bool scaleWidthWithLength = false, int roundVertices = 0)
	{
		MeshCache.Data[] array = new MeshCache.Data[meshes.Length];
		for (int i = 0; i < meshes.Length; i++)
		{
			array[i] = MeshCache.Get(meshes[i]);
		}
		MeshData meshData = new MeshData();
		meshData.vertices = Pool.Get<List<Vector3>>();
		meshData.triangles = Pool.Get<List<int>>();
		meshData.uv = Pool.Get<List<Vector2>>();
		MeshData meshData2 = new MeshData();
		meshData2.vertices = Pool.Get<List<Vector3>>();
		meshData2.triangles = Pool.Get<List<int>>();
		meshData2.uv = Pool.Get<List<Vector2>>();
		Dictionary<PathList.WeldVertex, int> dictionary = new Dictionary<PathList.WeldVertex, int>();
		Vector3 min = bounds.min;
		Vector3 size = bounds.size;
		float num = this.Width / bounds.size.x;
		int num2 = (int)(this.Path.Length / (num * bounds.size.z));
		int num3 = 5;
		float num4 = this.Path.Length / (float)num2;
		float randomScale = this.RandomScale;
		float meshOffset = this.MeshOffset;
		float baseRadius = this.Width * 0.5f;
		TerrainHeightMap heightMap = TerrainMeta.HeightMap;
		List<PathList.MeshObject> list = new List<PathList.MeshObject>();
		for (int j = 0; j < num2; j += num3)
		{
			float distance = (float)j * num4 + 0.5f * (float)num3 * num4;
			Vector3 vector = this.Spline ? this.Path.GetPointCubicHermite(distance) : this.Path.GetPoint(distance);
			int num5 = 0;
			while (num5 < num3 && j + num5 < num2)
			{
				float num6 = (float)(j + num5) * num4;
				bool flag = j == 0;
				bool flag2 = num5 == 0;
				bool flag3 = j == num2 - 1;
				bool flag4 = num5 == num3 - 1 || j + num5 == num2 - 1;
				int num7 = flag2 ? 4 : 3;
				for (int k = 0; k < num7; k++)
				{
					int num8 = k;
					MeshCache.Data data = array[num8];
					MeshData meshData3 = meshData;
					int count = meshData3.vertices.Count;
					float x;
					if (num8 == 2 && (!flag4 || !flag3))
					{
						x = 1f;
					}
					else if (num8 == 3 && flag2 && !flag)
					{
						x = 1f;
					}
					else
					{
						x = 0f;
					}
					float y = (num8 == 0) ? 1f : 0f;
					for (int l = 0; l < data.vertices.Length; l++)
					{
						Vector3 vector2 = data.vertices[l];
						float t = (vector2.x - min.x) / size.x;
						float num9 = vector2.y - min.y;
						float num10 = (vector2.z - min.z) / size.z;
						float num11 = num6 + num10 * num4;
						object obj = this.Spline ? this.Path.GetPointCubicHermite(num11) : this.Path.GetPoint(num11);
						Vector3 tangent = this.Path.GetTangent(num11);
						Vector3 normalized = tangent.XZ3D().normalized;
						Vector3 vector3 = PathList.rot90 * normalized;
						Vector3 a = Vector3.Cross(tangent, vector3);
						float radius = PathList.GetRadius(num11, this.Path.Length, baseRadius, randomScale, scaleWidthWithLength);
						object a2 = obj;
						Vector3 vector4 = a2 - vector3 * radius;
						Vector3 vector5 = a2 + vector3 * radius;
						if (snapToTerrain)
						{
							vector4.y = heightMap.GetHeight(vector4);
							vector5.y = heightMap.GetHeight(vector5);
						}
						vector4 += a * meshOffset;
						vector5 += a * meshOffset;
						vector2 = Vector3.Lerp(vector4, vector5, t);
						if ((snapStartToTerrain && num11 < 0.1f) || (snapEndToTerrain && num11 > this.Path.Length - 0.1f))
						{
							vector2.y = heightMap.GetHeight(vector2);
						}
						else
						{
							vector2.y += num9;
						}
						vector2 -= vector;
						if (roundVertices > 0)
						{
							vector2.x = (float)Math.Round((double)vector2.x, roundVertices);
							vector2.y = (float)Math.Round((double)vector2.y, roundVertices);
							vector2.z = (float)Math.Round((double)vector2.z, roundVertices);
						}
						meshData3.vertices.Add(vector2);
						meshData3.uv.Add(new Vector2(x, y));
					}
					for (int m = 0; m < data.triangles.Length; m++)
					{
						int num12 = data.triangles[m];
						meshData3.triangles.Add(count + num12);
					}
				}
				num5++;
			}
			for (int n = 0; n < meshData.triangles.Count; n++)
			{
				int index = meshData.triangles[n];
				Vector3 vector6 = meshData.vertices[index];
				Vector2 item = meshData.uv[index];
				PathList.WeldVertex key = new PathList.WeldVertex
				{
					x = vector6.x,
					y = vector6.y,
					z = vector6.z,
					alwaysUnderwater = meshData.uv[index].x,
					topSurface = meshData.uv[index].y
				};
				int count2;
				if (!dictionary.TryGetValue(key, out count2))
				{
					count2 = meshData2.vertices.Count;
					dictionary.Add(key, count2);
					meshData2.vertices.Add(vector6);
					meshData2.uv.Add(item);
				}
				meshData2.triangles.Add(count2);
			}
			list.Add(new PathList.MeshObject(vector, new MeshData[]
			{
				meshData2
			}));
			meshData.Clear();
			meshData2.Clear();
			dictionary.Clear();
		}
		meshData.Free();
		meshData2.Free();
		return list;
	}

	// Token: 0x06005438 RID: 21560 RVA: 0x001D10D0 File Offset: 0x001CF2D0
	public static float GetRadius(float distance, float length, float baseRadius, float randomScale, bool scaleWidthWithLength)
	{
		if (scaleWidthWithLength)
		{
			float t = Mathf.Sqrt(Mathf.Max(0f, length - distance) / 100f);
			float num = (length > 0f) ? Mathf.Lerp(3f, 1f, t) : 1f;
			float t2 = distance / 1000f;
			float num2 = Mathf.Lerp(1f, 8f, t2);
			baseRadius = baseRadius * num2 * num;
		}
		return Mathf.Lerp(baseRadius, baseRadius * randomScale, Noise.SimplexUnsigned(distance * 0.005f));
	}

	// Token: 0x06005439 RID: 21561 RVA: 0x001D1150 File Offset: 0x001CF350
	public static float GetDepth(float distance, float length, float baseDepth, float randomScale, bool scaleWidthWithLength)
	{
		if (scaleWidthWithLength)
		{
			float t = distance / 1000f;
			float num = Mathf.Lerp(1f, 3f, t);
			baseDepth *= num;
		}
		return Mathf.Lerp(baseDepth, baseDepth * randomScale, Noise.SimplexUnsigned(distance * 0.005f));
	}

	// Token: 0x040046B2 RID: 18098
	private static Quaternion rot90 = Quaternion.Euler(0f, 90f, 0f);

	// Token: 0x040046B3 RID: 18099
	private static Quaternion rot180 = Quaternion.Euler(0f, 180f, 0f);

	// Token: 0x040046B4 RID: 18100
	private static Quaternion rot270 = Quaternion.Euler(0f, 270f, 0f);

	// Token: 0x040046B5 RID: 18101
	public const float EndWidthScale = 3f;

	// Token: 0x040046B6 RID: 18102
	public const float EndScaleDistance = 100f;

	// Token: 0x040046B7 RID: 18103
	public const float LengthWidthScale = 8f;

	// Token: 0x040046B8 RID: 18104
	public const float LengthDepthScale = 3f;

	// Token: 0x040046B9 RID: 18105
	public const float LengthScaleDistance = 1000f;

	// Token: 0x040046BA RID: 18106
	public string Name;

	// Token: 0x040046BB RID: 18107
	public PathInterpolator Path;

	// Token: 0x040046BC RID: 18108
	public bool Spline;

	// Token: 0x040046BD RID: 18109
	public bool Start;

	// Token: 0x040046BE RID: 18110
	public bool End;

	// Token: 0x040046BF RID: 18111
	public float Width;

	// Token: 0x040046C0 RID: 18112
	public float InnerPadding;

	// Token: 0x040046C1 RID: 18113
	public float OuterPadding;

	// Token: 0x040046C2 RID: 18114
	public float InnerFade;

	// Token: 0x040046C3 RID: 18115
	public float OuterFade;

	// Token: 0x040046C4 RID: 18116
	public float RandomScale;

	// Token: 0x040046C5 RID: 18117
	public float MeshOffset;

	// Token: 0x040046C6 RID: 18118
	public float TerrainOffset;

	// Token: 0x040046C7 RID: 18119
	public int Topology;

	// Token: 0x040046C8 RID: 18120
	public int Splat;

	// Token: 0x040046C9 RID: 18121
	public int Hierarchy;

	// Token: 0x040046CA RID: 18122
	public PathFinder.Node ProcgenStartNode;

	// Token: 0x040046CB RID: 18123
	public PathFinder.Node ProcgenEndNode;

	// Token: 0x040046CC RID: 18124
	public const float StepSize = 1f;

	// Token: 0x040046CD RID: 18125
	private static float[] placements = new float[]
	{
		0f,
		-1f,
		1f
	};

	// Token: 0x02000BF8 RID: 3064
	public enum Side
	{
		// Token: 0x040046CF RID: 18127
		Both,
		// Token: 0x040046D0 RID: 18128
		Left,
		// Token: 0x040046D1 RID: 18129
		Right,
		// Token: 0x040046D2 RID: 18130
		Any
	}

	// Token: 0x02000BF9 RID: 3065
	public enum Placement
	{
		// Token: 0x040046D4 RID: 18132
		Center,
		// Token: 0x040046D5 RID: 18133
		Side
	}

	// Token: 0x02000BFA RID: 3066
	public enum Alignment
	{
		// Token: 0x040046D7 RID: 18135
		None,
		// Token: 0x040046D8 RID: 18136
		Neighbor,
		// Token: 0x040046D9 RID: 18137
		Forward,
		// Token: 0x040046DA RID: 18138
		Inward
	}

	// Token: 0x02000BFB RID: 3067
	[Serializable]
	public class BasicObject
	{
		// Token: 0x040046DB RID: 18139
		public string Folder;

		// Token: 0x040046DC RID: 18140
		public SpawnFilter Filter;

		// Token: 0x040046DD RID: 18141
		public PathList.Placement Placement;

		// Token: 0x040046DE RID: 18142
		public bool AlignToNormal = true;

		// Token: 0x040046DF RID: 18143
		public bool HeightToTerrain = true;

		// Token: 0x040046E0 RID: 18144
		public float Offset;
	}

	// Token: 0x02000BFC RID: 3068
	[Serializable]
	public class SideObject
	{
		// Token: 0x040046E1 RID: 18145
		public string Folder;

		// Token: 0x040046E2 RID: 18146
		public SpawnFilter Filter;

		// Token: 0x040046E3 RID: 18147
		public PathList.Side Side;

		// Token: 0x040046E4 RID: 18148
		public PathList.Alignment Alignment;

		// Token: 0x040046E5 RID: 18149
		public float Density = 1f;

		// Token: 0x040046E6 RID: 18150
		public float Distance = 25f;

		// Token: 0x040046E7 RID: 18151
		public float Offset = 2f;
	}

	// Token: 0x02000BFD RID: 3069
	[Serializable]
	public class PathObject
	{
		// Token: 0x040046E8 RID: 18152
		public string Folder;

		// Token: 0x040046E9 RID: 18153
		public SpawnFilter Filter;

		// Token: 0x040046EA RID: 18154
		public PathList.Alignment Alignment;

		// Token: 0x040046EB RID: 18155
		public float Density = 1f;

		// Token: 0x040046EC RID: 18156
		public float Distance = 5f;

		// Token: 0x040046ED RID: 18157
		public float Dithering = 5f;
	}

	// Token: 0x02000BFE RID: 3070
	[Serializable]
	public class BridgeObject
	{
		// Token: 0x040046EE RID: 18158
		public string Folder;

		// Token: 0x040046EF RID: 18159
		public float Distance = 10f;
	}

	// Token: 0x02000BFF RID: 3071
	public class MeshObject
	{

		// Token: 0x0600543F RID: 21567 RVA: 0x001D1284 File Offset: 0x001CF484
		public MeshObject(Vector3 meshPivot, MeshData[] meshData)
		{
			this.Position = meshPivot;
			this.Meshes = new Mesh[meshData.Length];
			for (int i = 0; i < this.Meshes.Length; i++)
			{
				MeshData meshData2 = meshData[i];
				Mesh mesh = this.Meshes[i] = new Mesh();
				meshData2.Apply(mesh);
			}
		}

		// Token: 0x040046F0 RID: 18160
		public Vector3 Position;

		// Token: 0x040046F1 RID: 18161
		public Mesh[] Meshes;
		
	}

	// Token: 0x02000C00 RID: 3072
	private struct WeldVertex : IEquatable<PathList.WeldVertex>
	{
		// Token: 0x06005440 RID: 21568 RVA: 0x001D12D9 File Offset: 0x001CF4D9
		public override bool Equals(object other)
		{
			return other is PathList.WeldVertex && this.Equals((PathList.WeldVertex)other);
		}

		// Token: 0x06005441 RID: 21569 RVA: 0x001D12F4 File Offset: 0x001CF4F4
		public bool Equals(PathList.WeldVertex other)
		{
			return Vector3.Distance(new Vector3(this.x, this.y, this.z), new Vector3(other.x, other.y, other.z)) < 0.001f && this.alwaysUnderwater == other.alwaysUnderwater && this.topSurface == other.topSurface;
		}

		// Token: 0x06005442 RID: 21570 RVA: 0x001D135C File Offset: 0x001CF55C
		public override int GetHashCode()
		{
			int value = Mathf.RoundToInt(this.x * 999.99994f);
			int value2 = Mathf.RoundToInt(this.y * 999.99994f);
			int value3 = Mathf.RoundToInt(this.z * 999.99994f);
			return HashCode.Combine<int, int, int, float, float>(value, value2, value3, this.alwaysUnderwater, this.topSurface);
		}

		// Token: 0x040046F2 RID: 18162
		private const float EPSILON = 0.001f;

		// Token: 0x040046F3 RID: 18163
		private const float INV_EPSILON = 999.99994f;

		// Token: 0x040046F4 RID: 18164
		public float x;

		// Token: 0x040046F5 RID: 18165
		public float y;

		// Token: 0x040046F6 RID: 18166
		public float z;

		// Token: 0x040046F7 RID: 18167
		public float alwaysUnderwater;

		// Token: 0x040046F8 RID: 18168
		public float topSurface;
	}
}
*/