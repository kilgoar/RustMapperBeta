using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UIRecycleTreeNamespace;
using static WorldSerialization;

public class ItemsWindow : MonoBehaviour
{
	public Text footer;
    public UIRecycleTree tree;
	public InputField query;
	public Button deleteChecked, checkAll, uncheckAll, findInBreaker, saveCustom, applyTerrain;
	private int currentMatchIndex = 0; 
	private List<Node> matchingNodes = new List<Node>();
	public List<InputField> prefabDataFields = new List<InputField>();
	public List<InputField> snapFields = new List<InputField>();
	
	public static ItemsWindow Instance { get; private set; }
	

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;            
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }
	
	private void Start(){
		InitializeComponents();
	}

    public void SetSelection(GameObject go)
    {
        if (go == null)
        {
            DefaultPrefabData(); // Clear UI if no object
            return;
        }

        RetrievePrefabData(go); // Just fetch and display data directly
    }

	private GameObject _lastProcessedSelection;
	public void UpdateData()
		{
			var selectedObjects = CameraManager.Instance._selectedObjects;
			if (selectedObjects.Count > 0)
			{
				GameObject lastSelected = selectedObjects[^1];
				if (lastSelected != _lastProcessedSelection)
				{
					SetSelection(lastSelected);
					_lastProcessedSelection = lastSelected;
				}
			}
			else if (_lastProcessedSelection != null)
			{
				DefaultPrefabData();
				_lastProcessedSelection = null;
			}
		}

    public void RetrievePrefabData(GameObject go)
    {
        DestroyListeners();

        PrefabDataHolder prefabHolder = go.GetComponent<PrefabDataHolder>();
        bool isCollection = go.CompareTag("Collection");
		saveCustom.interactable = false;
		
		//Monument terrainPlacement = go.GetComponent<Monument>();
        applyTerrain.interactable = true;
        
		
        if (prefabHolder != null && !isCollection) // Prefab with data
        {
            PrefabData data = prefabHolder.prefabData;
            prefabHolder.UpdatePrefabData(); // Ensure data is fresh

            prefabDataFields[0].text = data.position.x.ToString("F3");
            prefabDataFields[1].text = data.position.y.ToString("F3");
            prefabDataFields[2].text = data.position.z.ToString("F3");

            prefabDataFields[3].text = data.rotation.x.ToString("F3");
            prefabDataFields[4].text = data.rotation.y.ToString("F3");
            prefabDataFields[5].text = data.rotation.z.ToString("F3");

            prefabDataFields[6].text = data.scale.x.ToString("F3");
            prefabDataFields[7].text = data.scale.y.ToString("F3");
            prefabDataFields[8].text = data.scale.z.ToString("F3");

            prefabDataFields[9].text = data.category;
            prefabDataFields[10].text = data.id.ToString();
        }
        else // Collection or no prefab data, use transform
        {
            Transform transform = go.transform;
            prefabDataFields[0].text = transform.localPosition.x.ToString("F3");
            prefabDataFields[1].text = transform.localPosition.y.ToString("F3");
            prefabDataFields[2].text = transform.localPosition.z.ToString("F3");

			Vector3 rotation = transform.eulerAngles;
			prefabDataFields[3].text = rotation.x.ToString("F3");
			prefabDataFields[4].text = rotation.y.ToString("F3");
			prefabDataFields[5].text = rotation.z.ToString("F3");
			
            prefabDataFields[6].text = transform.localScale.x.ToString("F3");
            prefabDataFields[7].text = transform.localScale.y.ToString("F3");
            prefabDataFields[8].text = transform.localScale.z.ToString("F3");

            prefabDataFields[9].text = isCollection ? go.name : string.Empty;
            prefabDataFields[10].text = isCollection ? string.Empty : go.name;
			saveCustom.interactable = true;
        }

        CreateListeners(go); // Pass the GameObject to listeners
    }

	public void SendPrefabData(GameObject go)
	{
		// Parse transform data from fields
		Vector3 position = new Vector3(
			float.Parse(prefabDataFields[0].text),
			float.Parse(prefabDataFields[1].text),
			float.Parse(prefabDataFields[2].text));

		Vector3 rotation = new Vector3(
			float.Parse(prefabDataFields[3].text),
			float.Parse(prefabDataFields[4].text),
			float.Parse(prefabDataFields[5].text));

		Vector3 scale = new Vector3(
			float.Parse(prefabDataFields[6].text),
			float.Parse(prefabDataFields[7].text),
			float.Parse(prefabDataFields[8].text));


		// Handle PrefabData if it exists (prefabs)
		PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
		if (holder != null && !go.CompareTag("Collection"))
		{
			PrefabData data = holder.prefabData;
			data.position = new VectorData(position.x, position.y, position.z);
			data.rotation = new VectorData(rotation.x, rotation.y, rotation.z);
			data.scale = new VectorData(scale.x, scale.y, scale.z);
			data.category = go.name; // Sync category with name
			data.id = uint.Parse(prefabDataFields[10].text);
			holder.CastPrefabData(); // Sets transform for prefabs
		}
		else if (go.CompareTag("Collection"))
		{
			// For collections, set transform directly since no PrefabData
			go.transform.localPosition = position;
			go.transform.rotation = Quaternion.Euler(rotation);
			go.transform.localScale = scale;
			
			// Update name from category field
			go.name = prefabDataFields[9].text;
			    Node node = tree.FindFirstNodeByDataRecursive(go);
			if (node != null)
			{
				node.name = go.name;
			}
		}

		CameraManager.Instance.UpdateGizmoState();

	}

    public void DefaultPrefabData()
    {
        DestroyListeners();
        for (int i = 0; i < prefabDataFields.Count; i++)
        {
            prefabDataFields[i].text = string.Empty;
        }
    }

    public void CreateListeners(GameObject go)
    {
        for (int i = 0; i < 9; i++)
        {
            int index = i;
            prefabDataFields[i].onEndEdit.AddListener(text =>
            {
                if (float.TryParse(text, out float value))
                {
                    UpdatePrefabData(go, index, value);
                }
                SendPrefabData(go);
            });
        }

        prefabDataFields[9].onEndEdit.AddListener(text => 
        {
                SendPrefabData(go);
        });

        prefabDataFields[10].onEndEdit.AddListener(text =>
        {
            if (uint.TryParse(text, out uint id) && go.GetComponent<PrefabDataHolder>() is PrefabDataHolder holder)
            {
                holder.prefabData.id = id;
                SendPrefabData(go);
            }
        });
    }

    private void UpdatePrefabData(GameObject go, int index, float value)
    {
        PrefabDataHolder holder = go.GetComponent<PrefabDataHolder>();
        if (holder == null) return;

        PrefabData data = holder.prefabData;
        int vectorIndex = index % 3;
        switch (index / 3)
        {
            case 0: // Position
                if (vectorIndex == 0) data.position.x = value;
                else if (vectorIndex == 1) data.position.y = value;
                else data.position.z = value;
                break;
            case 1: // Rotation
                if (vectorIndex == 0) data.rotation.x = value;
                else if (vectorIndex == 1) data.rotation.y = value;
                else data.rotation.z = value;
                break;
            case 2: // Scale
                if (vectorIndex == 0) data.scale.x = value;
                else if (vectorIndex == 1) data.scale.y = value;
                else data.scale.z = value;
                break;
        }
    }
	
	private void InitializeComponents()
	{	

		if (tree != null)
		{
			tree.onNodeCheckedChanged.AddListener(OnChecked);
			tree.onSelectionChanged.AddListener(OnSelect);
			tree.onNodeDblClick.AddListener(FocusItem);
			
			tree.onNodeDragStart += OnNodeDragStart;
            tree.onNodeDrop += OnNodeDrop;
            tree.onNodeDragEnd += OnNodeDragEnd;
		}

		if (query != null)
		{
			query.onEndEdit.AddListener(OnQueryEntered);
			query.onValueChanged.AddListener(OnQuery);
		}

		deleteChecked.onClick.AddListener(DeleteCheckedNodes);
		checkAll.onClick.AddListener(CheckNodes);
		uncheckAll.onClick.AddListener(UncheckNodes);
		findInBreaker.onClick.AddListener(FindBreakerWithPath);
		applyTerrain.onClick.AddListener(ApplyTerrain);
        //applyTerrain.interactable = false;
	

		saveCustom.onClick.AddListener(() =>
		{
			if (CameraManager.Instance._selectedObjects.Count > 0)
			{
				SaveCollection(CameraManager.Instance._selectedObjects[^1]);
			}
			else
			{
				Debug.LogWarning("No object selected to save as a collection.");
			}
		});
		
	}
	
	private void ApplyTerrain()
	{
		if (CameraManager.Instance._selectedObjects.Count == 0) return;

		foreach (GameObject selected in CameraManager.Instance._selectedObjects)
		{
			Monument terrainPlacement = selected.GetComponent<Monument>();
			if (terrainPlacement != null)
			{
				TerrainBounds dimensions = new TerrainBounds();
				Vector3 position = selected.transform.position;
				Quaternion rotation = selected.transform.rotation;
				Vector3 scale = selected.transform.localScale;
				
				terrainPlacement.ApplyHeight(position, rotation, scale, dimensions);
				terrainPlacement.ApplySplat(position, rotation, scale, dimensions);
				terrainPlacement.ApplyTopology(position, rotation, scale, dimensions);
				terrainPlacement.ApplyAlpha(position, rotation, scale, dimensions);
				terrainPlacement.ApplyWater(position, rotation, scale, dimensions);
			}
			else
			{
				Debug.LogWarning($"No valid TerrainPlacement component with height map found on {selected.name}");
			}
		}
	}
	
	private bool IsDescendant(Node draggedNode, Node targetNode)
	{
		if (draggedNode == null || targetNode == null) return false;

		// Get all descendants of the dragged node
		var descendants = draggedNode.GetAllChildrenRecursive();
		// Check if targetNode is in the descendants list
		foreach (var descendant in descendants)
		{
			if (descendant == targetNode)
				return true;
		}
		return false;
	}
	
	public void SaveCollection(GameObject go)
	{
		// Check if the GameObject is valid
		if (go == null)
		{
			Debug.LogWarning("No collection provided to save.");
			return;
		}

		string savePath = System.IO.Path.Combine(SettingsManager.AppDataPath(), "custom", $"{go.name}.prefab");

		// Ensure the 'custom' directory exists
		string customDir = System.IO.Path.Combine(SettingsManager.AppDataPath(), "custom");
		if (!System.IO.Directory.Exists(customDir))
		{
			System.IO.Directory.CreateDirectory(customDir);
		}

		// Save the collection using MapManager
		MapManager.SaveCollectionPrefab(savePath, go.transform);

		Debug.Log($"Collection saved to: {savePath}");
	}
	
	public void FindBreakerWithPath(){
		AppManager.Instance.ActivateWindow(5);
		if(BreakerWindow.Instance!=null){
			BreakerWindow.Instance.FocusByPath(prefabDataFields[9].text);
		}
	}
	
	public void DestroyListeners()
	{
		for (int i = 0; i < prefabDataFields.Count; i++)
		{
			prefabDataFields[i].onEndEdit.RemoveAllListeners();
		}
	}
	


	
	void OnEnable()	{
		PopulateList();
		FocusItem(CameraManager.Instance._selectedObjects);
	}
	
	
	private void LogExpandedNodes(string context)
	{
		if (tree == null)
		{
			Debug.LogWarning($"LogExpandedNodes ({context}): Tree is null.");
			return;
		}

		//Debug.Log($"LogExpandedNodes ({context}): Starting expanded node list...");
		Stack<Node> stack = new Stack<Node>();
		stack.Push(tree.rootNode);

		int expandedCount = 0;
		while (stack.Count > 0)
		{
			Node currentNode = stack.Pop();
			if (currentNode.isExpanded)
			{
				//Debug.Log($"LogExpandedNodes ({context}): Expanded node '{currentNode.name}' (ID: {currentNode.nodeId}, isChecked: {currentNode.isChecked}, isSelected: {currentNode.isSelected})");
				expandedCount++;
			}

			for (int i = currentNode.nodes.Count - 1; i >= 0; i--)
			{
				stack.Push(currentNode.nodes[i]);
			}
		}

		Debug.Log($"LogExpandedNodes ({context}): Found {expandedCount} expanded nodes.");
	}
	
