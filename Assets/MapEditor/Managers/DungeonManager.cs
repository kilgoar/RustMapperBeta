using UnityEngine;
using System.Collections; 
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RustMapEditor.Variables;

public static class DungeonManager
{
    private class PieceConfig
    {
        public GeologyItem geologyItem;
    }

    private class PlacedPiece
    {
        public GameObject prefabInstance;
        public GeologyItem geologyItem;
        public List<DungeonBaseSocket> sockets;
        public HashSet<DungeonBaseSocket> usedSockets;
		public MeshCollider[] colliders;
		public Bounds bounds;
		public int depth;
    }

    public static IEnumerator GenerateDungeon(string dungeonFilePath)
    {
        string parentName = Path.GetFileNameWithoutExtension(dungeonFilePath);
        GameObject dungeonParent = new GameObject(parentName)
        {
            tag = "Collection",
            transform = { parent = PrefabManager.PrefabParent }
        };

        // Load and parse dungeon config
        (List<PieceConfig> pieces, int maxPieces, int maxDepth, GeologyItem startPiece, HashSet<char> validSectors, HashSet<string> allowCollision) = LoadDungeonConfig(dungeonFilePath);
        if (pieces == null || maxPieces <= 0 || maxDepth <= 0 || startPiece == null || validSectors == null)
        {
            Debug.LogError("Failed to load or invalid dungeon config");
            Object.Destroy(dungeonParent);
            yield break;
        }

        // Validate sector connectivity
        if (!ValidateSectorConnectivity(pieces, startPiece, validSectors))
        {
            Debug.LogError("Invalid dungeon configuration: Non-contiguous sectors detected");
            Object.Destroy(dungeonParent);
            yield break;
        }

        // Track placed pieces and usage counts
        List<PlacedPiece> placedPieces = new List<PlacedPiece>();
        Dictionary<GeologyItem, int> usageCounts = pieces.ToDictionary(p => p.geologyItem, p => 0, new GeologyItemEqualityComparer());

        // Place the starting piece
        GameObject startInstance = GenerativeManager.SpawnFeature(startPiece, dungeonParent.transform);
        if (startInstance == null)
        {
            Debug.LogError($"Failed to spawn start piece: {startPiece.customPrefab}");
            Object.Destroy(dungeonParent);
            yield break;
        }

        Vector3 startInstanceWorldPosition = startInstance.transform.position;
        startInstance.transform.localPosition = Vector3.zero;
        dungeonParent.transform.position = startInstanceWorldPosition;

        placedPieces.Add(new PlacedPiece
        {
            prefabInstance = startInstance,
            geologyItem = startPiece,
            sockets = startInstance.GetComponentsInChildren<DungeonBaseSocket>().ToList(),
            usedSockets = new HashSet<DungeonBaseSocket>(),
            depth = 0 // Start piece is at depth 0
        });
        usageCounts[startPiece]++;

        // Collect open sockets
        List<(DungeonBaseSocket socket, PlacedPiece piece)> openSockets = new List<(DungeonBaseSocket, PlacedPiece)>();
        foreach (var socket in placedPieces[0].sockets)
        {
            openSockets.Add((socket, placedPieces[0]));
        }

        // Generate dungeon
        int totalPlaced = 1;
        int socketIndex = 0; // Iterate through sockets sequentially
        int piecesSinceLastYield = 0; // Track pieces placed since last yield
        while (openSockets.Count > 0 && totalPlaced < maxPieces)
        {
            if (socketIndex >= openSockets.Count)
            {
                socketIndex = 0; // Reset to start if we reach the end
                if (openSockets.All(s => s.piece.usedSockets.Contains(s.socket)))
                {
                    break; // All sockets are used, exit
                }
            }

            var (currentSocket, currentPiece) = openSockets[socketIndex];
            if (currentPiece.usedSockets.Contains(currentSocket))
            {
                socketIndex++;
                continue;
            }

            // Try to connect a piece to this socket
            HashSet<GeologyItem> triedPieces = new HashSet<GeologyItem>();
            bool connected = false;

			// Filter pieces by usage, sector compatibility, piece depth range, and dungeon max depth
			int targetDepth = currentPiece.depth + 1;
			var availablePieces = pieces
				.Where(p => usageCounts[p.geologyItem] < p.geologyItem.maximum &&
							p.geologyItem.CanConnectTo(currentPiece.geologyItem) &&
							targetDepth >= p.geologyItem.minDepth &&
							targetDepth <= p.geologyItem.maxDepth &&
							targetDepth <= maxDepth)
				.ToList();

            // Shuffle available pieces for random selection
            availablePieces = availablePieces.OrderBy(_ => Random.value).ToList();

            foreach (var selectedPiece in availablePieces)
            {
                if (triedPieces.Contains(selectedPiece.geologyItem))
                    continue;

                if (TryConnectPiece(pieces, currentSocket, currentPiece, placedPieces, usageCounts, openSockets, dungeonParent.transform, allowCollision))
                {
                    // Set depth of the new piece
                    placedPieces.Last().depth = currentPiece.depth + 1;
                    currentPiece.usedSockets.Add(currentSocket);
                    connected = true;
                    totalPlaced++;
                    piecesSinceLastYield++;

                    // Yield every 4 pieces
                    if (piecesSinceLastYield >= 4)
                    {
                        piecesSinceLastYield = 0; // Reset counter
                        yield return null; // Wait for the next frame
                    }
                    break; // Successfully connected, move to next socket
                }

                triedPieces.Add(selectedPiece.geologyItem);
            }

            if (!connected)
            {
                // No piece could be connected to this socket
                currentPiece.usedSockets.Add(currentSocket);
                openSockets.RemoveAt(socketIndex);
            }
            else
            {
                socketIndex++; // Move to next socket only if a piece was placed
            }
        }

        Debug.Log($"Dungeon generated with {placedPieces.Count} pieces under {dungeonParent.name}, max depth reached: {placedPieces.Max(p => p.depth)}");
    }
	
