using UnityEngine;
using DynamicMeshCutter;

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
                    // This is the standard DynamicMeshCutter call you already use:
                    Cut(target, _lastPlanePoint, _lastPlaneNormal, null, OnCreated);
                }
            }
        }

        void OnCreated(Info info, MeshCreationData cData)
        {
            // First let the library do its usual translation
            MeshCreation.TranslateCreatedObjects(info, cData.CreatedObjects, cData.CreatedTargets, Separation);

            // Then handle component copying, including joints
            var sourceGO = info.Source.gameObject; // adjust if your Info type uses a different name

            foreach (var createdTarget in cData.CreatedTargets)
            {
                GameObject piece = createdTarget.gameObject;
                CopyComponentsFromSource(sourceGO, piece, _lastPlanePoint, _lastPlaneNormal);
            }
        }

        void CopyComponentsFromSource(GameObject source, GameObject piece, Vector3 planePoint, Vector3 planeNormal)
        {
            var sourceRb = source.GetComponent<Rigidbody>();

            foreach (var comp in source.GetComponents<Component>())
            {
                if (comp is Transform || comp is MeshFilter || comp is MeshRenderer || comp is MeshTarget)
                    continue; // skip core components already handled

                // SPECIAL CASE: joints
                if (comp is Joint joint)
                {
                    TryAttachJointToPiece(joint, source, piece, planePoint, planeNormal, sourceRb);
                    continue;
                }

                // Generic component copy: easiest is to add a new one of the same type
                var type = comp.GetType();
                var newComp = piece.AddComponent(type);

                // Option A: you manually copy the fields you care about.
                // Option B (editor only): use UnityEditorInternal.ComponentUtility to copy all.
                // (For runtime builds, you'd do manual / reflection-based copy.)
            }
        }

        void TryAttachJointToPiece(
            Joint original,
            GameObject source,
            GameObject piece,
            Vector3 planePoint,
            Vector3 planeNormal,
            Rigidbody sourceRb)
        {
            var pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider == null)
                return;

            // World position of the joint’s anchor on the source mesh
            Vector3 anchorWorld = original.transform.TransformPoint(original.anchor);

            // Simple heuristic: which piece's collider bounds contains the anchor?
            if (!pieceCollider.bounds.Contains(anchorWorld))
                return;

            // At this point we decided: this piece should own the joint.
            var cloned = piece.AddComponent(original.GetType()) as Joint;
            if (cloned == null)
                return;

            // Copy basic joint settings (add more as needed)
            cloned.anchor = original.anchor;
            cloned.autoConfigureConnectedAnchor = original.autoConfigureConnectedAnchor;
            cloned.connectedAnchor = original.connectedAnchor;
            cloned.breakForce = original.breakForce;
            cloned.breakTorque = original.breakTorque;
            cloned.enableCollision = original.enableCollision;

            // Connected body logic:
            if (original.connectedBody != null && original.connectedBody != sourceRb)
            {
                // The joint was attached to some other rigidbody in the scene (e.g., ceiling).
                // Keep that connection.
                cloned.connectedBody = original.connectedBody;
            }
            else
            {
                // If it was self-connected (or to the cut body), decide what to do:
                //  - null = let it hang freely from this anchor
                //  - or assign some new body if you have one
                cloned.connectedBody = null;
            }

            // For specific joint types you can copy extra fields, e.g. hinge limits:
            if (original is HingeJoint origHinge && cloned is HingeJoint newHinge)
            {
                newHinge.axis = origHinge.axis;
                newHinge.useLimits = origHinge.useLimits;
                newHinge.limits = origHinge.limits;
                newHinge.useSpring = origHinge.useSpring;
                newHinge.spring = origHinge.spring;
            }

            // Same idea for ConfigurableJoint, SpringJoint, etc.
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

            // If originally attached to some OTHER body (e.g., flower stalk)
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

            // Add similar blocks for SpringJoint, ConfigurableJoint, etc
        }


    }


}
