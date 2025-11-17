using UnityEngine;
using UnityEngine.UI;

public class VisibilityPanel : MonoBehaviour
{
    public Toggle prefabs, volumes, monumentVolumes, land, water, wireframe;

    [Header("Materials")]
    public Material defaultTerrainMaterial;   // Assets/MapEditor/New Terrain Material
    public Material wireframeMaterial;        // Assets/Resources/Materials/Terrain Wireframe

    // Reference to the terrain GameObject (assign in inspector or auto-find)
    [Header("Terrain Object")]
    public GameObject terrainObject;          // Drag your "Land" or "Terrain" object here

    public static VisibilityPanel Instance { get; private set; }

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

    void Start()
    {
        // Register each toggle to listeners for changes
        if (prefabs != null)
            prefabs.onValueChanged.AddListener(isOn => { if (isOn) ShowPrefabs(); else HidePrefabs(); });

        if (volumes != null)
            volumes.onValueChanged.AddListener(isOn => { if (isOn) ShowVolumes(); else HideVolumes(); });

        if (monumentVolumes != null)
            monumentVolumes.onValueChanged.AddListener(isOn => { if (isOn) ShowMonumentVolumes(); else HideMonumentVolumes(); });

        if (land != null)
            land.onValueChanged.AddListener(isOn => { if (isOn) ShowLand(); else HideLand(); });

        if (water != null)
            water.onValueChanged.AddListener(isOn => { if (isOn) ShowWater(); else HideWater(); });
		
		// WIRE FRAME TOGGLE
        if (wireframe != null)
        {
            wireframe.onValueChanged.AddListener(isOn =>
            {
                if (isOn) EnableWireframe();
                else DisableWireframe();
            });

            // Optional: sync initial state
            if (wireframe.isOn) EnableWireframe();
            else DisableWireframe();
        }
    }

    // Layer indices based on provided information
    private const int PrefabsLayer = 3; // Layer 3 for Prefabs
    private const int LandLayer = 10; // Layer 10 for Land
    private const int WaterLayer = 4; // Layer 4 for Water
    private const int VolumesLayer = 11; // Placeholder: Adjust if Volumes has a specific layer
    private const int MonumentVolumesLayer = 12; // Primary layer for MonumentVolumes
    private const int MonumentVolumesLayer2 = 13; // Additional layer for MonumentVolumes

	private void EnableWireframe()
	{
		if (terrainObject == null || wireframeMaterial == null) return;

		var terrain = terrainObject.GetComponent<Terrain>();
		if (terrain != null)
		{
			terrain.materialTemplate = wireframeMaterial;
			terrain.Flush();
			Debug.Log("Wireframe mode ENABLED on Terrain");
		}
		else
		{
			Debug.LogError("No Terrain component found on " + terrainObject.name);
		}
	}

	private void DisableWireframe()
	{
		if (terrainObject == null || defaultTerrainMaterial == null) return;

		var terrain = terrainObject.GetComponent<Terrain>();
		if (terrain != null)
		{
			terrain.materialTemplate = defaultTerrainMaterial;
			terrain.Flush();
			Debug.Log("Wireframe mode DISABLED - back to normal terrain");
		}
	}

    private void ShowPrefabs()
    {
        Debug.Log("ShowPrefabs called");
        SetLayerVisibility(PrefabsLayer, true);
    }

    private void HidePrefabs()
    {
        Debug.Log("HidePrefabs called");
        SetLayerVisibility(PrefabsLayer, false);
    }

    private void ShowVolumes()
    {
        Debug.Log("ShowVolumes called");
        SetLayerVisibility(VolumesLayer, true);
    }

    private void HideVolumes()
    {
        Debug.Log("HideVolumes called");
        SetLayerVisibility(VolumesLayer, false);
    }

    private void ShowMonumentVolumes()
    {
        Debug.Log("ShowMonumentVolumes called");
        SetLayerVisibility(MonumentVolumesLayer, true);
        SetLayerVisibility(MonumentVolumesLayer2, true); // Include layer 13
    }

    private void HideMonumentVolumes()
    {
        Debug.Log("HideMonumentVolumes called");
        SetLayerVisibility(MonumentVolumesLayer, false);
        SetLayerVisibility(MonumentVolumesLayer2, false); // Include layer 13
    }

    private void ShowLand()
    {
        Debug.Log("ShowLand called");
        SetLayerVisibility(LandLayer, true);
    }

    private void HideLand()
    {
        Debug.Log("HideLand called");
        SetLayerVisibility(LandLayer, false);
    }

    private void ShowWater()
    {
        Debug.Log("ShowWater called");
        SetLayerVisibility(WaterLayer, true);
    }

    private void HideWater()
    {
        Debug.Log("HideWater called");
        SetLayerVisibility(WaterLayer, false);
    }

    private void SetLayerVisibility(int layer, bool isVisible)
    {
        if (CameraManager.Instance == null || CameraManager.Instance.cam == null)
        {
            Debug.LogError("CameraManager or Camera is not assigned.");
            return;
        }

        Camera cam = CameraManager.Instance.cam;
        if (isVisible)
        {
            // Enable the layer in the culling mask
            cam.cullingMask |= 1 << layer;
        }
        else
        {
            // Disable the layer in the culling mask
            cam.cullingMask &= ~(1 << layer);
        }

        Debug.Log($"Layer {layer} ({LayerMask.LayerToName(layer)}) visibility set to {isVisible}");
    }
	
}