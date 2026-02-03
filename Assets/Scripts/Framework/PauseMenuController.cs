/**
 * @file PauseMenuController.cs
 * @brief PauseMenuController script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup ui
 */

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

[DisallowMultipleComponent]
/**
 * @class PauseMenuController
 * @brief PauseMenuController component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup ui
 */
public class PauseMenuController : MonoBehaviour
{
    [System.Serializable]
    /**
     * @class PausePage
     * @brief PausePage component.
     * @details
     * Responsibilities:
     * - (Documented) See fields and methods below.
     *
     * Unity lifecycle:
     * - Awake(): cache references / validate setup.
     * - OnEnable()/OnDisable(): hook/unhook events.
     * - Update(): per-frame behavior (if any).
     *
     * Gotchas:
     * - Keep hot paths allocation-free (Update/cuts/spawns).
     * - Prefer event-driven UI updates over per-frame string building.
     *
     * @ingroup ui
     */
    public class PausePage
    {
        [Tooltip("Internal ID for this page (e.g. 'Records', 'Items', 'System').")]
        public string pageId;

        [Tooltip("Display name shown in the pause menu header.")]
        public string displayName;

        [Tooltip("Root GameObject for this page's UI.")]
        public GameObject root;
    }

    [Header("Input (New Input System)")]
    [Tooltip("Action that toggles the pause menu (e.g. Start / Esc).")]
    public InputActionReference pauseAction;

    [Tooltip("Action that moves to previous page (e.g. L shoulder).")]
    public InputActionReference pageLeftAction;

    [Tooltip("Action that moves to next page (e.g. R shoulder).")]
    public InputActionReference pageRightAction;

    [Header("Root UI")]
    [Tooltip("Root object of the entire pause menu (usually a Canvas or panel).")]
    public GameObject pauseRoot;

    [Tooltip("Label showing the current page's display name.")]
    public TMP_Text pageTitleLabel;

    [Tooltip("Optional: label showing 'Page 1 / 3' style info.")]
    public TMP_Text pageIndexLabel;

    [Header("Pages (set up in Inspector)")]
    public PausePage[] pages;

    [Header("Options Subpanel")]
    [Tooltip("Optional options panel that can be shown from the System page.")]
    public GameObject optionsPanel;

    [Header("Cursor Handling")]
    [Tooltip("If true, show mouse cursor and unlock it while paused.")]
    public bool manageCursor = true;

    [Header("Debug")]
    public bool debugLogs = false;

    int _currentPageIndex = 0;
    bool _isPaused = false;

    void Start()
    {
        if (pauseRoot != null)
            pauseRoot.SetActive(false);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    void OnEnable()
    {
        // Subscribe + enable actions
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed += OnPausePerformed;
            pauseAction.action.Enable();
        }

        if (pageLeftAction != null && pageLeftAction.action != null)
        {
            pageLeftAction.action.performed += OnPageLeftPerformed;
            pageLeftAction.action.Enable();
        }

        if (pageRightAction != null && pageRightAction.action != null)
        {
            pageRightAction.action.performed += OnPageRightPerformed;
            pageRightAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed -= OnPausePerformed;
            pauseAction.action.Disable();
        }

        if (pageLeftAction != null && pageLeftAction.action != null)
        {
            pageLeftAction.action.performed -= OnPageLeftPerformed;
            pageLeftAction.action.Disable();
        }

        if (pageRightAction != null && pageRightAction.action != null)
        {
            pageRightAction.action.performed -= OnPageRightPerformed;
            pageRightAction.action.Disable();
        }
    }

    // ─────────────────── Input callbacks ───────────────────

    void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (!_isPaused) OpenPauseMenu();
        else ClosePauseMenu();
    }

    void OnPageLeftPerformed(InputAction.CallbackContext ctx)
    {
        if (!_isPaused) return;
        PreviousPage();
    }

    void OnPageRightPerformed(InputAction.CallbackContext ctx)
    {
        if (!_isPaused) return;
        NextPage();
    }

    // ─────────────────── Pause open/close ───────────────────

    public void OpenPauseMenu()
    {
        if (_isPaused)
            return;

        _isPaused = true;
        TimeScaleManager.Set(TimeScaleManager.PRIORITY_PAUSE, 0f);

        if (pauseRoot != null)
            pauseRoot.SetActive(true);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        SetPage(0); // Start on Records page

        if (manageCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        if (debugLogs)
            Debug.Log("[PauseMenuController] Pause menu opened.", this);
    }

    public void ClosePauseMenu()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        TimeScaleManager.Clear(TimeScaleManager.PRIORITY_PAUSE);

        if (pauseRoot != null)
            pauseRoot.SetActive(false);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);

        if (manageCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (debugLogs)
            Debug.Log("[PauseMenuController] Pause menu closed.", this);
    }

    // For UI button callbacks (e.g. Resume)
    public void UI_ClosePauseMenu() => ClosePauseMenu();

    // ─────────────────── Page switching ───────────────────

    public void NextPage()
    {
        if (pages == null || pages.Length == 0)
            return;

        int newIndex = (_currentPageIndex + 1) % pages.Length;
        SetPage(newIndex);
    }

    public void PreviousPage()
    {
        if (pages == null || pages.Length == 0)
            return;

        int newIndex = (_currentPageIndex - 1 + pages.Length) % pages.Length;
        SetPage(newIndex);
    }

    public void SetPage(int index)
    {
        if (pages == null || pages.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, pages.Length - 1);
        _currentPageIndex = index;

        for (int i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            if (page == null || page.root == null) continue;
            page.root.SetActive(i == _currentPageIndex);
        }

        var current = pages[_currentPageIndex];

        if (pageTitleLabel != null)
            pageTitleLabel.text = current != null ? current.displayName : "";

        if (pageIndexLabel != null && pages.Length > 0)
            pageIndexLabel.text = $"{_currentPageIndex + 1} / {pages.Length}";

        if (debugLogs && current != null)
            Debug.Log($"[PauseMenuController] Switched to page '{current.pageId}' (index {_currentPageIndex}).", this);
    }

    // UI hooks if you want arrow buttons on the header
    public void UI_NextPage() => NextPage();
    public void UI_PreviousPage() => PreviousPage();

    // ─────────────────── System page actions ───────────────────

    public void RestartLevel()
    {
        if (debugLogs)
            Debug.Log("[PauseMenuController] RestartLevel called.", this);

        TimeScaleManager.ClearAll();
        var current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    public void GoToMainMenu(string mainMenuSceneName)
    {
        if (debugLogs)
            Debug.Log("[PauseMenuController] GoToMainMenu called → " + mainMenuSceneName, this);

        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning("[PauseMenuController] GoToMainMenu called with empty scene name.", this);
            return;
        }

        TimeScaleManager.ClearAll();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ShowOptionsPanel()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(true);
    }

    public void HideOptionsPanel()
    {
        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    public void QuitGame()
    {
        if (debugLogs)
            Debug.Log("[PauseMenuController] QuitGame called.", this);

        TimeScaleManager.ClearAll();
        Application.Quit();
    }
}
