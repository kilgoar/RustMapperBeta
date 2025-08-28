using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class MeshTreeLOD : MeshLOD
{
    public new TreeLODState[] States; // New array combining TreeLOD and MeshLOD state properties

    // Local fields to track LOD levels, avoiding MeshLOD's private setters
    private int localMinLODLevel;
    private int localMaxLODLevel;
    private int localCurrentLODLevel = -1;

    protected override void Awake()
    {
        base.Awake();

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
            localMinLODLevel = 0;
            localMaxLODLevel = States.Length - 1;
        }

        // Initialize MeshLOD components
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        materialPropertyBlock = new MaterialPropertyBlock();
    }

    protected override void Start()
    {
        if (States == null || States.Length == 0)
        {
            return;
        }

        // Initialize shared material for batching
        if (meshRenderer != null && meshRenderer.sharedMaterial != null)
        {
            sharedMaterial = meshRenderer.sharedMaterial;
        }

        // Sort states by distance to ensure correct order
        System.Array.Sort(States, (a, b) => a.distance.CompareTo(b.distance));

        RefreshLOD(); // Initial LOD refresh
    }

    protected override void CheckLOD(float distance)
    {
        if (States == null || meshRenderer == null)
        {
            return;
        }

        int newLevel = CalculateLODLevel(distance);
        if (newLevel != localCurrentLODLevel)
        {
            UpdateLOD(newLevel);
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

    public override List<Renderer> RendererList()
    {
        List<Renderer> renderers = new List<Renderer>();
        if (States == null || States.Length == 0)
        {
            return renderers;
        }

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
            ShowLOD(newLevel);
        }

        // Update local LOD level and call base UpdateLOD
        localCurrentLODLevel = newLevel;
        base.UpdateLOD(newLevel); // Call base to maintain MeshLOD functionality
    }

    private new void ShowLOD(int level)
    {
        if (meshRenderer == null || States[level].renderer == null)
        {
            return;
        }

        // Enable the renderer for this LOD
        States[level].Show();

        // Apply mesh and material properties
        meshRenderer.enabled = true;
        meshRenderer.GetPropertyBlock(materialPropertyBlock);

        if (sharedMaterial != null)
        {
            meshRenderer.sharedMaterial = sharedMaterial;
        }

        // Set shadow properties
        meshRenderer.shadowCastingMode = States[level].shadowCastingMode;
        meshRenderer.receiveShadows = States[level].receiveShadows;

        // Update mesh
        if (meshFilter != null && States[level].mesh != null)
        {
            meshFilter.mesh = States[level].mesh;
        }

        meshRenderer.SetPropertyBlock(materialPropertyBlock);
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

    public new Mesh GetMesh(out Matrix4x4 matrix)
    {
        matrix = Matrix4x4.identity;
        if (localCurrentLODLevel >= 0 && localCurrentLODLevel < States.Length)
        {
            return States[localCurrentLODLevel].mesh;
        }
        return null;
    }

    public new void SetMaterials(Material[] materials)
    {
        if (materials.Length > 0)
        {
            sharedMaterial = materials[0];
        }
    }

    [System.Serializable]
    public class TreeLODState : LODState
    {
        public Mesh mesh; // From MeshLOD.State
        public ShadowCastingMode shadowCastingMode; // From MeshLOD.State
        public bool receiveShadows; // From MeshLOD.State

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