	public static void StartDungeon(string path){
		CoroutineManager.Instance.StartRuntimeCoroutine(GenerateDungeon(path));
	}

	private static (List<PieceConfig>, int, int, GeologyItem, HashSet<char>, HashSet<string>) LoadDungeonConfig(string filePath)
	{
		List<PieceConfig> pieces = new List<PieceConfig>();
		int maxPieces = 0;
		int maxDepth = int.MaxValue; // Dungeon-wide max depth
		GeologyItem startPiece = null;
		HashSet<char> validSectors = new HashSet<char>();
		HashSet<string> allowCollision = new HashSet<string>();

		try
		{
			string[] lines = File.ReadAllLines(filePath);
			foreach (string line in lines)
			{
				string trimmedLine = line.Trim();
				if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
				{
					if (trimmedLine.StartsWith("# Max pieces:"))
					{
						if (!int.TryParse(Regex.Match(trimmedLine, @"\d+").Value, out maxPieces) || maxPieces <= 0)
						{
							Debug.LogError($"Invalid max pieces in {filePath}: {trimmedLine}");
							return (null, 0, 0, null, null, null);
						}
					}
					else if (trimmedLine.StartsWith("# Max depth:"))
					{
						if (!int.TryParse(Regex.Match(trimmedLine, @"\d+").Value, out maxDepth) || maxDepth <= 0)
						{
							Debug.LogError($"Invalid dungeon max depth in {filePath}: {trimmedLine}");
							return (null, 0, 0, null, null, null);
						}
					}
					else if (trimmedLine.StartsWith("# Start piece:"))
					{
						string[] parts = trimmedLine.Replace("# Start piece:", "").Trim().Split(',');
						if (parts.Length < 4)
						{
							Debug.LogError($"Invalid start piece format in {filePath}: {trimmedLine}");
							return (null, 0, 0, null, null, null);
						}
						string path = parts[0].Trim();
						int emphasis = parts.Length > 1 && int.TryParse(parts[1].Trim(), out int e) ? e : 1;
						int maximum = parts.Length > 2 && int.TryParse(parts[2].Trim(), out int m) ? m : int.MaxValue;
						string sectors = parts.Length > 3 ? parts[3].Trim() : "";
						int minDepth = parts.Length > 4 && int.TryParse(parts[4].Trim(), out int minD) ? minD : 0;
						int maxDepthPiece = parts.Length > 5 && int.TryParse(parts[5].Trim(), out int maxD) ? maxD : int.MaxValue;
						if (minDepth > 0)
						{
							Debug.LogError($"Start piece must have minDepth <= 0, got {minDepth} in {filePath}: {trimmedLine}");
							return (null, 0, 0, null, null, null);
						}
						if (minDepth > maxDepthPiece)
						{
							Debug.LogError($"Start piece minDepth ({minDepth}) cannot be greater than maxDepth ({maxDepthPiece}) in {filePath}: {trimmedLine}");
							return (null, 0, 0, null, null, null);
						}
						if (maxDepthPiece > maxDepth)
						{
							Debug.LogError($"Start piece maxDepth ({maxDepthPiece}) cannot exceed dungeon max depth ({maxDepth}) in {filePath}: {trimmedLine}");
							return (null, 0, 0, null, null, null);
						}
						startPiece = new GeologyItem(path, emphasis, maximum, sectors, minDepth, maxDepthPiece);
						foreach (char c in sectors)
							validSectors.Add(c);
					}
					else if (trimmedLine.StartsWith("# Sectors:"))
					{
						string sectors = trimmedLine.Replace("# Sectors:", "").Trim();
						foreach (char c in sectors)
							validSectors.Add(c);
					}
					else if (trimmedLine.StartsWith("# AllowCollision:"))
					{
						string allowed = trimmedLine.Replace("# AllowCollision:", "").Trim();
						foreach (string prefab in allowed.Split(',').Select(s => s.Trim()))
						{
							if (!string.IsNullOrEmpty(prefab))
								allowCollision.Add(prefab);
							Debug.Log(prefab + " marked for no collisions");
						}
					}
					continue;
				}

				// Parse piece config: path [emphasis,maximum,sectors,minDepth,maxDepth];
				var match = Regex.Match(trimmedLine, @"^(.+?)\s*\[(\d+),(\d+),([A-Za-z]*),(\d+),(\d+)\];$");
				if (match.Success)
				{
					string path = match.Groups[1].Value.Trim();
					int emphasis = int.Parse(match.Groups[2].Value);
					int maximum = int.Parse(match.Groups[3].Value);
					string sectors = match.Groups[4].Value;
					int minDepth = int.Parse(match.Groups[5].Value);
					int maxDepthPiece = int.Parse(match.Groups[6].Value);
					if (minDepth > maxDepthPiece)
					{
						Debug.LogError($"Piece minDepth ({minDepth}) cannot be greater than maxDepth ({maxDepthPiece}) in {filePath}: {trimmedLine}");
						return (null, 0, 0, null, null, null);
					}
					if (maxDepthPiece > maxDepth)
					{
						Debug.LogError($"Piece maxDepth ({maxDepthPiece}) cannot exceed dungeon max depth ({maxDepth}) in {filePath}: {trimmedLine}");
						return (null, 0, 0, null, null, null);
					}
					pieces.Add(new PieceConfig
					{
						geologyItem = new GeologyItem(path, emphasis, maximum, sectors, minDepth, maxDepthPiece)
					});
					foreach (char c in sectors)
						validSectors.Add(c);
				}
				else
				{
					Debug.LogWarning($"Invalid line format in {filePath}: {trimmedLine}");
				}
			}

			if (maxPieces <= 0)
			{
				Debug.LogError("Max pieces not specified or invalid");
				return (null, 0, 0, null, null, null);
			}

			if (maxDepth <= 0)
			{
				Debug.LogError("Max depth not specified or invalid");
				return (null, 0, 0, null, null, null);
			}

			if (startPiece == null)
			{
				Debug.LogError("Start piece not specified");
				return (null, 0, 0, null, null, null);
			}

			if (validSectors.Count == 0)
			{
				Debug.LogWarning("No sectors defined; pieces can connect freely");
			}

			return (pieces, maxPieces, maxDepth, startPiece, validSectors, allowCollision);
		}
		catch (System.Exception ex)
		{
			Debug.LogError($"Failed to load dungeon config from {filePath}: {ex.Message}");
			return (null, 0, 0, null, null, null);
		}
	}

