/**
 * @file VirtualMesh.cs
 * @brief CPU-side snapshot of Unity mesh data used by the DynamicMeshCutter pipeline.
 *
 * @details
 * VirtualMesh is a data container created from a Unity Mesh and then used by the cutter
 * and separation logic to:
 * - read original vertices/normals/UVs/boneweights
 * - produce new cut meshes (VirtualMesh instances) without mutating the source mesh
 * - optionally propagate ragdoll / assignment metadata when DynamicRagdoll is involved
 *
 * This file also defines VertexInfo, a helper structure used by MeshSeperation's flood-fill
 * clustering logic for connectivity analysis.
 *
 * Performance notes:
 * - VirtualMesh construction copies arrays from Unity Mesh (CPU allocation cost).
 * - The pipeline expects VirtualMesh to be "owned" by a single cut execution.
 *
 * @ingroup thirdparty_meshcutting
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
    /**
     * @class VertexInfo
     * @brief Helper record used during flood-fill clustering to track duplicate vertex occurrences and adjacency.
     *
     * @details
     * MeshSeperation.FloodFillClusters uses a Dictionary<Vector3, VertexInfo> keyed by vertex position
     * (note: equality by Vector3 value) to build an adjacency list and map occurrences back to
     * triangle indices and submesh membership.
     *
     * @warning
     * Using Vector3 as a dictionary key relies on exact float equality. If your upstream pipeline
     * generates vertices with tiny float differences, this can reduce clustering accuracy.
     *
     * @ingroup thirdparty_meshcutting
     */
    public class VertexInfo
    {
        /// The vertex position (kept for convenience; dictionary key is also this value).
        public Vector3 Vertex;

        /// Indices into the "flattened triangle index stream" where this vertex occurs.
        public List<int> Occasions_Vertex;

        /// Submesh indices corresponding to each Occasions_Vertex entry.
        public List<int> Occasions_Submesh;

        /// Neighbor vertex positions (adjacency list for flood-fill).
        public List<Vector3> Neighbors;

        /**
         * @brief Construct a VertexInfo with its first occurrence.
         * @param occasion_Vertex Index of this vertex occurrence in the flattened indices list.
         * @param occasion_Submesh Submesh index where this occurrence appears.
         */
        public VertexInfo(int occasion_Vertex, int occasion_Submesh)
        {
            Occasions_Vertex = new List<int>() { occasion_Vertex };
            Occasions_Submesh = new List<int>() { occasion_Submesh };
            Neighbors = new List<Vector3>();
        }
    }

    /**
     * @class VirtualMesh
     * @brief Helper which holds all the necessary data to create and cut a mesh.
     *
     * @details
     * This is the primary data format consumed by MeshCutting.
     * It can be created from a Unity Mesh, filled/modified during cuts, and later used to
     * construct new Unity Mesh instances (via MeshCreation).
     *
     * Ragdoll metadata:
     * - If AssignRagdoll() is called, Assignments and DynamicRagdoll references are stored.
     * - The cutting pipeline may propagate and trim these assignments.
     *
     * @ingroup thirdparty_meshcutting
     */
    public class VirtualMesh
    {
        public Mesh Mesh;

        public int[] Triangles;
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public BoneWeight[] BoneWeights;

        public DynamicRagdoll DynamicRagdoll; //the original ragdoll
        public int[] Assignments; //ragdoll assignments
        public Dictionary<int, Vector3[]> DynamicGroups;
        public Dictionary<int, Bounds> AdjustedBounds; //new bounds for cut off rigidbodies

        /// Overall AABB of all vertices (local/mesh-space).
        public Bounds MeshBounds;
        /// True after ComputeBounds() has run (Bounds is a value type that defaults to zero).
        public bool HasMeshBounds;

        private int _subMeshCount;
        private int[][] _subMeshIndices;

        public int UniqueVerticesCount = -1;

        public int SubMeshCount
        {
            get { return _subMeshCount; }
        }

        private bool _hasBoneWeight = false;

        public bool HasBoneWeight
        {
            get { return _hasBoneWeight; }
        }

        public VirtualMesh() { }

        /**
         * @brief Construct from a Unity Mesh and copy relevant arrays.
         * @param mesh The Unity Mesh to snapshot.
         */
        public VirtualMesh(Mesh mesh)
        {
            Mesh = mesh;

            Vertices = mesh.vertices;
            Normals = mesh.normals;
            UVs = mesh.uv;
            BoneWeights = mesh.boneWeights;

            if (mesh.boneWeights.Length > 0)
            {
                _hasBoneWeight = true;
            }

            _subMeshCount = mesh.subMeshCount;
            _subMeshIndices = new int[_subMeshCount][];
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                _subMeshIndices[i] = mesh.GetIndices(i);
            }
        }

        /**
         * @brief Get the index buffer for a given submesh.
         * @param index Submesh index.
         * @return Indices for that submesh.
         */
        public int[] GetIndices(int index)
        {
            return _subMeshIndices[index];
        }

        /**
         * @brief Replace submesh indices (used after separation / reconstruction).
         * @param indices Array-of-arrays where each entry is a submesh index buffer.
         */
        public void SetIndices(int[][] indices)
        {
            _subMeshIndices = indices;
            _subMeshCount = indices.Length;
        }

        /**
         * @brief Fill UV array with zeros if missing.
         *
         * @details
         * MeshCutting expects UVs to exist. If the input mesh has no UVs, this function creates
         * a UV array of length Vertices.Length and initializes all entries to Vector2.zero.
         *
         * @warning
         * This destroys meaningful UV mapping. It is a fallback for stability only.
         */
        public void FillUVs()
        {
            int length = Vertices.Length;
            UVs = new Vector2[length];
            for (int i = 0; i < Vertices.Length; i++)
            {
                UVs[i] = Vector2.zero;
            }
        }

        /**
         * @brief Attach ragdoll metadata to this virtual mesh.
         * @param dynamicRagdoll The source ragdoll data used for assignments/groups.
         */
        public void AssignRagdoll(DynamicRagdoll dynamicRagdoll)
        {
            DynamicRagdoll = dynamicRagdoll;
            Assignments = dynamicRagdoll.Assignments;
        }

        /**
         * @brief Placeholder for ragdoll-specific setup work (currently unused).
         *
         * @note
         * This is kept as-is from the original library; future work could compute bounds per group.
         */
        public void SetupRagdoll()
        {
            //foreach(var key in ColliderGroups.Keys)
            //{
            //    if (ColliderGroups[key].Length == DynamicRagdoll.Parts[key].Size) //are all vertices of that group present?
            //        continue;

            //    //calc center
            //    Vector3[] vertices = ColliderGroups[key];
            //    Vector3 center = new Vector3();
            //    for (int i = 0; i < vertices.Length; i++)
            //    {
            //        center += vertices[i];
            //    }
            //    center /= vertices.Length;

            //    Bounds bounds = new Bounds(center, new Vector3(0, 0, 0));
            //    for(int i = 0; i < vertices.Length; i++)
            //    {
            //        bounds.Encapsulate(vertices[i]);
            //    }
            //}
        }

        /// <summary>
        /// Compute overall mesh bounds and per-group bounds from vertex data.
        /// Safe to call from a background thread (struct math only, no Unity API).
        /// </summary>
        public void ComputeBounds()
        {
            // --- Overall mesh bounds ---
            if (Vertices == null || Vertices.Length == 0)
                return;

            // Initialize from first vertex to avoid inflated bounds around origin
            Bounds mb = new Bounds(Vertices[0], Vector3.zero);
            for (int i = 1; i < Vertices.Length; i++)
                mb.Encapsulate(Vertices[i]);

            MeshBounds = mb;
            HasMeshBounds = true;

            // --- Per-group bounds (populates AdjustedBounds) ---
            if (DynamicGroups == null || DynamicGroups.Count == 0)
                return;

            AdjustedBounds = new Dictionary<int, Bounds>(DynamicGroups.Count);
            foreach (var kvp in DynamicGroups)
            {
                Vector3[] groupVerts = kvp.Value;
                if (groupVerts == null || groupVerts.Length == 0)
                    continue;

                Bounds gb = new Bounds(groupVerts[0], Vector3.zero);
                for (int i = 1; i < groupVerts.Length; i++)
                    gb.Encapsulate(groupVerts[i]);

                AdjustedBounds[kvp.Key] = gb;
            }
        }

        /**
         * @section viz_virtualmesh_relations Visual Relationships
         * @dot
         * digraph VirtualMeshDeps {
         *   rankdir=LR;
         *   node [shape=box];
         *   VirtualMesh -> DynamicRagdoll;
         *   VirtualMesh -> VertexInfo [style=dashed, label="used by separation"];
         * }
         * @enddot
         */
    }
}
