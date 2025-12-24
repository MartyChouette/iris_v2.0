using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // for IsPointerOverGameObject

namespace DynamicMeshCutter
{
    [RequireComponent(typeof(LineRenderer))]
    public class MouseBehaviour : CutterBehaviour
    {
        private enum CutHitKind
        {
            None,
            Stem,
            Leaf,
            Petal,
            Crown
        }

        public LineRenderer LR => GetComponent<LineRenderer>();

        private Vector3 _from;
        private Vector3 _to;
        private bool _isDragging;

        [Header("Input")]
        [Tooltip("If true, mouse cutting will not start while pointer is over UI.")]
        public bool ignoreWhenPointerOverUI = true;

        [Header("Raycast Settings")]
        [Tooltip("Layers considered for picking the cut points.")]
        public LayerMask raycastMask = ~0;

        [Tooltip("Max distance for the mouse raycast.")]
        public float maxRayDistance = 20f;

        [Tooltip("Fallback depth in front of the camera if raycast misses.")]
        public float fallbackDepth = 3f;

        [Header("Joint / Physics Guardrails")]
        [Tooltip("If true, temporarily disables XYTetherJoint break forces during the cut so the whole flower doesn't explode.")]
        public bool suppressJointBreaksDuringCut = true;

        [Header("Generic Audio")]
        [Tooltip("AudioSource used to play slicing/drag sounds.")]
        public AudioSource audioSource;

        [Tooltip("Played once when you start dragging to define a cut.")]
        public AudioClip dragStartClip;

        [Tooltip("Fallback generic slice sound if no per-type clip is provided.")]
        public AudioClip genericSliceClip;

        [Range(0.5f, 2f)]
        public float pitchMin = 0.95f;

        [Range(0.5f, 2f)]
        public float pitchMax = 1.05f;

        [Header("Per-Type Slice Audio (Optional)")]
        public AudioClip stemSliceClip;
        public AudioClip leafSliceClip;
        public AudioClip petalSliceClip;
        public AudioClip crownSliceClip;

        [Header("Generic VFX")]
        [Tooltip("Optional VFX on the mouse cutter itself when any cut happens.")]
        public ParticleSystem planeCutFX;

        [Tooltip("Fallback generic cut VFX prefab spawned at the cut point.")]
        public ParticleSystem genericCutFXPrefab;

        [Header("Per-Type VFX (Optional)")]
        public ParticleSystem stemCutFXPrefab;
        public ParticleSystem leafCutFXPrefab;
        public ParticleSystem petalCutFXPrefab;
        public ParticleSystem crownCutFXPrefab;

        [Tooltip("If true, instantiate VFX with rotation aligned to the cut direction (toward camera).")]
        public bool alignVFXToCamera = true;

        [Header("Gore / Intensity")]
        [Range(0f, 2f)]
        public float goreIntensity = 1f;

        // ───────────────────────────────── Unity Loop ─────────────────────────────────

        protected override void Update()
        {
            base.Update();

            if (ignoreWhenPointerOverUI &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    VisualizeLine(false);
                }

                return;
            }

            HandleMouseInput();
        }

        // ───────────────────────────────── Mouse Handling ─────────────────────────────────

        private void HandleMouseInput()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;
                _from = GetMouseWorldPoint(cam);