    // Validate that sectors allow a connected dungeon
    private static bool ValidateSectorConnectivity(List<PieceConfig> pieces, GeologyItem startPiece, HashSet<char> validSectors)
    {
        if (validSectors.Count == 0)
            return true; // No sectors defined, all pieces can connect

        // Build a graph of sectors based on pieces that can connect them
        Dictionary<char, HashSet<char>> sectorGraph = new Dictionary<char, HashSet<char>>();
        foreach (char sector in validSectors)
            sectorGraph[sector] = new HashSet<char>();

        foreach (var piece in pieces.Concat(new[] { new PieceConfig { geologyItem = startPiece } }))
        {
            var sectors = piece.geologyItem.sectors;
            for (int i = 0; i < sectors.Length; i++)
            {
                for (int j = i + 1; j < sectors.Length; j++)
                {
                    sectorGraph[sectors[i]].Add(sectors[j]);
                    sectorGraph[sectors[j]].Add(sectors[i]);
                }
            }
        }

        // Perform DFS to check if all sectors are reachable from startPiece's sectors
        HashSet<char> reachable = new HashSet<char>();
        Queue<char> queue = new Queue<char>();
        foreach (char c in startPiece.sectors)
        {
            queue.Enqueue(c);
            reachable.Add(c);
        }

        while (queue.Count > 0)
        {
            char current = queue.Dequeue();
            foreach (char neighbor in sectorGraph[current])
            {
                if (!reachable.Contains(neighbor))
                {
                    reachable.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return reachable.Count == validSectors.Count;
    }

private static bool TryConnectPiece(List<PieceConfig> pieces, DungeonBaseSocket currentSocket, PlacedPiece currentPiece,
    List<PlacedPiece> placedPieces, Dictionary<GeologyItem, int> usageCounts,
    List<(DungeonBaseSocket socket, PlacedPiece piece)> openSockets, Transform parent, HashSet<string> allowCollision)
	
{
    // Calculate the depth for the new piece
    int targetDepth = currentPiece.depth + 1;

    // Filter pieces by usage, sector compatibility, piece depth range, and dungeon max depth
    var availablePieces = pieces
        .Where(p => usageCounts[p.geologyItem] < p.geologyItem.maximum &&
                    p.geologyItem.CanConnectTo(currentPiece.geologyItem) &&
                    targetDepth >= p.geologyItem.minDepth &&
                    targetDepth <= p.geologyItem.maxDepth)
        .ToList();
    if (availablePieces.Count == 0)
    {
        return false;
    }

    // Weighted random selection
    int totalWeight = availablePieces.Sum(p => p.geologyItem.emphasis);
    if (totalWeight <= 0)
    {
        return false;
    }

    int randomWeight = Random.Range(0, totalWeight);
    int currentWeight = 0;
    PieceConfig selectedPiece = null;
    foreach (var piece in availablePieces)
    {
        currentWeight += piece.geologyItem.emphasis;
        if (randomWeight < currentWeight)
        {
            selectedPiece = piece;
            break;
        }
    }

    if (selectedPiece == null)
    {
        return false;
    }

    GameObject tempInstance = GenerativeManager.SpawnFeature(selectedPiece.geologyItem, parent);
    if (tempInstance == null)
    {
        Debug.LogWarning($"Failed to spawn prefab for {selectedPiece.geologyItem.customPrefab}");
        return false;
    }

	//Debug.Log(tempInstance.gameObject.name + " spawned");

	var newSockets = tempInstance.GetComponentsInChildren<DungeonBaseSocket>()
		.Where(socket => socket.gameObject.layer == 13)
		.ToList();

	if (newSockets.Count == 0)
	{
		Debug.LogWarning($"No sockets found in 'SocketInfo' on {tempInstance.gameObject.name}");
		pieces.Remove(selectedPiece);
		Object.Destroy(tempInstance);
		return false;
	}

    // Cache new colliders for efficiency
    MeshCollider[] newColliders = tempInstance.GetComponentsInChildren<MeshCollider>();


    var shuffledNewSockets = newSockets.OrderBy(_ => Random.value).ToList();
    foreach (var targetSocket in shuffledNewSockets)
    {
        if (SocketManager.IsValidConnection(currentSocket, targetSocket))
        {
            // Align the piece using socket attachment
            SocketManager.AttachSockets(currentSocket, targetSocket);

			bool testCollision = true;
			if (allowCollision.Any(allowed => tempInstance.name.Contains(allowed)))	{
				testCollision = false;
			}
			
            bool hasCollision = false;

			if(testCollision){
				//collect colliders and test for collisions
				foreach (var placedPiece in placedPieces)				{
							

					
						MeshCollider[] existingColliders = placedPiece.prefabInstance.GetComponentsInChildren<MeshCollider>();
						if (existingColliders.Length == 0)
						{
							Debug.LogWarning($"No MeshColliders found on placed piece {placedPiece.geologyItem.customPrefab}");
							continue;
						}

						foreach (var newCollider in newColliders)
						{
								if (newCollider.gameObject.layer != 3)	{
									continue;
								}

							if (allowCollision.Contains(newCollider.gameObject.name)) {
								continue;
							}
							
							//if either new collider or existing collider's game object name is in allowedCollision hash set skip to next collider
							foreach (var existingCollider in existingColliders)
							{
									if (newCollider.gameObject.layer != 3)	{
										continue;
									}
								
								
								Vector3 direction;
								float distance;
								if (Physics.ComputePenetration(
									newCollider, newCollider.transform.position, newCollider.transform.rotation,
									existingCollider, existingCollider.transform.position, existingCollider.transform.rotation,
									out direction, out distance))
								{
									//Debug.Log("penetration detected of magnitude" + distance);
									// Define a threshold for acceptable penetration
									float penetrationThreshold = 0.1f; // Adjust this value as needed (e.g., 0.01 units)
									if (distance > penetrationThreshold)
									{
										hasCollision = true;
										break;
									}
								}
							}
							if (hasCollision) break;
						}
						if (hasCollision) break;
					
				}
			}

            if (!hasCollision)
            {
                // No collisions detected, finalize placement
                placedPieces.Add(new PlacedPiece
                {
                    prefabInstance = tempInstance,
                    geologyItem = selectedPiece.geologyItem,
                    sockets = newSockets,
                    usedSockets = new HashSet<DungeonBaseSocket> { targetSocket },
                    colliders = newColliders // Cache colliders for future checks
                });
                usageCounts[selectedPiece.geologyItem]++;

                foreach (var socket in newSockets)
                {
                    if (socket != targetSocket)
                    {
                        openSockets.Add((socket, placedPieces.Last()));
                    }
                }

                return true;
            }
            else
            {
                //Debug.Log($"Collision detected for {selectedPiece.geologyItem.customPrefab} at socket {targetSocket.gameObject.name}");
            }
        }
    }

    Object.Destroy(tempInstance);
    return false;
}


    private class GeologyItemEqualityComparer : IEqualityComparer<GeologyItem>
    {
        public bool Equals(GeologyItem x, GeologyItem y)
        {
            if (x == null || y == null) return false;
            return x.custom == y.custom && x.prefabID == y.prefabID && x.customPrefab == y.customPrefab;
        }

        public int GetHashCode(GeologyItem obj)
        {
            return (obj.custom, obj.prefabID, obj.customPrefab).GetHashCode();
        }
    }
}