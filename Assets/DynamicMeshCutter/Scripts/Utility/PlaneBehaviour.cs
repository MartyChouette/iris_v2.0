using UnityEngine;

namespace DynamicMeshCutter
{
    public class PlaneBehaviour : CutterBehaviour
    {
        public float DebugPlaneLength = 2;

        // Cache these so OnCreated knows which plane we used
        private Vector3 _lastPlanePoint;
        private Vector3 _lastPlaneNormal;

        public void Cut()
        {
            _lastPlanePoint = transform.position;
            _lastPlaneNormal = transform.forward;

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (!root.activeInHierarchy)
                    continue;

                var targets = root.GetComponentsInChildren<MeshTarget>();
                foreach (var target in targets)
                {
                    // Standard DynamicMeshCutter call:
                    Cut(target, _lastPlanePoint, _lastPlaneNormal, null, OnCreated);
                }
            }
        }

        void OnCreated(Info info, MeshCreationData cData)
        {
            // Let DMC move/offset the created objects first
            MeshCreation.TranslateCreatedObjects(info, cData.CreatedObjects, cData.CreatedTargets, Separation);

            // Correct source object (the thing that was cut)
            GameObject sourceGO = info.MeshTarget.gameObject;

            foreach (var createdTarget in cData.CreatedTargets)
            {
                GameObject piece = createdTarget.gameObject;
                CopyComponentsFromSource(sourceGO, piece, _lastPlanePoint, _lastPlaneNormal);
            }
        }

        void CopyComponentsFromSource(GameObject source, GameObject piece, Vector3 planePoint, Vector3 planeNormal)
        {
            foreach (var comp in source.GetComponents<Component>())
            {
                // Skip core components already handled by DMC
                if (comp is Transform || comp is MeshFilter || comp is MeshRenderer || comp is MeshTarget)
                    continue;

                // SPECIAL CASE: joints
                if (comp is Joint joint)
                {
                    TryHandleJointOnCut(joint, source, piece, planePoint, planeNormal);
                    continue;
                }

                // Generic component copy – add as needed
                var type = comp.GetType();
                var newComp = piece.AddComponent(type);
                // You can manually copy specific fields here if needed.
            }
        }

        void TryHandleJointOnCut(
            Joint original,
            GameObject source,
            GameObject piece,
            Vector3 planePoint,
            Vector3 planeNormal)
        {
            var policy = original.GetComponent<JointCutPolicy>();
            var mode = policy != null ? policy.mode : JointSplitMode.KeepAnchorSideOnly;

            if (mode == JointSplitMode.DestroyOnCut)
                return;

            var pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider == null)
                return;

            // Convert anchor to world space
            Vector3 anchorWorld = original.transform.TransformPoint(original.anchor);

            bool thisPieceHasAnchor = pieceCollider.bounds.Contains(anchorWorld);

            // --- Mode: keep only anchor side ---
            if (mode == JointSplitMode.KeepAnchorSideOnly && !thisPieceHasAnchor)
                return;

            // --- Mode: Custom hook ---
            if (mode == JointSplitMode.CustomLogic)
            {
                policy?.OnCutCustom?.Invoke(original, source, piece);
                return;
            }

            // -------------------------------------------------------
            // CLONE THE JOINT ONTO THIS PIECE
            // -------------------------------------------------------
            var cloned = piece.AddComponent(original.GetType()) as Joint;
            if (!cloned)
                return;

            cloned.anchor = original.anchor;
            cloned.autoConfigureConnectedAnchor = original.autoConfigureConnectedAnchor;
            cloned.connectedAnchor = original.connectedAnchor;
            cloned.breakForce = original.breakForce;
            cloned.breakTorque = original.breakTorque;
            cloned.enableCollision = original.enableCollision;

            // connectedBody logic:
            var srcRb = source.GetComponent<Rigidbody>();

            // If originally attached to some OTHER body (e.g., stalk, ceiling)
            if (original.connectedBody != null && original.connectedBody != srcRb)
            {
                cloned.connectedBody = original.connectedBody;
            }
            else
            {
                // If it was self-connected, you decide:
                cloned.connectedBody = null;
            }

            // Hinge extra fields
            if (original is HingeJoint oH && cloned is HingeJoint cH)
            {
                cH.axis = oH.axis;
                cH.useLimits = oH.useLimits;
                cH.limits = oH.limits;
                cH.useSpring = oH.useSpring;
                cH.spring = oH.spring;
            }

            // TODO: Add similar blocks for SpringJoint, ConfigurableJoint, etc. as needed.
        }
    }
}
