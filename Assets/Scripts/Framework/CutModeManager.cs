using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


namespace DynamicMeshCutter
{
    public class CutModeManager : MonoBehaviour
    {
        [Header("Cutters")]
        public MouseBehaviour mouseCutter;
        public PlaneBehaviour planeCutter;
        public AngleStagePlaneBehaviour anglePlaneCutter;

        [Header("Controllers")]
        [Tooltip("Controller that moves the standard plane cutter up/down.")]
        public CuttingPlaneController planeController;

        [Tooltip("Controller that handles the two-stage angle plane.")]
        public AngleCuttingPlaneController anglePlaneController;

        [Header("Visual GameObjects")]
        public GameObject mouseVisual;
        public GameObject planeVisual;
        public GameObject anglePlaneVisual;

        [Header("UI Buttons")]
        public Button mouseButton;
        public Button planeButton;
        public Button anglePlaneButton;
        public Button offButton;

        [Header("Button Colors")]
        public Color inactiveColor = Color.white;
        public Color activeColor = new Color(0.9f, 0.6f, 0.2f, 1f);

        [Header("Optional: Keyboard Shortcuts")]
        public bool enableHotkeys = true;
        public KeyCode mouseModeKey = KeyCode.Alpha1;
        public KeyCode planeModeKey = KeyCode.Alpha2;
        public KeyCode anglePlaneModeKey = KeyCode.Alpha3;
        public KeyCode disableAllKey = KeyCode.Alpha4;

        [Header("Debug")]
        public bool debugLogs = true;

        public enum CutMode { None, Mouse, Plane, AnglePlane }
        public CutMode currentMode = CutMode.None;

        void Start()
        {
            ApplyMode(currentMode);
        }

        void Update()
        {
            if (!enableHotkeys) return;

            if (Input.GetKeyDown(mouseModeKey))
                SetMouseMode();

            if (Input.GetKeyDown(planeModeKey))
                SetPlaneMode();

            if (Input.GetKeyDown(anglePlaneModeKey))
                SetAnglePlaneMode();

            if (Input.GetKeyDown(disableAllKey))
                DisableAll();
        }

        // ───────────────────── Public methods for UI buttons ─────────────────────

        public void SetMouseMode()
        {
            currentMode = CutMode.Mouse;
            ApplyMode(currentMode);
        }

        public void SetPlaneMode()
        {
            currentMode = CutMode.Plane;
            ApplyMode(currentMode);
        }

        public void SetAnglePlaneMode()
        {
            currentMode = CutMode.AnglePlane;
            ApplyMode(currentMode);
        }

        public void DisableAll()
        {
            currentMode = CutMode.None;
            ApplyMode(currentMode);
        }

        // 0=None, 1=Mouse, 2=Plane, 3=AnglePlane
        public void SetModeByIndex(int index)
        {
            currentMode = (CutMode)Mathf.Clamp(index, 0, 3);
            ApplyMode(currentMode);
        }

        // ───────────────────── Core mode logic ─────────────────────

        private void ApplyMode(CutMode mode)
        {
            // behaviours
            if (mouseCutter) mouseCutter.enabled = (mode == CutMode.Mouse);
            if (planeCutter) planeCutter.enabled = (mode == CutMode.Plane);
            if (anglePlaneCutter) anglePlaneCutter.enabled = (mode == CutMode.AnglePlane);

            // controllers
            if (planeController) planeController.enabled = (mode == CutMode.Plane);
            if (anglePlaneController) anglePlaneController.enabled = (mode == CutMode.AnglePlane);

            // visuals
            if (mouseVisual) mouseVisual.SetActive(mode == CutMode.Mouse);
            if (planeVisual) planeVisual.SetActive(mode == CutMode.Plane);
            if (anglePlaneVisual) anglePlaneVisual.SetActive(mode == CutMode.AnglePlane);

            // buttons
            UpdateButtonVisual(mouseButton, mode == CutMode.Mouse);
            UpdateButtonVisual(planeButton, mode == CutMode.Plane);
            UpdateButtonVisual(anglePlaneButton, mode == CutMode.AnglePlane);
            UpdateButtonVisual(offButton, mode == CutMode.None);

          

            if (debugLogs)
                Debug.Log($"[CutModeManager] Mode → {mode}");

            // After switching modes, clear any selected UI element
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void UpdateButtonVisual(Button button, bool isActive)
        {
            if (button == null) return;

            var img = button.GetComponent<Image>();
            if (img != null)
                img.color = isActive ? activeColor : inactiveColor;

            var colors = button.colors;
            colors.normalColor = isActive ? activeColor : inactiveColor;
            colors.highlightedColor = isActive ? activeColor : inactiveColor;
            button.colors = colors;
        }
    }
}
