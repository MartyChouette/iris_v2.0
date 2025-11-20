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

        [Header("Debug")]
        public bool debugLogs = true;

        protected override void Update()
        {
            base.Update();

            // Start drag (right mouse)
            if (Input.GetMouseButtonDown(1))
            {
                _isDragging = true;

                var mousePos = new Vector3(
                    Input.mousePosition.x,
                    Input.mousePosition.y,
                    Camera.main.nearClipPlane + 0.05f
                );
                _from = Camera.main.ScreenToWorldPoint(mousePos);
            }

            // While dragging, update line
            if (_isDragging)
            {
                var mousePos = new Vector3(
                    Input.mousePosition.x,
                    Input.mousePosition.y,
                    Camera.main.nearClipPlane + 0.05f
                );
                _to = Camera.main.ScreenToWorldPoint(mousePos);
                VisualizeLine(true);
            }
            else
            {
                VisualizeLine(false);
            }

            // Finish drag (left mouse up, same as your original)
            if (Input.GetMouseButtonUp(0) && _isDragging)
            {
                Cut();
                _isDragging = false;
            }
        }

        private void Cut()
        {
            if (Camera.main == null)
            {
                if (debugLogs) Debug.LogError("[MouseBehaviour] No main camera found.", this);
                return;
            }

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
                    {
                        if (debugLogs)
                            Debug.LogWarning("[MouseBehaviour] Found null MeshTarget, skipping.", this);
                        continue;
                    }

                    // Make sure there is a valid mesh to cut
                    var mf = target.GetComponent<MeshFilter>();
                    var smr = target.GetComponent<SkinnedMeshRenderer>();
                    var mr = target.GetComponent<MeshRenderer>();

                    bool hasMesh =
                        (mf != null && mf.sharedMesh != null) ||
                        (smr != null && smr.sharedMesh != null);

                    if (!hasMesh || mr == null && smr == null)
                    {
                        if (debugLogs)
                            Debug.LogWarning($"[MouseBehaviour] MeshTarget '{target.name}' has no valid mesh/renderer, skipping.", target);
                        continue;
                    }

                    try
                    {
                        // Call into CutterBehaviour.Cut ONLY when target looks valid
                        Cut(target, _to, plane.normal, null, OnCreated);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[MouseBehaviour] Exception while cutting '{target.name}': {e}", target);
                    }
                }
            }
        }

        void OnCreated(Info info, MeshCreationData cData)
        {
            MeshCreation.TranslateCreatedObjects(info, cData.CreatedObjects, cData.CreatedTargets, Separation);
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