public void OnChecked(Node node)
{
    if (node == null || node.data == null) return;

    GameObject go = (GameObject)node.data;
    if (go == null || !go.activeInHierarchy)
    {
        Debug.LogWarning($"Node '{node.name}' has invalid or destroyed data: {node.data}");
        return;
    }

    if (node.isChecked)
    {
        // Add to selection if not already present
        if (!CameraManager.Instance._selectedObjects.Contains(go))
        {    

            // If the selected object is a Node, show the parent road's nodes
            if (go.CompareTag("Node"))
            {
                GameObject roadObject = go.transform.parent?.parent?.gameObject; // Node -> NodeParent -> Path
                if (roadObject != null && roadObject.CompareTag("Path"))
                {
					ClearRoads();
					CameraManager.Instance.Unselect();
                    CameraManager.Instance._selectedRoad = roadObject;
                    NodeCollection nodeCollection = roadObject.GetComponent<NodeCollection>();
                    if (nodeCollection != null)
                    {
                        nodeCollection.ShowNodes(); // Apply gold material to nodes
                    }
                }
            }
			CameraManager.Instance.CheckSelect(go, true);
        }
    }
    else
    {
        // Remove from selection
        if (CameraManager.Instance._selectedObjects.Contains(go))
        {
            CameraManager.Instance.UnselectChecked(go);
            // Clear road-related state if no objects are selected
            if (CameraManager.Instance._selectedObjects.Count == 0)
            {
                ClearRoads();
            }
        }
    }
    CameraManager.Instance.UpdateGizmoState();
    CameraManager.Instance.NotifySelectionChanged();
    //tree.Rebuild();        
}
	
	public void FocusItem(Node node)
	{
		if (node?.data == null) return; 

		Vector3 targetPosition = Vector3.zero;
		GameObject go = (GameObject)node.data;
		targetPosition = go.transform.position;
		CameraManager.Instance.SetCamera(targetPosition);
		
		node.SetCheckedWithoutNotify(true);
		//tree.Rebuild();
	}

	public void FocusList(Node node)
	{
		tree.FocusOn(node);
		//node.isSelected=true;
		//node.SetCheckedWithoutNotify(true);
		//tree.Rebuild();
	}
	
	public void FocusItem(List<GameObject> selections){
		int itemCount = selections.Count;
		if(itemCount > 0){
			Node lastNode = tree.FindFirstNodeByDataRecursive(selections[itemCount-1]);
			tree.FocusOn(lastNode);
			//lastNode.SetSelectedWithoutNotify(true);
		}

	}
