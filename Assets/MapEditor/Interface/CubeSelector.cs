using UnityEngine;
using System.Collections.Generic;

public class CubeSelector : MonoBehaviour
{
    private GameObject selectionCube; // The cube visualization
    private bool isSelecting; // Flag to track if selection is active
    private Vector2 startScreenPos; // Starting screen position of the drag
    private Vector3 startPoint; // Starting world point on the camera plane
    private Vector3 endPoint; // Current world point on the camera plane
    private float cubeDepth = 15f; // Depth of the cube, adjusted by scroll wheel
    private readonly float minCubeDepth = 5f; // Minimum depth
    private readonly float maxCubeDepth = 300f; // Maximum depth
    private readonly float scrollSensitivity = 10f; // Scroll wheel sensitivity
    private readonly float planeDistance = 10f; // Distance of virtual plane from camera
    private Camera cam; // Reference to the main camera
    private int prefabMask; // Layer mask for prefabs
    private List<GameObject> cubeSelectedObjects = new List<GameObject>(); // Objects selected by the cube
    private List<GameObject> preservedSelections = new List<GameObject>(); // Pre-existing selections

	private void Awake()
	{
		// Initialize selection cube
		selectionCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
		selectionCube.name = "SelectionCube";
		selectionCube.SetActive(false);
		var renderer = selectionCube.GetComponent<Renderer>();
		renderer.material = new Material(Shader.Find("Custom/Wireframe")); // Use wireframe shader
		renderer.material.SetColor("_WireColor", AppManager.Instance.color4); // Set wireframe color
		renderer.material.SetColor("_InteriorColor", AppManager.Instance.color4); // Set wireframe color
		renderer.material.SetFloat("_WireThickness", 3f); // Set wireframe thickness
		Destroy(selectionCube.GetComponent<Collider>()); // Remove collider to avoid raycast interference
	}

    public void Initialize(Camera camera, int prefabLayerMask)
    {
        cam = camera;
        prefabMask = prefabLayerMask;
    }

    public void StartSelection(Vector2 screenPos)
    {
		CameraManager.Instance._suppressUndoCreation = true;
        if (!isSelecting)
        {
            isSelecting = true;
            startScreenPos = screenPos;
            startPoint = ScreenToPlanePoint(screenPos);
            endPoint = startPoint;
            selectionCube.SetActive(true);
            cubeSelectedObjects.Clear(); // Clear previous cube selections
            preservedSelections.Clear();
            preservedSelections.AddRange(CameraManager.Instance._selectedObjects); // Store pre-existing selections
        }
    }

    public void UpdateSelection(Vector2 screenPos, float scrollDelta)
    {
        if (!isSelecting) return;

        // Update startPoint to follow camera movement/rotation
        startPoint = ScreenToPlanePoint(startScreenPos);
        endPoint = ScreenToPlanePoint(screenPos);

        // Adjust cube depth with scroll wheel
        cubeDepth = Mathf.Clamp(cubeDepth + scrollDelta * scrollSensitivity * Time.deltaTime, minCubeDepth, maxCubeDepth);

        // Update cube visualization
        UpdateCubeVisualization();

        // Update selections within the cube
        UpdateObjectsInCube();
    }

    public void EndSelection()
    {
		CameraManager.Instance._suppressUndoCreation = false;
        if (isSelecting)
        {
            bool isMultiSelect = BindManager.IsPressed("multiSelect"); // Check multiSelect bind
            if (!isMultiSelect)
            {
                // Clear all selections and select only cube objects
                CameraManager.Instance.Unselect();
                foreach (GameObject prefab in cubeSelectedObjects)
                {
                    CameraManager.Instance.SelectPrefab(prefab);
                }
            }
			CameraManager.Instance.NotifySelectionChanged();
            // In multiSelect mode, leave all selections (preserved + cube) intact
            isSelecting = false;
            selectionCube.SetActive(false);
        }
    }

