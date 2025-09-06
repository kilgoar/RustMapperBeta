using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RustMapEditor.Variables;

public static class BindManager
{
    private static Dictionary<string, InputAction> bindActions = new Dictionary<string, InputAction>();
    public static List<Bind> binds = new List<Bind>();

    // Ensure initialization on first access
    public static void RuntimeInit()
    {
        LoadBinds();
		if(binds == null){
			SetDefaultBinds();
		}
    }
	
	    public static void ResetBind(string bindName)
    {
        var bind = binds.Find(b => b.bindName == bindName);
        if (bind.Equals(default(Bind)))
        {
            Debug.LogWarning($"Cannot reset bind '{bindName}': not found.");
            return;
        }

        // Start listening for new input
        InputAction action;
        if (!bindActions.TryGetValue(bindName, out action))
        {
            Debug.LogWarning($"Action for bind '{bindName}' not found.");
            return;
        }

        // Disable action temporarily
        action.Disable();

        // Perform rebinding operation
        var rebindingOperation = action.PerformInteractiveRebinding()
            .WithControlsExcluding("<Mouse>/position") // Exclude mouse position
            .WithControlsExcluding("<Mouse>/delta")    // Exclude mouse delta
            .OnMatchWaitForAnother(0.1f)              // Wait time between inputs
            .OnComplete(operation =>
            {
                // Get the new input path
                string newInput = operation.selectedControl.path;

                // Check for modifier keys during rebinding
                bool ctrlPressed = Keyboard.current.ctrlKey.isPressed;
                bool shiftPressed = Keyboard.current.leftShiftKey.isPressed;
                bool altPressed = Keyboard.current.leftAltKey.isPressed;

                // Update bind with new input and modifier states
                UpdateBind(
                    bindName,
                    newInput,
                    ctrlPressed,
                    shiftPressed,
                    altPressed
                );

                // Re-enable action
                action.Enable();
                operation.Dispose();

                Debug.Log($"Bind '{bindName}' rebound to {newInput} (Ctrl: {ctrlPressed}, Shift: {shiftPressed}, Alt: {altPressed})");

                // Update UI
                SettingsWindow.Instance?.PopulateBindUI();
            })
            .OnCancel(operation =>
            {
                // Re-enable action if cancelled
                action.Enable();
                operation.Dispose();
                Debug.Log($"Rebinding for '{bindName}' cancelled");
            });

        // Start the rebinding
        rebindingOperation.Start();
    }

    public static void RemoveBind(string bindName)
    {
        var bindIndex = binds.FindIndex(b => b.bindName == bindName);
        if (bindIndex == -1)
        {
            Debug.LogWarning($"Cannot remove bind '{bindName}': not found.");
            return;
        }

        // Update bind to have no input and reset modifiers
        binds[bindIndex] = new Bind(
            bindName,
            binds[bindIndex].actionType,
            string.Empty, // Clear input
            false,       // Reset Ctrl
            false,       // Reset Shift
            false        // Reset Alt
        );

        SettingsManager.SaveSettings();
        
        Debug.Log($"Bind '{bindName}' cleared (modifiers reset)");

        // Update UI
        SettingsWindow.Instance?.PopulateBindUI();
    }
	
	public static void LoadBinds(){
		binds = SettingsManager.binds;
		
		if(binds.Count == 0)
		{
			SetDefaultBinds();
			return;
		}
		
		bindActions = new Dictionary<string, InputAction>();		
		foreach (var bind in binds)
        {
            var action = new InputAction(bind.bindName, bind.actionType, bind.primaryInput);
            action.Enable();
            bindActions.Add(bind.bindName, action);
        }
		Debug.Log("actions initialized");
	}
	
	public static void GetBinds(){
		SettingsManager.binds = binds;
	}

