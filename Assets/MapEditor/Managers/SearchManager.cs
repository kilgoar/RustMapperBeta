/*
using System;
using System.Collections; // For IEnumerator
using System.Collections.Generic; // For Dictionary and HashSet
using System.IO; // For File and Path
using System.Runtime.Serialization.Formatters.Binary; // For BinaryFormatter
using System.Text;
using UnityEngine; // For Debug

public static class SearchManager
{
    private static readonly string SubstringDataPath = Path.Combine(SettingsManager.AppDataPath(), "nameTable.dat"); // Use SettingsManager.AppDataPath
    private static int LastAssetCount { get; set; } // Track last known asset count
    public static Dictionary<string, HashSet<uint>> SubstringToPrefabIDs { get; private set; } = new Dictionary<string, HashSet<uint>>(StringComparer.Ordinal);

    // Static constructor to subscribe to AssetManager callbacks
    public static void RuntimeInit()
    {
        AssetManager.Callbacks.BundlesLoaded += BuildNameTable;
    }

    // Initialize the substring hash table, either by building it or loading from disk
    public static IEnumerator Initialize()
    {
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        // Check if we can load serialized data
        bool loadFromDisk = false;
        if (TryDeserializeSubstringData(out int storedAssetCount))
        {
            if (storedAssetCount == AssetManager.BundleLookup.Count)
            {
                loadFromDisk = true;
                Debug.Log($"SearchManager: Asset count unchanged ({storedAssetCount}). Loaded SubstringToPrefabIDs from {SubstringDataPath}.");
            }
            else
            {
                Debug.Log($"SearchManager: Asset count changed (stored: {storedAssetCount}, current: {AssetManager.BundleLookup.Count}). Rebuilding SubstringToPrefabIDs.");
            }
        }
        else
        {
            Debug.Log($"SearchManager: No serialized data found or deserialization failed at {SubstringDataPath}. Building SubstringToPrefabIDs.");
        }

        // Build SubstringToPrefabIDs if needed
        if (!loadFromDisk)
        {
            SubstringToPrefabIDs.Clear();
            foreach (var filename in AssetManager.BundleLookup.Keys)
            {
                // Extract asset name and get prefab ID
                string assetName = AssetManager.pathToName(filename);
                if (AssetManager.PathLookup.TryGetValue(filename, out uint prefabID))
                {
                    // Generate all substrings and map to prefab ID
                    for (int start = 0; start < assetName.Length; start++)
                    {
                        for (int length = 1; length <= assetName.Length - start; length++)
                        {
                            string substring = assetName.Substring(start, length);
                            if (!SubstringToPrefabIDs.TryGetValue(substring, out HashSet<uint> prefabIDs))
                            {
                                prefabIDs = new HashSet<uint>();
                                SubstringToPrefabIDs.Add(substring, prefabIDs);
                            }
                            prefabIDs.Add(prefabID);
                        }
                    }
                }
                if (sw.Elapsed.TotalMilliseconds >= 0.5f)
                {
                    yield return null;
                    sw.Restart();
                }
            }
            // Serialize the new data
            SerializeSubstringData();
        }

        Debug.Log($"SearchManager: SubstringToPrefabIDs initialized with {SubstringToPrefabIDs.Count} substring entries.");
        yield return null;
    }

    // Start the Initialize coroutine via CoroutineManager
    public static void BuildNameTable()
    {
        if (CoroutineManager.Instance == null)
        {
            Debug.LogError("SearchManager: Cannot initialize because CoroutineManager.Instance is null.");
            return;
        }
        CoroutineManager.Instance.StartRuntimeCoroutine(Initialize());
    }

    // Serialize SubstringToPrefabIDs and asset count to disk
    // Serialize SubstringToPrefabIDs and asset count to disk
    private static void SerializeSubstringData()
    {
        try
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(SubstringDataPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (FileStream fs = new FileStream(SubstringDataPath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs, Encoding.UTF8))
            {
                Debug.Log($"SearchManager: Starting serialization to {SubstringDataPath} with {SubstringToPrefabIDs.Count} entries.");

                // Write asset count
                writer.Write(AssetManager.BundleLookup.Count);
                Debug.Log($"SearchManager: Wrote asset count: {AssetManager.BundleLookup.Count}");

                // Write number of dictionary entries
                writer.Write(SubstringToPrefabIDs.Count);
                Debug.Log($"SearchManager: Wrote entry count: {SubstringToPrefabIDs.Count}");

                int entryIndex = 0;
                // Write each key-value pair
                foreach (var kvp in SubstringToPrefabIDs)
                {
                    // Write key length and bytes
                    byte[] keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                    writer.Write(keyBytes.Length);
                    writer.Write(keyBytes);

                    // Write number of uints in HashSet
                    writer.Write(kvp.Value.Count);
                    // Write each uint
                    foreach (uint id in kvp.Value)
                    {
                        writer.Write(id);
                    }

                    entryIndex++;
                    if (entryIndex % 100000 == 0)
                    {
                        Debug.Log($"SearchManager: Serialized {entryIndex} entries.");
                    }
                }
                Debug.Log($"SearchManager: Completed serialization of {entryIndex} entries.");
            }
            Debug.Log($"SearchManager: Serialized SubstringToPrefabIDs to {SubstringDataPath} with {AssetManager.BundleLookup.Count} assets and {SubstringToPrefabIDs.Count} substring entries.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SearchManager: Failed to serialize SubstringToPrefabIDs to {SubstringDataPath}: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }


    // Deserialize SubstringToPrefabIDs and asset count from disk
    private static bool TryDeserializeSubstringData(out int assetCount)
    {
        assetCount = 0;
        try
        {
            if (File.Exists(SubstringDataPath))
            {
                using (FileStream fs = new FileStream(SubstringDataPath, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    assetCount = (int)formatter.Deserialize(fs); // Read asset count
                    SubstringToPrefabIDs = (Dictionary<string, HashSet<uint>>)formatter.Deserialize(fs);
                    return true;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SearchManager: Failed to deserialize SubstringToPrefabIDs from {SubstringDataPath}: {e.Message}");
        }
        return false;
    }
}
*/