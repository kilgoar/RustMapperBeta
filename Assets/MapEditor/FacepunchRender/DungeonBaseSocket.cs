using UnityEngine;
using System;

public enum DungeonBaseSocketType
{
    Horizontal,
    Vertical,
    Pillar
}

public class DungeonBaseSocket : MonoBehaviour
{
    public DungeonBaseSocketType Type;
    public bool Male = true;
    public bool Female = true;

    [HideInInspector] public string prefabPath;
    [HideInInspector] public int socketIndex = -1;
    [HideInInspector] public bool isUserDefined = false;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float lastChangeTime;
    private const float DEBOUNCE_DELAY = 0.1f; // Debounce delay to prevent excessive updates

    // Event to notify subscribers of transform changes
    public event Action<DungeonBaseSocket> OnTransformChanged;

    private void Awake()
    {
        // Store initial position and rotation
        lastPosition = transform.position;
        lastRotation = transform.rotation;

        // Check if the direct parent is SocketInfo to determine if user-defined
        if (transform.parent != null && transform.parent.name == "SocketInfo")
        {
            var (prefabParent, parentTag) = SocketManager.GetParentAndTag(transform);
            if (parentTag == "Prefab")
            {
                prefabPath = SocketManager.GetPrefabPath(prefabParent);
                if (!string.IsNullOrEmpty(prefabPath) && SocketManager.TryGetSocketIndex(gameObject.name, out int index))
                {
                    socketIndex = index;
                    isUserDefined = true; // Valid user-defined prefab, needs updates to sync to JSON
                }
            }
        }
    }

    private void Update()
    {
        if (!isUserDefined || string.IsNullOrEmpty(prefabPath) || socketIndex < 0)
        {
            return;
        }

        // Check for position or rotation changes with a small threshold for floating-point precision
        float distance = Vector3.Distance(transform.position, lastPosition);
        bool rotationChanged = Quaternion.Angle(transform.rotation, lastRotation) > 0.001f;

        if (distance < 0.001f && !rotationChanged)
        {
            return; // No significant change
        }

        // Update stored transform values
        lastPosition = transform.position;
        lastRotation = transform.rotation;

        // Debounce updates to avoid excessive calls
        if (Time.time - lastChangeTime >= DEBOUNCE_DELAY)
        {
            ReportTransformChange();
            lastChangeTime = Time.time;
        }
    }

    private void ReportTransformChange()
    {
		Debug.Log("socket modified");
        // Notify subscribers of transform change
        OnTransformChanged?.Invoke(this);
        // Update SocketInfo using cached prefabPath and socketIndex
        SocketManager.UpdateSocket(this, prefabPath, socketIndex);
        // Debug.Log($"DungeonBaseSocket {gameObject.name} transform changed. New position: {transform.position}, rotation: {transform.rotation}");
    }

    // Public method to manually force an update
    public void ForceUpdate()
    {
        if (!isUserDefined || string.IsNullOrEmpty(prefabPath) || socketIndex < 0)
        {
            return;
        }
        ReportTransformChange();
    }

    public void Delete()
    {
            SocketManager.DeleteSocket(this, prefabPath, socketIndex);
    }

}