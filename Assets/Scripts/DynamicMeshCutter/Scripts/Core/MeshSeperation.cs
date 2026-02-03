/**
 * @file MeshSeperation.cs
 * @brief Separates DynamicMesh geometry into disconnected clusters.
 *
 * @details
 * Intent:
 * - After a cut, the output mesh may contain multiple disconnected "islands" (clusters).
 * - This file identifies those clusters and can emit one VirtualMesh per cluster.
 *
 * Why this exists:
 * - Mesh cutting can create floating fragments not connected to the main stem.
 * - Gameplay needs those fragments to become separate rigidbodies (falling pieces),
 *   while the held piece remains anchored / evaluated.
 *
 * Stability & determinism (critical patch):
 * - The original implementation uses Dictionary<Vector3, ...> for "unique vertices".
 *   Using raw float Vector3 keys is fragile: tiny float differences can split what should
 *   be identical vertices into separate dictionary buckets.
 * - That causes non-deterministic clusters (sometimes extra clusters), which then makes
 *   "which chunk is held" inconsistent and worsens the visible "drop on cut" issue.
 *
 * Patch approach:
 * - Quantize vertex positions into integer grid coordinates before hashing / dictionary use.
 * - Replace List.Contains membership checks with HashSet for visited/gray sets (perf).
 * - Fix AmountOfUniqueVertices calculation (original accidentally wrote 0 due to clearing).
 *
 * Invariants:
 * - Clusters are computed over vertex adjacency derived from triangle neighborhood.
 * - No triangles are created or destroyed; triangles are assigned to clusters.
 *
 * Performance notes:
 * - This is expensive (O(V + T) plus adjacency overhead) and should run only on cut events.
 * - HashSet membership makes flood-fill significantly cheaper than repeated List.Contains.
 *
 * Gotchas / failure modes:
 * - Quantization step too large can merge distinct vertices into one "unique vertex",
 *   incorrectly connecting clusters.
 * - Quantization step too small won't improve stability.
 * - This file does not "fix physics drops" alone; it only stabilizes cluster identity.
 *
 * @ingroup thirdparty_meshcutting
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
    public static class MeshSeperation
    {
        // ─────────────────────────────────────────────────────────────
        // Quantization: stable hashing for float positions
        // ─────────────────────────────────────────────────────────────

        /**
         * Quantization step (world/mesh units).
         * 0.0001 = 0.1mm if your meshes are in meters.
         *
         * Tune if needed:
         * - If you still see random extra clusters: increase slightly (e.g., 0.0002–0.001).
         * - If clusters incorrectly merge: decrease (e.g., 0.00005).
         */
        private const float kQuantizeStep = 0.0001f;

        /// <summary>
        /// A stable integer key derived from a Vector3, suitable for dictionary hashing.
        /// </summary>
        private readonly struct QuantizedV3 : IEquatable<QuantizedV3>
        {
            public readonly int x;
            public readonly int y;
            public readonly int z;

            public QuantizedV3(Vector3 v)
            {
                x = Mathf.RoundToInt(v.x / kQuantizeStep);
                y = Mathf.RoundToInt(v.y / kQuantizeStep);
                z = Mathf.RoundToInt(v.z / kQuantizeStep);
            }

            public bool Equals(QuantizedV3 other) => x == other.x && y == other.y && z == other.z;
            public override bool Equals(object obj) => obj is QuantizedV3 other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    // A simple, fast integer hash combine
                    int h = 17;
                    h = h * 31 + x;
                    h = h * 31 + y;
                    h = h * 31 + z;
                    return h;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Populates dynamicMesh.Cluster and dynamicMesh.SubMesh arrays by flood-filling vertex adjacency.
        /// </summary>
        /// <remarks>
        /// Implementation notes:
        /// - "Unique vertices" here means "unique positions after quantization", not strict float equality.
        /// - Adjacency is derived from triangle neighborhood in the indexed stream.
        /// </remarks>
        public static void FloodFillClusters(DynamicMesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            // Copy submesh index arrays (as original)
            int[][] allSubIndices = new int[mesh.SubIndices.Count][];
            for (int i = 0; i < allSubIndices.Length; i++)
                allSubIndices[i] = mesh.SubIndices[i].ToArray();

            Vector3[] vertices = mesh.Vertices.ToArray();

            // Map quantized position -> info about occurrences & neighbors
            // NOTE: We keep the original VertexInfo model, but make the key stable.
            Dictionary<QuantizedV3, VertexInfo> uniqueVertices = new();

            // Build occurrence lists per unique quantized position
            for (int sub = 0; sub < allSubIndices.Length; sub++)
            {
                int[] subIndices = allSubIndices[sub];
                for (int i = 0; i < subIndices.Length; i++)
                {
                    int index = subIndices[i];
                    if ((uint)index >= (uint)vertices.Length) continue;

                    Vector3 vertex = vertices[index];
                    var key = new QuantizedV3(vertex);

                    if (!uniqueVertices.TryGetValue(key, out var info))
                    {
                        info = new VertexInfo(index, sub);
                        uniqueVertices.Add(key, info);
                    }
                    else
                    {
                        info.Occasions_Vertex.Add(index);
                        info.Occasions_Submesh.Add(sub);
                    }
                }
            }

            // Build neighbor graph over UNIQUE POSITIONS (not raw vertex indices).
            //
            // IMPORTANT:
            // This logic preserves your original interpretation where "vi % 3" defines corner
            // and uses vertices[vi +/- 1..2] to find triangle neighbors. This assumes vertices
            // (and the indices stream) are laid out in a triangle-soup-friendly way in this library.
            //
            // If your mesh data is a traditional indexed mesh with shared vertices, you should
            // instead use the triangle index list to connect neighbors. But we are NOT changing
            // that here because it’s third-party behavior and likely relied upon.
            foreach (var kvp in uniqueVertices)
            {
                VertexInfo info = kvp.Value;
                List<int> occ = info.Occasions_Vertex;
                List<Vector3> neighbors = info.Neighbors;

                for (int j = 0; j < occ.Count; j++)
                {
                    int vi = occ[j];
                    int r = vi % 3;

                    // Neighbor vertices in the same triangle (per original logic)
                    int bIndex = r == 0 ? vi + 1 : r == 1 ? vi - 1 : vi - 2;
                    int cIndex = r == 0 ? vi + 2 : r == 1 ? vi + 1 : vi - 1;

                    if ((uint)bIndex < (uint)vertices.Length)
                    {
                        Vector3 b = vertices[bIndex];
                        if (!neighbors.Contains(b)) neighbors.Add(b);
                    }

                    if ((uint)cIndex < (uint)vertices.Length)
                    {
                        Vector3 c = vertices[cIndex];
                        if (!neighbors.Contains(c)) neighbors.Add(c);
                    }
                }
            }

            // Per-vertex assignment arrays (same as original shape)
            int[] cluster = new int[vertices.Length];
            int[] subMesh = new int[vertices.Length];
            Array.Fill(cluster, -1);
            Array.Fill(subMesh, -1);

            // Flood-fill over UNIQUE POSITIONS
            // We use HashSet for membership speed.
            var unvisited = new HashSet<QuantizedV3>(uniqueVertices.Keys);
            var grayQueue = new Queue<QuantizedV3>(128);

            int group = 0;

            void EnqueueNeighbors(QuantizedV3 key, VertexInfo info)
            {
                // Convert neighbor raw Vector3 into quantized keys for lookup.
                // Only consider neighbors that exist in our uniqueVertices map.
                for (int i = 0; i < info.Neighbors.Count; i++)
                {
                    var nKey = new QuantizedV3(info.Neighbors[i]);
                    if (unvisited.Contains(nKey))
                        grayQueue.Enqueue(nKey);
                }
            }

            while (unvisited.Count > 0)
            {
                // Start a new group from any remaining unvisited vertex.
                // PERF: Avoid LINQ .First() - use enumerator to get first element
                QuantizedV3 start = default;
                foreach (var v in unvisited) { start = v; break; }
                grayQueue.Enqueue(start);

                while (grayQueue.Count > 0)
                {
                    QuantizedV3 pop = grayQueue.Dequeue();
                    if (!unvisited.Remove(pop))
                        continue; // already processed

                    // Expand
                    if (!uniqueVertices.TryGetValue(pop, out var info))
                        continue;

                    EnqueueNeighbors(pop, info);

                    // Assign this unique position's occurrences to the cluster/submesh arrays.
                    // NOTE: Occasions_Vertex stores indices into the Vertices buffer.
                    for (int i = 0; i < info.Occasions_Vertex.Count; i++)
                    {
                        int vIndex = info.Occasions_Vertex[i];
                        if ((uint)vIndex >= (uint)cluster.Length) continue;

                        cluster[vIndex] = group;
                        subMesh[vIndex] = info.Occasions_Submesh[i];
                    }
                }

                group++;
            }

            // Write results back to mesh
            mesh.AmountOfClusters = group;

            // FIX: The original code wrote visited.Count after clearing visited each loop,
            // which often ends up 0.
            // The correct "unique vertex" count for this algorithm is the number of unique keys.
            mesh.AmountOfUniqueVertices = uniqueVertices.Count;

            mesh.Cluster = cluster;
            mesh.SubMesh = subMesh;
        }

        /// <summary>
        /// Returns VirtualMeshes either as one combined mesh or separated per cluster.
        /// </summary>
        /// <param name="dynamicMesh">Input dynamic mesh produced by cutter.</param>
        /// <param name="seperateByClusters">If true, returns one VirtualMesh per connected component.</param>
        public static VirtualMesh[] GetVirtualMeshes(DynamicMesh dynamicMesh, bool seperateByClusters)
        {
            if (!seperateByClusters)
                return new[] { ConstructVirtualMeshFromDynamic(dynamicMesh) };

            FloodFillClusters(dynamicMesh);

            int clusters = dynamicMesh.AmountOfClusters;
            int subMeshes = dynamicMesh.SubIndices.Count;

            DynamicMesh[] dynMeshes = new DynamicMesh[clusters];
            for (int i = 0; i < clusters; i++)
            {
                dynMeshes[i] = new DynamicMesh();
                for (int j = 0; j < subMeshes; j++)
                    dynMeshes[i].SubIndices.Add(new List<int>());

                dynMeshes[i].DynamicRagdoll = dynamicMesh.DynamicRagdoll;
            }

            // Reassign triangles into per-cluster dynamic meshes.
            // We derive cluster/submesh from the first vertex of the triangle.
            for (int i = 0; i < dynamicMesh.Triangles.Count; i += 3)
            {
                int a = dynamicMesh.Triangles[i];
                if ((uint)a >= (uint)dynamicMesh.Cluster.Length) continue;

                int cluster = dynamicMesh.Cluster[a];
                if (cluster < 0 || cluster >= clusters) continue;

                int sub = dynamicMesh.SubMesh[a];
                if (sub < 0 || sub >= subMeshes) sub = 0;

                dynMeshes[cluster].AddTriangle(
                    new[]
                    {
                        dynamicMesh.Triangles[i],
                        dynamicMesh.Triangles[i + 1],
                        dynamicMesh.Triangles[i + 2]
                    },
                    sub,
                    dynamicMesh
                );
            }

            VirtualMesh[] result = new VirtualMesh[clusters];
            for (int i = 0; i < clusters; i++)
                result[i] = ConstructVirtualMeshFromDynamic(dynMeshes[i]);

            return result;
        }

        /// <summary>
        /// Constructs a VirtualMesh from the DynamicMesh buffers.
        /// </summary>
        /// <remarks>
        /// This is a "data packaging" step: it does not change geometry.
        /// </remarks>
        private static VirtualMesh ConstructVirtualMeshFromDynamic(DynamicMesh dynamicMesh)
        {
            VirtualMesh vm = new()
            {
                Vertices = dynamicMesh.Vertices.ToArray(),
                Triangles = dynamicMesh.Triangles.ToArray(),
                Normals = dynamicMesh.Normals.ToArray(),
                UVs = dynamicMesh.UVs.ToArray(),
                BoneWeights = dynamicMesh.BoneWeights.ToArray(),
                UniqueVerticesCount = dynamicMesh.AmountOfUniqueVertices
            };

            if (dynamicMesh.DynamicRagdoll != null)
            {
                vm.DynamicRagdoll = dynamicMesh.DynamicRagdoll;
                vm.Assignments = dynamicMesh.RD.ToArray();
                // PERF: Avoid LINQ ToDictionary - manual copy
                vm.DynamicGroups = new Dictionary<int, Vector3[]>(dynamicMesh.ColliderGroups.Count);
                foreach (var kvp in dynamicMesh.ColliderGroups)
                    vm.DynamicGroups[kvp.Key] = kvp.Value.ToArray();
            }

            int[][] subIndices = new int[dynamicMesh.SubIndices.Count][];
            for (int i = 0; i < subIndices.Length; i++)
                subIndices[i] = dynamicMesh.SubIndices[i].ToArray();

            vm.SetIndices(subIndices);
            vm.ComputeBounds();
            return vm;
        }
    }
}
