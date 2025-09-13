using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RustMapEditor.Variables;

public static class LayoutManager
{
    private class POIConfig
    {
        public POIItem poiItem;
    }
	
	public class POIItem
	{
		public List<Vector2> spawnArea;
		
		public string customPrefab;
		public int? requiredCount;
		public int weight;
		public int minCount;
		public int maxCount;
		public string biomes; // Valid biomes (e.g., "TD" for Temperate and Arid)
		public string tiers;
		public float minPOIDistance;
		public float maxPOIDistance;
		public int terrainTolerance;
		public int minHeight;
		public int maxHeight;
		public bool isOcean; // True if OCEAN tag is present
		public bool isTunnelSnap; // True if TUNNELSNAP tag is present
		public bool isWaterfront; // True if WATERFRONT tag is present
		public bool isLockRotate; //when true rotations are 0,0,0

		public POIItem(string customPrefab, int? requiredCount, int weight, int minCount, int maxCount, string biomes, string tiers,
					   float minPOIDistance, float maxPOIDistance, int terrainTolerance, int minHeight, int maxHeight, 
					   bool isOcean, bool isTunnelSnap, bool isWaterfront, bool isLockRotate)
		{
			this.customPrefab = customPrefab;
			this.requiredCount = requiredCount;
			this.weight = weight;
			this.minCount = minCount;
			this.maxCount = maxCount;
			this.biomes = biomes;
			this.tiers = tiers;
			this.minPOIDistance = minPOIDistance;
			this.maxPOIDistance = maxPOIDistance;
			this.terrainTolerance = terrainTolerance;
			this.minHeight = minHeight;
			this.maxHeight = maxHeight;
			this.isOcean = isOcean;
			this.isTunnelSnap = isTunnelSnap;
			this.isWaterfront = isWaterfront;
			this.isLockRotate = isLockRotate;
		}
		
		public bool CanPlaceInBiome(char biome)
		{
			return string.IsNullOrEmpty(biomes) || biomes.Contains(biome);
		}

		public bool CanPlaceInTier(char tier)
		{
			return string.IsNullOrEmpty(tiers) || tiers.Contains(tier);
		}

		public bool CanPlaceAtHeight(int height)
		{
			return height >= minHeight && height <= maxHeight;
		}

		public (int min, int max) GetCountConstraints()
		{
			if (requiredCount.HasValue)
				return (requiredCount.Value, requiredCount.Value);
			return (minCount, maxCount);
		}
		
