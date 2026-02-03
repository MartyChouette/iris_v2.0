/**
 * @file MeshCreation.cs
 * @brief MeshCreation script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
 * * * IRIS MANIFESTO MODIFICATIONS:
 * - Implements "Metric Cruelty" via Collapse Threshold.
 * - Updates AnchorTopStemPiece to drop stems that are too short.
 *
 * @ingroup thirdparty
 */

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DynamicMeshCutter
{
    /**
     * @class MeshCreationData
     * @brief MeshCreationData component.
     */
    public class MeshCreationData
    {
        public GameObject[] CreatedObjects;
        public MeshTarget[] CreatedTargets;

        public MeshCreationData(int size)
        {
            CreatedObjects = new GameObject[size];
            CreatedTargets = new MeshTarget[size];
        }
    }

    /**
     * @class MeshCreation
     * @brief MeshCreation component.
     */
    public static class MeshCreation
    {
        static float _ragdoll_vertex_threshold = 0.75f;

        // IRIS MOD: Defines how short a stem must be before the system declares it "dead"
        public static float CollapseThreshold = 0.15f;

        /// <summary>
        /// Creates the actual GameObjects (stone, ragdoll, animated) for each VirtualMesh
        /// produced by the cut.
        /// </summary>
        public static MeshCreationData CreateObjects(Info info, Material defaultMaterial, int vertexCreationThreshold)
        {
            if (info.MeshTarget == null)
                return null;

            VirtualMesh[] createdMeshes = info.CreatedMeshes;

            MeshCreationData cData = new MeshCreationData(createdMeshes.Length);

            MeshTarget target = info.MeshTarget as MeshTarget;
            Material[] materials = GetMaterials(target.gameObject);
            Material[] materialsNew = new Material[materials.Length + 1];

            materials.CopyTo(materialsNew, 0);
            materialsNew[materialsNew.Length - 1] =
                (target.FaceMaterial != null) ? target.FaceMaterial : defaultMaterial;
            materials = materialsNew;

            // Detect if this cut is happening on a FlowerStemRuntime
            global::FlowerStemRuntime stemRuntime = null;
            if (target.GameobjectRoot != null)
            {
                stemRuntime = target.GameobjectRoot.GetComponentInParent<global::FlowerStemRuntime>();
            }
            bool isStemTarget = (stemRuntime != null);

            for (int i = 0; i < createdMeshes.Length; i++)
            {
                VirtualMesh vMesh = createdMeshes[i];
                if (vMesh.Vertices.Length < vertexCreationThreshold)
                    continue;

                // Reject dust-sized pieces before allocating any Unity objects
                if (vMesh.HasMeshBounds && vMesh.MeshBounds.size.magnitude < 0.005f)
                {
                    Debug.Log($"[MeshCreation] Skipping dust-sized piece {i}: bounds magnitude {vMesh.MeshBounds.size.magnitude:F5} < 0.005");
                    continue;
                }

                int bt = info.BT[i]; // bottom(0) / top(1) flag

                Transform parent = null;
                GameObject root = null;

                // Build a Unity Mesh from the VirtualMesh
                Mesh mesh = new Mesh
                {
                    vertices = vMesh.Vertices,
                    triangles = vMesh.Triangles,
                    normals = vMesh.Normals,
                    uv = vMesh.UVs,
                    subMeshCount = vMesh.SubMeshCount
                };

                for (int j = 0; j < vMesh.SubMeshCount; j++)
                {
                    mesh.SetIndices(vMesh.GetIndices(j), MeshTopology.Triangles, j);
                }

                // Decide behaviour for this piece
                Behaviour behaviour = target.DefaultBehaviour[bt];

                if (vMesh.DynamicGroups != null)
                {
                    int[] keys = new int[vMesh.DynamicGroups.Keys.Count];
                    int index = 0;
                    foreach (var key in vMesh.DynamicGroups.Keys)
                        keys[index++] = key;

                    for (int j = 0; j < target.GroupBehaviours.Count; j++)
                    {
                        if (target.GroupBehaviours[j].Passes(keys))
                        {
                            behaviour = target.GroupBehaviours[j].Behaviour;
                            break;
                        }
                    }
                }

                // Create the actual object(s)
                switch (behaviour)
                {
                    case Behaviour.Stone:
                        CreateMesh(ref root, ref parent, target, mesh, vMesh, materials, bt);
                        break;

                    case Behaviour.Ragdoll:
                        DynamicRagdoll tRagdoll = target.DynamicRagdoll;
                        if (tRagdoll != null && vMesh.DynamicGroups.Count > 1)
                        {
                            if (WillBeValidRagdoll(tRagdoll, vMesh))
                                CreateRagdoll(ref root, ref parent, info, target, mesh, vMesh, materials, bt, behaviour);
                            else
                                CreateMesh(ref root, ref parent, target, mesh, vMesh, materials, bt, true);
                        }
                        else
                        {
                            CreateMesh(ref root, ref parent, target, mesh, vMesh, materials, bt, true);
                        }
                        break;

                    case Behaviour.Animation:
                        if (target.Animator != null)
                        {
                            CreateAnimatedMesh(ref root, ref parent, info, target, mesh, vMesh, materials, bt, behaviour);
                        }
                        else
                        {
                            Debug.LogWarning("Behaviour is set to Animation, but there was no Animator found in parent!");
                            CreateMesh(ref root, ref parent, target, mesh, vMesh, materials, bt, true);
                        }
                        break;
                }

                // NEW: mark stem pieces so we can find them later
                if (isStemTarget && parent != null)
                {
                    var marker = parent.gameObject.AddComponent<StemPieceMarker>();
                    marker.stemRuntime = stemRuntime;
                }

                // Name the parent "(i/total)Stem" etc.
                string prefix = $"({i}/{createdMeshes.Length})";
                parent.name = prefix + parent.name;
                parent.name = parent.name.Replace("(Clone)", "");

                // 🔍 TRACE: log info about each stem piece we create
                if (isStemTarget && parent != null)
                {
                    var col = parent.GetComponentInChildren<Collider>();
                    Bounds b = col ? col.bounds : new Bounds(parent.position, Vector3.zero);
                    Vector3 size = b.size;

                    Debug.Log(
                        $"[MeshCreation] Stem piece created: '{parent.name}' " +
                        $"size=({size.x:F3}, {size.y:F3}, {size.z:F3}) pos={parent.position}",
                        parent);
                }


                // Safety: if for any reason this piece failed to create, skip it.
                if (parent == null || root == null)
                    continue;

                // Ensure a MeshTarget lives on the root
                var nTarget = root.GetComponent<MeshTarget>();
                if (nTarget == null)
                    nTarget = root.AddComponent<MeshTarget>();

                nTarget.GameobjectRoot = parent.gameObject;
                nTarget.OverrideFaceMaterial = target.OverrideFaceMaterial;
                nTarget.SeparateMeshes = target.SeparateMeshes;
                nTarget.ApplyTranslation = target.ApplyTranslation;
                nTarget.GroupBehaviours = target.GroupBehaviours;

                // Match scale of original
                nTarget.transform.localScale = target.transform.localScale;

                // Inherit behaviour/settings or copy from bt side
                if (target.Inherit[bt])
                {
                    for (int j = 0; j < 2; j++)
                    {
                        nTarget.DefaultBehaviour[j] = target.DefaultBehaviour[j];
                        nTarget.CreateRigidbody[j] = target.CreateRigidbody[j];
                        nTarget.CreateMeshCollider[j] = target.CreateMeshCollider[j];
                        nTarget.Physics[j] = target.Physics[j];
                        nTarget.Inherit[j] = target.Inherit[j];
                    }
                }
                else
                {
                    for (int j = 0; j < 2; j++)
                    {
                        nTarget.DefaultBehaviour[j] = target.DefaultBehaviour[bt];
                        nTarget.CreateRigidbody[j] = target.CreateRigidbody[bt];
                        nTarget.CreateMeshCollider[j] = target.CreateMeshCollider[bt];
                        nTarget.Physics[j] = target.Physics[bt];
                        nTarget.Inherit[j] = false;
                    }
                }

                cData.CreatedObjects[i] = parent.gameObject;
                cData.CreatedTargets[i] = nTarget;
            }

            // For stem cuts, find the keeper (closest to crown) and configure physics
            if (isStemTarget)
            {
                AnchorTopStemPiece(cData.CreatedObjects, stemRuntime);
            }

            return cData;
        }


        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (!obj) return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);          // safe: end-of-frame
            else
                SafeDestroy(obj); // editor-time only
        }



        /// <summary>
        /// For stem cuts: find the piece closest to the crown (by center of mass distance),
        /// disable gravity on it, and let other pieces fall. Delete if too small.
        /// </summary>
        static void AnchorTopStemPiece(GameObject[] createdObjects,
                                       global::FlowerStemRuntime stemRuntime)
        {
            if (stemRuntime == null || createdObjects == null || createdObjects.Length == 0)
                return;

            // Get crown anchor position
            Vector3 crownPos = stemRuntime.StemAnchor != null
                ? stemRuntime.StemAnchor.position
                : stemRuntime.transform.position;

            // Find the piece FARTHEST from crown (usually the rooted bottom piece)
            GameObject keeper = null;
            float farthestDistSq = -1f;

            foreach (var go in createdObjects)
            {
                if (go == null) continue;
                
                // Find rigidbody - check on this object first, then CHILDREN
                var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInChildren<Rigidbody>();
                if (rb == null) continue;
                
                float distSq = (rb.worldCenterOfMass - crownPos).sqrMagnitude;
                Debug.Log($"[AnchorTopStemPiece] Piece '{go.name}' (rb on '{rb.gameObject.name}') distance to crown: {Mathf.Sqrt(distSq):F3}", go);
                
                if (distSq > farthestDistSq)
                {
                    farthestDistSq = distSq;
                    keeper = go;
                }
            }

            if (keeper == null)
                return;

            Debug.Log($"[AnchorTopStemPiece] Selected keeper: '{keeper.name}' (farthest from crown)", keeper);

            // Configure all pieces
            foreach (var go in createdObjects)
            {
                if (go == null) continue;
                
                // Find rigidbody - check on this object first, then CHILDREN
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = go.GetComponentInChildren<Rigidbody>();
                    if (rb != null)
                    {
                        Debug.Log($"[AnchorTopStemPiece] Found Rigidbody on CHILD '{rb.gameObject.name}' for piece '{go.name}'", rb);
                    }
                }
                if (rb == null) continue;

                if (go == keeper)
                {
                    // Debug: trace the hierarchy and find ALL rigidbodies
                    Debug.Log($"[AnchorTopStemPiece] KEEPER '{go.name}' hierarchy trace:", go);

                    // Find all rigidbodies in this piece (self and children)
                    var allRigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
                    Debug.Log($"  - Found {allRigidbodies.Length} Rigidbodies in keeper hierarchy", go);

                    // Configure ALL rigidbodies in this piece
                    // FIXED: Keep DYNAMIC (not kinematic) so joints to leaves/petals can still break.
                    // Only freeze position, not rotation, and disable gravity.
                    foreach (var pieceRb in allRigidbodies)
                    {
                        pieceRb.isKinematic = false;
                        pieceRb.useGravity = false;
                        pieceRb.constraints = RigidbodyConstraints.FreezePosition;
                        pieceRb.linearDamping = 5f;
                        pieceRb.angularDamping = 5f;
                        Debug.Log($"  - Configured rb on '{pieceRb.gameObject.name}': DYNAMIC, useGravity=false, FreezePosition (joints can break)", pieceRb);
                    }

                    // Parent to stem runtime (keeps world position with true)
                    go.transform.SetParent(stemRuntime.transform, true);

                    // Mark as kept piece
                    var marker = go.GetComponent<StemPieceMarker>();
                    if (marker != null) marker.isKeptPiece = true;

                    Debug.Log($"[AnchorTopStemPiece] KEEPER '{go.name}': rigidbodies DYNAMIC with position frozen, parented to '{stemRuntime.name}'", go);
                }
                else
                {
                    // Falling piece: check size first - delete if too small (< 0.005)
                    MeshFilter mf = go.GetComponentInChildren<MeshFilter>();
                    float size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.magnitude : 0.1f;
                    
                    if (size < 0.005f)
                    {
                        Debug.Log($"[AnchorTopStemPiece] Falling piece '{go.name}' too small ({size:F4} < 0.005), deleting", go);
                        UnityEngine.Object.Destroy(go);
                        continue;
                    }
                    
                    // Falling piece: enable gravity
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    Debug.Log($"[AnchorTopStemPiece] FALLING '{go.name}': gravity ON", go);
                }
            }
        }

        /// <summary>
        /// Preserves important components from the original stem onto the kept piece.
        /// This ensures the kept piece maintains all necessary attributes for crown/leaf connections.
        /// Note: Joints are NOT copied here - they're rebounded by FlowerJointRebinder after the cut.
        /// </summary>
        static void PreserveComponentsForKeptPiece(GameObject originalStemRoot, GameObject keptPiece)
        {
            if (originalStemRoot == null || keptPiece == null) return;

            // Components to preserve (skip ones that shouldn't be copied)
            // IMPORTANT: Only copy components that should be on the kept piece
            // Don't duplicate components that should stay where they are
            var skipTypes = new System.Type[]
            {
                typeof(Transform),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider),
                typeof(Rigidbody), // Rigidbody is already created by MeshCreation
                typeof(FlowerStemRuntime), // Don't copy - this is on the parent hierarchy
                typeof(StemPieceMarker), // Already added by MeshCreation
                typeof(Joint), // Joints are rebounded by FlowerJointRebinder
                typeof(LeafAttachmentMarker), // These are handled by rebinder
                typeof(EnsureCompoundConvex), // Don't copy - should stay where it is
                typeof(EnsureConvexCollider), // Don't copy - should stay where it is
                typeof(GrabPull), // Don't copy - this is for leaves/petals, not stems
                typeof(SquishMove), // Don't copy - might cause issues on cut pieces
            };

            var components = originalStemRoot.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                var compType = comp.GetType();
                
                // Skip if in skip list
                bool shouldSkip = false;
                foreach (var skipType in skipTypes)
                {
                    if (skipType.IsAssignableFrom(compType))
                    {
                        shouldSkip = true;
                        break;
                    }
                }
                if (shouldSkip) continue;

                // Skip if component already exists
                if (keptPiece.GetComponent(compType) != null) continue;

                // Runtime-safe component copying: create new instance and copy serialized fields
                try
                {
                    var newComp = keptPiece.AddComponent(compType);
                    if (newComp != null)
                    {
                        // Copy serialized fields using reflection (runtime-safe)
                        CopySerializedFields(comp, newComp);
                    }
                }
                catch (System.Exception ex)
                {
                    // Some components can't be copied (e.g., require specific setup)
                    // Log but don't fail - this is expected for some component types
                    Debug.LogWarning($"[MeshCreation] Could not copy component {compType.Name} from {originalStemRoot.name} to {keptPiece.name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Copies serialized fields from source to destination component using reflection.
        /// Runtime-safe alternative to UnityEditorInternal.ComponentUtility.
        /// </summary>
        static void CopySerializedFields(Component source, Component destination)
        {
            if (source == null || destination == null) return;
            if (source.GetType() != destination.GetType()) return;

            var type = source.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | 
                                       System.Reflection.BindingFlags.NonPublic | 
                                       System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                // Skip certain fields that shouldn't be copied
                if (field.Name == "m_GameObject" || field.Name == "m_Enabled" || 
                    field.Name.Contains("m_Rigidbody") || field.Name.Contains("connectedBody"))
                    continue;

                try
                {
                    var value = field.GetValue(source);
                    if (value != null)
                    {
                        field.SetValue(destination, value);
                    }
                }
                catch
                {
                    // Skip fields that can't be copied (e.g., read-only or complex types)
                }
            }
        }

        /// <summary>
        /// Removes unnecessary components from falling pieces for optimal performance.
        /// Keeps only: Rigidbody, Collider, and essential components.
        /// </summary>
        static void CleanupFallingPiece(GameObject fallingPiece)
        {
            if (fallingPiece == null) return;

            // Components to keep (everything else gets removed)
            // IMPORTANT: Keep Rigidbody, EnsureCompoundConvex, and EnsureConvexCollider
            var keepTypes = new System.Type[]
            {
                typeof(Transform),
                typeof(Rigidbody), // CRITICAL: Must keep Rigidbody
                typeof(Collider),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider),
                typeof(StemPieceMarker), // Keep for identification
                typeof(OffScreenDespawner), // Keep for cleanup
                typeof(EnsureCompoundConvex), // CRITICAL: Must keep this
                typeof(EnsureConvexCollider), // CRITICAL: Must keep this
            };

            var components = fallingPiece.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;

                var compType = comp.GetType();
                bool shouldKeep = false;

                foreach (var keepType in keepTypes)
                {
                    if (keepType.IsAssignableFrom(compType))
                    {
                        shouldKeep = true;
                        break;
                    }
                }

                if (!shouldKeep)
                {
                    // CRITICAL: Check if component is still valid before destroying
                    // This prevents memory corruption from destroying already-destroyed objects
                    if (comp == null) continue;
                    
                    // CRITICAL: Don't destroy Joints - they're managed by Unity's physics system
                    // Destroying them while physics is active can cause memory corruption
                    if (comp is Joint)
                    {
                        // Skip joints - let Unity handle them or disconnect them first
                        continue;
                    }
                    
                    // CRITICAL: Don't destroy Rigidbody - physics system needs it
                    if (comp is Rigidbody)
                    {
                        continue;
                    }
                    
                    // CRITICAL: Don't destroy Colliders while physics is active
                    if (comp is Collider)
                    {
                        // Skip colliders - they're needed for physics
                        continue;
                    }
                    
                    // Remove unnecessary component (but skip physics-critical components)
                    try
                    {
                        // Use Destroy with delay to avoid corruption during physics updates
                        if (Application.isPlaying)
                            UnityEngine.Object.Destroy(comp, 0.1f); // Delay destruction
                        else
                            SafeDestroy(comp);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[MeshCreation.CleanupFallingPiece] Failed to destroy component {compType.Name}: {ex.Message}", fallingPiece);
                    }
                }
            }

            // Also clean up children recursively (but keep mesh/collider structure)
            // IMPORTANT: Keep Rigidbody on children too (EnsureConvexCollider requires it)
            var children = fallingPiece.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (child == null || child == fallingPiece.transform) continue;

                var childComps = child.GetComponents<Component>();
                foreach (var comp in childComps)
                {
                    if (comp == null) continue;

                    var compType = comp.GetType();
                    bool shouldKeep = compType == typeof(Transform) ||
                                     compType == typeof(MeshFilter) ||
                                     compType == typeof(MeshRenderer) ||
                                     compType == typeof(Collider) ||
                                     compType == typeof(MeshCollider) ||
                                     compType == typeof(Rigidbody) || // Keep Rigidbody (EnsureConvexCollider requires it)
                                     compType == typeof(EnsureCompoundConvex) ||
                                     compType == typeof(EnsureConvexCollider);

                    if (!shouldKeep)
                    {
                        // CRITICAL: Check if component is still valid before destroying
                        if (comp == null) continue;
                        
                        // CRITICAL: Never destroy physics-critical components
                        if (comp is Rigidbody || comp is Joint || comp is Collider)
                        {
                            continue; // Skip - physics system needs these
                        }
                        
                        // Don't remove Rigidbody if EnsureConvexCollider exists (it requires Rigidbody)
                        if (compType == typeof(Rigidbody))
                        {
                            var ensureConvex = child.GetComponent<EnsureConvexCollider>();
                            if (ensureConvex != null)
                                continue; // Skip - can't remove Rigidbody when EnsureConvexCollider depends on it
                        }

                        try
                        {
                            // Use delayed destruction to avoid corruption during physics updates
                            if (Application.isPlaying)
                                UnityEngine.Object.Destroy(comp, 0.1f); // Delay destruction
                            else
                                SafeDestroy(comp);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[MeshCreation.CleanupFallingPiece] Failed to destroy child component {compType.Name}: {ex.Message}", child);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Standard "stone" piece: parent has Rigidbody, child (root) has Mesh + collider.
        /// </summary>
        static void CreateMesh(ref GameObject root,
                               ref Transform parent,
                               MeshTarget target,
                               Mesh mesh,
                               VirtualMesh vMesh,
                               Material[] materials,
                               int bt,
                               bool forcePhysics = false)
        {
            // Parent: physics root
            parent = new GameObject($"{target.GameobjectRoot.name}").transform;
            parent.transform.rotation = target.transform.rotation;
            parent.transform.position = target.transform.position;
            parent.gameObject.tag = target.GameobjectRoot.tag;

            // Child: actual render mesh
            root = new GameObject($"{target.gameObject.name}");
            root.transform.position = target.transform.position;
            root.transform.rotation = target.transform.rotation;
            root.gameObject.tag = target.transform.tag;

            // Mesh + renderer
            var filter = root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();

            filter.mesh = mesh;
            renderer.materials = materials;

            // Center parent at mesh bounds center (prefer pre-computed bounds)
            Vector3 worldCenter = vMesh.HasMeshBounds
                ? root.transform.TransformPoint(vMesh.MeshBounds.center)
                : renderer.bounds.center;
            parent.transform.position = worldCenter;

            root.transform.SetParent(parent, true);

            // --- Rigidbody on parent ---
            if (target.CreateRigidbody[bt] || forcePhysics)
            {
                var rb = parent.gameObject.AddComponent<Rigidbody>();
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // --- MeshCollider on root ---
            if (target.CreateMeshCollider[bt])
            {
                // Only create when mesh is "large enough".
                bool validForCollider =
                    vMesh.UniqueVerticesCount < 0 ||
                    (vMesh.UniqueVerticesCount > 3 && vMesh.Vertices.Length > 20);

                if (validForCollider)
                {
                    // Remove any stray colliders that might somehow exist
                    RemoveAllColliders(root);
                    RemoveAllColliders(parent.gameObject);

                    MeshCollider collider = root.AddComponent<MeshCollider>();

                    // IMPORTANT: set the sharedMesh and force convex so we never hit
                    // "Concave Mesh Colliders are not supported with dynamic Rigidbody" errors.
                    collider.sharedMesh = mesh;
                    collider.convex = true;
                    //collider.inflateMesh = true; // optional stability helper
                }
            }
        }

        static bool WillBeValidRagdoll(DynamicRagdoll ragdoll, VirtualMesh vMesh)
        {
            foreach (int key in ragdoll.Parts.Keys)
            {
                if (vMesh.DynamicGroups.ContainsKey(key))
                {
                    DynamicRagdollPart part = ragdoll.Parts[key];
                    Vector3[] vertices = vMesh.DynamicGroups[key];
                    float percent = (float)vertices.Length / (float)part.Vertices.Length;
                    if (part.Colliders.Length > 0 && percent > _ragdoll_vertex_threshold)
                        return true;
                }
            }
            return false;
        }

        static void TrimRagdoll(DynamicRagdoll ragdoll, MeshTarget target, VirtualMesh vMesh)
        {
            ragdoll.Assignments = vMesh.Assignments;

            int[] keys = new int[ragdoll.Parts.Keys.Count];
            int index = 0;
            foreach (var key in ragdoll.Parts.Keys)
                keys[index++] = key;

            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
                DynamicRagdollPart part = ragdoll.Parts[key];
                if (vMesh.DynamicGroups.ContainsKey(key))
                {
                    Vector3[] vertices = vMesh.DynamicGroups[key];
                    float percent = (float)vertices.Length / (float)part.Vertices.Length;
                    if (part.Colliders.Length > 0 && percent > _ragdoll_vertex_threshold)
                    {
                        // keep
                    }
                    else
                    {
                        for (int k = 0; k < part.Colliders.Length; k++)
                            SafeDestroy(part.Colliders[k]);
                        part.Colliders = new Collider[0];

                    }

                    part.Vertices = vertices;
                }
                else
                {
                    if (part.Joint != null)
                        SafeDestroy(part.Joint);
                    if (part.Rigidbody != null)
                        SafeDestroy(part.Rigidbody);

                    if (part.Colliders != null)
                    {
                        for (int k = 0; k < part.Colliders.Length; k++)
                            SafeDestroy(part.Colliders[k]);
                    }

                    // optional: clear refs to reduce “use after scheduled destroy” inside your own codepaths
                    part.Joint = null;
                    part.Rigidbody = null;
                    part.Colliders = new Collider[0];

                    SafeDestroy(part);
                    ragdoll.Parts.Remove(key);

                }
            }
        }

        static void CreateRagdoll(ref GameObject root,
                                  ref Transform parent,
                                  Info info,
                                  MeshTarget target,
                                  Mesh mesh,
                                  VirtualMesh vMesh,
                                  Material[] materials,
                                  int bt,
                                  Behaviour behaviour)
        {
            Transform rootBone = CreateSkinnedMeshRenderer(ref root, ref parent, info, target, mesh, vMesh, materials, bt, behaviour);

            parent.transform.position = target.GameobjectRoot.transform.position;
            parent.transform.rotation = target.GameobjectRoot.transform.rotation;

            DynamicRagdoll ragdoll = parent.GetComponent<DynamicRagdoll>();
            List<DynamicRagdollPart> parts = ragdoll.Parts.Values.ToList();

            if (parts.Count == 0)
            {
                Debug.LogError("This shouldn't happen. (Bugreport: Parts of ragdoll is 0)");
            }

            // find outermost "root" parts
            List<DynamicRagdollPart> roots = new List<DynamicRagdollPart>();
            List<DynamicRagdollPart> remainingPartsToCheck = ragdoll.Parts.Values.ToList();
            while (remainingPartsToCheck.Count > 0)
            {
                DynamicRagdollPart part = remainingPartsToCheck[0];
                var toRemove = remainingPartsToCheck[0].GetComponentsInChildren<DynamicRagdollPart>();
                for (int j = 0; j < toRemove.Length; j++)
                {
                    if (parts.Contains(toRemove[j]))
                        remainingPartsToCheck.Remove(toRemove[j]);
                }

                var ancestor = part.GetComponentInParentIgnoreSelf<DynamicRagdollPart>();
                if (ancestor != null && parts.Contains(ancestor))
                {
                    remainingPartsToCheck.Remove(part);
                }
                else
                {
                    remainingPartsToCheck.Remove(part);
                    roots.Add(part);
                }
            }

            // move all roots to top, make them direct children of parent
            var allKids = rootBone.transform.GetComponentsInChildren<Transform>(true);
            List<Transform> childrenToMove = new List<Transform>();
            for (int i = 0; i < allKids.Length; i++)
                childrenToMove.Add(allKids[i]);

            foreach (var r in roots)
            {
                r.transform.SetParent(parent);
                Transform[] rootChildren = r.transform.GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < rootChildren.Length; j++)
                    childrenToMove.Remove(rootChildren[j]);
            }

            // flat hierarchy, move to closest root
            for (int j = 0; j < childrenToMove.Count; j++)
            {
                DynamicRagdollPart closestRoot = roots[0];
                for (int i = 1; i < roots.Count; i++)
                {
                    if (Vector3.Distance(roots[i].transform.position, childrenToMove[j].position) <
                        Vector3.Distance(closestRoot.transform.position, target.transform.position))
                    {
                        closestRoot = roots[i];
                    }
                }
                childrenToMove[j].SetParent(closestRoot.transform);
            }

            // connect outer roots together
            if (roots.Count > 1)
            {
                for (int j = 0; j < roots.Count - 1; j++)
                    roots[j].Joint.connectedBody = roots[j + 1].Rigidbody;
            }

            bool hasCollider = false;

            // ensure inner roots have connected rigidbody
            for (int j = 0; j < parts.Count; j++)
            {
                if (!hasCollider && parts[j].Colliders.Length > 0)
                    hasCollider = true;

                if (parts[j].Joint == null)
                    continue;

                if (parts[j].Joint.connectedBody == null)
                {
                    var rb = parts[j].GetComponentInParentIgnoreSelf<Rigidbody>();
                    if (rb != null)
                    {
                        parts[j].Joint.connectedBody = rb;
                    }
                    else
                    {
                        if (!roots.Contains(parts[j]))
                            Debug.LogError("DynamicRagdoll: joint with no connectedBody and no root found.");
                    }
                }
            }

            if (!hasCollider)
            {
                Debug.LogError("Dynamic Ragdoll has no more collider");
            }

            // activate physics for the rigidbody
            switch (target.Physics[bt])
            {
                case RagdollPhysics.LeaveAsIs:
                    break;
                case RagdollPhysics.NonKinematic:
                    ragdoll.SetRagdollKinematic(false);
                    break;
                case RagdollPhysics.Kinematic:
                    ragdoll.SetRagdollKinematic(true);
                    break;
            }
        }

        static void CreateAnimatedMesh(ref GameObject root,
                                       ref Transform parent,
                                       Info info,
                                       MeshTarget target,
                                       Mesh mesh,
                                       VirtualMesh vMesh,
                                       Material[] materials,
                                       int bt,
                                       Behaviour behaviour)
        {
            Animator tAnimator = target.Animator;

            if (target.IsSkinned)
            {
                CreateSkinnedMeshRenderer(ref root, ref parent, info, target, mesh, vMesh, materials, bt, behaviour);
            }
            else
            {
                parent = GameObject.Instantiate(target.Animator.gameObject).transform;
                root = parent.GetComponentInChildren<MeshTarget>().gameObject;

                var filter = root.GetComponent<MeshFilter>();
                var renderer = root.GetComponent<MeshRenderer>();
                filter.mesh = mesh;
                renderer.materials = materials;
            }

            parent.transform.position = tAnimator.transform.position;
            parent.transform.rotation = tAnimator.transform.rotation;

            // copy animator data and play
            AnimatorStateInfo tState = tAnimator.GetCurrentAnimatorStateInfo(0);
            Animator nAnimator = parent.gameObject.GetComponent<Animator>();

            nAnimator.runtimeAnimatorController = tAnimator.runtimeAnimatorController;
            nAnimator.avatar = tAnimator.avatar;
            nAnimator.applyRootMotion = tAnimator.applyRootMotion;
            nAnimator.updateMode = tAnimator.updateMode;
            nAnimator.cullingMode = tAnimator.cullingMode;

            nAnimator.Play(tState.fullPathHash, 0, tState.normalizedTime);
        }

        /// <summary>
        /// Duplicates the armature and returns the root bone.
        /// </summary>
        public static Transform CreateSkinnedMeshRenderer(ref GameObject meshRoot,
                                                          ref Transform parent,
                                                          Info info,
                                                          MeshTarget target,
                                                          Mesh mesh,
                                                          VirtualMesh vMesh,
                                                          Material[] materials,
                                                          int bt,
                                                          Behaviour behaviour)
        {
            parent = GameObject.Instantiate(target.GameobjectRoot).transform;
            var nRenderer = parent.GetComponentInChildren<SkinnedMeshRenderer>();
            meshRoot = nRenderer.gameObject;
            Transform rootbone = nRenderer.rootBone;

            if (target.DynamicRagdoll != null)
            {
                DynamicRagdoll nRagdoll = parent.GetComponent<DynamicRagdoll>();
                TrimRagdoll(nRagdoll, target, vMesh);
            }

            if (target.Animator != null)
            {
                Animator nAnimator = parent.GetComponent<Animator>();
                if (behaviour == Behaviour.Animation)
                {
                    // keep animator component
                }
                else
                {
                    SafeDestroy(nAnimator);

                }
            }

            mesh.bindposes = info.Bindposes;
            mesh.boneWeights = vMesh.BoneWeights;
            nRenderer.sharedMesh = mesh;
            nRenderer.materials = materials;

            return rootbone;
        }

        /// <summary>
        /// Translate created objects away from the cutting plane by "separation".
        /// </summary>
        public static void TranslateCreatedObjects(Info info, GameObject[] createdObjects, MeshTarget[] targets, float separation)
        {
            if (createdObjects == null)
                return;

            VirtualPlane plane = info.Plane;

            for (int i = 0; i < createdObjects.Length; i++)
            {
                if (createdObjects[i] == null || targets[i] == null)
                    continue;

                if (!targets[i].ApplyTranslation)
                    continue;

                GameObject createdObject = createdObjects[i];

                int sign = (info.Sides[i] == 1) ? -1 : 1;

                Vector3 translation = sign * plane.WorldNormal.normalized * separation;
                createdObject.transform.position += translation;
            }
        }

        public static Material[] GetMaterials(GameObject target)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
                return renderer.materials;

            SkinnedMeshRenderer sRenderer = target.GetComponent<SkinnedMeshRenderer>();
            if (sRenderer != null)
                return sRenderer.materials;

            return null;
        }

        public static T GetComponentInParentIgnoreSelf<T>(this Component target, bool includeInactive = false) where T : Component
        {
            Component[] allComponents = target.GetComponentsInParent<T>(includeInactive);
            foreach (var c in allComponents)
            {
                if (c.transform.gameObject != target.transform.gameObject)
                    return c as T;
            }
            return null;
        }

        static void RemoveAllColliders(GameObject go)
        {
            if (go == null) return;

            var cols = go.GetComponents<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (Application.isPlaying)
                    GameObject.Destroy(cols[i]);
                else
                    SafeDestroy(cols[i]);
       
            }
        }

        public static void GetMeshInfo(MeshTarget target, out Mesh outMesh, out Matrix4x4[] outBindposes)
        {
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter != null)
            {
                outMesh = filter.sharedMesh;
                outBindposes = new Matrix4x4[0];
                return;
            }

            SkinnedMeshRenderer renderer = target.GetComponent<SkinnedMeshRenderer>();
            if (renderer != null)
            {
                Mesh mesh = new Mesh();
                renderer.BakeMesh(mesh);
                mesh.boneWeights = renderer.sharedMesh.boneWeights;
                outMesh = mesh;

                Matrix4x4 scale = Matrix4x4.Scale(target.transform.localScale).inverse;
                outBindposes = renderer.sharedMesh.bindposes;
            }
            else
            {
                outMesh = null;
                outBindposes = null;
            }
        }
    }
}