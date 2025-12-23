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
using UnityEngine;

namespace DynamicMeshCutter
{
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

    public static class MeshCreation
    {
        static float _ragdoll_vertex_threshold = 0.75f;

        // IRIS MOD: Defines how short a stem must be before the system declares it "dead"
        public static float CollapseThreshold = 0.15f;

        // IMPORTANT: turn this OFF unless actively debugging (Console logs can blow memory).
        public static bool DebugStemPieceLogs = false;

        /// <summary>
        /// Creates the actual GameObjects (stone, ragdoll, animated) for each VirtualMesh
        /// produced by the cut.
        /// </summary>
        public static MeshCreationData CreateObjects(Info info, Material defaultMaterial, int vertexCreationThreshold)
        {
            if (info == null || info.MeshTarget == null)
                return null;

            VirtualMesh[] createdMeshes = info.CreatedMeshes;
            if (createdMeshes == null)
                return null;

            MeshCreationData cData = new MeshCreationData(createdMeshes.Length);

            MeshTarget target = info.MeshTarget as MeshTarget;
            if (target == null)
                return cData;

            // Materials can be null depending on renderer type / missing renderer.
            Material[] materials = GetMaterials(target.gameObject);
            if (materials == null) materials = Array.Empty<Material>();

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
                if (vMesh == null) continue;
                if (vMesh.Vertices == null) continue;
                if (vMesh.Vertices.Length < vertexCreationThreshold)
                    continue;

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
                    mesh.SetIndices(vMesh.GetIndices(j), MeshTopology.Triangles, j);

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
                        if (tRagdoll != null && vMesh.DynamicGroups != null && vMesh.DynamicGroups.Count > 1)
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
                            CreateAnimatedMesh(ref root, ref parent, info, target, mesh, vMesh, materials, bt, behaviour);
                        else
                        {
                            Debug.LogWarning("Behaviour is set to Animation, but there was no Animator found in parent!");
                            CreateMesh(ref root, ref parent, target, mesh, vMesh, materials, bt, true);
                        }
                        break;
                }

                // Safety: if for any reason this piece failed to create, skip it.
                if (parent == null || root == null)
                    continue;

                // NEW: mark stem pieces so we can find them later
                if (isStemTarget)
                {
                    var marker = parent.gameObject.AddComponent<global::StemPieceMarker>();
                    marker.stemRuntime = stemRuntime;
                }

                // Name the parent "(i/total)Stem" etc.
                string prefix = $"({i}/{createdMeshes.Length})";
                parent.name = prefix + parent.name;
                parent.name = parent.name.Replace("(Clone)", "");

                // 🔍 TRACE: log info about each stem piece we create (OFF BY DEFAULT!)
                if (DebugStemPieceLogs && isStemTarget)
                {
                    Bounds b;
                    if (TryGetBounds(parent.gameObject, out b))
                    {
                        Vector3 size = b.size;
                        Debug.Log(
                            $"[MeshCreation] Stem piece created: '{parent.name}' " +
                            $"size=({size.x:F3}, {size.y:F3}, {size.z:F3}) pos={parent.position}",
                            parent);
                    }
                }

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

            // NEW: for stem cuts, treat the longest piece as the main stem
            if (isStemTarget)
            {
                AnchorTopStemPiece(cData.CreatedObjects, stemRuntime);
            }

            return cData;
        }

        /// <summary>
        /// For stem cuts:
        /// - Choose the kept piece as the one nearest Crown (not stemStart), ignoring microscopic slivers.
        /// - Collapse only if the *largest piece* is below CollapseThreshold.
        /// </summary>
        static void AnchorTopStemPiece(GameObject[] createdObjects, global::FlowerStemRuntime stemRuntime)
        {
            if (createdObjects == null || stemRuntime == null)
                return;

            var crownT = FindCrownTransform(stemRuntime);
            if (crownT == null)
                return;

            Vector3 crownPos = crownT.position;

            // Ignore tiny fragments when choosing kept piece (measured as "length" = max bounds axis).
            const float SLIVER_IGNORE_LENGTH = 0.05f; // tune for scale

            int bestIndex = -1;
            float bestDist = float.MaxValue;

            int largestIndex = -1;
            float largestLen = 0f;

            // Pass 1: find largest piece by LEN, and best kept piece near crown ignoring slivers.
            for (int i = 0; i < createdObjects.Length; i++)
            {
                var go = createdObjects[i];
                if (go == null) continue;

                Bounds b;
                if (!TryGetBounds(go, out b)) continue;

                float len = GetPieceLengthFromBounds(b);

                if (len > largestLen)
                {
                    largestLen = len;
                    largestIndex = i;
                }

                if (len < SLIVER_IGNORE_LENGTH)
                    continue;

                float dist = Vector3.Distance(b.center, crownPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }

            // If everything was a sliver by our ignore rule, fall back to the largest piece.
            if (bestIndex < 0)
                bestIndex = largestIndex;

            if (bestIndex < 0)
            {
                Debug.LogWarning("[MeshCreation.AnchorTopStemPiece] Could not find best piece (bestIndex < 0)", stemRuntime);
                return;
            }

            // Collapse should only happen if the *largest surviving piece* is below the threshold.
            bool mainPieceSurvives = (largestLen >= CollapseThreshold);

            // Keep the chosen piece only if the main piece survives. Otherwise all fall.
            for (int i = 0; i < createdObjects.Length; i++)
            {
                var go = createdObjects[i];
                if (go == null) continue;

                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) continue;

                if (i == bestIndex && mainPieceSurvives)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.constraints = RigidbodyConstraints.None;

                    go.transform.SetParent(stemRuntime.transform, true);

                    if (DebugStemPieceLogs)
                        Debug.Log($"[MeshCreation.AnchorTopStemPiece] KEPT piece '{go.name}': largestLen≈{largestLen:F3}, isKinematic=true, useGravity=false, parented to '{stemRuntime.name}'", go);
                }
                else
                {
                    rb.useGravity = true;
                    rb.isKinematic = false;

                    // unlock translation (keep rotation locks if you had them)
                    rb.constraints &= ~(RigidbodyConstraints.FreezePositionX |
                                        RigidbodyConstraints.FreezePositionY |
                                        RigidbodyConstraints.FreezePositionZ);

                    if (go.transform.IsChildOf(stemRuntime.transform))
                        go.transform.SetParent(null, true);
                }
            }

