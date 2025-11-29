using UnityEngine;

namespace DynamicMeshCutter
{
    /// <summary>
    /// Angle-stage wrapper around PlaneBehaviour:
    /// - First Cut() call: ARM angle mode.
    /// - Move/tilt plane with AngleCuttingPlaneController.
    /// - Second Cut() call: forwards to base PlaneBehaviour.Cut().
    /// 
    /// All actual slicing logic + OnCreated come from PlaneBehaviour,
    /// which we already know works.
    /// </summary>
    public class AngleStagePlaneBehaviour : PlaneBehaviour
    {
        [Header("Two-Stage Angle Mode")]
        [Tooltip("If true, first Cut() arms the angle stage, second Cut() executes the cut.")]
        public bool useTwoStageAngleMode = true;

        [Tooltip("Optional: automatically cancel the angle stage if this object is disabled.")]
        public bool autoCancelOnDisable = true;

        [Header("Angle Snapping")]
        [Tooltip("Snap the tilt angle in degrees (e.g. 5 = snap to 0, 5, 10, 15...). " +
                 "Set to 0 to disable snapping. Controller reads this value.")]
        public float AngleSnapStepDeg = 5f;

        /// <summary>
        /// Fired whenever the armed state changes (true = armed, false = not).
        /// Used by AngleCuttingPlaneController.
        /// </summary>
        public System.Action<bool> OnAngleStageArmedChanged;

        // internal flag for armed state
        private bool _angleStageArmed = false;
        public bool IsAngleStageArmed => _angleStageArmed;

        // ───────────────────── Unity ─────────────────────

        // We add extra preview when ARMED, then let PlaneBehaviour do its normal Update.
        new void Update()
        {
            // While armed, keep pushing the current plane pose into the HUD preview.
            if (_angleStageArmed && useTwoStageAngleMode)
            {
                PreviewAgainstFlower();
            }

            // This calls PlaneBehaviour.Update(), which:
            //  - does previewBeforeCut if enabled
            //  - calls CutterBehaviour.Update() for DMC async work
            base.Update();
        }

        void OnDisable()
        {
            if (autoCancelOnDisable && _angleStageArmed)
            {
                _angleStageArmed = false;
                OnAngleStageArmedChanged?.Invoke(false);
            }
        }

        // ───────────────────── Public API ─────────────────────

        /// <summary>
        /// External callers (AngleCuttingPlaneController) use this instead of PlaneBehaviour.Cut().
        /// In two-stage mode:
        ///   - First press: arm.
        ///   - Second press: perform the actual cut via base.Cut().
        /// </summary>
        public new void Cut()
        {
            if (!useTwoStageAngleMode)
            {
                // Just behave like a normal PlaneBehaviour
                base.Cut();
                return;
            }

            if (!_angleStageArmed)
            {
                ArmAngleStage();
                return;
            }

            // Already armed → this second press actually performs the cut.
            // We simply call the working PlaneBehaviour.Cut().
            base.Cut();

            // After a real cut, disarm.
            _angleStageArmed = false;
            OnAngleStageArmedChanged?.Invoke(false);
        }

        /// <summary>
        /// Called by controller while in angle mode to cancel without cutting.
        /// </summary>
        public void CancelAngleStage()
        {
            if (!_angleStageArmed) return;

            if (debugLogs)
                Debug.Log("[AngleStagePlane] Angle stage cancelled.", this);

            _angleStageArmed = false;
            OnAngleStageArmedChanged?.Invoke(false);
        }

        // ───────────────────── Internal helpers ─────────────────────

        private void ArmAngleStage()
        {
            _angleStageArmed = true;
            OnAngleStageArmedChanged?.Invoke(true);

            // Immediate preview so HUD updates right away
            PreviewAgainstFlower();

            if (debugLogs)
                Debug.Log("[AngleStagePlane] Angle stage ARMED. Tilt plane to desired angle, then press Cut() again.", this);
        }
    }
}
