using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UIRecycleTreeNamespace;
using RustMapEditor.Variables;

public class HierarchyWindow : MonoBehaviour
{
	public static HierarchyWindow Instance { get; private set; }

    public UIRecycleTree tree;
	public InputField query;
	public Text footer;
	public Button sendIDToBreaker;
	public Toggle showAll, blacklist, hide;
	public GeologyItem item;
	
	public GameObject itemTemplate;
	public GameObject content;
	
	private void Awake()
    {
        // Ensure only one instance of HierarchyWindow exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: Persist across scenes
        }
    }

	
    private void Start()
    {
        AssetManager.Callbacks.BundlesLoaded += OnBundlesLoaded;
		
		query.onValueChanged.AddListener(OnQueryEntered);
		tree.onSelectionChanged.AddListener(OnSelect);
		tree.onNodeCheckedChanged.AddListener(OnCheck);
		
		sendIDToBreaker.onClick.AddListener(SendIDToBreaker);
		
		hide.interactable = false;
		blacklist.interactable = false;
		
		showAll.onValueChanged.AddListener((isOn) => OnShowToggleChanged());
		blacklist.onValueChanged.AddListener((isOn) => OnBlacklistToggleChanged());
		hide.onValueChanged.AddListener((isOn) => OnHideToggleChanged());
		
	    // Subscribe to tree's drag-and-drop events
		tree.onNodeDragStart += OnNodeDragStart;
		tree.onNodeDrop += OnNodeDrop;
		tree.onNodeDragEnd += OnNodeDragEnd;
    }
	
	public void OnShowToggleChanged(){
		OnQueryEntered(query.text);
	}
	
	

	private void UpdateItemBlacklist(Node selection, bool isBlacklistToggle, bool toggleValue)
	{
		if (selection == null || string.IsNullOrEmpty(selection.fullPath))
		{
			Debug.LogWarning("No valid selection provided for updating blacklist.");
			return; // No valid selection, exit early
		}

		// Determine path with ".prefab" suffix for consistency
		string path = selection.fullPath;
		string dictKey = path + ".prefab";

		// Log for debugging
		Debug.Log($"Updating blacklist for path: {dictKey}, BlacklistToggle: {isBlacklistToggle}, Value: {toggleValue}");

		// Get or create ItemSettings
		if (!PrefabManager.ItemBlacklist.TryGetValue(dictKey, out ItemSettings settings))
		{
			settings = new ItemSettings
			{
				path = dictKey,
				blacklisted = false,
				hidden = false
			};
		}

		// Update the relevant field
		if (isBlacklistToggle)
		{
			settings.blacklisted = toggleValue;
		}
		else
		{
			settings.hidden = toggleValue;
		}

		// Update dictionary with consistent key
		PrefabManager.ItemBlacklist[dictKey] = settings;

		// Save blacklist
		PrefabManager.SaveBlacklist();
		
		selection.styleIndex = SettingsManager.GetNodeStyleIndex(selection, path);  //give UI color feedback
	}

	public void OnBlacklistToggleChanged()
	{
		UpdateItemBlacklist(tree.selectedNode, true, blacklist.isOn);
	}

	public void OnHideToggleChanged()
	{
		UpdateItemBlacklist(tree.selectedNode, false, hide.isOn);
	}

	void SendIDToBreaker(){
		if(BreakerWindow.Instance !=null){
			BreakerWindow.Instance.fields[19].text = footer.text;
		}
	}

	public void OnEnable()
	{
		if (tree.nodesCount == -1){
			tree.Clear();
			LoadTree();
		}
	}


	
	private GeologyItem selectedNodeItem(){
		GeologyItem foundItem = new GeologyItem();
		
		if (tree.selectedNode.hasChildren){
			return null;
		}
		
		if (tree.selectedNode == null){
			return null;
		}
		
		string path;
		path = tree.selectedNode.fullPath;
		
		if(tree.selectedNode.data!=null){
			path=(string)tree.selectedNode.data;
		}
		
		if(path == null){
			return null;
		}
		
		if (path[0] == '~')		{		
			path = path.Replace("~", "", StringComparison.Ordinal);
			path = path.Replace("\\", "/", StringComparison.Ordinal);
			foundItem.custom=true;
			foundItem.customPrefab = path;
			foundItem.prefabID = 0;
			foundItem.emphasis = 1;
		}
		else {
			foundItem.custom=false;
			foundItem.prefabID =  AssetManager.ToID(path + ".prefab");
			foundItem.customPrefab = "";
			foundItem.emphasis = 1;
		}
		return foundItem;
	}
	
	private GeologyItem selectedNodeItem(Node node)
	{
		GeologyItem foundItem = new GeologyItem();
		
		if (node == null || node.hasChildren)
		{
			return null;
		}
		
		string path = node.fullPath;
		
		if (node.data != null)
		{
			path = (string)node.data;
		}
		
		if (path == null)
		{
			return null;
		}
		
		if (path[0] == '~')
		{        
			path = path.Replace("~", "", StringComparison.Ordinal);
			path = path.Replace("\\", "/", StringComparison.Ordinal);
			foundItem.custom = true;
			foundItem.customPrefab = path;
			foundItem.prefabID = 0;
			foundItem.emphasis = 1;
		}
		else
		{
			foundItem.custom = false;
			foundItem.prefabID = AssetManager.ToID(path + ".prefab");
			foundItem.customPrefab = "";
			foundItem.emphasis = 1;
		}
		
		return foundItem;
	}
	
	private void OnGeologyPressed()	{
		SettingsManager.geology.geologyItems.Add(selectedNodeItem());
		PopulateItemList();
	}
	
	public void PlacePrefab(Vector3 position){
		if(tree.selectedNode!=null){
			GenerativeManager.SpawnFeature(selectedNodeItem(), position-PrefabManager.PrefabParent.transform.position, Vector3.zero, Vector3.one, PrefabManager.PrefabParent);
			PrefabManager.NotifyItemsChanged();
		}
	}
	
	private void OnPlaceOrigin(){
		if(tree.selectedNode!=null){
		GenerativeManager.SpawnFeature(selectedNodeItem(), Vector3.zero, Vector3.zero, Vector3.one, PrefabManager.PrefabParent);
		PrefabManager.NotifyItemsChanged();
		Transform origin = PrefabManager.PrefabParent.GetChild(PrefabManager.PrefabParent.childCount - 1);
		
		//enable the window for access
		AppManager.Instance.ActivateWindow(5);
		BreakerWindow.Instance.PopulateTree(origin);
		}
	}
	
	public void PopulateItemList()
	{
		if (SettingsManager.geology.geologyItems == null){
			Debug.Log("invalid geology list, resetting");
			SettingsManager.SetDefaultGeology();
		}
		ClearItemList();
		Debug.Log("validated geology items list and cleared UI");
		foreach (GeologyItem item in SettingsManager.geology.geologyItems)
		{
			var itemCopy = Instantiate(itemTemplate);
			var itemPathText = itemCopy.transform.Find("ItemPath").GetComponent<Text>();
			var itemWeight = itemCopy.transform.Find("WeightField").GetComponent<InputField>();
			var button = itemCopy.transform.Find("RemoveItem").GetComponent<Button>();
			string path;
			
			//Debug.Log("template loaded");
			if (item == null)
			{
				continue;
			}
			
			if (item.custom){
				itemPathText.text = item.customPrefab;
			}
			else{
				path = AssetManager.ToPath(item.prefabID);
				string fileName = path.Substring(path.LastIndexOf('/') + 1); // Get file name after last folder
				itemPathText.text = fileName.Replace(".prefab", "", StringComparison.Ordinal);
			}
			//Debug.Log("item identified");
			
			itemWeight.text = item.emphasis.ToString();
			//Debug.Log("item ui element created");
			
			var currentItem = item;
			button.onClick.AddListener(() =>
			{
				SettingsManager.geology.geologyItems.Remove(currentItem); 
				PopulateItemList(); 
			});
			
			itemWeight.onValueChanged.AddListener(value =>
			{
				if (float.TryParse(value, out float newEmphasis))
				{
					currentItem.emphasis = (int)newEmphasis;
				}
			});
			//Debug.Log("item ui listener methods created");
			
			itemCopy.transform.SetParent(content.transform, false);
			itemCopy.gameObject.SetActive(true);
			//Debug.Log("item parented and activated");
		}
		Debug.Log("populated UI");
	}
	
	public void ClearItemList(){
		foreach (Transform child in content.transform)
		{
			Destroy(child.gameObject);
		}
	}

		// Called when a Node starts being dragged
	private void OnNodeDragStart(Node node)
	{
		//Debug.Log("drag start");
		//_draggedNode = node; // Track the Node
	}

	// Called when a Node is dropped
	private void OnNodeDrop(Node draggedNode, Node targetNode)
	{
		int landMask = 1 << 10;
		
		if (draggedNode == null ){ return;	}
		// If dropped on another node, handle reordering/reparenting (optional)
		if (targetNode != null)		{
			Debug.Log($"Dropped {draggedNode.name} onto {targetNode.name}");
			return;
		}
		
		// Check if dropped on UI panels
		if (EventSystem.current.IsPointerOverGameObject())
		{
			PointerEventData pointerData = new PointerEventData(EventSystem.current)
			{
				position = Mouse.current.position.ReadValue()
			};
			
			List<RaycastResult> results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(pointerData, results);
			
			foreach (RaycastResult result in results)
			{
				if (result.gameObject.name == "GeologyWindow")
				{
					SettingsManager.geology.geologyItems.Add(selectedNodeItem(draggedNode));
					Debug.Log("added dragged node to list");
					PopulateItemList();
					Debug.Log("populated item list");
					return;
				}
				else if (result.gameObject.name == "BreakerWindow")
				{
					draggedNode.isSelected = true;
					Debug.Log($"Dropped {draggedNode.name} on BreakerWindow");
					OnPlaceOrigin();
					return;
				}
			}
			
			// If dropped on other UI elements, return
			return;
		}	

		Vector2 mousePosition = Mouse.current.position.ReadValue();
		Ray ray = CameraManager.Instance.cam.ScreenPointToRay(mousePosition);
		if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, landMask))
		{
			// Use the dragged Node directly
			draggedNode.isSelected = true; // Set for selectedNodeItem()
			GeologyItem item = selectedNodeItem();
			if (item != null)
			{
				// Spawn prefab at hit point, same as PlacePrefab
				GenerativeManager.SpawnFeature(item, hit.point - PrefabManager.PrefabParent.transform.position, Vector3.zero, Vector3.one, PrefabManager.PrefabParent);
				PrefabManager.NotifyItemsChanged();
			}
		}
	}

	// Called when a Node drag ends
	private void OnNodeDragEnd(Node node)
	{
		//_draggedNode = null; // Clear the dragged Node
	}

	private void OnCheck(Node selection){
		if(selection.hasChildren){
			selection.isSelected = true;
			return;
		}
		
		Node faveRoot = tree.rootNode.FindNodeByNameRecursive ("~Favorites");
		SettingsManager.UpdateFavorite(selection);
		
		Node faveNode = new Node(selection.name);
		faveNode.data=selection.fullPath;
		
		if(selection.isChecked){
			faveNode.SetCheckedWithoutNotify(true);
			faveRoot.nodes.Add(faveNode);
		}
		else{
			faveRoot.nodes.Remove(selection);
		}
		
		SettingsManager.CheckFavorites(tree);
	}



	private void OnSelect(Node selection){
		
		if (selection.hasChildren){
			selection.isExpanded=selection.isSelected;
			blacklist.interactable = false;
			hide.interactable = false;
			blacklist.SetIsOnWithoutNotify(false);
			hide.SetIsOnWithoutNotify(false);
			footer.text = "";
			return;
		}
		
		if(string.IsNullOrEmpty(selection.fullPath)){
			blacklist.interactable = false;
			hide.interactable = false;
			blacklist.SetIsOnWithoutNotify(false);
			hide.SetIsOnWithoutNotify(false);
			footer.text = "";
			return;
		}
		
		//determine prefab path for determining ID
		string path = selection.fullPath;
		
		// Check if path exists in ItemBlacklist and set toggle values
		if (PrefabManager.ItemBlacklist.TryGetValue(path + ".prefab", out ItemSettings settings))
		{
			blacklist.SetIsOnWithoutNotify(settings.blacklisted);
			hide.SetIsOnWithoutNotify(settings.hidden);
		}
		else
		{
			// If path not in blacklist, set toggles to false without notifying
			blacklist.SetIsOnWithoutNotify(false);
			hide.SetIsOnWithoutNotify(false);
		}
		
		if(selection.data!=null){
			path=(string)selection.data;
		}
		
		uint ID =  AssetManager.ToID(path + ".prefab");
		
		
		if (path[0] == '~')		{
			footer.text = path + ".prefab";
			blacklist.interactable = true;
			hide.interactable = true;
			return;
		}
		
		if (ID != 0)	{
			footer.text = "ID " + ID;
			blacklist.interactable = true;
			hide.interactable = true;
			return;
		}
		
		blacklist.interactable = false;
		hide.interactable = false;
		blacklist.SetIsOnWithoutNotify(false);
		hide.SetIsOnWithoutNotify(false);
		footer.text = "";
	}
	
    private void OnBundlesLoaded()
    {
        LoadTree();
    }
	
	public void LoadTree(){	
		List<string> paths = new List<string>();
		//Debug.Log("loading asset tree");
		// Initialize favoriteCustoms if null
	if (SettingsManager.faves.favoriteCustoms == null)
	{
		var tempFaves = SettingsManager.faves; // Copy the struct
		tempFaves.favoriteCustoms = new List<string>();
		SettingsManager.faves = tempFaves; // Reassign to persist
	}
		
		foreach (var path in SettingsManager.faves.favoriteCustoms)		{
				paths.Add("~Favorites/" + path);
		}
		paths.AddRange(AssetManager.BundleLookup.Keys);
		paths.AddRange(AssetManager.SceneAssetCache.Keys);

		string basePath = Path.Combine(SettingsManager.AppDataPath(), "Custom");

		List<string> collectionPaths = SettingsManager.GetDataPaths(basePath, "Custom");
		
		paths.AddRange(collectionPaths);


		SettingsManager.ConvertPathsToNodes(tree, paths, ".prefab", ".monument", query.text, showAll.isOn);
		SettingsManager.CheckFavorites(tree);
		//Debug.Log("asset tree loaded");
	}


	public void OnQueryEntered(string query)    {
		tree.Clear();
        LoadTree();
		if(!query.Equals("",StringComparison.Ordinal)){
			tree.ExpandAll();
			}
		else{
			tree.CollapseAll();
		}
    }
	



	
}
