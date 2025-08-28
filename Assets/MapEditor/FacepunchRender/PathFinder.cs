/*
using System;
using System.Collections.Generic;
using UnityEngine;

// Token: 0x02001131 RID: 4401
public class PathFinder
{
	// Token: 0x06006394 RID: 25492 RVA: 0x002243CC File Offset: 0x002225CC
	public PathFinder(int[,] costmap, bool diagonals = true, bool directional = true)
	{
		this.costmap = costmap;
		this.neighbors = (diagonals ? PathFinder.mooreNeighbors : PathFinder.neumannNeighbors);
		this.diagonals = diagonals;
		this.directional = directional;
	}


	    public static PathList GeneratePath(Vector3 startPos, Vector3 endPos, string pathName = "Road", bool spline = true, int maxDepth = 10000)
    {
        // Generate costmap
        uint seed = UnityEngine.Random.Range(0,u100000); // Initialize seed
        int[,] costmap = CreateRoadCostmap(ref seed);

        // Initialize PathFinder
        PathFinder pathFinder = new PathFinder(costmap);
        int res = costmap.GetLength(0); // Assume square costmap

        // Convert world-space positions to grid coordinates
        PathFinder.Point start = PathFinder.GetPoint(startPos, res);
        PathFinder.Point end = PathFinder.GetPoint(endPos, res);

        // Find path in grid space (returns path from end to start)
        List<PathFinder.Point> gridPath = pathFinder.FindPathReversed(start, end, maxDepth);

        // If no path found, return empty PathList
        if (gridPath == null || gridPath.Count == 0)
        {
            Debug.LogWarning($"No path found from {startPos} to {endPos}");
            return new PathList(pathName, new Vector3[] { startPos, endPos });
        }

        // Convert grid path to world-space path
        List<Vector3> worldPath = new List<Vector3>();
        foreach (PathFinder.Point point in gridPath)
        {
            // Convert grid coordinates to normalized UV [0, 1]
            float uvX = (float)point.x / res;
            float uvZ = (float)point.y / res;

            // Convert UV to world-space
            Vector3 worldPos = TerrainManager.TerrainUVToWorld(new Vector3(uvX, 0f, uvZ));

            // Set height to terrain surface
            float height = TerrainManager.GetHeightAtPosition(worldPos);
            worldPath.Add(new Vector3(worldPos.x, height, worldPos.z));
        }

        // Reverse path to go from start to end
        worldPath.Reverse();

        // Create PathList
        PathList pathList = new PathList(pathName, worldPath.ToArray())
        {
            Spline = spline,
            Width = 10f, // Example width (adjust as needed)
            InnerPadding = 1f,
            OuterPadding = 1f,
            InnerFade = 2f,
            OuterFade = 2f,
            RandomScale = 1f,
            MeshOffset = 0f,
            TerrainOffset = 0f,
            Topology = TerrainTopology.TypeToIndex(TerrainTopology.Enum.Road), // Example topology
            Splat = TerrainSplat.TypeToIndex(TerrainSplat.Enum.Dirt), // Example splat
            Hierarchy = 0
        };

        // Optional: Adjust terrain to match path
        pathList.AdjustTerrainHeight();
        pathList.AdjustTerrainTopology();

        return pathList;
    }
	
	

public static int[,] CreateRoadCostmap(ref uint seed, bool trail = false)
{
    float radius = 5f; // Small radius for topology check
    float radius2 = 15f; // Larger radius for topology check
    int num = TerrainManager.SplatMapRes; // Use splatmap resolution for costmap
    int[,] array = new int[num, num];

    for (int i = 0; i < num; i++)
    {
        float normZ = ((float)i + 0.5f) / num; // Normalized [0, 1]
        for (int j = 0; j < num; j++)
        {
            float normX = ((float)j + 0.5f) / num; // Normalized [0, 1]
            int gridX = j; // Direct grid index (since num = SplatMapRes)
            int gridZ = i;

            // Random cost variation
            int num2 = SeedRandom.Range(ref seed, 100, 200);

            // Get slope at grid position
            float? slope = TerrainManager.GetSlope(gridX, gridZ);
            if (!slope.HasValue)
            {
                array[j, i] = int.MaxValue; // Invalid slope, mark as impassable
                continue;
            }

            // Get topology for specific layers
            int topology = 0;
            int topology2 = 0;
            int num3 = 196996; // Example topology mask (adjust as needed)
            int num4 = 10487296; // Example larger radius topology mask
            int num5 = 2; // Example topology (e.g., monument)
            int num6 = 49152; // Example topology (e.g., road)

            // Convert radius to grid units
            int gridRadius = Mathf.CeilToInt(radius / TerrainManager.SplatSize);
            int gridRadius2 = Mathf.CeilToInt(radius2 / TerrainManager.SplatSize);

            // Check topology in a radius
            bool[,] topologyMap = TerrainManager.GetTopologyBitview(TerrainTopology.TypeToIndex(TerrainTopology.Enum.Road), 
                gridX - gridRadius, gridZ - gridRadius, gridRadius * 2, gridRadius * 2);
            bool[,] topologyMap2 = TerrainManager.GetTopologyBitview(TerrainTopology.TypeToIndex(TerrainTopology.Enum.Road), 
                gridX - gridRadius2, gridZ - gridRadius2, gridRadius2 * 2, gridRadius2 * 2);

            for (int tz = 0; tz < topologyMap.GetLength(0); tz++)
            {
                for (int tx = 0; tx < topologyMap.GetLength(1); tx++)
                {
                    if (topologyMap[tx, tz])
                        topology |= TerrainTopology.IndexToType(TerrainTopology.TypeToIndex(TerrainTopology.Enum.Road));
                }
            }
            for (int tz = 0; tz < topologyMap2.GetLength(0); tz++)
            {
                for (int tx = 0; tx < topologyMap2.GetLength(1); tx++)
                {
                    if (topologyMap2[tx, tz])
                        topology2 |= TerrainTopology.IndexToType(TerrainTopology.TypeToIndex(TerrainTopology.Enum.Road));
                }
            }

            // Assign costs based on slope and topology
            if (slope > 20f || (topology & num3) != 0 || (topology2 & num4) != 0)
            {
                array[j, i] = int.MaxValue; // Impassable
            }
            else if ((topology & num6) != 0)
            {
                array[j, i] = trail ? int.MaxValue : 5000; // Roads are costly unless trail
            }
            else if ((topology & num5) != 0  || placement check/)
            {
                array[j, i] = 5000; // Monuments or blocked areas
            }
            else
            {
                array[j, i] = 1 + (int)(slope.Value * slope.Value * 10f) + num2; // Base cost + slope penalty
            }
        }
    }
    return array;
}

	// Token: 0x06006395 RID: 25493 RVA: 0x0022441F File Offset: 0x0022261F
	public int GetResolution(int index)
	{
		return this.costmap.GetLength(index);
	}

	// Token: 0x06006396 RID: 25494 RVA: 0x0022442D File Offset: 0x0022262D
	public PathFinder.Node FindPath(PathFinder.Point start, PathFinder.Point end, int depth = 2147483647)
	{
		return this.FindPathReversed(end, start, depth);
	}

	// Token: 0x06006397 RID: 25495 RVA: 0x00224438 File Offset: 0x00222638
	private PathFinder.Node FindPathReversed(PathFinder.Point start, PathFinder.Point end, int depth = 2147483647)
	{
		if (this.visited == null)
		{
			this.visited = new int[this.costmap.GetLength(0), this.costmap.GetLength(1)];
		}
		else
		{
			Array.Clear(this.visited, 0, this.visited.Length);
		}
		int num = 0;
		int num2 = this.costmap.GetLength(0) - 1;
		int num3 = 0;
		int num4 = this.costmap.GetLength(1) - 1;
		IntrusiveMinHeap<PathFinder.Node> intrusiveMinHeap = default(IntrusiveMinHeap<PathFinder.Node>);
		int num5 = this.Cost(start);
		if (num5 != 2147483647)
		{
			int heuristic = this.Heuristic(start, end);
			intrusiveMinHeap.Add(new PathFinder.Node(start, num5, heuristic, null));
		}
		this.visited[start.x, start.y] = num5;
		while (!intrusiveMinHeap.Empty && depth-- > 0)
		{
			PathFinder.Node node = intrusiveMinHeap.Pop();
			if (node.heuristic == 0)
			{
				return node;
			}
			for (int i = 0; i < this.neighbors.Length; i++)
			{
				PathFinder.Point point = node.point + this.neighbors[i];
				if (point.x >= num && point.x <= num2 && point.y >= num3 && point.y <= num4)
				{
					int num6 = this.Cost(point, node);
					if (num6 != 2147483647)
					{
						int num7 = this.visited[point.x, point.y];
						if (num7 == 0 || num6 < num7)
						{
							int cost = node.cost + num6;
							int heuristic2 = this.Heuristic(point, end);
							intrusiveMinHeap.Add(new PathFinder.Node(point, cost, heuristic2, node));
							this.visited[point.x, point.y] = num6;
						}
					}
					else
					{
						this.visited[point.x, point.y] = -1;
					}
				}
			}
		}
		return null;
	}

	// Token: 0x06006398 RID: 25496 RVA: 0x00224632 File Offset: 0x00222832
	public PathFinder.Node FindPathDirected(List<PathFinder.Point> startList, List<PathFinder.Point> endList, int depth = 2147483647)
	{
		if (startList.Count == 0 || endList.Count == 0)
		{
			return null;
		}
		return this.FindPathReversed(endList, startList, depth);
	}

	// Token: 0x06006399 RID: 25497 RVA: 0x0022464F File Offset: 0x0022284F
	public PathFinder.Node FindPathUndirected(List<PathFinder.Point> startList, List<PathFinder.Point> endList, int depth = 2147483647)
	{
		if (startList.Count == 0 || endList.Count == 0)
		{
			return null;
		}
		if (startList.Count > endList.Count)
		{
			return this.FindPathReversed(endList, startList, depth);
		}
		return this.FindPathReversed(startList, endList, depth);
	}

	// Token: 0x0600639A RID: 25498 RVA: 0x00224684 File Offset: 0x00222884
	private PathFinder.Node FindPathReversed(List<PathFinder.Point> startList, List<PathFinder.Point> endList, int depth = 2147483647)
	{
		if (this.visited == null)
		{
			this.visited = new int[this.costmap.GetLength(0), this.costmap.GetLength(1)];
		}
		else
		{
			Array.Clear(this.visited, 0, this.visited.Length);
		}
		int num = 0;
		int num2 = this.costmap.GetLength(0) - 1;
		int num3 = 0;
		int num4 = this.costmap.GetLength(1) - 1;
		IntrusiveMinHeap<PathFinder.Node> intrusiveMinHeap = default(IntrusiveMinHeap<PathFinder.Node>);
		using (List<PathFinder.Point>.Enumerator enumerator = startList.GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				PathFinder.Point point = enumerator.Current;
				int num5 = this.Cost(point);
				if (num5 != 2147483647)
				{
					int heuristic = this.Heuristic(point, endList);
					intrusiveMinHeap.Add(new PathFinder.Node(point, num5, heuristic, null));
				}
				this.visited[point.x, point.y] = num5;
			}
			goto IL_206;
		}
		IL_E9:
		PathFinder.Node node = intrusiveMinHeap.Pop();
		if (node.heuristic == 0)
		{
			return node;
		}
		for (int i = 0; i < this.neighbors.Length; i++)
		{
			PathFinder.Point point2 = node.point + this.neighbors[i];
			if (point2.x >= num && point2.x <= num2 && point2.y >= num3 && point2.y <= num4)
			{
				int num6 = this.Cost(point2, node);
				if (num6 != 2147483647)
				{
					int num7 = this.visited[point2.x, point2.y];
					if (num7 == 0 || num6 < num7)
					{
						int cost = node.cost + num6;
						int heuristic2 = this.Heuristic(point2, endList);
						intrusiveMinHeap.Add(new PathFinder.Node(point2, cost, heuristic2, node));
						this.visited[point2.x, point2.y] = num6;
					}
				}
				else
				{
					this.visited[point2.x, point2.y] = -1;
				}
			}
		}
		IL_206:
		if (intrusiveMinHeap.Empty || depth-- <= 0)
		{
			return null;
		}
		goto IL_E9;
	}

	// Token: 0x0600639B RID: 25499 RVA: 0x002248C0 File Offset: 0x00222AC0
	public PathFinder.Node FindClosestWalkable(PathFinder.Point start, int depth = 2147483647)
	{
		if (this.visited == null)
		{
			this.visited = new int[this.costmap.GetLength(0), this.costmap.GetLength(1)];
		}
		else
		{
			Array.Clear(this.visited, 0, this.visited.Length);
		}
		int num = 0;
		int num2 = this.costmap.GetLength(0) - 1;
		int num3 = 0;
		int num4 = this.costmap.GetLength(1) - 1;
		if (start.x < num)
		{
			return null;
		}
		if (start.x > num2)
		{
			return null;
		}
		if (start.y < num3)
		{
			return null;
		}
		if (start.y > num4)
		{
			return null;
		}
		IntrusiveMinHeap<PathFinder.Node> intrusiveMinHeap = default(IntrusiveMinHeap<PathFinder.Node>);
		int num5 = 1;
		int heuristic = this.Heuristic(start);
		intrusiveMinHeap.Add(new PathFinder.Node(start, num5, heuristic, null));
		this.visited[start.x, start.y] = num5;
		while (!intrusiveMinHeap.Empty && depth-- > 0)
		{
			PathFinder.Node node = intrusiveMinHeap.Pop();
			if (node.heuristic == 0)
			{
				return node;
			}
			for (int i = 0; i < this.neighbors.Length; i++)
			{
				PathFinder.Point point = node.point + this.neighbors[i];
				if (point.x >= num && point.x <= num2 && point.y >= num3 && point.y <= num4)
				{
					int num6 = 1;
					if (this.visited[point.x, point.y] == 0)
					{
						int cost = node.cost + num6;
						int heuristic2 = this.Heuristic(point);
						intrusiveMinHeap.Add(new PathFinder.Node(point, cost, heuristic2, node));
						this.visited[point.x, point.y] = num6;
					}
				}
			}
		}
		return null;
	}

	// Token: 0x0600639C RID: 25500 RVA: 0x00224A94 File Offset: 0x00222C94
	public bool IsWalkable(PathFinder.Point point)
	{
		return this.costmap[point.x, point.y] != int.MaxValue;
	}

	// Token: 0x0600639D RID: 25501 RVA: 0x00224AB8 File Offset: 0x00222CB8
	public bool IsWalkableWithNeighbours(PathFinder.Point point)
	{
		if (this.costmap[point.x, point.y] == 2147483647)
		{
			return false;
		}
		for (int i = 0; i < this.neighbors.Length; i++)
		{
			PathFinder.Point point2 = point + this.neighbors[i];
			if (this.costmap[point2.x, point2.y] == 2147483647)
			{
				return false;
			}
		}
		return true;
	}

	// Token: 0x0600639E RID: 25502 RVA: 0x00224B2C File Offset: 0x00222D2C
	public PathFinder.Node Reverse(PathFinder.Node start)
	{
		PathFinder.Node node = null;
		PathFinder.Node next = null;
		for (PathFinder.Node node2 = start; node2 != null; node2 = node2.next)
		{
			if (node != null)
			{
				node.next = next;
			}
			next = node;
			node = node2;
		}
		if (node != null)
		{
			node.next = next;
		}
		return node;
	}

	// Token: 0x0600639F RID: 25503 RVA: 0x00224B64 File Offset: 0x00222D64
	public PathFinder.Node FindEnd(PathFinder.Node start)
	{
		for (PathFinder.Node node = start; node != null; node = node.next)
		{
			if (node.next == null)
			{
				return node;
			}
		}
		return start;
	}

	// Token: 0x060063A0 RID: 25504 RVA: 0x00224B8C File Offset: 0x00222D8C
	public int Cost(PathFinder.Point a)
	{
		int num = this.costmap[a.x, a.y];
		int num2 = 0;
		if (this.BlockedPointsAdditional.Contains(a))
		{
			num = int.MaxValue;
		}
		if (num == 2147483647)
		{
			return num;
		}
		if (this.PushMultiplier > 0)
		{
			int num3 = (this.PushRadius > 0) ? Mathf.Max(0, this.Heuristic(a, this.PushPoint) - this.PushRadius) : (this.PushDistance * 2);
			for (int i = 0; i < this.PushPointsAdditional.Count; i++)
			{
				num3 = Mathf.Min(num3, this.Heuristic(a, this.PushPointsAdditional[i]));
			}
			float num4 = Mathf.Max(0f, (float)(this.PushDistance - num3)) / (float)this.PushDistance;
			if (this.PushMultiplier == 2147483647)
			{
				num2 = ((num4 > 0f) ? int.MaxValue : 0);
			}
			else
			{
				num2 = Mathf.CeilToInt((float)this.PushMultiplier * num4);
			}
		}
		if (num2 == 2147483647)
		{
			return num2;
		}
		return num + num2;
	}

	// Token: 0x060063A1 RID: 25505 RVA: 0x00224C98 File Offset: 0x00222E98
	public int Cost(PathFinder.Point a, PathFinder.Node prev)
	{
		int num = this.Cost(a);
		int num2 = 0;
		if (num != 2147483647 && this.directional && prev != null && prev.next != null && this.Heuristic(a, prev.next.point) <= 1)
		{
			num2 = 10000;
		}
		return num + num2;
	}

	// Token: 0x060063A2 RID: 25506 RVA: 0x00224CE6 File Offset: 0x00222EE6
	public int Heuristic(PathFinder.Point a)
	{
		if (this.costmap[a.x, a.y] != 2147483647)
		{
			return 0;
		}
		return 1;
	}

	// Token: 0x060063A3 RID: 25507 RVA: 0x00224D0C File Offset: 0x00222F0C
	public int Heuristic(PathFinder.Point a, PathFinder.Point b)
	{
		int num = Mathf.Abs(a.x - b.x);
		int num2 = Mathf.Abs(a.y - b.y);
		if (this.diagonals)
		{
			return Mathf.Max(num, num2);
		}
		return num + num2;
	}

	// Token: 0x060063A4 RID: 25508 RVA: 0x00224D54 File Offset: 0x00222F54
	public int Heuristic(PathFinder.Point a, List<PathFinder.Point> b)
	{
		int num = int.MaxValue;
		for (int i = 0; i < b.Count; i++)
		{
			num = Mathf.Min(num, this.Heuristic(a, b[i]));
		}
		return num;
	}

	// Token: 0x060063A5 RID: 25509 RVA: 0x00224D90 File Offset: 0x00222F90
	public float Distance(PathFinder.Point a, PathFinder.Point b)
	{
		int num = a.x - b.x;
		int num2 = a.y - b.y;
		return Mathf.Sqrt((float)(num * num + num2 * num2));
	}

	// Token: 0x060063A6 RID: 25510 RVA: 0x00224DC4 File Offset: 0x00222FC4
	public static PathFinder.Point GetPoint(Vector3 worldPos, int res)
	{
		Vector3 normalized = TerrainManager.WorldToTerrainUV(worldPos);
		return new PathFinder.Point
		{
			x = Mathf.Clamp((int)(normalized.x * (float)res), 0, res - 1),
			y = Mathf.Clamp((int)(normalized.z * (float)res), 0, res - 1)
		};
	}

	// Token: 0x04005FBF RID: 24511
	private int[,] costmap;

	// Token: 0x04005FC0 RID: 24512
	private int[,] visited;

	// Token: 0x04005FC1 RID: 24513
	private PathFinder.Point[] neighbors;

	// Token: 0x04005FC2 RID: 24514
	private bool diagonals;

	// Token: 0x04005FC3 RID: 24515
	private bool directional;

	// Token: 0x04005FC4 RID: 24516
	public PathFinder.Point PushPoint;

	// Token: 0x04005FC5 RID: 24517
	public int PushRadius;

	// Token: 0x04005FC6 RID: 24518
	public int PushDistance;

	// Token: 0x04005FC7 RID: 24519
	public int PushMultiplier;

	// Token: 0x04005FC8 RID: 24520
	public List<PathFinder.Point> PushPointsAdditional = new List<PathFinder.Point>();

	// Token: 0x04005FC9 RID: 24521
	public HashSet<PathFinder.Point> BlockedPointsAdditional = new HashSet<PathFinder.Point>();

	// Token: 0x04005FCA RID: 24522
	private static PathFinder.Point[] mooreNeighbors = new PathFinder.Point[]
	{
		new PathFinder.Point(0, 1),
		new PathFinder.Point(-1, 0),
		new PathFinder.Point(1, 0),
		new PathFinder.Point(0, -1),
		new PathFinder.Point(-1, 1),
		new PathFinder.Point(1, 1),
		new PathFinder.Point(-1, -1),
		new PathFinder.Point(1, -1)
	};

	// Token: 0x04005FCB RID: 24523
	private static PathFinder.Point[] neumannNeighbors = new PathFinder.Point[]
	{
		new PathFinder.Point(0, 1),
		new PathFinder.Point(-1, 0),
		new PathFinder.Point(1, 0),
		new PathFinder.Point(0, -1)
	};

	// Token: 0x02001132 RID: 4402
	public struct Point : IEquatable<PathFinder.Point>
	{
		// Token: 0x060063A8 RID: 25512 RVA: 0x00224EE7 File Offset: 0x002230E7
		public Point(int x, int y)
		{
			this.x = x;
			this.y = y;
		}

		// Token: 0x060063A9 RID: 25513 RVA: 0x00224EF7 File Offset: 0x002230F7
		public static PathFinder.Point operator +(PathFinder.Point a, PathFinder.Point b)
		{
			return new PathFinder.Point(a.x + b.x, a.y + b.y);
		}

		// Token: 0x060063AA RID: 25514 RVA: 0x00224F18 File Offset: 0x00223118
		public static PathFinder.Point operator -(PathFinder.Point a, PathFinder.Point b)
		{
			return new PathFinder.Point(a.x - b.x, a.y - b.y);
		}

		// Token: 0x060063AB RID: 25515 RVA: 0x00224F39 File Offset: 0x00223139
		public static PathFinder.Point operator *(PathFinder.Point p, int i)
		{
			return new PathFinder.Point(p.x * i, p.y * i);
		}

		// Token: 0x060063AC RID: 25516 RVA: 0x00224F50 File Offset: 0x00223150
		public static PathFinder.Point operator /(PathFinder.Point p, int i)
		{
			return new PathFinder.Point(p.x / i, p.y / i);
		}

		// Token: 0x060063AD RID: 25517 RVA: 0x00224F67 File Offset: 0x00223167
		public static bool operator ==(PathFinder.Point a, PathFinder.Point b)
		{
			return a.Equals(b);
		}

		// Token: 0x060063AE RID: 25518 RVA: 0x00224F71 File Offset: 0x00223171
		public static bool operator !=(PathFinder.Point a, PathFinder.Point b)
		{
			return !a.Equals(b);
		}

		// Token: 0x060063AF RID: 25519 RVA: 0x00224F7E File Offset: 0x0022317E
		public override int GetHashCode()
		{
			return this.x.GetHashCode() ^ this.y.GetHashCode();
		}

		// Token: 0x060063B0 RID: 25520 RVA: 0x00224F97 File Offset: 0x00223197
		public override bool Equals(object other)
		{
			return other is PathFinder.Point && this.Equals((PathFinder.Point)other);
		}

		// Token: 0x060063B1 RID: 25521 RVA: 0x00224FAF File Offset: 0x002231AF
		public bool Equals(PathFinder.Point other)
		{
			return this.x == other.x && this.y == other.y;
		}

		// Token: 0x04005FCC RID: 24524
		public int x;

		// Token: 0x04005FCD RID: 24525
		public int y;
	}

	// Token: 0x02001133 RID: 4403
	public class Node : IMinHeapNode<PathFinder.Node>, ILinkedListNode<PathFinder.Node>
	{
		// Token: 0x1700079A RID: 1946
		// (get) Token: 0x060063B2 RID: 25522 RVA: 0x00224FCF File Offset: 0x002231CF
		// (set) Token: 0x060063B3 RID: 25523 RVA: 0x00224FD7 File Offset: 0x002231D7
		public PathFinder.Node next { get; set; }

		// Token: 0x1700079B RID: 1947
		// (get) Token: 0x060063B4 RID: 25524 RVA: 0x00224FE0 File Offset: 0x002231E0
		// (set) Token: 0x060063B5 RID: 25525 RVA: 0x00224FE8 File Offset: 0x002231E8
		public PathFinder.Node child { get; set; }

		// Token: 0x1700079C RID: 1948
		// (get) Token: 0x060063B6 RID: 25526 RVA: 0x00224FF1 File Offset: 0x002231F1
		public int order
		{
			get
			{
				return this.cost + this.heuristic;
			}
		}

		// Token: 0x060063B7 RID: 25527 RVA: 0x00225000 File Offset: 0x00223200
		public Node(PathFinder.Point point, int cost, int heuristic, PathFinder.Node next = null)
		{
			this.point = point;
			this.cost = cost;
			this.heuristic = heuristic;
			this.next = next;
		}

		// Token: 0x04005FCE RID: 24526
		public PathFinder.Point point;

		// Token: 0x04005FCF RID: 24527
		public int cost;

		// Token: 0x04005FD0 RID: 24528
		public int heuristic;
	}
}






public interface IMinHeapNode<T>
{
    T child { get; set; }
    int order { get; }
}

public interface ILinkedListNode<T>
{
    T next { get; set; }
}

public struct IntrusiveMinHeap<T> where T : IMinHeapNode<T>
{
    /// <summary>
    /// Gets a value indicating whether the heap is empty.
    /// </summary>
    public bool Empty => head == null;

    private T head;

    /// <summary>
    /// Adds an item to the heap, maintaining the min-heap property based on the item's order.
    /// </summary>
    /// <param name="item">The item to add to the heap.</param>
    public void Add(T item)
    {
        if (head == null)
        {
            head = item;
            return;
        }

        if (head.child == null && item.order <= head.order)
        {
            item.child = head;
            head = item;
            return;
        }

        T current = head;
        while (current.child != null)
        {
            T next = current.child;
            if (next.order >= item.order)
            {
                break;
            }
            current = current.child;
        }

        item.child = current.child;
        current.child = item;
    }

    /// <summary>
    /// Removes and returns the item with the smallest order from the heap.
    /// </summary>
    /// <returns>The item with the smallest order, or the default value of <typeparamref name="T"/> if the heap is empty.</returns>
    public T Pop()
    {
        T result = head;
        if (head != null)
        {
            head = head.child;
            result.child = default;
        }
        return result;
    }
}
*/