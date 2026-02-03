using UnityEngine;
using Unity.Cinemachine;

namespace Iris.Camera
{
    /// <summary>
    /// Trigger volume that tells HorrorCameraManager to switch to a specific camera
    /// when the player enters the zone.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CameraZoneTrigger : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("The CinemachineCamera to activate when the player enters this zone.")]
        public CinemachineCamera targetCamera;

        [Header("Blend Settings")]
        [Tooltip("True = instant hard cut. False = smooth blend over blendDuration.")]
        public bool hardCut = true;

        [Tooltip("Blend duration in seconds (ignored when hardCut is true).")]
        public float blendDuration = 0.5f;

        [Header("Debug")]
        [SerializeField] private string zoneName = "";

        private void Reset()
        {
            // Auto-configure collider as trigger when component is first added
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            string label = string.IsNullOrEmpty(zoneName) ? gameObject.name : zoneName;
            Debug.Log($"[CameraZoneTrigger] Player entered zone: {label} " +
                      $"(camera={targetCamera?.name}, hardCut={hardCut}, blend={blendDuration}s)");

            if (targetCamera == null)
            {
                Debug.LogWarning($"[CameraZoneTrigger] No target camera assigned on {gameObject.name}");
                return;
            }

            var manager = HorrorCameraManager.Instance;
            if (manager != null)
                manager.SwitchToCamera(targetCamera, hardCut, blendDuration);
            else
                Debug.LogWarning("[CameraZoneTrigger] HorrorCameraManager not found in scene.");
        }

        // ── Scene-view gizmos ──────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            DrawZoneGizmo(0.15f);
        }

        private void OnDrawGizmosSelected()
        {
            DrawZoneGizmo(0.35f);
        }

        private void DrawZoneGizmo(float alpha)
        {
            Color color = hardCut
                ? new Color(1f, 0.2f, 0.2f, alpha)   // red  = hard cut
                : new Color(0.2f, 1f, 0.3f, alpha);   // green = blend

            Gizmos.color = color;
            Gizmos.matrix = transform.localToWorldMatrix;

            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Min(alpha + 0.3f, 1f));
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
}