            if (!mainPieceSurvives)
            {
                Debug.Log($"[Iris] Cut too close! Largest stem piece was {largestLen:F3} (Threshold: {CollapseThreshold}). Collapsing.", stemRuntime);
            }
        }

        static Transform FindCrownTransform(global::FlowerStemRuntime stemRuntime)
        {
            if (stemRuntime == null) return null;

            // 1) Direct name "Crown" anywhere under the stem runtime.
            var byName = stemRuntime.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t != null && t.name == "Crown");
            if (byName != null) return byName;

            // 2) Anything on layer "CrownCore"
            int crownLayer = LayerMask.NameToLayer("CrownCore");
            if (crownLayer >= 0)
            {
                var byLayer = stemRuntime.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t != null && t.gameObject.layer == crownLayer);
                if (byLayer != null) return byLayer;
            }

            // 3) Tag "Crown" (if your project has it)
            try
            {
                var tagged = GameObject.FindGameObjectsWithTag("Crown")
                    .Select(go => go.transform)
                    .FirstOrDefault(t => t != null && t.IsChildOf(stemRuntime.transform));
                if (tagged != null) return tagged;
            }
            catch { /* tag may not exist */ }

            // 4) Last resort (kept for compatibility, but NOT preferred):
            return stemRuntime.stemStart;
        }

        static float GetPieceLengthFromBounds(Bounds b)
        {
            // Don’t assume Y is “length”. Use the longest axis.
            return Mathf.Max(b.size.x, b.size.y, b.size.z);
        }

        static bool TryGetBounds(GameObject go, out Bounds b)
        {
            b = default;

            if (go == null) return false;

            var col = go.GetComponentInChildren<Collider>();
            if (col != null)
            {
                b = col.bounds;
                return true;
            }

            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                b = rend.bounds;
                return true;
            }

            return false;
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

            // Center parent at mesh bounds center
            Vector3 worldCenter = renderer.bounds.center;
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
                    RemoveAllColliders(root);
                    RemoveAllColliders(parent.gameObject);

                    MeshCollider collider = root.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = true;
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
                            GameObject.DestroyImmediate(part.Colliders[k]);
                        part.Colliders = new Collider[0];
                    }

                    part.Vertices = vertices;
                }
                else
                {
                    if (part.Joint != null)
                        GameObject.DestroyImmediate(part.Joint);
                    if (part.Rigidbody != null)
                        GameObject.DestroyImmediate(part.Rigidbody);
                    if (part.Colliders != null)
                    {
                        for (int k = 0; k < part.Colliders.Length; k++)
                            GameObject.DestroyImmediate(part.Colliders[k]);
                    }
                    GameObject.DestroyImmediate(part);
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

            if (roots.Count > 1)
            {
                for (int j = 0; j < roots.Count - 1; j++)
                    roots[j].Joint.connectedBody = roots[j + 1].Rigidbody;
            }

            bool hasCollider = false;

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

            AnimatorStateInfo tState = tAnimator.GetCurrentAnimatorStateInfo(0);
            Animator nAnimator = parent.gameObject.GetComponent<Animator>();

            nAnimator.runtimeAnimatorController = tAnimator.runtimeAnimatorController;
            nAnimator.avatar = tAnimator.avatar;
            nAnimator.applyRootMotion = tAnimator.applyRootMotion;
            nAnimator.updateMode = tAnimator.updateMode;
            nAnimator.cullingMode = tAnimator.cullingMode;

            nAnimator.Play(tState.fullPathHash, 0, tState.normalizedTime);
        }

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
                if (behaviour != Behaviour.Animation)
                    GameObject.DestroyImmediate(nAnimator);
            }

            mesh.bindposes = info.Bindposes;
            mesh.boneWeights = vMesh.BoneWeights;
            nRenderer.sharedMesh = mesh;
            nRenderer.materials = materials;

            return rootbone;
        }

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
                    GameObject.DestroyImmediate(cols[i]);
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
