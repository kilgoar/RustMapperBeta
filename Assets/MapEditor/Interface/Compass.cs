using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using RustMapEditor.Variables;

public class Compass : MonoBehaviour
{
    public Camera targetCamera;
    public InputField xField, yField, zField;
    public Text directionText;
    public Text quadrantText;
    public Transform compass;
    private Vector3 lastPosition;

    public static Compass Instance { get; private set; }

    // Key: filename (e.g., "MyMap"), Value: 10 camera slots
    private Dictionary<string, CameraSlots> _cameraDictionary 
        = new Dictionary<string, CameraSlots>();

    private const string FileName = "CameraDictionary.json";

    /// <summary>
    /// The current map/prefab filename. Set externally via SetCurrentFile().
    /// </summary>
    public string CurrentFileName;

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
            return;
        }

        Initialize();
    }

    public void Initialize()
    {
		CurrentFileName = "default";
        SetupInputFields();
        lastPosition = targetCamera.transform.position;
        LoadCameraDictionary();
    }

    public void SetupInputFields()
    {
        xField.onEndEdit.AddListener(OnFieldChanged);
        yField.onEndEdit.AddListener(OnFieldChanged);
        zField.onEndEdit.AddListener(OnFieldChanged);
    }

    public void Hide()
    {
        if (compass != null)
            compass.gameObject.SetActive(false);
    }

    public void Show()
    {
        if (compass != null)
            compass.gameObject.SetActive(true);
    }

    public void SyncScaleWithMenu()
    {
        Vector3 menuScale = MenuManager.Instance.GetMenuScale();
        Vector3 newScale = menuScale - Vector3.one;

        newScale.x = Mathf.Clamp(newScale.x, 0.6f, 3f);
        newScale.y = Mathf.Clamp(newScale.y, 0.6f, 3f);
        newScale.z = Mathf.Clamp(newScale.z, 0.6f, 3f);

        compass.localScale = newScale;
    }

    private void Update()
    {
        Vector3 currentPos = targetCamera.transform.position;
        Vector3 relativePos = currentPos - PrefabManager.PrefabParent.position;

        if (currentPos != lastPosition)
        {
            xField.SetTextWithoutNotify(relativePos.x.ToString("F2"));
            yField.SetTextWithoutNotify(relativePos.y.ToString("F2"));
            zField.SetTextWithoutNotify(relativePos.z.ToString("F2"));

            quadrantText.text = PositionToGridString(relativePos);

            lastPosition = currentPos;
        }

        UpdateOrdinalDisplay(targetCamera.transform.forward);
    }

    private void OnFieldChanged(string value)
    {
        if (!targetCamera) return;

        Vector3 newPosition = GetVector3();
        CameraManager.Instance.SetCameraPosition(newPosition + PrefabManager.PrefabParent.position);
        UpdateOrdinalDisplay(targetCamera.transform.forward);
        lastPosition = newPosition;
    }

    public Vector3 GetVector3()
    {
        float x = ParseField(xField?.text);
        float y = ParseField(yField?.text);
        float z = ParseField(zField?.text);
        return new Vector3(x, y, z);
    }

    private float ParseField(string text)
    {
        if (float.TryParse(text, out float result))
            return result;
        return 0f;
    }

    public void SetVector3(Vector3 value)
    {
        if (!targetCamera) return;

        if (xField) xField.text = value.x.ToString("F2");
        if (yField) yField.text = value.y.ToString("F2");
        if (zField) zField.text = value.z.ToString("F2");
        targetCamera.transform.position = value;
        lastPosition = value;
    }

    public string PositionToGridString(Vector3 position)
    {
        float gridUnit = 146.28572f;
        int gridCount = Mathf.FloorToInt(TerrainManager.TerrainSize.x / gridUnit + 0.001f);
        float cellSize = TerrainManager.TerrainSize.x / gridCount;
        Vector2 origin = new Vector2(-TerrainManager.TerrainSize.x / 2f, TerrainManager.TerrainSize.x / 2f);
        Vector2 gridPos = new Vector2((position.x - origin.x) / cellSize, (origin.y - position.z) / cellSize);
        int x = Mathf.FloorToInt(gridPos.x);
        int y = Mathf.FloorToInt(gridPos.y);
        x = Mathf.Max(x, 0);
        int letterNum = x + 1;
        string letters = "";
        while (letterNum > 0)
        {
            letterNum--;
            letters = ((char)(65 + letterNum % 26)).ToString() + letters;
            letterNum /= 26;
        }
        return $"{letters}{y}";
    }

    private void UpdateOrdinalDisplay(Vector3 forward)
    {
        if (!directionText) return;

        Vector2 flatForward = new Vector2(forward.x, forward.z).normalized;
        float angle = Mathf.Atan2(flatForward.x, flatForward.y) * Mathf.Rad2Deg;
        string ordinal = GetOrdinalDirection(angle);
        directionText.text = ordinal;
    }

    private string GetOrdinalDirection(float angle)
    {
        angle = (angle + 360) % 360;

        if (angle >= 337.5f || angle < 22.5f) return "N";
        if (angle >= 22.5f && angle < 67.5f) return "NE";
        if (angle >= 67.5f && angle < 112.5f) return "E";
        if (angle >= 112.5f && angle < 157.5f) return "SE";
        if (angle >= 157.5f && angle < 202.5f) return "S";
        if (angle >= 202.5f && angle < 247.5f) return "SW";
        if (angle >= 247.5f && angle < 292.5f) return "W";
        if (angle >= 292.5f && angle < 337.5f) return "NW";
        return "NO";
    }

    // ==============================================================
    //  EXTERNAL API: Set current file context
    // ==============================================================

    /// <summary>
    /// Call this when loading a new map or prefab.
    /// Example: Compass.Instance.SetCurrentFile("MyMap.json");
    /// </summary>
    public void SetCurrentFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            CurrentFileName = null;
            return;
        }

        CurrentFileName = Path.GetFileNameWithoutExtension(fileName);
        Debug.Log($"[Compass] Context set to: {CurrentFileName}");
    }

    // ==============================================================
    //  PER-FILE CAMERA SLOTS (0-9)
    // ==============================================================

    private CameraSlots GetOrCreateSlots(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new System.ArgumentException("fileName cannot be null or empty");

        if (!_cameraDictionary.TryGetValue(fileName, out var slots))
        {
            slots = new CameraSlots(); // All slots null
            _cameraDictionary[fileName] = slots;
        }
        return slots;
    }

    private bool IsValidSlot(int slot) => slot >= 0 && slot <= 9;

    /// <summary>
    /// Save current camera to a specific slot for a specific file.
    /// </summary>
    public void SaveCameraSlot(int slot)
    {
		string fileName = CurrentFileName;
        if (!IsValidSlot(slot) || targetCamera == null) return;

        var slots = GetOrCreateSlots(fileName);
        slots[slot] = new CameraEntry(
            targetCamera.transform.position,
            targetCamera.transform.rotation
        );

        _cameraDictionary[fileName] = slots;
        SaveCameraDictionary();

        Debug.Log($"[Compass] Saved camera to {fileName}[{slot}]");
    }

    /// <summary>
    /// Load camera from a specific slot for a specific file.
    /// </summary>
    public void LoadCameraSlot(int slot)
    {
		string fileName = CurrentFileName;
        if (!IsValidSlot(slot) || targetCamera == null) return;

        if (_cameraDictionary.TryGetValue(fileName, out CameraSlots slots) && slots[slot] != null)
        {
            var entry = slots[slot];
            CameraManager.Instance.SetCameraPosition(entry.position);
            targetCamera.transform.rotation = entry.rotation;
            lastPosition = entry.position;

            Debug.Log($"[Compass] Loaded camera from {fileName}[{slot}]");
        }
        else
        {
            Debug.LogWarning($"[Compass] No camera saved in {fileName}[{slot}]");
        }
    }

    /// <summary>
    /// Check if a slot has saved data.
    /// </summary>
    public bool HasCameraSlot(string fileName, int slot)
    {
        return _cameraDictionary.TryGetValue(fileName, out var slots) &&
               IsValidSlot(slot) &&
               slots[slot] != null;
    }

    /// <summary>
    /// Clear a specific camera slot.
    /// </summary>
    public void ClearCameraSlot(string fileName, int slot)
    {
        if (!IsValidSlot(slot)) return;

        if (_cameraDictionary.TryGetValue(fileName, out var slots))
        {
            var modified = slots;
            modified[slot] = null;
            _cameraDictionary[fileName] = modified;
            SaveCameraDictionary();
        }
    }

    // ==============================================================
    //  PERSISTENCE
    // ==============================================================

    private void LoadCameraDictionary()
    {
        string path = Path.Combine(SettingsManager.AppDataPath(), FileName);
        if (!File.Exists(path)) return;

        try
        {
            string json = File.ReadAllText(path);
            var loaded = JsonConvert.DeserializeObject<Dictionary<string, CameraSlots>>(json);
            _cameraDictionary = loaded ?? new Dictionary<string, CameraSlots>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Compass] Failed to load CameraDictionary: {e}");
        }
    }

    private void SaveCameraDictionary()
    {
        string path = Path.Combine(SettingsManager.AppDataPath(), FileName);
        try
        {
            var cleaned = _cameraDictionary
                .Where(kvp => kvp.Value.HasAnyData())
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            string json = JsonConvert.SerializeObject(cleaned, Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Compass] Failed to save CameraDictionary: {e}");
        }
    }

    private void OnApplicationQuit()
    {
        SaveCameraDictionary();
    }
}