using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DynamicMeshCutter
{
    [RequireComponent(typeof(LineRenderer))]
    public class MouseBehaviour : CutterBehaviour
    {
        public LineRenderer LR => GetComponent<LineRenderer>();
        private Vector3 _from;
        private Vector3 _to;
        private bool _isDragging;

        protected override void Update()
        {
            base.Update();

            if (Input.GetMouseButtonDown(0))
            {
                _isDragging = true;

                var mousePos = new Vector3(
                    Input.mousePosition.x,
                    Input.mousePosition.y,
                    Camera.main.nearClipPlane + 0.05f);

                _from = Camera.main.ScreenToWorldPoint(mousePos);
            }

            if (_isDragging)
            {
                var mousePos = new Vector3(
                    Input.mousePosition.x,
                    Input.mousePosition.y,
                    Camera.main.nearClipPlane + 0.05f);

                _to = Camera.main.ScreenToWorldPoint(mousePos);
                VisualizeLine(true);
            }
            else
            {
                VisualizeLine(false);
            }

            if (Input.GetMouseButtonUp(0) && _isDragging)
            {
                Cut();
                _isDragging = false;
            }
        }

        private void Cut()
        {
            Plane plane = new Plane(_from, _to, Camera.main.transform.position);

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (!root.activeInHierarchy)
                    continue;

                var targets = root.GetComponentsInChildren<MeshTarget>();
                foreach (var target in targets)
                {
                    if (target == null)
                        continue;

                    // NEW: only cut targets that belong to a FlowerStemRuntime
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

        void OnCreated(Info info, MeshCreationData cData)
        {
            MeshCreation.TranslateCreatedObjects(
                info,
                cData.CreatedObjects,
                cData.CreatedTargets,
                Separation);

            // inform the flower stem runtime
            var stem = info.MeshTarget.GetComponentInParent<FlowerStemRuntime>();
            if (stem != null)
            {
                stem.ApplyCutFromPlane(
                    _to,
                    (Camera.main.transform.position - _to).normalized);

                var session = stem.GetComponentInParent<FlowerSessionController>();
                session?.CheckStemCutImmediate();

                // rebind after cut
                var rebinder = stem.GetComponentInParent<FlowerJointRebinder>();
                rebinder?.RebindAllPartsToClosestStemPiece();
            }
        }

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
    }
}