public void PopulateList()
{
    if (tree == null)
    {
        Debug.LogWarning("PopulateList: Tree is null, cannot populate.");
        return;
    }

    tree.nodes.Clear();
    Dictionary<Transform, Node> transformToNodeMap = new Dictionary<Transform, Node>();

    // Get the current query from the InputField
    string currentQuery = query != null ? query.text.ToLower() : string.Empty;

    // Iterate through PrefabParent and PathParent, passing the query
    foreach (Transform child in PrefabManager.PrefabParent)
    {
        BuildTreeRecursive(child, null, transformToNodeMap, currentQuery);
    }
    foreach (Transform child in PathManager.PathParent)
    {
		Debug.Log("populating paths");
        BuildTreeRecursive(child, null, transformToNodeMap, currentQuery);
    }

    CheckSelection();
    tree.Rebuild();
}

private bool BuildTreeRecursive(Transform current, Node parentNode, Dictionary<Transform, Node> transformToNodeMap, string query)
{
    Node currentNode = null;
    bool hasMatchingDescendant = false; // Tracks if this node or any descendant matches the query

    // Ensure tree is searchable and suffix trees are enabled
    if (tree == null || !tree.searchable)
    {
        Debug.LogWarning($"BuildTreeRecursive: Tree is {(tree == null ? "null" : "not searchable")}. Skipping node '{current.name}'.");
        return false;
    }

	//Debug.Log(current.tag + " = current tag?");

    // Determine the node type based on the tag
    switch (current.tag)
    {
        case "Prefab":
            if (current.GetComponent<PrefabDataHolder>() is PrefabDataHolder prefabData)
            {
                string prefabName = AssetManager.ToName(prefabData.prefabData.id);
                currentNode = new Node(prefabName) { data = current.gameObject };
				tree.AddNodeNameReference(currentNode);
                // Check if the prefab's suffix tree matches the query
                if (string.IsNullOrEmpty(query) || (currentNode.SuffixTree.Contains(query)))
                {
                    hasMatchingDescendant = true; // This node matches
                }
            }
            break;

        case "Collection":
            currentNode = new Node(current.name) { data = current.gameObject };
			tree.AddNodeNameReference(currentNode);
            // Check if the collection's suffix tree matches the query
            if (string.IsNullOrEmpty(query) || (currentNode.SuffixTree.Contains(query)))
            {
                hasMatchingDescendant = true; // This node matches
            }
            break;

        case "Path":
            currentNode = new Node(current.name) { data = current.gameObject };
			tree.AddNodeNameReference(currentNode);
            if (string.IsNullOrEmpty(query) || (currentNode.SuffixTree.Contains(query)))
            {
                hasMatchingDescendant = true; // This node matches
            }
            break;

        case "NodeParent":
            currentNode = new Node(current.name) { data = current.gameObject };
            tree.AddNodeNameReference(currentNode);
            if (string.IsNullOrEmpty(query) || (currentNode.SuffixTree.Contains(query)))
            {
                hasMatchingDescendant = true; // This node matches
            }
            break;

        case "Node":
            currentNode = new Node(current.name) { data = current.gameObject };
            tree.AddNodeNameReference(currentNode);
            if (string.IsNullOrEmpty(query) || (currentNode.SuffixTree.Contains(query)))
            {
                hasMatchingDescendant = true; // This node matches
            }
            break;

        case "EasyRoads":
			hasMatchingDescendant = true;
            break; // easy roads parents all roads
        default:
            return false; // Skip other tags, no matches possible
    }

    // Recursively check children to see if any descendants match the query
    for (int i = 0; i < current.childCount; i++)
    {
        bool childMatches = BuildTreeRecursive(current.GetChild(i), currentNode, transformToNodeMap, query);
        if (childMatches && currentNode != null)
        {
            hasMatchingDescendant = true; // A descendant matches
        }
    }

    // Only add the current node to the tree if it or any of its descendants match the query
    if (hasMatchingDescendant && currentNode != null)
    {
        // Safeguard: Ensure data is a valid GameObject
        if (currentNode.data is GameObject go && go != null && go.activeInHierarchy)
        {
            if (parentNode != null)
            {
                parentNode.nodes.AddWithoutNotify(currentNode);
            }
            else
            {
                tree.nodes.AddWithoutNotify(currentNode);
            }

            transformToNodeMap[current] = currentNode;
        }
        else
        {
            Debug.LogWarning($"Node '{current.name}' has invalid or destroyed data: {currentNode.data}. Skipping.");
            return false;
        }
    }

    return hasMatchingDescendant;
}

	private void OnNodeDragStart(Node node)
	{
		if (node == null || node.data == null) return;

		GameObject go = node.data as GameObject;
		if (go == null)
		{
			Debug.LogWarning("Dragged node has invalid data.");
			return;
		}

		// Rule: Path members ("Path" and "Node") are illiquid and cannot be dragged
		if (go.CompareTag("Path") || go.CompareTag("Node"))
		{
			Debug.Log($"Cannot drag '{node.name}' - Path members are illiquid.");
			return;
		}

		Debug.Log($"Started dragging '{node.name}'");
	}
	
