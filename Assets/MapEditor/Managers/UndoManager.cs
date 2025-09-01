using UnityEngine;
using System.Collections.Generic;
using RustMapEditor.Variables;

public static class UndoManager
{
    private static List<IUndoAction> undoStack = new List<IUndoAction>();
    private static List<IUndoAction> redoStack = new List<IUndoAction>();
    private static long totalMemoryUsage = 0;
    private static int maxActions = 512;

    // Event to notify when the undo/redo state stacks change
    public delegate void StateStackChangedHandler();
    public static event StateStackChangedHandler OnStateStackChanged;

    public static int MaxActions { get => maxActions; set => maxActions = Mathf.Max(1, value); }

    public static IReadOnlyList<IUndoAction> GetUndoStack() => undoStack.AsReadOnly();
    public static IReadOnlyList<IUndoAction> GetRedoStack() => redoStack.AsReadOnly();

    public static void RegisterAction(IUndoAction action)
    {
        totalMemoryUsage += action.EstimateMemoryUsage();
        Debug.Log($"Registered action '{action.OperationName}'. Total memory: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");

        undoStack.Add(action);
        redoStack.Clear();

        while (undoStack.Count > maxActions || totalMemoryUsage > maxActions * 1024 * 1024)
        {
            if (undoStack.Count == 0) break;
            var oldestAction = undoStack[0];
            totalMemoryUsage -= oldestAction.EstimateMemoryUsage();
            undoStack.RemoveAt(0);
            oldestAction.OnRemoved();
            Debug.Log($"Removed oldest action '{oldestAction.OperationName}'. New total: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");
        }

        OnStateStackChanged?.Invoke(); // Notify listeners of state stack change
    }

    public static void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("No actions to undo.");
            return;
        }

        var action = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);
        redoStack.Add(action);

        action.Undo();
        totalMemoryUsage -= action.EstimateMemoryUsage();
        Debug.Log($"Undid action: '{action.OperationName}'. Total memory: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");

        OnStateStackChanged?.Invoke(); // Notify listeners of state stack change
    }

    public static void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.Log("No actions to redo.");
            return;
        }

        var action = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);
        undoStack.Add(action);

        action.Redo();
        totalMemoryUsage += action.EstimateMemoryUsage();
        Debug.Log($"Redid action: '{action.OperationName}'. Total memory: {(totalMemoryUsage / (1024f * 1024f)):F2} MB");

        OnStateStackChanged?.Invoke(); // Notify listeners of state stack change
    }
	

    public static void ClearHistory()
    {
        foreach (var action in undoStack)
        {
            action.OnRemoved();
        }
        foreach (var action in redoStack)
        {
            action.OnRemoved();
        }
        undoStack.Clear();
        redoStack.Clear();
        totalMemoryUsage = 0;
        Debug.Log("Cleared undo/redo history.");

        OnStateStackChanged?.Invoke(); // Notify listeners of state stack change
    }
}