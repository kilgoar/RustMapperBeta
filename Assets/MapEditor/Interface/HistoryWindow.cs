using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UIRecycleTreeNamespace;
using RustMapEditor.Variables;

public class HistoryWindow : MonoBehaviour
{
    public UIRecycleTree tree;
    public Text footer;

    public static HistoryWindow Instance { get; private set; }

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

    private void OnEnable()
    {
        // Subscribe to UndoManager state stack changed event
        UndoManager.OnStateStackChanged += PopulateList;
        PopulateList();
    }

    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        UndoManager.OnStateStackChanged -= PopulateList;
    }

    private void Start()
    {
        InitializeComponents();
        PopulateList();
    }

    private void InitializeComponents()
    {
        if (tree != null)
        {
            tree.onSelectionChanged.AddListener(OnSelect);
            tree.onNodeDblClick.AddListener(OnNodeDoubleClick);
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
    Debug.Log("PopulateList: Cleared tree nodes.");

    // Get undo and redo stacks
    var undoStack = UndoManager.GetUndoStack().ToList();
    var redoStack = UndoManager.GetRedoStack().ToList();
    Debug.Log($"Undo stack count: {undoStack.Count}, Redo stack count: {redoStack.Count}");
    Debug.Log($"Undo stack: {string.Join(", ", undoStack.Select(a => a.OperationName))}");
    Debug.Log($"Redo stack: {string.Join(", ", redoStack.Select(a => a.OperationName))}");

    // Combine actions in chronological order (newest first)
    var allActions = new List<IUndoAction>();
    allActions.AddRange(redoStack); // Redo stack (assumed newest first)
    allActions.AddRange(undoStack.AsEnumerable().Reverse()); // Undo stack, reversed to put newest first
    Debug.Log($"All actions (newest first): {string.Join(", ", allActions.Select(a => a.OperationName))}");

    // Group actions by OperationName
    var groupedActions = new List<Node>();
    Node currentGroupNode = null;
    Node focusNode = null;
    string lastOperationName = null;
    int actionIndex = allActions.Count - 1; // Start from highest index for newest action

    for (int i = 0; i < allActions.Count; i++)
    {
        var action = allActions[i];
        string operationName = action.OperationName;

        // Start a new group if OperationName changes or this is the first action
        if (operationName != lastOperationName || currentGroupNode == null)
        {
            if (currentGroupNode != null)
            {
                groupedActions.Add(currentGroupNode);
            }

            string groupName = $"{operationName} ({(action is TerrainUndoAction ? "Terrain" : "Gizmo")})";
            currentGroupNode = new Node(groupName) { isExpanded = true };
            lastOperationName = operationName;
        }

        // Add action to the current group
        Node actionNode = new Node($"{operationName} ({actionIndex})") { data = action };
        actionNode.styleIndex = redoStack.Contains(action) ? 1 : 0;
        tree.AddNodeNameReference(actionNode);
        currentGroupNode.nodes.AddWithoutNotify(actionNode);
        Debug.Log($"Added node: {actionNode.name} (styleIndex: {actionNode.styleIndex})");
        actionIndex--;

        // Set focusNode to the most recent undo action
        if (undoStack.Count > 0 && action == undoStack[undoStack.Count - 1])
        {
            focusNode = actionNode;
            Debug.Log($"Set focusNode to most recent undo: {focusNode.name} (index {i})");
        }
    }

    // Add the last group
    if (currentGroupNode != null)
    {
        groupedActions.Add(currentGroupNode);
    }

    // If focusNode is null, set to the most recent node (first node in first group)
    if (focusNode == null && groupedActions.Count > 0)
    {
        focusNode = groupedActions[0].nodes.FirstOrDefault();
        Debug.Log($"FocusNode null, set to most recent node: {(focusNode != null ? focusNode.name : "None")}");
    }

    // Add grouped nodes to the tree (newest groups at the top)
    foreach (var groupNode in groupedActions)
    {
        tree.nodes.AddWithoutNotify(groupNode);
    }
    Debug.Log($"Tree nodes (before rebuild): {string.Join(", ", tree.nodes.Select(n => n.name))}");


    tree.Rebuild();
    Debug.Log($"Tree rebuilt with nodes: {string.Join(", ", tree.nodes.Select(n => n.name))}");

    // Focus on the node
    if (focusNode != null)
    {
        Debug.Log($"Focusing on node: {focusNode.name}");
        tree.FocusOn(focusNode);
    }
    else
    {
        Debug.LogWarning("PopulateList: No focusNode available to focus.");
    }

    UpdateFooter();
}

    private void OnSelect(Node node)
    {
        if (node == null || node.data == null)
        {
            return;
        }

        IUndoAction selectedAction = node.data as IUndoAction;
        if (selectedAction == null)
        {
            Debug.LogWarning($"Selected node '{node.name}' has invalid data.");
            return;
        }

        JumpToAction(node);
    }

    private void OnNodeDoubleClick(Node node)
    {
        if (node == null || node.data == null)
        {
            return;
        }

        JumpToAction(node);
    }

    private void JumpToAction(Node node)
    {
        IUndoAction targetAction = node.data as IUndoAction;
        if (targetAction == null)
        {
            Debug.LogWarning($"Node '{node.name}' has invalid action data.");
            return;
        }

        // Convert read-only lists to List<IUndoAction> for Contains and IndexOf
        var undoStack = UndoManager.GetUndoStack().ToList();
        var redoStack = UndoManager.GetRedoStack().ToList();

        // Determine if the action is in the undo or redo stack
        bool isUndoStack = undoStack.Contains(targetAction);
        int targetIndex = isUndoStack ? undoStack.IndexOf(targetAction) : redoStack.IndexOf(targetAction);

        if (isUndoStack)
        {
            // Undo until we reach the target action
            while (undoStack.Count > targetIndex)
            {
                UndoManager.Undo();
                undoStack = UndoManager.GetUndoStack().ToList(); // Refresh the list after undo
            }
        }
        else
        {
            // Redo until we reach the target action
            int redoCount = redoStack.Count - targetIndex;
            for (int i = 0; i < redoCount; i++)
            {
                UndoManager.Redo();
                redoStack = UndoManager.GetRedoStack().ToList(); // Refresh the list after redo
            }
        }

        // No need to call PopulateList here, as Undo/Redo triggers OnStateStackChanged
        Debug.Log($"Jumped to action: '{node.name}'");
    }

    private void UpdateFooter()
    {
        int totalActions = UndoManager.GetUndoStack().Count + UndoManager.GetRedoStack().Count;
        long totalMemory = 0;
        foreach (var action in UndoManager.GetUndoStack())
        {
            totalMemory += action.EstimateMemoryUsage();
        }
        foreach (var action in UndoManager.GetRedoStack())
        {
            totalMemory += action.EstimateMemoryUsage();
        }
        footer.text = $"{totalActions} actions, {(totalMemory / (1024f * 1024f)):F2} MB";
    }
}