private void OnNodeDrop(Node draggedNode, Node targetNode)
{
    if (draggedNode == null || draggedNode.data == null) return;

    GameObject draggedGO = draggedNode.data as GameObject;
    if (draggedGO == null || !draggedGO.activeInHierarchy)
    {
        Debug.LogWarning("Dragged node has invalid or destroyed data.");
        return;
    }

    // Rule: Path members are illiquid and cannot be dragged
    if (draggedGO.CompareTag("Path") || draggedGO.CompareTag("Node"))
    {
        Debug.Log($"Cannot drop '{draggedNode.name}' - Path members are illiquid.");
        return;
    }

    // If dropped on another node, handle reparenting or reordering
    if (targetNode != null && targetNode.data != null)
    {
        GameObject targetGO = targetNode.data as GameObject;
        if (targetGO == null || !targetGO.activeInHierarchy)
        {
            Debug.LogWarning("Target node has invalid or destroyed data.");
            return;
        }

        // Rule: Path members cannot be parents or have items moved into them
        if (targetGO.CompareTag("Path") || targetGO.CompareTag("Node"))
        {
            Debug.Log($"Cannot drop '{draggedNode.name}' onto '{targetNode.name}' - Path members are illiquid.");
            return;
        }

        // Rule: Collections can be parents
        if (targetGO.CompareTag("Collection"))
        {
            // Prevent reparenting a collection to itself or its descendants
            if (draggedGO.CompareTag("Collection") && IsDescendant(draggedNode, targetNode))
            {
                Debug.Log($"Cannot reparent '{draggedNode.name}' to '{targetNode.name}' - Target is a descendant.");
                return;
            }

            draggedNode.parentNode?.nodes.RemoveWithoutNotify(draggedNode);
            targetNode.nodes.AddWithoutNotify(draggedNode);
            draggedNode.parentNode = targetNode;
            draggedGO.transform.SetParent(targetGO.transform, true);
            Debug.Log($"Reparented '{draggedNode.name}' under collection '{targetNode.name}'");
        }
        // Rule: Prefabs cannot be parents, but allow reparenting/reordering for prefabs and collections when dropped on a prefab
        else if (targetGO.CompareTag("Prefab") && (draggedGO.CompareTag("Prefab") || draggedGO.CompareTag("Collection")))
        {
            // Get the target's parent (collection or root)
            var targetParentNode = targetNode.parentNode ?? tree.rootNode;
            var parentNodes = targetParentNode == tree.rootNode ? tree.nodes : targetParentNode.nodes;

            // Prevent reparenting a collection to its own descendants
            if (draggedGO.CompareTag("Collection") && IsDescendant(draggedNode, targetParentNode))
            {
                Debug.Log($"Cannot reparent '{draggedNode.name}' to '{targetParentNode?.name ?? "root"}' - Parent is a descendant.");
                return;
            }

            // Remove dragged node from its current parent
            draggedNode.parentNode?.nodes.RemoveWithoutNotify(draggedNode);

            // Insert before the target node in the target's parent
            int targetIndex = parentNodes.IndexOf(targetNode);
            if (targetIndex >= 0)
            {
                parentNodes.Insert(targetIndex, draggedNode);
                draggedNode.parentNode = targetParentNode;
                // Update Unity Transform hierarchy
                draggedGO.transform.SetParent(targetGO.transform.parent ? targetGO.transform.parent : PrefabManager.PrefabParent, true);
                draggedGO.transform.SetSiblingIndex(targetGO.transform.GetSiblingIndex());
                Debug.Log($"Reparented/reordered '{draggedNode.name}' before '{targetNode.name}' in parent '{targetParentNode?.name ?? "root"}'");
            }
        }
        else
        {
            Debug.Log($"Cannot drop '{draggedNode.name}' onto '{targetNode.name}' - Invalid operation.");
            return;
        }
    }
    else
    {
        // If dropped in empty space, move to root level as a child of the root node
        draggedNode.parentNode?.nodes.RemoveWithoutNotify(draggedNode);
        tree.nodes.AddWithoutNotify(draggedNode);
        draggedNode.parentNode = tree.rootNode;
        if (draggedGO.CompareTag("Prefab") || draggedGO.CompareTag("Collection"))
        {
            draggedGO.transform.SetParent(PrefabManager.PrefabParent, true);
        }
        Debug.Log($"Moved '{draggedNode.name}' to root level under tree's root node");
    }

    tree.Rebuild();
    CameraManager.Instance.NotifySelectionChanged();
    PrefabManager.NotifyItemsChanged(false);
}