    private Vector3 ScreenToPlanePoint(Vector2 screenPos)
    {
        // Create a plane 5 units in front of the camera, aligned with its rotation
        Vector3 planeCenter = cam.transform.position + cam.transform.forward * planeDistance;
        Plane plane = new Plane(-cam.transform.forward, planeCenter);

        // Ray from screen position
        Ray ray = cam.ScreenPointToRay(screenPos);

        // Find intersection with the plane
        if (plane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        // Fallback: return point at planeDistance along the ray
        return ray.GetPoint(planeDistance);
    }
	
	public void UpdateFrustumVisualization(Vector2 screenPos)
	{
		// Update startPoint and endPoint to follow camera movement/rotation
		startPoint = ScreenToPlanePoint(startScreenPos);
		endPoint = ScreenToPlanePoint(screenPos);

		// Calculate the rectangle's center and size
		Vector3 center = (startPoint + endPoint) / 2f;
		Vector3 delta = endPoint - startPoint;

		// Project delta onto camera's right and up vectors to get width and height
		Vector3 camRight = cam.transform.right;
		Vector3 camUp = cam.transform.up;
		Vector3 camForward = cam.transform.forward;

		float width = Mathf.Abs(Vector3.Dot(delta, camRight));
		float height = Mathf.Abs(Vector3.Dot(delta, camUp));

		// Set rectangle scale with fixed depth
		Vector3 scale = new Vector3(width, height, .01f);
		if (scale.x < 0.01f) scale.x = 0.01f; // Prevent zero scale
		if (scale.y < 0.01f) scale.y = 0.01f;

		// Position the rectangle: center + slight offset along camera forward for visibility
		Vector3 cubeCenter = center + camForward * (.01f / 2f);
		selectionCube.transform.position = cubeCenter;

		// Orient the rectangle to face the camera
		selectionCube.transform.rotation = Quaternion.LookRotation(camForward, camUp);

		// Apply scale
		selectionCube.transform.localScale = scale;
	}
	
	    private void FilterTaggedObjects(List<GameObject> currentObjects, List<GameObject> filteredObjects)
    {
        // Filter out children of "Collection" tagged objects
        foreach (GameObject obj in currentObjects)
        {
            // If the object is tagged "Collection", include it and skip its children
            if (obj.CompareTag("Collection"))
            {
                filteredObjects.Add(obj);
            }
            // If the object is tagged "Prefab", ensure it's not a child of a "Collection" in the list
            else if (obj.CompareTag("Prefab"))
            {
                bool isChildOfCollection = false;
                Transform current = obj.transform.parent;
                while (current != null)
                {
                    if (current.gameObject.CompareTag("Collection") && currentObjects.Contains(current.gameObject))
                    {
                        isChildOfCollection = true;
                        break;
                    }
                    current = current.parent;
                }
                if (!isChildOfCollection)
                {
                    filteredObjects.Add(obj);
                }
            }
        }

        // Unselect objects no longer in the selection, if not preserved
        for (int i = cubeSelectedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = cubeSelectedObjects[i];
            if (!filteredObjects.Contains(obj) && !preservedSelections.Contains(obj))
            {
                cubeSelectedObjects.RemoveAt(i);
                CameraManager.Instance.Unselect(obj);
            }
        }

        // Select new objects, if not already preserved
        foreach (GameObject validObject in filteredObjects)
        {
            if (!cubeSelectedObjects.Contains(validObject) && !preservedSelections.Contains(validObject))
            {
                cubeSelectedObjects.Add(validObject);
                CameraManager.Instance.SelectPrefab(validObject);
            }
        }
    }

    public void UpdateObjectsInFrustum(Vector2 screenPos)
    {
        // Create screen rectangle
        Rect screenRect = new Rect(
            Mathf.Min(startScreenPos.x, screenPos.x),
            Mathf.Min(startScreenPos.y, screenPos.y),
            Mathf.Abs(screenPos.x - startScreenPos.x),
            Mathf.Abs(screenPos.y - startScreenPos.y)
        );

        // Build initial list of candidate objects
        List<GameObject> currentObjectsInFrustum = new List<GameObject>();
        foreach (PrefabDataHolder holder in PrefabManager.CurrentMapPrefabs)
        {
			
            GameObject validObject = CameraManager.Instance.FindParentWithTag(holder.gameObject, "Collection") ?? CameraManager.Instance.FindParentWithTag(holder.gameObject, "Prefab");

            if (validObject != null)
            {
                // Check if the object's within the screen rectangle
                Vector3 boundsCenter = validObject.transform.position;
                Vector3 screenPoint = cam.WorldToScreenPoint(boundsCenter);

                if (screenRect.Contains(new Vector2(screenPoint.x, screenPoint.y)) && screenPoint.z > 0)
                {
                    if (!currentObjectsInFrustum.Contains(validObject))
                    {
                        currentObjectsInFrustum.Add(validObject);
                    }
                }
            }
        }

        // Filter and update selections
        List<GameObject> filteredObjects = new List<GameObject>();
        FilterTaggedObjects(currentObjectsInFrustum, filteredObjects);
    }

    private void UpdateObjectsInCube()
    {
        // Get cube bounds in world space
        Bounds cubeBounds = new Bounds(selectionCube.transform.position, selectionCube.transform.localScale);

        // Find all colliders within the cube bounds on the prefab layer
        Collider[] colliders = Physics.OverlapBox(cubeBounds.center, cubeBounds.extents, selectionCube.transform.rotation, prefabMask);

        // Process colliders with a validity check for cube (always true since OverlapBox handles bounds)
        ProcessCollidersForSelection(colliders, obj => true);
    }
	
	private void ProcessCollidersForSelection(Collider[] colliders, System.Func<GameObject, bool> isValidForSelection)
    {
        // Use HashSet for efficient lookup
        HashSet<GameObject> currentObjects = new HashSet<GameObject>();

        // Process colliders to find valid objects
        foreach (Collider collider in colliders)
        {
            GameObject obj = collider.gameObject;
            // Find the parent with "Collection" or "Prefab" tag
            GameObject validObject = CameraManager.Instance.FindParentWithTag(obj, "Collection") ?? CameraManager.Instance.FindParentWithTag(obj, "Prefab");

            // Only add if it has the correct tag and is valid for selection
            if (validObject != null && isValidForSelection(validObject) && !currentObjects.Contains(validObject))
            {
                currentObjects.Add(validObject);
            }
        }

        // Unselect objects no longer in the selection, if not preserved
        for (int i = cubeSelectedObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = cubeSelectedObjects[i];
            if (!currentObjects.Contains(obj) && !preservedSelections.Contains(obj))
            {
                cubeSelectedObjects.RemoveAt(i);
                CameraManager.Instance.Unselect(obj);
            }
        }

        // Select new objects, if not already preserved
        foreach (GameObject validObject in currentObjects)
        {
            if (!cubeSelectedObjects.Contains(validObject) && !preservedSelections.Contains(validObject))
            {
                cubeSelectedObjects.Add(validObject);
                CameraManager.Instance.SelectPrefab(validObject);
            }
        }
    }


    private void UpdateCubeVisualization()
    {
        // Calculate the cube's center and size
        Vector3 center = (startPoint + endPoint) / 2f;
        Vector3 delta = endPoint - startPoint;

        // Project delta onto camera's right and up vectors to get width and height
        Vector3 camRight = cam.transform.right;
        Vector3 camUp = cam.transform.up;
        Vector3 camForward = cam.transform.forward;

        float width = Mathf.Abs(Vector3.Dot(delta, camRight));
        float height = Mathf.Abs(Vector3.Dot(delta, camUp));

        // Set cube scale
        Vector3 scale = new Vector3(width, height, cubeDepth);
        if (scale.x < 0.01f) scale.x = 0.01f; // Prevent zero scale
        if (scale.y < 0.01f) scale.y = 0.01f;

        // Position the cube: center + offset along camera forward for depth
        Vector3 cubeCenter = center + camForward * (cubeDepth / 2f);
        selectionCube.transform.position = cubeCenter;

        // Orient the cube to face the camera
        selectionCube.transform.rotation = Quaternion.LookRotation(camForward, camUp);

        // Apply scale
        selectionCube.transform.localScale = scale;
    }


	public void Hide(){
		selectionCube.SetActive(false);
	}
	
	public void Show(){
		selectionCube.SetActive(true);
	}

    public bool IsSelecting => isSelecting;

    public List<GameObject> GetSelectedObjects() => cubeSelectedObjects;

    private void OnDestroy()
    {
        if (selectionCube != null)
        {
            Destroy(selectionCube);
        }
    }
}