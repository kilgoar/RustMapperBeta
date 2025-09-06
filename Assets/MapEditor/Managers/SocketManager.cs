using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using ProtoBuf;
using ProtoBuf.Meta;

using RustMapEditor.Variables;

public static class SocketManager
{
    // Public dictionary field
    public static SocketData socketData = new SocketData();
    public static DungeonBaseSocket firstSocket = null;

    public static void AddOrUpdateSockets(string prefabFilePath, List<DungeonBaseSocket> sockets)
    {
        List<SocketInfo> socketInfos = new List<SocketInfo>();
        foreach (var socket in sockets)
        {
            if (socket != null)
            {
                socketInfos.Add(SocketInfo.FromDungeonBaseSocket(socket)); // Use FromDungeonBaseSocket to handle Euler angles
            }
        }
        socketData[prefabFilePath] = socketInfos;
    }

    // Get sockets for a prefab file path
    public static List<SocketInfo> GetSockets(string prefabFilePath)
    {
        return socketData[prefabFilePath];
    }

    // Save socket data to file
    public static void SaveSocketData(string filePath = null)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(SettingsManager.AppDataPath(), "socketData.json");
            }
            string json = JsonConvert.SerializeObject(socketData.Dictionary, Formatting.Indented);
            File.WriteAllText(filePath, json);
            Debug.Log($"Socket data saved to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save socket data: {ex.Message}");
        }
    }

    // Load socket data from file
    public static void LoadSocketData(string filePath = null)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(SettingsManager.AppDataPath(), "socketData.json");
            }
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                socketData.Dictionary = JsonConvert.DeserializeObject<Dictionary<string, List<SocketInfo>>>(json);
                Debug.Log($"Socket data loaded from {filePath}");
            }
            else
            {
                Debug.LogWarning($"No socket data file found at {filePath}");
                socketData = new SocketData();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load socket data: {ex.Message}");
            socketData = new SocketData();
        }
    }

    public static void Connect(DungeonBaseSocket socket)
    {
        if (socket == null) return;

        if (firstSocket == null)
        {
            firstSocket = socket;
            CameraManager.Instance.Unselect();
            SelectSoft(firstSocket);
            return;
        }

        if (IsValidConnection(socket, firstSocket))
        {
            AttachSockets(socket, firstSocket);
            CameraManager.Instance.Unselect();
        }

        Unselect(firstSocket);
        firstSocket = null; // Reset after attempting connection
    }

    public static void SelectSoft(DungeonBaseSocket socket)
    {
        if (socket != null)
        {
            CameraManager.Instance.SelectSocketSoft(socket.gameObject);
        }
    }

    public static void Select(DungeonBaseSocket socket)
    {
        if (socket != null)
        {
            CameraManager.Instance.SelectPrefab(socket.gameObject);
        }
    }

    public static void Unselect()
    {
        if (firstSocket != null)
        {
            firstSocket = null;
            CameraManager.Instance.Unselect();
        }
    }

    public static void Clear()
    {
        firstSocket = null;
    }

    public static void Unselect(DungeonBaseSocket socket)
    {
        if (socket != null)
        {
            List<Renderer> renderers = new List<Renderer>(socket.gameObject.GetComponentsInChildren<Renderer>());
            CameraManager.Instance.EmissionHighlight(renderers, false);
        }
    }

    private static bool IsValidConnection(DungeonBaseSocket socket1, DungeonBaseSocket socket2)
    {
        Transform prefabParent1 = GetPrefabParent(socket1.transform);
        Transform prefabParent2 = GetPrefabParent(socket2.transform);

        if (prefabParent1 == null || prefabParent2 == null)
        {
            Debug.LogWarning("One or both sockets do not have a parent with 'Prefab' tag");
            return false;
        }

        if (prefabParent1 == prefabParent2)
        {
            return false;
        }

        if (socket1.Type != socket2.Type)
        {
            return false;
        }

        if (socket1.Male && socket1.Female)
        {
            return socket2.Male || socket2.Female;
        }

        if (socket1.Male)
        {
            return socket2.Female;
        }

        if (socket1.Female)
        {
            return socket2.Male;
        }

        return false;
    }

    private static void AttachSockets(DungeonBaseSocket socket1, DungeonBaseSocket socket2)
    {
        Transform prefabParent = GetPrefabParent(socket2.transform);
        if (prefabParent == null)
        {
            Debug.LogWarning("No parent object with 'Prefab' tag found for socket2");
            return;
        }

        Vector3 targetForward = -socket1.transform.forward;
        Quaternion targetRotation = Quaternion.LookRotation(targetForward, socket1.transform.up);
        Quaternion socket2LocalRotation = Quaternion.Inverse(prefabParent.rotation) * socket2.transform.rotation;
        Vector3 socket2LocalForward = socket2LocalRotation * Vector3.forward;
        Quaternion rotationCorrection = Quaternion.FromToRotation(socket2LocalForward, Vector3.forward);
        prefabParent.rotation = targetRotation * rotationCorrection;
        Vector3 positionOffset = socket1.transform.position - socket2.transform.position;
        prefabParent.position += positionOffset;
    }

    public static void SetupSocket(DungeonBaseSocket component)
    {
        GameObject socket = Resources.Load<GameObject>("Prefabs/TranslucentConnection");
        GameObject sibling = UnityEngine.Object.Instantiate(socket, component.gameObject.transform.parent);

        sibling.SetLayerRecursively(13);
        sibling.SetTagRecursively("Untagged");
        sibling.transform.localScale = Vector3.one;
        sibling.transform.localPosition = component.gameObject.transform.localPosition;
        sibling.transform.localRotation = component.gameObject.transform.localRotation;

        var renderers = sibling.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                Color colorWithAlpha = new Color(AppManager.Instance.color3.r, AppManager.Instance.color3.g, AppManager.Instance.color3.b, 0.8f);
                renderer.material.color = colorWithAlpha;
            }
        }

        DungeonBaseSocket siblingSocket = sibling.AddComponent<DungeonBaseSocket>();
        siblingSocket.Type = component.Type;
        siblingSocket.Male = component.Male;
        siblingSocket.Female = component.Female;
    }

	public static void AddSocket(RaycastHit hit)
	{
		GameObject hitObject = hit.collider.gameObject;
		(Transform parent, string parentTag) = GetParentAndTag(hitObject.transform);
		if (parent == null)
		{
			Debug.LogWarning("No parent with 'Prefab' or 'Collection' tag found");
			return;
		}

		GameObject socketInfoObject = GetOrCreateSocketInfo(parent);
		if (socketInfoObject == null) return;

		// Create socket and set name immediately
		GameObject newSocket = CreateSocketGameObject(socketInfoObject.transform, hit.point);
		List<SocketInfo> socketInfos = null;
		string prefabPath = null;

		if (parentTag == "Prefab")
		{
			prefabPath = GetPrefabPath(parent);
			if (string.IsNullOrEmpty(prefabPath)) return;
			socketInfos = GetOrCreateSocketList(prefabPath);
			newSocket.name = $"Socket_{socketInfos.Count}"; // Set name before adding component
		}
		else
		{
			newSocket.name = $"Socket_{UnityEngine.Random.Range(0, 10000)}"; // Temporary name for non-prefab sockets
		}

		// Add DungeonBaseSocket component after setting name
		DungeonBaseSocket newSocketComponent = newSocket.AddComponent<DungeonBaseSocket>();
		newSocketComponent.Type = DungeonBaseSocketType.Horizontal;
		newSocketComponent.Male = true;
		newSocketComponent.Female = true;

		if (parentTag == "Prefab")
		{
			socketInfos.Add(CreateSocketInfo(newSocketComponent));
			socketData[prefabPath] = socketInfos;
			Debug.Log($"Added socket '{newSocket.name}' to prefab: {prefabPath}");
			SaveSocketData();
		}

		Select(newSocketComponent);
	}

    public static bool IsPrefabSocket(DungeonBaseSocket socket)
    {
        var (parent, parentTag) = GetParentAndTag(socket.transform);
        return parent != null && parentTag == "Prefab";
    }

	public static void UpdateSocket(DungeonBaseSocket socket, string prefabPath, int socketIndex)
	{
		if (socket == null || string.IsNullOrEmpty(prefabPath) || socketIndex < 0) return;

		if(IsPrefabSocket(socket)){
			List<SocketInfo> socketInfos = GetOrCreateSocketList(prefabPath);
			if (socketIndex >= socketInfos.Count)
			{
				Debug.LogWarning($"Socket index {socketIndex} is out of range for prefab: {prefabPath}");
				return;
			}

			socketInfos[socketIndex] = CreateSocketInfo(socket);
			socketData[prefabPath] = socketInfos;
			Debug.Log($"Updated socket '{socket.gameObject.name}' for prefab: {prefabPath}");
			SaveSocketData();
		}
	}

	public static void DeleteSocket(DungeonBaseSocket socket, string prefabPath, int socketIndex)
	{
		if (socket == null || string.IsNullOrEmpty(prefabPath) || socketIndex < 0) return;

		List<SocketInfo> socketInfos = GetOrCreateSocketList(prefabPath);
		if (socketIndex >= socketInfos.Count)
		{
			Debug.LogWarning($"Socket index {socketIndex} is out of range for prefab: {prefabPath}");
			return;
		}

		socketInfos.RemoveAt(socketIndex);
		var (prefabParent, _) = GetParentAndTag(socket.transform); // Still need parent for renaming
		UpdateSocketNames(prefabParent, socketIndex, socketInfos.Count);

		if (socketInfos.Count == 0)
		{
			socketData.Dictionary.Remove(prefabPath);
		}
		else
		{
			socketData[prefabPath] = socketInfos;
		}

		UnityEngine.Object.Destroy(socket.gameObject);
		Debug.Log($"Deleted socket '{socket.gameObject.name}' from prefab: {prefabPath}");
		SaveSocketData();
	}

    // Helper method to get parent with "Prefab" or "Collection" tag
    public static (Transform parent, string tag) GetParentAndTag(Transform transform)
    {
        Transform collection=null;
		Transform prefab=null;
		Transform current = transform;
		
		while (current != null)		{
			if (current.CompareTag("Collection"))
			{
				collection = current;
			}
			if (current.CompareTag("Prefab") && prefab == null)
			{
				prefab = current; // Store the first Prefab, but continue searching for a Collection
			}
			current = current.parent;
		}
		
		if (collection != null)				{
					return (collection, "Collection");
				}
				
				if (prefab != null)				{
					return (prefab, "Prefab");
				}
				
		return (null, "");
    }

    // Helper method to get prefab parent specifically
    public static Transform GetPrefabParent(Transform transform)
    {
        Transform parent = transform;
        while (parent != null && !parent.CompareTag("Prefab") && !parent.CompareTag("Collection"))
        {
            parent = parent.parent;
        }
        return parent;
    }

    // Helper method to get prefab path from PrefabDataHolder
    public static string GetPrefabPath(Transform prefabParent)
    {
        if (prefabParent == null)
        {
            Debug.LogWarning("No parent with 'Prefab' tag found");
            return null;
        }

        PrefabDataHolder holder = prefabParent.GetComponent<PrefabDataHolder>();
        if (holder == null)
        {
            Debug.LogWarning("No PrefabDataHolder found on prefab parent");
            return null;
        }

        if(!AssetManager.IDLookup.TryGetValue(holder.prefabData.id, out string prefabPath)){
            Debug.LogWarning($"Could not find prefab path for ID: {holder.prefabData.id}");
            return null;
        }

        return prefabPath;
    }

    // Helper method to get or create SocketInfo GameObject
    public static GameObject GetOrCreateSocketInfo(Transform parent)
    {
        Transform socketInfoTransform = parent.Find("SocketInfo");
        if (socketInfoTransform != null)
        {
            return socketInfoTransform.gameObject;
        }

        GameObject socketInfoObject = new GameObject("SocketInfo");
        socketInfoObject.transform.SetParent(parent, false);
        return socketInfoObject;
    }

    // Helper method to create socket GameObject
    public static GameObject CreateSocketGameObject(Transform parent, Vector3 position)
    {
        GameObject socketPrefab = Resources.Load<GameObject>("Prefabs/TranslucentConnection");
        GameObject newSocket = UnityEngine.Object.Instantiate(socketPrefab, parent);
        newSocket.SetLayerRecursively(13);
        newSocket.SetTagRecursively("Untagged");
        newSocket.transform.position = position;
        newSocket.transform.localScale = Vector3.one;
        newSocket.transform.rotation = Quaternion.identity;

        Color colorWithAlpha = new Color(AppManager.Instance.color3.r, AppManager.Instance.color3.g, AppManager.Instance.color3.b, 0.8f);
        var renderers = newSocket.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.material.color = colorWithAlpha;
            }
        }

        return newSocket;
    }
	
	public static GameObject SpawnSocketGameObject(Transform parent, Vector3 localPosition)
{
    GameObject socketPrefab = Resources.Load<GameObject>("Prefabs/TranslucentConnection");
    GameObject newSocket = UnityEngine.Object.Instantiate(socketPrefab, parent);
    newSocket.SetLayerRecursively(13);
    newSocket.SetTagRecursively("Untagged");
    newSocket.transform.localPosition = localPosition; // Use localPosition directly
    newSocket.transform.localScale = Vector3.one;
    newSocket.transform.localRotation = Quaternion.identity;

    Color colorWithAlpha = new Color(AppManager.Instance.color3.r, AppManager.Instance.color3.g, AppManager.Instance.color3.b, 0.8f);
    var renderers = newSocket.GetComponentsInChildren<Renderer>(true);
    foreach (var renderer in renderers)
    {
        if (renderer != null)
        {
            renderer.material.color = colorWithAlpha;
        }
    }

    return newSocket;
}

    // Helper method to get or create socket list
    public static List<SocketInfo> GetOrCreateSocketList(string prefabPath)
    {
        return socketData[prefabPath] ?? new List<SocketInfo>();
    }

    // Helper method to create SocketInfo from DungeonBaseSocket
    public static SocketInfo CreateSocketInfo(DungeonBaseSocket socket)
    {
        return SocketInfo.FromDungeonBaseSocket(socket); // Use FromDungeonBaseSocket for consistency
    }

    // Helper method to parse and validate socket index from name
    public static bool TryGetSocketIndex(string socketName, out int index)
    {
        index = -1;
        if (!socketName.StartsWith("Socket_"))
        {
            Debug.LogWarning($"Socket name '{socketName}' does not follow expected format 'Socket_<index>'");
            return false;
        }

        if (!int.TryParse(socketName.Substring(7), out index) || index < 0)
        {
            Debug.LogWarning($"Invalid index in socket name: {socketName}");
            return false;
        }

        return true;
    }

    // Helper method to update socket names after deletion
    public static void UpdateSocketNames(Transform prefabParent, int startIndex, int socketCount)
    {
        for (int i = startIndex; i < socketCount; i++)
        {
            GameObject socketObject = FindSocketGameObjectByIndex(prefabParent, i + 1);
            if (socketObject != null)
            {
                socketObject.name = $"Socket_{i}";
            }
        }
    }

    // Helper method to find a socket GameObject by its index
    public static GameObject FindSocketGameObjectByIndex(Transform prefabParent, int index)
    {
        Transform socketInfo = prefabParent.Find("SocketInfo");
        if (socketInfo == null)
        {
            return null;
        }

        foreach (Transform child in socketInfo)
        {
            if (child.name == $"Socket_{index}")
            {
                return child.gameObject;
            }
        }
        return null;
    }
	
	
	public static void PopulateSockets(GameObject prefab, string filePath = null)
	{
		Debug.Log("Populating sockets");
		if (prefab == null)
		{
			Debug.LogWarning("Prefab is null, can't populate sockets");
			return;
		}

		// Resolve filePath if not provided
		if (string.IsNullOrEmpty(filePath))
		{
			Transform prefabTransform = prefab.transform;
			PrefabDataHolder holder = prefabTransform.GetComponent<PrefabDataHolder>();
			if (holder == null)
			{
				Debug.LogWarning("No PrefabDataHolder found on prefab, can't populate sockets");
				return;
			}

			if (!AssetManager.IDLookup.TryGetValue(holder.prefabData.id, out filePath))
			{
				Debug.LogWarning($"Could not find prefab path for ID: {holder.prefabData.id}");
				return;
			}
		}

		// Get socket list from dictionary
		List<SocketInfo> socketInfos = GetOrCreateSocketList(filePath);
		if (socketInfos == null || socketInfos.Count == 0)
		{
			Debug.Log($"No sockets found for prefab: {filePath}, can't populate sockets");
			return;
		}

		// Create or get SocketInfo GameObject
		GameObject socketInfoObject = GetOrCreateSocketInfo(prefab.transform);
		if (socketInfoObject == null)
		{
			Debug.LogWarning("Failed to create or find SocketInfo object, can't populate sockets");
			return;
		}

		// Spawn sockets
		for (int i = 0; i < socketInfos.Count; i++)
		{
			SocketInfo socketInfo = socketInfos[i];
			GameObject newSocket = SpawnSocketGameObject(socketInfoObject.transform, socketInfo.Position);
			newSocket.name = $"Socket_{i}";

			// Set rotation using Euler angles from SocketInfo
			newSocket.transform.localRotation = Quaternion.Euler(socketInfo.Rotation);

			// Configure DungeonBaseSocket component
			DungeonBaseSocket socketComponent = newSocket.AddComponent<DungeonBaseSocket>();
			socketComponent.Type = socketInfo.Type;
			socketComponent.Male = socketInfo.Male;
			socketComponent.Female = socketInfo.Female;

			//Debug.Log($"Spawned socket '{newSocket.name}' for prefab: {prefabPath}");
		}
	}
	
	
}