    public static void SetDefaultBinds()
    {
		Debug.Log("setting default binds");
        binds = new List<Bind>();
     binds.AddRange(new[]
		{
			// Camera movement
			new Bind("moveForward", InputActionType.Button, "<Keyboard>/w", false, false, false),
			new Bind("moveBackward", InputActionType.Button, "<Keyboard>/s", false, false, false),
			new Bind("moveLeft", InputActionType.Button, "<Keyboard>/a", false, false, false),
			new Bind("moveRight", InputActionType.Button, "<Keyboard>/d", false, false, false),
			new Bind("moveUp", InputActionType.Button, "<Keyboard>/space", false, false, false),
			new Bind("moveDown", InputActionType.Button, "<Keyboard>/z", false, false, false),
			new Bind("moveSlow", InputActionType.Button, "<Keyboard>/leftAlt", false, false, false),
			new Bind("moveVerySlow", InputActionType.Button, "<Keyboard>/leftShift", false, false, true),			
			new Bind("moveFast", InputActionType.Button, "<Keyboard>/leftShift", false, false, false),
			new Bind("rotateCamera", InputActionType.Button, "<Mouse>/rightButton", false, false, false),
			// Gizmo controls
			new Bind("gizmoMove", InputActionType.Button, "<Keyboard>/y", false, false, false),
			new Bind("gizmoRotate", InputActionType.Button, "<Keyboard>/e", false, false, false),
			new Bind("gizmoScale", InputActionType.Button, "<Keyboard>/r", false, false, false),
			new Bind("gizmoUniversal", InputActionType.Button, "<Keyboard>/t", false, false, false),
			new Bind("gizmoToggleSpace", InputActionType.Button, "<Keyboard>/x", false, false, false),
			// special control
			new Bind("transparencyToggle", InputActionType.Button, "<Keyboard>/f", false, false, false),
			// Selection controls
			new Bind("duplicate", InputActionType.Button, "<Keyboard>/d", true, false, false),
			new Bind("createParent", InputActionType.Button, "<Keyboard>/a", true, false, false),
			new Bind("flatten", InputActionType.Button, "<Keyboard>/f", true, false, false),
			new Bind("delete", InputActionType.Button, "<Keyboard>/delete", false, false, false),
			new Bind("selectPrefab", InputActionType.Button, "<Mouse>/leftButton", false, false, false),
			new Bind("multiSelect", InputActionType.Button, "<Keyboard>/leftShift", false, false, false),
			// Prefab and path placement
			new Bind("placePrefab", InputActionType.Button, "<Mouse>/leftButton", false, false, true),
			new Bind("placePrefabFluid", InputActionType.Button, "<Mouse>/leftButton", false, true, true),
			new Bind("placePath", InputActionType.Button, "<Mouse>/leftButton", false, false, true),
			new Bind("placePathFluid", InputActionType.Button, "<Mouse>/leftButton", false, true, true),
			// Terrain editing
			new Bind("paintBrush", InputActionType.Button, "<Mouse>/leftButton", false, false, false),
			new Bind("sampleHeight", InputActionType.Button, "<Mouse>/leftButton", true, false, false),
			
			
			new Bind("undo", InputActionType.Button, "<Keyboard>/z", true, false, false),
			new Bind("redo", InputActionType.Button, "<Keyboard>/y", true, false, false),
			
			new Bind("socketConnect", InputActionType.Button, "<Keyboard>/c", false, false, false),
			new Bind("socketSelect", InputActionType.Button, "<Keyboard>/g", false, false, false),
			new Bind("createSocket", InputActionType.Button, "<Keyboard>/v", false, false, false)
		});

		Debug.Log("default bind list created, initializing actions");
        // Initialize InputActions for each bind
        foreach (var bind in binds)
        {
            var action = new InputAction(bind.bindName, bind.actionType, bind.primaryInput);
            action.Enable();
            bindActions.Add(bind.bindName, action);
        }
		Debug.Log("actions initialized");
		SettingsManager.SaveSettings();
    }

    // Check if the bind was pressed this frame
    public static bool WasPressedThisFrame(string bindName)
    {

        if (!bindActions.TryGetValue(bindName, out var action))
        {
            Debug.LogWarning($"Bind '{bindName}' not found.");
            return false;
        }

        var bind = binds.Find(b => b.bindName == bindName);
        if (bind.Equals(default(Bind)))
        {
            Debug.LogWarning($"Bind '{bindName}' not found in bind list.");
            return false;
        }

        // Check modifiers
        bool modifiersSatisfied =
            (!bind.requiresCtrl || Keyboard.current.ctrlKey.isPressed) &&
            (!bind.requiresShift || Keyboard.current.leftShiftKey.isPressed) &&
            (!bind.requiresAlt || Keyboard.current.leftAltKey.isPressed);

        if (!modifiersSatisfied)
            return false;

        return action.WasPressedThisFrame();
    }

    // Check if the bind was released this frame
    public static bool WasReleasedThisFrame(string bindName)
    {
        if (!bindActions.TryGetValue(bindName, out var action))
        {
            Debug.LogWarning($"Bind '{bindName}' not found.");
            return false;
        }

        var bind = binds.Find(b => b.bindName == bindName);
        if (bind.Equals(default(Bind)))
        {
            Debug.LogWarning($"Bind '{bindName}' not found in bind list.");
            return false;
        }

        // Check modifiers
        bool modifiersSatisfied =
            (!bind.requiresCtrl || Keyboard.current.ctrlKey.isPressed) &&
            (!bind.requiresShift || Keyboard.current.leftShiftKey.isPressed) &&
            (!bind.requiresAlt || Keyboard.current.leftAltKey.isPressed);

        if (!modifiersSatisfied)
            return false;

        return action.WasReleasedThisFrame();
    }

    // Check if the bind is currently pressed
    public static bool IsPressed(string bindName)
    {

        if (!bindActions.TryGetValue(bindName, out var action))
        {
            Debug.LogWarning($"Bind '{bindName}' not found.");
            return false;
        }

        var bind = binds.Find(b => b.bindName == bindName);
        if (bind.Equals(default(Bind)))
        {
            Debug.LogWarning($"Bind '{bindName}' not found in bind list.");
            return false;
        }

        // Check modifiers
        bool modifiersSatisfied =
            (!bind.requiresCtrl || Keyboard.current.ctrlKey.isPressed) &&
            (!bind.requiresShift || Keyboard.current.leftShiftKey.isPressed) &&
            (!bind.requiresAlt || Keyboard.current.leftAltKey.isPressed);

        if (!modifiersSatisfied)
            return false;

        return action.IsPressed();
    }


    // Update a bind's input (for rebinding, to be used with SettingsManager later)
    public static void UpdateBind(string bindName, string newInput, bool requiresCtrl = false, bool requiresShift = false, bool requiresAlt = false)
    {
        var bindIndex = binds.FindIndex(b => b.bindName == bindName);
        if (bindIndex == -1)
        {
            Debug.LogWarning($"Cannot update bind '{bindName}': not found.");
            return;
        }

        // Update bind
        binds[bindIndex] = new Bind(bindName, binds[bindIndex].actionType, newInput, requiresCtrl, requiresShift, requiresAlt);

        // Update InputAction
        if (bindActions.TryGetValue(bindName, out var action))
        {
            action.Disable();
            action.ChangeBinding(0).WithPath(newInput);
            action.Enable();
        }
		SettingsManager.SaveSettings();
    }

}