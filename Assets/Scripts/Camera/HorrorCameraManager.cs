using UnityEngine;
using Unity.Cinemachine;

namespace Iris.Camera
{
    /// <summary>
    /// Scene-scoped singleton that manages Cinemachine camera switching for
    /// fixed-angle horror camera zones. Manipulates CinemachineCamera.Priority
    /// and overrides CinemachineBrain.DefaultBlend per transition.
    /// </summary>
    public class HorrorCameraManager : MonoBehaviour
    {
        public static HorrorCameraManager Instance { get; private set; }

        [Header("References (auto-found if empty)")]
        [SerializeField] private CinemachineBrain brain;

        private const int PriorityActive   = 20;
        private const int PriorityInactive = 0;

        private CinemachineCamera _activeCamera;

        private void Awake()
        {
            // Scene-scoped singleton — no DontDestroyOnLoad
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[HorrorCameraManager] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (brain == null)
                brain = FindAnyObjectByType<CinemachineBrain>();

            if (brain == null)
                Debug.LogError("[HorrorCameraManager] No CinemachineBrain found in scene.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Switch to the given camera, optionally using a hard cut or smooth blend.
        /// </summary>
        public void SwitchToCamera(CinemachineCamera cam, bool hardCut, float blendDuration)
        {
            if (cam == null || cam == _activeCamera) return;

            Debug.Log($"[HorrorCameraManager] Switching to {cam.name} " +
                      $"(hardCut={hardCut}, blend={blendDuration}s)");

            // Override the brain's default blend for this transition
            if (brain != null)
            {
                if (hardCut)
                {
                    brain.DefaultBlend = new CinemachineBlendDefinition(
                        CinemachineBlendDefinition.Styles.Cut, 0f);
                }
                else
                {
                    brain.DefaultBlend = new CinemachineBlendDefinition(
                        CinemachineBlendDefinition.Styles.EaseInOut, blendDuration);
                }
            }

            // Lower all cameras, raise the target
            var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var c in allCams)
                c.Priority = PriorityInactive;

            cam.Priority = PriorityActive;
            _activeCamera = cam;
        }
    }
}