private void OnNodeDragEnd(Node node)
{
    if (node == null) return;
    Debug.Log($"Finished dragging '{node.name}'");
}
	
	private Node FindFirstCheckedChild(Node parentNode)
	{
		if (parentNode == null || parentNode.nodes == null)
			return null;

		foreach (Node child in parentNode.nodes)
		{
			if (child.isChecked)
			{
				return child;
			}
		}
		return null;
	}
	
public void OnSelect(Node node)
{
	
    GameObject goSelect = (GameObject)node.data;
		

		// clear selection when not multi-selecting
		if (!Keyboard.current.leftShiftKey.isPressed)	{
			CameraManager.Instance.Unselect();
			UnselectAllInTree();			
		}

		//clear all roads when not selecting roads or nodes
		if(!goSelect.CompareTag("Node") && !goSelect.CompareTag("Path")){ 
			ClearRoads();
			CameraManager.Instance.NotifySelectionChanged();
			//CameraManager.Instance.UpdateItemsWindow();
		}
	
		//make path selection
		if (goSelect.CompareTag("Path"))		{
			ClearRoads();
			CameraManager.Instance._selectedRoad = goSelect;
			CameraManager.Instance.SelectRoad(goSelect);
			FocusList(FindFirstCheckedChild(node));
			CameraManager.Instance.NotifySelectionChanged();			
			//CameraManager.Instance.UpdateItemsWindow();
			return;
		}
		
		//make node selection
		if (goSelect.CompareTag("Node"))		{
			CameraManager.Instance._selectedRoad = goSelect.transform.parent.gameObject;
			CameraManager.Instance.SelectRoad(CameraManager.Instance._selectedRoad, goSelect);
			node.SetCheckedWithoutNotify(true);
			FocusList(node);
			CameraManager.Instance.NotifySelectionChanged();
			//CameraManager.Instance.UpdateItemsWindow();
			return;
		}
	

	

	//select children (nonpaths)
    if (Keyboard.current.leftAltKey.isPressed)    {
        SelectChildren(node, true);
        node.ExpandAll();
    }

    // Prevent duplicate selection (nonpaths)
    if (!CameraManager.Instance._selectedObjects.Contains(goSelect))        {
            node.SetCheckedWithoutNotify(true);
            CameraManager.Instance.SelectPrefabWithoutNotify(goSelect);
			FocusList(node);
            CameraManager.Instance.NotifySelectionChanged();
        }
	
	//CameraManager.Instance.UpdateItemsWindow();
}

