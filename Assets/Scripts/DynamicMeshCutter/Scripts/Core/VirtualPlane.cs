/**
 * @file VirtualPlane.cs
 * @brief Immutable representation of a cutting plane in local and world space.
 *
 * @details
 * Encapsulates plane math for the DynamicMeshCutter pipeline.
 * This class is allocation-free after construction and intentionally minimal.
 *
 * @warning
 * Side classification is sensitive to floating point precision.
 * Avoid epsilon-sensitive logic around the plane boundary.
 *
 * @ingroup thirdparty_meshcutting
 */

using UnityEngine;

namespace DynamicMeshCutter
{
    public class VirtualPlane
    {
        private Vector3 _localposition;
        private Vector3 _localNormal;
        private Vector3 _worldPosition;
        private Vector3 _worldNormal;

        public Vector3 LocalPosition => _localposition;
        public Vector3 LocalNormal => _localNormal;
        public Vector3 WorldPosition => _worldPosition;
        public Vector3 WorldNormal => _worldNormal;

        public VirtualPlane(Vector3 localPosition,
                            Vector3 localNormal,
                            Vector3 worldPosition,
                            Vector3 worldNormal)
        {
            _localposition = localPosition;
            _localNormal = localNormal.normalized;
            _worldPosition = worldPosition;
            _worldNormal = worldNormal.normalized;
        }

        public int GetSide(Vector3 point)
        {
            Vector3 delta = (point - _localposition).normalized;
            return Vector3.Dot(_localNormal, delta) <= 0 ? 1 : 0;
        }

        public void Raycast(Ray ray, out float distance)
        {
            Vector3 origin = ray.origin;
            Vector3 direction = ray.direction;
            Vector3 normal = _localNormal;

            float dot = Vector3.Dot(normal, _localposition);
            Vector3 intersection =
                origin + ((dot - Vector3.Dot(normal, origin)) /
                          Vector3.Dot(normal, direction)) * direction;

            distance = Vector3.Distance(intersection, origin);
        }
    }
}
