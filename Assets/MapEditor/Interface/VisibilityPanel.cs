using UnityEngine;
using UnityEngine.UI;

public class VisibilityPanel : MonoBehaviour
{
    public Toggle prefabs, volumes, monumentVolumes, land, water;

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
    }

    // Layer indices based on provided information
    private const int PrefabsLayer = 3; // Layer 3 for Prefabs
    private const int LandLayer = 10; // Layer 10 for Land
    private const int WaterLayer = 4; // Layer 4 for Water
    private const int VolumesLayer = 11; // Placeholder: Adjust if Volumes has a specific layer
    private const int MonumentVolumesLayer = 12; // Placeholder: Adjust if MonumentVolumes has a specific layer

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
    }

    private void HideMonumentVolumes()
    {
        Debug.Log("HideMonumentVolumes called");
        SetLayerVisibility(MonumentVolumesLayer, false);
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