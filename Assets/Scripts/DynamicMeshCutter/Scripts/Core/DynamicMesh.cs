/**
 * @file DynamicMesh.cs
 * @brief Mutable mesh container used during cutting.
 *
 * @details
 * DynamicMesh accumulates geometry during the cutting process.
 * It is later converted into one or more VirtualMesh instances.
 *
 * @warning
 * This class allocates heavily by design.
 * Never reuse across cuts.
 *
 * @ingroup thirdparty_meshcutting
 */

using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
    public class DynamicMesh
    {
        public List<int> Triangles = new();
        public List<Vector3> Vertices = new();
        public List<Vector3> Normals = new();
        public List<Vector2> UVs = new();
        public List<BoneWeight> BoneWeights = new();
        public List<List<int>> SubIndices = new();

        public int AmountOfClusters;
        public int AmountOfUniqueVertices = -1;
        public int[] Cluster;
        public int[] SubMesh;

        public DynamicRagdoll DynamicRagdoll;
        public List<int> RD = new();
        public Dictionary<int, List<Vector3>> ColliderGroups = new();

        private VirtualMesh _targetMesh;

        public void SetTargetMesh(VirtualMesh targetMesh)
        {
            _targetMesh = targetMesh;
        }

        public void AddTriangle(int[] triangle, int sub, DynamicMesh parent)
        {
            int floor = Vertices.Count;

            for (int i = 0; i < 3; i++)
            {
                SubIndices[sub].Add(floor + i);
                Triangles.Add(floor + i);

                int index = triangle[i];
                Vertices.Add(parent.Vertices[index]);
                Normals.Add(parent.Normals[index]);
                UVs.Add(parent.UVs[index]);

                if (parent._targetMesh.HasBoneWeight)
                    BoneWeights.Add(parent.BoneWeights[index]);

                if (parent.DynamicRagdoll != null)
                {
                    int part = parent.RD[index];
                    RD.Add(part);
                    if (part > -1)
                    {
                        if (!ColliderGroups.ContainsKey(part))
                            ColliderGroups.Add(part, new List<Vector3>());
                        ColliderGroups[part].Add(parent.Vertices[index]);
                    }
                }
            }
        }

        public void AddTriangle(int[] triangle, int sub)
        {
            int floor = Vertices.Count;

            for (int i = 0; i < 3; i++)
            {
                SubIndices[sub].Add(floor + i);
                Triangles.Add(floor + i);

                int index = triangle[i];
                Vertices.Add(_targetMesh.Vertices[index]);
                Normals.Add(_targetMesh.Normals[index]);
                UVs.Add(_targetMesh.UVs[index]);

                if (_targetMesh.HasBoneWeight)
                    BoneWeights.Add(_targetMesh.BoneWeights[index]);

                if (_targetMesh.DynamicRagdoll != null)
                {
                    int part = _targetMesh.Assignments[index];
                    RD.Add(part);
                    if (part > -1)
                    {
                        if (!ColliderGroups.ContainsKey(part))
                            ColliderGroups.Add(part, new List<Vector3>());
                        ColliderGroups[part].Add(_targetMesh.Vertices[index]);
                    }
                }
            }
        }

        public void AddTriangle(Vector3[] vertices,
                                Vector3[] normals,
                                Vector2[] uvs,
                                BoneWeight[] boneWeights,
                                int[] rd,
                                Vector3 faceNormal,
                                int submesh)
        {
            int floor = Vertices.Count;
            Vector3 calcNormal =
                Vector3.Cross((vertices[1] - vertices[0]).normalized,
                              (vertices[2] - vertices[0]).normalized);

            int[] order =
                Vector3.Dot(calcNormal, faceNormal) >= 0
                ? new int[3] { 0, 1, 2 }
                : new int[3] { 2, 1, 0 };

            for (int i = 0; i < 3; i++)
            {
                SubIndices[submesh].Add(floor + i);
                Triangles.Add(floor + i);

                int index = order[i];
                Vertices.Add(vertices[index]);
                Normals.Add(normals[index]);
                UVs.Add(uvs[index]);

                if (_targetMesh.HasBoneWeight)
                    BoneWeights.Add(boneWeights[index]);

                if (_targetMesh.Assignments != null)
                {
                    int part = rd[index];
                    RD.Add(part);
                    if (part > -1)
                    {
                        if (!ColliderGroups.ContainsKey(part))
                            ColliderGroups.Add(part, new List<Vector3>());
                        ColliderGroups[part].Add(_targetMesh.Vertices[index]);
                    }
                }
            }
        }
    }
}