                PlayOneShot(dragStartClip, 0.6f * goreIntensity);
            }

            if (_isDragging)
            {
                _to = GetMouseWorldPoint(cam);
                VisualizeLine(true);
            }
            else
            {
                VisualizeLine(false);
            }

            if (Input.GetMouseButtonUp(0) && _isDragging)
            {
                _isDragging = false;
                VisualizeLine(false);
                PerformCut(cam);
            }
        }

        private Vector3 GetMouseWorldPoint(Camera cam)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, raycastMask,
                                QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return ray.origin + ray.direction * fallbackDepth;
        }

        // ───────────────────────────────── Cutting Logic ─────────────────────────────────

        private void PerformCut(Camera cam)
        {
            Plane plane = new Plane(_from, _to, cam.transform.position);

            if (suppressJointBreaksDuringCut)
            {
                XYTetherJoint.SetCutBreakSuppressed(true);
            }

            try
            {
                var roots = UnityEngine.SceneManagement.SceneManager
                    .GetActiveScene()
                    .GetRootGameObjects();

                foreach (var root in roots)
                {
                    if (!root.activeInHierarchy)
                        continue;

                    var targets = root.GetComponentsInChildren<MeshTarget>();
                    foreach (var target in targets)
                    {
                        if (target == null)
                            continue;

                        FlowerStemRuntime stemRuntime = null;
                        if (target.GameobjectRoot != null)
                            stemRuntime = target.GameobjectRoot.GetComponentInParent<FlowerStemRuntime>();
                        else
                            stemRuntime = target.GetComponentInParent<FlowerStemRuntime>();

                        if (stemRuntime == null)
                            continue;

                        Cut(target, _to, plane.normal, null, OnCreated);
                    }
                }
            }
            finally
            {
                if (suppressJointBreaksDuringCut)
                {
                    XYTetherJoint.SetCutBreakSuppressed(false);
                }
            }
        }

        // ───────────────────────────────── Cut Callback ─────────────────────────────────

        void OnCreated(Info info, MeshCreationData cData)
        {
            if (info == null || info.MeshTarget == null)
                return;

            MeshCreation.TranslateCreatedObjects(
                info,
                cData.CreatedObjects,
                cData.CreatedTargets,
                Separation);

            CutHitKind kind = DetectHitKind(info);

            PlaySliceFX(info, kind);

            var meshTargetGO = info.MeshTarget != null ? info.MeshTarget.gameObject : null;
            if (meshTargetGO == null)
                return;

            var stem = meshTargetGO.GetComponentInParent<FlowerStemRuntime>();
            if (stem != null)
            {
                var cam = Camera.main;
                Vector3 dir = cam != null
                    ? (cam.transform.position - _to).normalized
                    : Vector3.up;

                var session = stem.GetComponentInParent<FlowerSessionController>();
                
                // CRITICAL: Start grace window BEFORE cut to prevent game over during cut process
                session?.StartCutGraceWindow();
                
                try
                {
                    stem.ApplyCutFromPlane(_to, dir);
                }
                catch (MissingReferenceException)
                {
                    return;
                }

                var rebinder = stem.GetComponentInParent<FlowerJointRebinder>();
                try
                {
                    // rebind leaves/petals to nearest stem chunk FIRST (before checking game over)
                    rebinder?.RebindAllPartsToClosestStemPiece();
                }
                catch (MissingReferenceException) { }

                // Check for game over AFTER rebinding (gives system time to stabilize)
                // Use a small delay to ensure physics has settled
                if (session != null)
                {
                    try
                    {
                        session.StartCoroutine(DelayedGameOverCheck(session, 0.1f));
                    }
                    catch (MissingReferenceException) { }
                }
            }
        }

        // ───────────────────────────────── Hit Kind Detection ─────────────────────────────────

        private CutHitKind DetectHitKind(Info info)
        {
            GameObject root =
                info.MeshTarget.GameobjectRoot != null
                    ? info.MeshTarget.GameobjectRoot
                    : info.MeshTarget.gameObject;

            if (root == null)
                return CutHitKind.None;

            // Tags first
            if (root.CompareTag("Stem")) return CutHitKind.Stem;
            if (root.CompareTag("Leaf")) return CutHitKind.Leaf;
            if (root.CompareTag("Petal")) return CutHitKind.Petal;
            if (root.CompareTag("Crown")) return CutHitKind.Crown;

            // Name heuristics
            string n = root.name.ToLowerInvariant();

            if (n.Contains("stem")) return CutHitKind.Stem;
            if (n.Contains("leaf")) return CutHitKind.Leaf;
            if (n.Contains("petal")) return CutHitKind.Petal;
            if (n.Contains("crown") || n.Contains("bud") || n.Contains("head"))
                return CutHitKind.Crown;

            return CutHitKind.None;
        }

        // ───────────────────────────────── FX Helpers ─────────────────────────────────

        private void PlaySliceFX(Info info, CutHitKind kind)
        {
            AudioClip clipToPlay = genericSliceClip;

            switch (kind)
            {
                case CutHitKind.Stem:
                    if (stemSliceClip != null) clipToPlay = stemSliceClip;
                    break;
                case CutHitKind.Leaf:
                    if (leafSliceClip != null) clipToPlay = leafSliceClip;
                    break;
                case CutHitKind.Petal:
                    if (petalSliceClip != null) clipToPlay = petalSliceClip;
                    break;
                case CutHitKind.Crown:
                    if (crownSliceClip != null) clipToPlay = crownSliceClip;
                    break;
            }

            float volume = Mathf.Clamp01(0.8f * goreIntensity);
            PlayOneShot(clipToPlay, volume);

            if (planeCutFX != null)
            {
                planeCutFX.Play();
            }

            ParticleSystem prefab = genericCutFXPrefab;

            switch (kind)
            {
                case CutHitKind.Stem:
                    if (stemCutFXPrefab != null) prefab = stemCutFXPrefab;
                    break;
                case CutHitKind.Leaf:
                    if (leafCutFXPrefab != null) prefab = leafCutFXPrefab;
                    break;
                case CutHitKind.Petal:
                    if (petalCutFXPrefab != null) prefab = petalCutFXPrefab;
                    break;
                case CutHitKind.Crown:
                    if (crownCutFXPrefab != null) prefab = crownCutFXPrefab;
                    break;
            }

            if (prefab != null)
            {
                Quaternion rot = Quaternion.identity;

                if (alignVFXToCamera)
                {
                    var cam = Camera.main;
                    Vector3 dir = cam != null
                        ? (cam.transform.position - _to).normalized
                        : Vector3.up;

                    rot = Quaternion.LookRotation(dir);
                }

                ParticleSystem fx = Instantiate(prefab, _to, rot);

                var main = fx.main;
                main.startSizeMultiplier *= Mathf.Max(goreIntensity, 0.01f);
                main.startSpeedMultiplier *= Mathf.Max(goreIntensity, 0.01f);

                fx.Play();

                Destroy(fx.gameObject, fx.main.duration + main.startLifetime.constantMax + 0.25f);
            }
        }

        private void PlayOneShot(AudioClip clip, float volumeScale = 1f)
        {
            if (audioSource == null || clip == null)
                return;

            audioSource.pitch = Random.Range(pitchMin, pitchMax);
            audioSource.PlayOneShot(clip, volumeScale);
        }

        // ───────────────────────────────── Line Renderer ─────────────────────────────────

        private void VisualizeLine(bool value)
        {
            if (LR == null)
                return;

            LR.enabled = value;

            if (value)
            {
                LR.positionCount = 2;
                LR.SetPosition(0, _from);
                LR.SetPosition(1, _to);
            }
        }
        
        /// <summary>
        /// Delays game over check to allow physics to settle after a cut.
        /// </summary>
        private System.Collections.IEnumerator DelayedGameOverCheck(FlowerSessionController session, float delay)
        {
            yield return new WaitForSeconds(delay);
            session?.CheckStemCutImmediate();
        }
    }
}
