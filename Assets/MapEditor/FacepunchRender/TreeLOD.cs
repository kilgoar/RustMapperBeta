using UnityEngine;
using System.Collections.Generic;

public class TreeLOD : LODComponent
{
    public TreeLODState[] States;

    protected override void Awake()
    {
        // Hide all renderers initially to prevent multiple LODs being visible
        if (States != null && States.Length > 0)
        {
            foreach (var state in States)
            {
                if (state.renderer != null)
                {
                    state.renderer.enabled = false;
                }
            }
        }
    }

    protected override void Start()
    {
        if (States == null || States.Length == 0)
        {
            return;
        }

        // Sort states by distance to ensure correct order
        System.Array.Sort(States, (a, b) => a.distance.CompareTo(b.distance));

        RefreshLOD(); // Initial LOD refresh
    }

    protected override void CheckLOD(float distance)
    {
        int newLevel = CalculateLODLevel(distance);
        if (newLevel != currentLODLevel)
        {
            UpdateLOD(newLevel);
            oldDistance = distance;
        }
    }

    protected override int CalculateLODLevel(float distance)
    {
        if (States.Length == 0)
        {
            return -1;
        }

        // Validate distances
        for (int i = 0; i < States.Length; i++)
        {
            if (float.IsNaN(States[i].distance) || States[i].distance < 0)
            {
                return -1;
            }
        }

        if (distance < States[0].distance)
        {
            return 0;
        }

        if (distance >= States[States.Length - 1].distance)
        {
            return States.Length - 1;
        }

        for (int i = 0; i < States.Length - 1; i++)
        {
            if (distance >= States[i].distance && distance < States[i + 1].distance)
            {
                return i;
            }
        }

        return -1;
    }

    public List<Renderer> RendererList()
    {
        List<Renderer> renderers = new List<Renderer>();
        if (States == null || States.Length == 0) return renderers;

        foreach (TreeLODState state in States)
        {
            if (state.renderer != null)
            {
                renderers.Add(state.renderer);
            }
        }
        return renderers;
    }

    protected override void UpdateLOD(int newLevel)
    {
        if (newLevel < 0 || newLevel >= States.Length)
        {
            return;
        }

        // Hide all LODs except the new one
        for (int i = 0; i < States.Length; i++)
        {
            if (i != newLevel && States[i].renderer != null)
            {
                States[i].Hide();
            }
        }

        // Show the new LOD
        if (States[newLevel].renderer != null)
        {
            States[newLevel].Show();
        }

        currentLODLevel = newLevel;
    }

    public override void RefreshLOD()
    {
        if (CameraManager.Instance == null)
        {
            return;
        }
        float distance = Vector3.Distance(transform.position, CameraManager.Instance.position);
        CheckLOD(distance);
    }

    [System.Serializable]
    public class TreeLODState : LODState
    {
        public void Show()
        {
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        public void Hide()
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
    }
}