public void ClearRoads()
{
    CameraManager.Instance.ClearAndHideSelection();
}

public void FocusFirstNode(Node node, GameObject roadObject)
{
    GameObject firstNodeGO = CameraManager.Instance.PopulateNodesForRoad(roadObject);
    if (firstNodeGO == null)
    {
        Debug.LogWarning($"Failed to populate nodes for '{roadObject.name}' in FocusFirstNode.");
        return;
    }

    Node firstNodeInTree = tree.FindFirstNodeByDataRecursive(firstNodeGO);
    if (firstNodeInTree != null)
    {
        Node current = firstNodeInTree;
        while (current != null && current != tree.rootNode)
        {
            current.isExpanded = true;
            current = current.parentNode;
        }
        firstNodeInTree.SetCheckedWithoutNotify(true);
        node.SetCheckedWithoutNotify(false);

        tree.Rebuild(); // Final rebuild after all changes
        tree.FocusOn(firstNodeInTree); // Final focus, overrides prior state
        Debug.Log($"Focused on first node '{firstNodeInTree.name}' of road '{roadObject.name}'.");
    }
    else
    {
        Debug.LogWarning($"Could not find tree node for first node '{firstNodeGO.name}'.");
        tree.Rebuild();
    }
}
	
	public void UnselectAllInTree(){
		ToggleNodes(false, query.text);
	}
	
	public void ToggleNodes(bool isChecked)	{
		CameraManager.Instance.Unselect();
		
		int count =0;
		if (tree != null){

			Stack<Node> stack = new Stack<Node>();
			stack.Push(tree.rootNode);

			while (stack.Count > 0)
			{
				Node currentNode = stack.Pop();
			
			for (int i = currentNode.nodes.Count - 1; i >= 0; i--)
				{
					Node childNode = currentNode.nodes[i];
					childNode.SetCheckedWithoutNotify(isChecked);
					
					if(isChecked){	CameraManager.Instance.SelectPrefabLight((GameObject)childNode.data);}
					
					count++;
					stack.Push(childNode);					
				}
			}
		}
		
		CameraManager.Instance.NotifySelectionChanged();
		CameraManager.Instance.UpdateGizmoState();
		tree.Rebuild();
	}
	

        public void ToggleNodes(bool isChecked, string query)
        {
            int count = 0;
            if (tree == null)
            {
                return;
            }
            if (!tree.searchable)
            {
                return;
            }
            if (string.IsNullOrEmpty(query))
            {
				ToggleNodes(isChecked);
                return;
            }

            Stack<Node> stack = new Stack<Node>();
            stack.Push(tree.rootNode);

            int nodesVisited = 0;
            while (stack.Count > 0)
            {
                Node currentNode = stack.Pop();
                nodesVisited++;


                for (int i = currentNode.nodes.Count - 1; i >= 0; i--)
                {
                    Node childNode = currentNode.nodes[i];
                    if (childNode.SuffixTree != null)
                    {
                        bool matches = childNode.SuffixTree.Contains(query);
                        if (matches)
                        {
                            childNode.SetCheckedWithoutNotify(isChecked);
							if(isChecked){	CameraManager.Instance.SelectPrefabLight((GameObject)childNode.data);	}
							else { CameraManager.Instance.UnselectPrefabLight((GameObject)childNode.data); }
                            count++;
                        }
                    }
                    stack.Push(childNode);
                }
            }

            if (count > 0)
            {
                tree.Rebuild();
            }
			
			CameraManager.Instance.NotifySelectionChanged();
			CameraManager.Instance.UpdateGizmoState();
            footer.text = count + " prefabs selected";
        }

		
	void SelectChildren(Node node, bool selected)
	{
		// Check if the node has children
		if (node != null && node.nodes != null)
		{
			Stack<Node> stack = new Stack<Node>();
			foreach (var child in node.nodes)
			{
				stack.Push(child);
			}

			while (stack.Count > 0)
			{
				Node currentNode = stack.Pop();
				currentNode.SetCheckedWithoutNotify(selected); // Toggle to true for selection
				CameraManager.Instance.SelectPrefabWithoutNotify((GameObject)node.data);

				// Push all children of current node onto the stack
				for (int i = currentNode.nodes.Count - 1; i >= 0; i--)
				{
					stack.Push(currentNode.nodes[i]);
				}
			}
		}
		tree.Rebuild();
	}
	

	
	private void OnQueryEntered(string query)
	{
		UnselectAllInTree();
		
		if (!string.IsNullOrEmpty(query))
		{
			matchingNodes = FindNodesByPartRecursive(tree.rootNode, query);
			if (matchingNodes.Count > 0)        {
				Node firstMatch = matchingNodes[0];
				firstMatch.isSelected = true;
				tree.FocusOn(firstMatch);
			}
			return;
		}
		matchingNodes = FindNodesByPartRecursive(tree.rootNode, "");
		PopulateList();
	}