		public void MakePointList()
		{
			TerrainManager.SyncTerrainResolutions();

			// Resolution checks
			if (TerrainManager.Height == null || TerrainManager.Height.GetLength(0) != TerrainManager.HeightMapRes || TerrainManager.Height.GetLength(1) != TerrainManager.HeightMapRes)
			{
				Debug.LogError($"Height map is null or has incorrect dimensions: Expected {TerrainManager.HeightMapRes}x{TerrainManager.HeightMapRes}, Got {(TerrainManager.Height != null ? $"{TerrainManager.Height.GetLength(0)}x{TerrainManager.Height.GetLength(1)}" : "null")}");
				spawnArea = new List<Vector2>();
				return;
			}
			if (TerrainManager.Biome == null || TerrainManager.Biome.GetLength(0) != TerrainManager.SplatMapRes || TerrainManager.Biome.GetLength(1) != TerrainManager.SplatMapRes)
			{
				Debug.LogError($"Biome map is null or has incorrect dimensions: Expected {TerrainManager.SplatMapRes}x{TerrainManager.SplatMapRes}, Got {(TerrainManager.Biome != null ? $"{TerrainManager.Biome.GetLength(0)}x{TerrainManager.Biome.GetLength(1)}" : "null")}");
				spawnArea = new List<Vector2>();
				return;
			}
			if (TerrainManager.Topology == null || TerrainManager.Topology.Any(t => t == null || t.GetLength(0) != TerrainManager.SplatMapRes || t.GetLength(1) != TerrainManager.SplatMapRes))
			{
				Debug.LogError($"Topology map is null or has incorrect dimensions: Expected {TerrainManager.SplatMapRes}x{TerrainManager.SplatMapRes} per layer, Got {(TerrainManager.Topology != null ? $"Length={TerrainManager.Topology.Length}, FirstLayer={(TerrainManager.Topology.Length > 0 && TerrainManager.Topology[0] != null ? $"{TerrainManager.Topology[0].GetLength(0)}x{TerrainManager.Topology[0].GetLength(1)}" : "null")}" : "null")}");
				spawnArea = new List<Vector2>();
				return;
			}
			if (TerrainManager.SplatMapRes == 0)
			{
				Debug.LogError($"SplatMapRes is zero, preventing division in resRatio calculation.");
				spawnArea = new List<Vector2>();
				return;
			}

			spawnArea = new List<Vector2>();
			int resRatio = TerrainManager.HeightMapRes / TerrainManager.SplatMapRes;
			float[,,] biomeMap = TerrainManager.Biome;
			float[][,,] topologies = TerrainManager.Topology;

			// Implement stride: process every 'stride' heightmap points (default 1 for full resolution)
			int stride = 8; // You can set this based on desired density or performance needs

			Parallel.For(0, TerrainManager.HeightMapRes - 1, (i, loopState) =>
			{
				int iResRatio = i / resRatio;
				for (int j = 0; j < TerrainManager.HeightMapRes - 1; j += stride)
				{
					int jResRatio = j / resRatio;

					// Step 1: Check height range
					int height = (int)(TerrainManager.Height[i, j]*1000f);
					if (!CanPlaceAtHeight(height))
						continue;
					
					
					// Step 2: Check biomes - include if ANY allowed biome is dominant (≥0.25)
					bool biomeMatch = false;
					if (CanPlaceInBiome('D') && biomeMap[iResRatio, jResRatio, 0] >= 0.25f) biomeMatch = true;
					else if (CanPlaceInBiome('T') && biomeMap[iResRatio, jResRatio, 1] >= 0.25f) biomeMatch = true;
					else if (CanPlaceInBiome('U') && biomeMap[iResRatio, jResRatio, 2] >= 0.25f) biomeMatch = true;
					else if (CanPlaceInBiome('A') && biomeMap[iResRatio, jResRatio, 3] >= 0.25f) biomeMatch = true;
					else if (CanPlaceInBiome('J') && biomeMap[iResRatio, jResRatio, 4] >= 0.25f) biomeMatch = true;
					if (!biomeMatch) continue;

					// Step 3: Check tiers - include if ANY allowed tier is dominant (≥0.5)
					bool tierMatch = false;
					if (CanPlaceInTier('0') && topologies[26][iResRatio, jResRatio, 0] >= 0.5f) tierMatch = true;
					else if (CanPlaceInTier('1') && topologies[27][iResRatio, jResRatio, 0] >= 0.5f) tierMatch = true;
					else if (CanPlaceInTier('2') && topologies[28][iResRatio, jResRatio, 0] >= 0.5f) tierMatch = true;
					if (!tierMatch) continue;
					

					lock (spawnArea) // Ensure thread-safe addition
					{
						spawnArea.Add(new Vector2(i, j));
					}
				}
			});
			Debug.Log($"spawn area of size {spawnArea.Count} created");
		}
	}
	private static bool ProcessPOIBlock(List<string> lines, int startIndex, string filePath, List<POIConfig> poiConfigs)
	{
		// Parse a two-line POI block (or one line if only path and tags)
		string path = null;
		int? requiredCount = null;
		bool isOcean = false;
		bool isTunnelSnap = false;
		bool isWaterfront = false;
		bool isLockRotate = false;
		int weight = 0;
		int minCount = 0;
		int maxCount = 0;
		string biomes = "";
		string tiers = "";
		float minPOIDistance = 0f;
		float maxPOIDistance = 0f;
		int terrainTolerance = 0;
		int minHeight = 0;
		int maxHeight = 0;

		// Parse first line: - path: <path> [required: <number>] [OCEAN] [TUNNELSNAP] [WATERFRONT] [LOCKROTATE]
		if (lines.Count == 0 || !lines[0].StartsWith("- path:"))
		{
			Debug.LogError($"Invalid POI block starting at line {startIndex + 1} in {filePath}: Missing path");
			return false;
		}

		string[] firstLineParts = lines[0].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
		path = firstLineParts[1].Substring("path:".Length);
		for (int i = 2; i < firstLineParts.Length; i++)
		{
			if (firstLineParts[i].StartsWith("required:"))
			{
				if (!int.TryParse(firstLineParts[i].Substring("required:".Length), out int req) || req < 0)
				{
					Debug.LogError($"Invalid required count in {filePath} at line {startIndex + 1}: {firstLineParts[i]}");
					return false;
				}
				requiredCount = req;
			}
			else if (firstLineParts[i] == "OCEAN")
				isOcean = true;
			else if (firstLineParts[i] == "TUNNELSNAP")
				isTunnelSnap = true;
			else if (firstLineParts[i] == "WATERFRONT")
				isWaterfront = true;
			else if (firstLineParts[i] == "LOCKROTATE")
				isLockRotate = true;
			else
				Debug.LogWarning($"Unrecognized token in {filePath} at line {startIndex + 1}: {firstLineParts[i]}");
		}

		// Parse second line (if present): weight, countRange, biomes, tiers, poiDistance, terrainTolerance, heightRange
		if (lines.Count > 1)
		{
			string secondLine = lines[1];
			var parts = secondLine.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
			int partIndex = 0;

			// Helper to get next part
			string GetNextPart()
			{
				if (partIndex < parts.Length)
					return parts[partIndex++];
				Debug.LogError($"Missing field in {filePath} at line {startIndex + 2}: {secondLine}");
				return null;
			}

			// weight
			if (GetNextPart()?.StartsWith("weight:") != true || !int.TryParse(parts[partIndex - 1].Substring("weight:".Length), out weight) || weight <= 0)
			{
				Debug.LogError($"Invalid weight in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}

			// countRange
			if (GetNextPart()?.StartsWith("countRange:") != true)
			{
				Debug.LogError($"Invalid countRange in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			var countMatch = Regex.Match(parts[partIndex - 1], @"countRange:\s*\[(\d+),\s*(\d+)\]");
			if (!countMatch.Success)
			{
				Debug.LogError($"Invalid countRange format in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			minCount = int.Parse(countMatch.Groups[1].Value);
			maxCount = int.Parse(countMatch.Groups[2].Value);

			// biomes
			if (GetNextPart()?.StartsWith("biomes:") != true)
			{
				Debug.LogError($"Invalid biomes in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			biomes = parts[partIndex - 1].Substring("biomes:".Length).Replace("\"", "");

			// tiers
			if (GetNextPart()?.StartsWith("tiers:") != true)
			{
				Debug.LogError($"Invalid tiers in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			tiers = parts[partIndex - 1].Substring("tiers:".Length).Replace("\"", "");

			// poiDistance
			if (GetNextPart()?.StartsWith("poiDistance:") != true)
			{
				Debug.LogError($"Invalid poiDistance in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			var distanceMatch = Regex.Match(parts[partIndex - 1], @"poiDistance:\s*\[(\d*\.?\d*),\s*(\d*\.?\d*)\]");
			if (!distanceMatch.Success)
			{
				Debug.LogError($"Invalid poiDistance format in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			minPOIDistance = float.Parse(distanceMatch.Groups[1].Value);
			maxPOIDistance = float.Parse(distanceMatch.Groups[2].Value);

			// terrainTolerance
			if (GetNextPart()?.StartsWith("terrainTolerance:") != true || !int.TryParse(parts[partIndex - 1].Substring("terrainTolerance:".Length), out terrainTolerance))
			{
				Debug.LogError($"Invalid terrainTolerance in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}

			// heightRange
			if (GetNextPart()?.StartsWith("heightRange:") != true)
			{
				Debug.LogError($"Invalid heightRange in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			var heightMatch = Regex.Match(parts[partIndex - 1], @"heightRange:\s*\[(\d+),\s*(\d+)\]");
			if (!heightMatch.Success)
			{
				Debug.LogError($"Invalid heightRange format in {filePath} at line {startIndex + 2}: {secondLine}");
				return false;
			}
			minHeight = int.Parse(heightMatch.Groups[1].Value);
			maxHeight = int.Parse(heightMatch.Groups[2].Value);
		}

		// Validation (same as original)
		if (string.IsNullOrEmpty(path))
		{
			Debug.LogError($"Missing path in POI block starting at line {startIndex + 1} in {filePath}");
			return false;
		}
		if (requiredCount.HasValue)
		{
			if (requiredCount.Value < 0)
			{
				Debug.LogError($"REQUIRED count cannot be negative in {filePath} at line {startIndex + 1}");
				return false;
			}
			if (minCount != maxCount || minCount != requiredCount.Value)
			{
				Debug.LogWarning($"REQUIRED:{requiredCount.Value} specified for {path}, ignoring countRange [{minCount},{maxCount}]");
				minCount = maxCount = requiredCount.Value;
			}
		}
		else
		{
			if (minCount < 0)
			{
				Debug.LogError($"minCount cannot be negative in {filePath} at line {startIndex + 1}");
				return false;
			}
			if (maxCount < minCount)
			{
				Debug.LogError($"maxCount ({maxCount}) cannot be less than minCount ({minCount}) in {filePath} at line {startIndex + 1}");
				return false;
			}
		}
		if (weight <= 0)
		{
			Debug.LogError($"Invalid weight in {filePath} at line {startIndex + 1}");
			return false;
		}
		if (!string.IsNullOrEmpty(biomes) && !Regex.IsMatch(biomes, @"^[TDUAJ]*$"))
		{
			Debug.LogError($"Invalid biomes ({biomes}) in {filePath} at line {startIndex + 1}. Must contain only T, D, U, A, J.");
			return false;
		}
		if (!string.IsNullOrEmpty(tiers) && !Regex.IsMatch(tiers, @"^[0-2]*$"))
		{
			Debug.LogError($"Invalid tiers ({tiers}) in {filePath} at line {startIndex + 1}. Must contain only 0, 1, 2.");
			return false;
		}
		if (minPOIDistance < 0)
		{
			Debug.LogError($"minPOIDistance cannot be negative in {filePath} at line {startIndex + 1}");
			return false;
		}
		if (terrainTolerance < 0 || terrainTolerance > 100)
		{
			Debug.LogError($"terrainTolerance ({terrainTolerance}) must be between 0 and 100 in {filePath} at line {startIndex + 1}");
			return false;
		}
		if (minHeight < 0 || minHeight > 1000)
		{
			Debug.LogError($"minHeight ({minHeight}) must be between 0 and 1000 in {filePath} at line {startIndex + 1}");
			return false;
		}
		if (maxHeight < minHeight || maxHeight > 1000)
		{
			Debug.LogError($"maxHeight ({maxHeight}) must be between {minHeight} and 1000 in {filePath} at line {startIndex + 1}");
			return false;
		}


		poiConfigs.Add(new POIConfig
		{
			poiItem = new POIItem(path, requiredCount, weight, minCount, maxCount, biomes, tiers, minPOIDistance, maxPOIDistance, terrainTolerance, minHeight, maxHeight, isOcean, isTunnelSnap, isWaterfront, isLockRotate)
		});
		return true;
	}

    public static void LoadLayout(string layoutFilePath)
    {
        var config = LoadLayoutConfig(layoutFilePath);
        if (config.poiConfigs == null)
        {
            Debug.LogError("Failed to load layout config. Aborting.");
            return;
        }

        Debug.Log($"Loaded layout config: {config.poiConfigs.Count} POIs, Min POIs: {config.minPOIs}, Max POIs: {config.maxPOIs}");
        // Further processing (e.g., generation) can be added here
    }

	private static (List<POIConfig> poiConfigs, int minPOIs, int maxPOIs) LoadLayoutConfig(string filePath)
	{
		List<POIConfig> poiConfigs = new List<POIConfig>();
		int minPOIs = 0;
		int maxPOIs = 0;
		List<string> currentIncludeBlock = null;
		int? includeCount = null;

		try
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError($"Layout file not found: {filePath}");
				return (null, 0, 0);
			}

			string[] lines = File.ReadAllLines(filePath);
			for (int i = 0; i < lines.Length; i++)
			{
				string trimmedLine = lines[i].Trim();
				if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
					continue;

				if (trimmedLine.StartsWith("- Min POIs:"))
				{
					if (!int.TryParse(Regex.Match(trimmedLine, @"\d+").Value, out minPOIs) || minPOIs < 0)
					{
						Debug.LogError($"Invalid min POIs in {filePath}: {trimmedLine}");
						return (null, 0, 0);
					}
					continue;
				}
				else if (trimmedLine.StartsWith("- Max POIs:"))
				{
					if (!int.TryParse(Regex.Match(trimmedLine, @"\d+").Value, out maxPOIs) || maxPOIs < minPOIs)
					{
						Debug.LogError($"Invalid max POIs in {filePath}: {trimmedLine}");
						return (null, 0, 0);
					}
					continue;
				}
				else if (trimmedLine.StartsWith("- INCLUDE:"))
				{
					if (!int.TryParse(Regex.Match(trimmedLine, @"\d+").Value, out int count) || count < 0)
					{
						Debug.LogError($"Invalid INCLUDE count in {filePath}: {trimmedLine}");
						return (null, 0, 0);
					}
					includeCount = count;
					currentIncludeBlock = new List<string>();
					int startLine = i;
					i++;
					while (i < lines.Length && !lines[i].Trim().StartsWith("}"))
					{
						if (!string.IsNullOrEmpty(lines[i].Trim()) && !lines[i].Trim().StartsWith("#"))
							currentIncludeBlock.Add(lines[i].Trim());
						i++;
					}
					if (i >= lines.Length || !lines[i].Trim().StartsWith("}"))
					{
						Debug.LogError($"Missing closing brace for INCLUDE block starting at line {startLine + 1} in {filePath}");
						return (null, 0, 0);
					}

					// Split into POI blocks (each up to 2 lines)
					List<List<string>> poiBlocks = new List<List<string>>();
					int blockStart = 0;
					for (int j = 0; j < currentIncludeBlock.Count; j++)
					{
						if (currentIncludeBlock[j].StartsWith("- path:"))
						{
							if (j > blockStart)
								poiBlocks.Add(currentIncludeBlock.GetRange(blockStart, j - blockStart));
							blockStart = j;
						}
					}
					if (blockStart < currentIncludeBlock.Count)
						poiBlocks.Add(currentIncludeBlock.GetRange(blockStart, currentIncludeBlock.Count - blockStart));

					if (poiBlocks.Count < includeCount)
					{
						Debug.LogError($"INCLUDE block at line {startLine + 1} has fewer items ({poiBlocks.Count}) than required ({includeCount}) in {filePath}");
						return (null, 0, 0);
					}

					// Randomly select includeCount POIs
					poiBlocks = poiBlocks.OrderBy(x => Random.value).Take(includeCount.Value).ToList();
					foreach (var block in poiBlocks)
					{
						if (!ProcessPOIBlock(block, startLine + 1, filePath, poiConfigs))
							return (null, 0, 0);
					}
					currentIncludeBlock = null;
					includeCount = null;
					continue;
				}
				else if (trimmedLine.StartsWith("- path:"))
				{
					// Collect up to 2 lines for a POI block
					List<string> poiBlock = new List<string> { trimmedLine };
					int startLine = i;
					if (i + 1 < lines.Length && !lines[i + 1].Trim().StartsWith("-") && !lines[i + 1].Trim().StartsWith("}") && !lines[i + 1].Trim().StartsWith("#"))
					{
						poiBlock.Add(lines[i + 1].Trim());
						i++;
					}
					if (!ProcessPOIBlock(poiBlock, startLine + 1, filePath, poiConfigs))
						return (null, 0, 0);
					continue;
				}
			}

			if (minPOIs < 0)
			{
				Debug.LogError($"Min POIs not specified or invalid in {filePath}");
				return (null, 0, 0);
			}

			if (maxPOIs < minPOIs)
			{
				Debug.LogError($"Max POIs ({maxPOIs}) cannot be less than Min POIs ({minPOIs}) in {filePath}");
				return (null, 0, 0);
			}

			if (poiConfigs.Count == 0)
			{
				Debug.LogError($"No valid POI configurations found in {filePath}");
				return (null, 0, 0);
			}

			// Validate that required POIs can fit within minPOIs and maxPOIs
			int totalRequired = poiConfigs.Where(p => p.poiItem.requiredCount.HasValue).Sum(p => p.poiItem.requiredCount.Value);
			if (totalRequired > maxPOIs)
			{
				Debug.LogError($"Total REQUIRED count ({totalRequired}) exceeds maxPOIs ({maxPOIs}) in {filePath}");
				return (null, 0, 0);
			}
			if (totalRequired > minPOIs)
			{
				Debug.LogWarning($"Total REQUIRED count ({totalRequired}) exceeds minPOIs ({minPOIs}). Adjusting minPOIs to {totalRequired}.");
				minPOIs = totalRequired;
			}

			return (poiConfigs, minPOIs, maxPOIs);
		}
		catch (System.Exception ex)
		{
			Debug.LogError($"Failed to load layout config from {filePath}: {ex.Message}");
			return (null, 0, 0);
		}
	}

public static List<POIItem> GenerateLayout(string layoutFilePath)
	{
		var config = LoadLayoutConfig(layoutFilePath);
		if (config.poiConfigs == null)
		{
			Debug.LogError("Failed to generate layout: Invalid or missing POI configurations.");
			return new List<POIItem>();
		}
		
		// Generate point lists for all POI items
		foreach (var poiConfig in config.poiConfigs)	{
			poiConfig.poiItem.MakePointList();
		}

		// Create the queue for placement, prioritizing REQUIRED items
		List<POIItem> placementQueue = new List<POIItem>();
		int totalPlaced = 0;

		// Step 1: Add REQUIRED items
		foreach (var poiConfig in config.poiConfigs.Where(p => p.poiItem.requiredCount.HasValue))
		{
			var poiItem = poiConfig.poiItem;
			int count = poiItem.requiredCount.Value;
			if (count > 0)
			{
				for (int i = 0; i < count; i++)
				{
					placementQueue.Add(poiItem);
				}
				totalPlaced += count;
			}
		}

		// Step 2: Add non-REQUIRED items to fill up to minPOIs, then up to maxPOIs
		var nonRequiredPOIs = config.poiConfigs
			.Where(p => !p.poiItem.requiredCount.HasValue)
			.Select(p => p.poiItem)
			.ToList();

		// Calculate weights for non-REQUIRED items
		int totalWeight = nonRequiredPOIs.Sum(p => p.weight);
		if (totalWeight == 0)
		{
			Debug.LogWarning("No valid weights for non-required POIs. Only REQUIRED items will be placed.");
		}
		else
		{
			// Fill up to minPOIs
			while (totalPlaced < config.minPOIs && nonRequiredPOIs.Any())
			{
				var selectedPOI = SelectWeightedPOI(nonRequiredPOIs, totalWeight);
				if (selectedPOI != null)
				{
					var (min, max) = selectedPOI.GetCountConstraints();
					int currentCount = placementQueue.Count(p => p == selectedPOI);
					if (currentCount < max)
					{
						placementQueue.Add(selectedPOI);
						totalPlaced++;
					}
					else
					{
						nonRequiredPOIs.Remove(selectedPOI);
						totalWeight -= selectedPOI.weight;
					}
				}
			}

			// Optionally fill up to maxPOIs
			while (totalPlaced < config.maxPOIs && nonRequiredPOIs.Any())
			{
				var selectedPOI = SelectWeightedPOI(nonRequiredPOIs, totalWeight);
				if (selectedPOI != null)
				{
					var (min, max) = selectedPOI.GetCountConstraints();
					int currentCount = placementQueue.Count(p => p == selectedPOI);
					if (currentCount < max)
					{
						placementQueue.Add(selectedPOI);
						totalPlaced++;
					}
					else
					{
						nonRequiredPOIs.Remove(selectedPOI);
						totalWeight -= selectedPOI.weight;
					}
				}
			}
		}

		// Shuffle the queue to randomize placement order (while preserving REQUIRED items at the start)
		int requiredCount = config.poiConfigs.Where(p => p.poiItem.requiredCount.HasValue).Sum(p => p.poiItem.requiredCount.Value);
		var requiredItems = placementQueue.Take(requiredCount).ToList();
		var nonRequiredItems = placementQueue.Skip(requiredCount).OrderBy(x => Random.value).ToList();
		placementQueue = requiredItems.Concat(nonRequiredItems).ToList();

		Debug.Log($"Generated layout with {placementQueue.Count} POI items (REQUIRED: {requiredCount}, Total: {totalPlaced}, Min: {config.minPOIs}, Max: {config.maxPOIs})");
		
		return placementQueue;

		// Helper method to select a POI based on weight
		POIItem SelectWeightedPOI(List<POIItem> pois, int totalWeight)
		{
			if (totalWeight <= 0 || !pois.Any())
				return null;

			float randomValue = Random.value * totalWeight;
			float currentWeight = 0;

			foreach (var poi in pois)
			{
				currentWeight += poi.weight;
				if (randomValue <= currentWeight)
					return poi;
			}
			return pois[pois.Count - 1]; // Fallback
		}
	}

	public static bool PlacePOI(POIItem poiItem, Transform parentContainer)
	{
		if (poiItem == null){Debug.LogWarning("poi item null"); return false;}
		if (poiItem.spawnArea == null){Debug.LogWarning("poi item area null"); return false;}
		if (poiItem.spawnArea.Count == 0){Debug.LogWarning("poi item area empty"); return false;}

		Terrain land = GameObject.FindGameObjectWithTag("Land").GetComponent<Terrain>();
		if (land == null)
		{
			Debug.LogError("No terrain found with tag 'Land'");
			return false;
		}

		float size = land.terrainData.size.x;
		float sizeZ = land.terrainData.size.y;
		int res = TerrainManager.HeightMapRes;
		int splatRes = TerrainManager.SplatMapRes;
		float resRatio = (float)res / splatRes;
		float ratio = size / splatRes;
		float halfPixelSize = ratio / 2f;

		// Define grid parameters for dungeon snapping
		float linkRadius = 3f; // Default from DungeonGridInfo
		float linkHeight = 1.5f; // Default from DungeonGridInfo

		// Shuffle spawn points to ensure random selection
		List<Vector2> spawnPoints = poiItem.spawnArea.OrderBy(x => Random.value).ToList();
		bool placed = false;
		int collisionAttempts = 0;
		const int maxCollisionAttempts = 32;
		const int maxToleranceAttempts = 32;

		Vector3 position = Vector3.zero;
		Quaternion rotation = Quaternion.identity;
		GameObject spawnedObject = null;
		Monument monument = null;
		TerrainBounds dimensions = new TerrainBounds();

		// Create the GameObject
		GeologyItem geologyItem = new GeologyItem(poiItem.customPrefab);
		spawnedObject = GenerativeManager.SpawnFeature(geologyItem, Vector3.zero, Vector3.zero, Vector3.one, parentContainer);
		if (spawnedObject == null)
		{
			Debug.LogWarning($"Failed to spawn {poiItem.customPrefab}");
			return false;
		}
		monument = spawnedObject.GetComponent<Monument>();

		while (collisionAttempts < maxCollisionAttempts && !placed)
		{
			if (spawnPoints.Count == 0)
			{
				Debug.LogWarning($"No valid spawn points left for {poiItem.customPrefab} after {collisionAttempts} collision attempts" +
								 (poiItem.minPOIDistance > 0 && collisionAttempts == 0 ? ". Minimum POI distance may be set on first item." : ""));
				UnityEngine.Object.DestroyImmediate(spawnedObject);
				return false;
			}

			// Select a random point
			Vector2 point = spawnPoints[0];
			spawnPoints.RemoveAt(0);
			int i = (int)(point.x / resRatio);
			int j = (int)(point.y / resRatio);

			// Convert to world position
			int heightMapsX = Mathf.Clamp((int)(i * resRatio), 0, res - 1);
			int heightMapsY = Mathf.Clamp((int)(j * resRatio), 0, res - 1);
			float[,] baseMap = land.terrainData.GetHeights(0, 0, res, res);
			float height = baseMap[heightMapsX, heightMapsY];
			position = new Vector3(
				(j * ratio) - (size / 2f) + halfPixelSize,
				(height * sizeZ) - (sizeZ * 0.5f),
				(i * ratio) - (size / 2f) + halfPixelSize
			);

			// Adjust position for TUNNELSNAP or OCEAN
			position = SnapToDungeonGrid(position, poiItem, linkRadius, linkHeight);

			// Perform sphere collision check if minPOIDistance or maxPOIDistance is non-zero
			bool collisionValid = true;
			if (poiItem.minPOIDistance > 0 || poiItem.maxPOIDistance > 0)
			{
				bool sphereResult = PrefabManager.sphereCollision(position, poiItem.maxPOIDistance, 3);
				if (poiItem.minPOIDistance > 0)
				{
					bool minSphereResult = PrefabManager.sphereCollision(position, poiItem.minPOIDistance, 3);
					collisionValid = !minSphereResult && (poiItem.maxPOIDistance == 0 || sphereResult);
				}
				else
				{
					collisionValid = sphereResult;
				}
			}

			if (!collisionValid)
			{
				collisionAttempts++;
				continue;
			}

			// Determine rotation
			if (poiItem.isLockRotate)
			{
				rotation = Quaternion.Euler(0, 0, 0);
			}
			else if (poiItem.isWaterfront)
			{
				Vector3 mapEdgeDirection = position.normalized; // Direction from origin to position
				Vector3 forward = new Vector3(mapEdgeDirection.x, 0, mapEdgeDirection.z).normalized; // Project onto XZ plane

				// Snap the forward direction to the nearest cardinal direction (0, 90, 180, 270 degrees)
				float angle = Mathf.Atan2(forward.z, forward.x);
				float snappedAngle = Mathf.Round(angle / (Mathf.PI / 2f)) * (Mathf.PI / 2f);
				forward = new Vector3(Mathf.Cos(snappedAngle), 0, Mathf.Sin(snappedAngle));

				// Ensure Z points away from center: if facing inward, flip 180 degrees
				if (Vector3.Dot(forward, mapEdgeDirection) < 0)
				{
					forward = -forward;
				}

				rotation = Quaternion.LookRotation(forward);
			}
			else
			{
				rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
			}

			// Update GameObject position and rotation
			spawnedObject.transform.localPosition = position;
			spawnedObject.transform.rotation = rotation;

			// Check terrain tolerance
			int toleranceAttempts = 0;
			bool toleranceValid = false;

			while (toleranceAttempts < maxToleranceAttempts)
			{
				if (monument == null)
				{
					Debug.LogWarning($"No Monument component found on {poiItem.customPrefab}. Skipping tolerance check.");
					toleranceValid = true;
					break;
				}

				int currentTolerance = monument.GetTolerance(spawnedObject.transform.position, rotation, Vector3.one, dimensions);

				if (currentTolerance <= poiItem.terrainTolerance)
				{
					Debug.Log($"tolerance test passed with {currentTolerance} / {poiItem.terrainTolerance}");
					toleranceValid = true;
					break;
				}

				// Try a new spawn point
				if (spawnPoints.Count == 0)
				{
					break;
				}

				point = spawnPoints[0];
				spawnPoints.RemoveAt(0);
				i = (int)(point.x / resRatio);
				j = (int)(point.y / resRatio);
				heightMapsX = Mathf.Clamp((int)(i * resRatio), 0, res - 1);
				heightMapsY = Mathf.Clamp((int)(j * resRatio), 0, res - 1);
				height = baseMap[heightMapsX, heightMapsY];
				position = new Vector3(
					(j * ratio) - (size / 2f) + halfPixelSize,
					(height * sizeZ) - (sizeZ * 0.5f),
					(i * ratio) - (size / 2f) + halfPixelSize
				);

				// Adjust position for TUNNELSNAP or OCEAN
				position = SnapToDungeonGrid(position, poiItem, linkRadius, linkHeight);

				// Re-check collision for new position
				collisionValid = true;
				if (poiItem.minPOIDistance > 0 || poiItem.maxPOIDistance > 0)
				{
					bool sphereResult = PrefabManager.sphereCollision(position, poiItem.maxPOIDistance, 3);
					if (poiItem.minPOIDistance > 0)
					{
						bool minSphereResult = PrefabManager.sphereCollision(position, poiItem.minPOIDistance, 3);
						collisionValid = !minSphereResult && (poiItem.maxPOIDistance == 0 || sphereResult);
					}
					else
					{
						collisionValid = sphereResult;
					}
				}

				if (!collisionValid)
				{
					toleranceAttempts++;
					continue;
				}

				// Update rotation
				if (poiItem.isLockRotate)
				{
					rotation = Quaternion.Euler(0, 0, 0);
				}
				else if (poiItem.isWaterfront)
				{
					Vector3 mapEdgeDirection = position.normalized;
					Vector3 forward = new Vector3(mapEdgeDirection.x, 0, mapEdgeDirection.z).normalized;
					float randomY = Random.Range(-15f, 15f);
					rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0, randomY, 0);
				}
				else
				{
					rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
				}

				// Update GameObject position and rotation
				spawnedObject.transform.localPosition = position;
				spawnedObject.transform.rotation = rotation;

				toleranceAttempts++;
			}

			if (toleranceValid)
			{
				placed = true;
			}
			else
			{
				collisionAttempts++;
			}
		}

		if (!placed)
		{
			Debug.LogWarning($"Failed to place {poiItem.customPrefab} after {maxCollisionAttempts} collision attempts" +
							 (poiItem.minPOIDistance > 0 && collisionAttempts == 0 ? ". Minimum POI distance may be set on first item." : ""));
			UnityEngine.Object.DestroyImmediate(spawnedObject);
			return false;
		}

		// Notify placement
		PrefabManager.NotifyItemsChanged();
		Debug.Log($"Successfully placed {poiItem.customPrefab} at {position} with rotation {rotation.eulerAngles}");
		return true;
	}
	
	
	
	private static Vector3 SnapToDungeonGrid(Vector3 position, POIItem poiItem, float linkRadius = 3f, float linkHeight = 1.5f)
	{
		Vector3 adjustedPosition = position;

		// Handle TUNNELSNAP: Snap to the dungeon grid
		if (poiItem.isTunnelSnap)
		{
			adjustedPosition.x = (float)Mathf.RoundToInt(position.x / linkRadius) * linkRadius;
			adjustedPosition.y = (float)Mathf.CeilToInt(position.y / linkHeight) * linkHeight;
			adjustedPosition.z = (float)Mathf.RoundToInt(position.z / linkRadius) * linkRadius;
		}

		// Handle OCEAN: Lock Y position to 0.5
		if (poiItem.isOcean)
		{
			adjustedPosition.y = 0.5f;
		}

		return adjustedPosition;
	}
	
	
	public static IEnumerator PlacePOIs(List<POIItem> poiItems, Transform parentContainer)
    {
        if (poiItems == null || poiItems.Count == 0)
        {
            Debug.LogError("No POIs provided for placement.");
            yield return null;
            yield break;
        }

        if (parentContainer == null)
        {
            Debug.LogError("Parent container Transform is null.");
            yield return null;
            yield break;
        }

        int successfullyPlaced = 0;
        int requiredCount = poiItems.Count(p => p.requiredCount.HasValue);
        int requiredPlaced = 0;

        // Track unique REQUIRED POIs to ensure all are placed
        var requiredPOIs = new HashSet<POIItem>(poiItems.Where(p => p.requiredCount.HasValue), new POIItemEqualityComparer());
        var requiredPlacedSet = new HashSet<POIItem>(new POIItemEqualityComparer());

        foreach (var poiItem in poiItems)
        {
            // Attempt to place the POI
            bool placed = PlacePOI(poiItem, parentContainer);
            if (placed)
            {
                successfullyPlaced++;
                if (poiItem.requiredCount.HasValue)
                {
                    requiredPlaced++;
                    requiredPlacedSet.Add(poiItem);
                }
                Debug.Log($"Placed POI: {poiItem.customPrefab} ({successfullyPlaced}/{poiItems.Count})");
            }
            else
            {
                Debug.LogWarning($"Failed to place POI: {poiItem.customPrefab}");
                if (poiItem.requiredCount.HasValue)
                {
                    Debug.LogError($"REQUIRED POI failed to place: {poiItem.customPrefab}. Layout is not valid.");
                }
            }

            // Yield to allow Unity to process other tasks
            yield return null; // Yields for one frame; adjust as needed (e.g., yield return new WaitForSeconds(0.1f) for a delay)
        }

        // Validate that all REQUIRED POIs were placed
        bool isValid = requiredPlacedSet.Count == requiredPOIs.Count;
        if (!isValid)
        {
            Debug.LogError($"Layout is not valid: Only {requiredPlacedSet.Count}/{requiredPOIs.Count} unique REQUIRED POIs were placed.");
        }
        else
        {
            Debug.Log($"Successfully placed {successfullyPlaced}/{poiItems.Count} POIs (REQUIRED: {requiredPlaced}/{requiredCount}).");
        }

        // Notify PrefabManager that all placements are complete
        PrefabManager.NotifyItemsChanged();
    }
	
	public static void LoadAndPlacePOIs(string layoutFilePath)
    {
        if (string.IsNullOrEmpty(layoutFilePath))
        {
            Debug.LogError("Layout file path is null or empty.");
            return;
        }
		
		string parentName = Path.GetFileNameWithoutExtension(layoutFilePath);
		GameObject parentContainer = new GameObject(parentName);
		parentContainer.tag = "Collection";
		parentContainer.transform.SetParent(PrefabManager.PrefabParent, true);
		parentContainer.transform.localPosition = Vector3.zero;
		
		// Step 1: Load and generate the POI placement queue
        List<POIItem> poiItems = GenerateLayout(layoutFilePath);
        if (poiItems == null || poiItems.Count == 0)
        {
            Debug.LogError($"Failed to generate POI layout from {layoutFilePath}. No POIs to place.");
            return;
        }

        Debug.Log($"Starting placement of {poiItems.Count} POIs from {layoutFilePath}");

        // Step 2: Start the PlacePOIs coroutine using CoroutineManager
        CoroutineManager.Instance.StartRuntimeCoroutine(PlacePOIs(poiItems, parentContainer.transform));
    }


    private class POIItemEqualityComparer : IEqualityComparer<POIItem>
    {
        public bool Equals(POIItem x, POIItem y)
        {
            if (x == null || y == null) return false;
            return x.customPrefab == y.customPrefab;
        }

        public int GetHashCode(POIItem obj)
        {
            return obj.customPrefab?.GetHashCode() ?? 0;
        }
    }
}