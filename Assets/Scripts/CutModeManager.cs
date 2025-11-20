using UnityEngine;
using UnityEngine.UI;   // for Button / Image

namespace DynamicMeshCutter
{
    public class CutModeManager : MonoBehaviour
    {
        [Header("Cutters")]
        [Tooltip("Drag the object with MouseBehaviour (line / scissor) here.")]
        public MouseBehaviour mouseCutter;

        [Tooltip("Drag the object with PlaneBehaviour here.")]
        public PlaneBehaviour planeCutter;

        [Header("Visual GameObjects")]
        [Tooltip("Optional visual for the mouse/scissor cutter (e.g. scissors model, cursor).")]
        public GameObject mouseVisual;

        [Tooltip("Optional visual for the plane cutter (e.g. plane mesh, gizmo).")]
        public GameObject planeVisual;

        [Header("UI Buttons")]
        [Tooltip("Button for scissor / mouse mode.")]
        public Button mouseButton;

        [Tooltip("Button for line / plane mode.")]
        public Button planeButton;

        [Tooltip("Button for turning cutting OFF.")]
        public Button offButton;

        [Header("Button Colors")]
        public Color inactiveColor = Color.white;
        public Color activeColor = new Color(0.9f, 0.6f, 0.2f, 1f); // warm highlight

        [Header("Optional: Keyboard Shortcuts")]
        public bool enableHotkeys = true;
        [Tooltip("Key to switch to mouse drag cutting.")]
        public KeyCode mouseModeKey = KeyCode.Alpha1;
        [Tooltip("Key to switch to plane cutting.")]
        public KeyCode planeModeKey = KeyCode.Alpha2;
        [Tooltip("Key to disable all cutting modes.")]
        public KeyCode disableAllKey = KeyCode.Alpha3;

        [Header("Debug")]
        public bool debugLogs = true;

        public enum CutMode { None, Mouse, Plane }
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

            if (Input.GetKeyDown(disableAllKey))
                DisableAll();
        }

        // ───────────────────── Public methods for UI buttons ─────────────────────

        // Hook this to your SCISSOR button OnClick
        public void SetMouseMode()
        {
            currentMode = CutMode.Mouse;
            ApplyMode(currentMode);
        }

        // Hook this to your LINE/PLANE button OnClick
        public void SetPlaneMode()
        {
            currentMode = CutMode.Plane;
            ApplyMode(currentMode);
        }

        // Hook this to your OFF button OnClick
        public void DisableAll()
        {
            currentMode = CutMode.None;
            ApplyMode(currentMode);
        }

        // For dropdowns, etc. (0=None, 1=Mouse, 2=Plane)
        public void SetModeByIndex(int index)
        {
            currentMode = (CutMode)Mathf.Clamp(index, 0, 2);
            ApplyMode(currentMode);
        }

        // ───────────────────── Core mode logic ─────────────────────

        private void ApplyMode(CutMode mode)
        {
            // Enable / disable cutter behaviour scripts
            if (mouseCutter)
                mouseCutter.enabled = (mode == CutMode.Mouse);

            if (planeCutter)
                planeCutter.enabled = (mode == CutMode.Plane);

            // Enable / disable visuals
            if (mouseVisual)
                mouseVisual.SetActive(mode == CutMode.Mouse);

            if (planeVisual)
                planeVisual.SetActive(mode == CutMode.Plane);

            // Update button colors
            UpdateButtonVisual(mouseButton, mode == CutMode.Mouse);
            UpdateButtonVisual(planeButton, mode == CutMode.Plane);
            UpdateButtonVisual(offButton, mode == CutMode.None);

            if (debugLogs)
                Debug.Log($"[CutModeManager] Mode → {mode}");
        }

        private void UpdateButtonVisual(Button button, bool isActive)
        {
            if (button == null) return;

            var img = button.GetComponent<Image>();
            if (img != null)
            {
                img.color = isActive ? activeColor : inactiveColor;
            }

            // Optional: also tweak ColorBlock if you want the hover/pressed colors
            var colors = button.colors;
            colors.normalColor = isActive ? activeColor : inactiveColor;
            colors.highlightedColor = isActive ? activeColor : inactiveColor;
            button.colors = colors;
        }
    }
}
