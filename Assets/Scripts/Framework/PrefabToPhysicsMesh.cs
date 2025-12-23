/**
 * @file PrefabToPhysicsMesh.cs
 * @brief PrefabToPhysicsMesh script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;

/// <summary>
/// Takes a prefab, extracts its meshes, and creates a new GameObject that:
/// - Has only a MeshFilter, MeshRenderer, MeshCollider, and Rigidbody
/// - Rigidbody has gravity turned off
/// Optionally combines all child meshes into a single mesh.
/// </summary>
/**
 * @class PrefabToPhysicsMesh
 * @brief PrefabToPhysicsMesh component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup tools
 */
public class PrefabToPhysicsMesh : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Prefab to sample the mesh(es) from.")]
    public GameObject sourcePrefab;

    [Header("Options")]
    [Tooltip("If true, combines all MeshFilters in the prefab into one mesh.")]
    public bool combineChildMeshes = true;

    [Tooltip("If true, generated MeshCollider will be convex.")]
    public bool makeColliderConvex = true;

    [Tooltip("Spawn the physics mesh at this transform's position/rotation.")]
    public bool useThisTransformAsSpawn = true;

    [Tooltip("Destroy the temporary instantiated prefab after baking.")]
    public bool destroyTempInstance = true;

    [Header("Runtime Spawn")]
    [Tooltip("If true, will automatically bake & spawn on Start().")]
    public bool bakeOnStart = true;

    private void Start()
    {
        if (bakeOnStart)
        {
            BakePrefabToPhysicsMesh();
        }
    }

    [ContextMenu("Bake Prefab To Physics Mesh Now")]
    public void BakePrefabToPhysicsMesh()
    {
        if (sourcePrefab == null)
        {
            Debug.LogError("[PrefabToPhysicsMesh] No sourcePrefab assigned.");
            return;
        }

        // 1. Instantiate the prefab as a temporary object to read its meshes
        GameObject tempInstance = Instantiate(
            sourcePrefab,
            useThisTransformAsSpawn ? transform.position : Vector3.zero,
            useThisTransformAsSpawn ? transform.rotation : Quaternion.identity
        );

        tempInstance.name = sourcePrefab.name + "_TempForBake";

        // 2. Collect all MeshFilters
        MeshFilter[] meshFilters = tempInstance.GetComponentsInChildren<MeshFilter>();
        if (meshFilters == null || meshFilters.Length == 0)
        {
            Debug.LogError("[PrefabToPhysicsMesh] No MeshFilter found in prefab.");
            if (destroyTempInstance) Destroy(tempInstance);
            return;
        }

        // 3. Create a new GameObject that will hold the final physics mesh
        GameObject physicsGO = new GameObject(sourcePrefab.name + "_Physics");
        if (useThisTransformAsSpawn)
        {
            physicsGO.transform.position = transform.position;
            physicsGO.transform.rotation = transform.rotation;
        }
        else
        {
            physicsGO.transform.position = tempInstance.transform.position;
            physicsGO.transform.rotation = tempInstance.transform.rotation;
        }

        // 4. Build / copy mesh
        Mesh finalMesh;

        if (combineChildMeshes && meshFilters.Length > 1)
        {
            // Combine all child meshes into one
            CombineInstance[] combine = new CombineInstance[meshFilters.Length];

            for (int i = 0; i < meshFilters.Length; i++)
            {
                combine[i].mesh = meshFilters[i].sharedMesh;
                combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            }

            finalMesh = new Mesh();
            finalMesh.name = sourcePrefab.name + "_CombinedMesh";
            finalMesh.CombineMeshes(combine, true, true);
        }
        else
        {
            // Just use the first mesh
            finalMesh = Instantiate(meshFilters[0].sharedMesh);
            finalMesh.name = sourcePrefab.name + "_SingleMeshCopy";
        }

        // 5. Add MeshFilter + MeshRenderer to new object
        MeshFilter mf = physicsGO.AddComponent<MeshFilter>();
        mf.sharedMesh = finalMesh;

        MeshRenderer mr = physicsGO.AddComponent<MeshRenderer>();

        // Copy a material from the first renderer if present (optional)
        MeshRenderer srcRenderer = meshFilters[0].GetComponent<MeshRenderer>();
        if (srcRenderer != null)
        {
            mr.sharedMaterials = srcRenderer.sharedMaterials;
        }

        // 6. Add MeshCollider
        MeshCollider mc = physicsGO.AddComponent<MeshCollider>();
        mc.sharedMesh = finalMesh;
        mc.convex = makeColliderConvex;

        // 7. Add Rigidbody (no gravity)
        Rigidbody rb = physicsGO.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false; // change this if you want it not to move at all

        // 8. Clean up temp instance
        if (destroyTempInstance)
        {
            Destroy(tempInstance);
        }

        Debug.Log("[PrefabToPhysicsMesh] Baked physics mesh created: " + physicsGO.name);
    }
}