private Node FindFirstNodeBySuffixTree(Node root, string query)
{
    if (tree == null)
    {
        Debug.LogWarning("FindFirstNodeBySuffixTree: Tree is null, cannot process query.");
        return null;
    }

    if (!tree.searchable)
    {
        Debug.LogWarning("FindFirstNodeBySuffixTree: Tree is not searchable, suffix trees are not enabled.");
        return null;
    }

    // Convert query to lowercase for case-insensitive matching
    query = query.ToLower();
    Debug.Log($"FindFirstNodeBySuffixTree: Searching for first match with query '{query}'");

    Stack<Node> stack = new Stack<Node>();
    stack.Push(root);

    int nodesVisited = 0;
    while (stack.Count > 0)
    {
        Node currentNode = stack.Pop();
        nodesVisited++;

        // Skip root node and nodes without a suffix tree or empty names
        if (currentNode != root && currentNode.SuffixTree != null && !string.IsNullOrEmpty(currentNode.name))
        {
            bool matches = currentNode.SuffixTree.Contains(query);
            Debug.Log($"Checking node, Name: '{currentNode.name ?? "null"}', ID: {currentNode.nodeId}, Query: '{query}', Matches: {matches}");
            if (matches)
            {
                Debug.Log($"Found first matching node, Name: '{currentNode.name}', ID: {currentNode.nodeId}, Nodes visited: {nodesVisited}");
                return currentNode;
            }
        }
        else if (currentNode != root && currentNode.SuffixTree == null)
        {
            Debug.LogWarning($"Node has no SuffixTree, Name: '{currentNode.name ?? "null"}', ID: {currentNode.nodeId}");
        }

        // Push children in reverse order to maintain consistent traversal
        for (int i = currentNode.nodes.Count - 1; i >= 0; i--)
        {
            stack.Push(currentNode.nodes[i]);
        }
    }

    Debug.Log($"FindFirstNodeBySuffixTree: Completed, Total nodes visited: {nodesVisited}, No match found for query: '{query}'");
    return null;
}
	
	public void CheckSelection()
	{
		if (tree == null) return;

		Stack<Node> stack = new Stack<Node>();
		stack.Push(tree.rootNode);

		while (stack.Count > 0)
		{
			Node currentNode = stack.Pop();
			currentNode.SetCheckedWithoutNotify(CameraManager.Instance._selectedObjects.Contains((GameObject)currentNode.data));

			for (int i = currentNode.nodes.Count - 1; i >= 0; i--)
			{
				stack.Push(currentNode.nodes[i]);
			}
		}
		tree.Rebuild();
	}
	
	public void UncheckAll(){
		ToggleNodes(false);
	}

	public void UncheckNodes(){
		OnQuery(query.text);
		ToggleNodes(false);
	}
	
	private void CheckNodes(){
		OnQuery(query.text);
		CameraManager.Instance.Unselect();
		ToggleNodes(true, query.text);
	}
	

	private void DeleteCheckedNodes()
	{
		DeleteCheckedNodesStack(tree.rootNode);
		CameraManager.Instance._selectedObjects.Clear();
		tree.Rebuild();
		CameraManager.Instance.UpdateGizmoState();
		
		PrefabManager.NotifyItemsChanged(false);
	}

	private void DeleteCheckedNodesStack(Node rootNode)
	{
		Stack<Node> stack = new Stack<Node>();
		stack.Push(rootNode);

		while (stack.Count > 0)
		{
			Node currentNode = stack.Pop();

			for (int i = currentNode.nodes.Count - 1; i >= 0; i--)
			{
				Node childNode = currentNode.nodes[i];

				if (!childNode.isChecked)
				{
					stack.Push(childNode);
					continue;
				}

				if (childNode.data is PrefabDataHolder childPrefabData && childPrefabData.gameObject != null)
				{
					Destroy(childPrefabData.gameObject);
					currentNode.nodes.RemoveAtWithoutNotify(i);
				}
				else if (childNode.data is Transform childTransform)
				{
					Destroy(childTransform.gameObject);
					currentNode.nodes.RemoveAtWithoutNotify(i);
				}
				else
				{
					stack.Push(childNode);
				}
			}

			if (!currentNode.isChecked)
				continue;

			if (currentNode.data is GameObject go)
			{
				Destroy(go);
				tree.nodes.RemoveWithoutNotify(currentNode);
			}
		}
	}

	private List<Node> FindNodesByPartRecursive(Node currentNode, string query){
	
		List<Node> matches = new List<Node>();

		if (currentNode.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)    {
			matches.Add(currentNode);
		}

		foreach (Node child in currentNode.nodes)    {
			matches.AddRange(FindNodesByPartRecursive(child, query));
		}

		return matches;
	}
	
	private void FocusNextMatch()
	{
		if (matchingNodes.Count == 0) return;

		currentMatchIndex = (currentMatchIndex + 1) % matchingNodes.Count;

		Node nextMatch = matchingNodes[currentMatchIndex];
		nextMatch.isSelected = true;
		tree.FocusOn(nextMatch);
	}
		
	private void OnQuery(string query)
	{
		PopulateList();
	}

}
