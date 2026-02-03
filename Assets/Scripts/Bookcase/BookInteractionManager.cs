using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class BookInteractionManager : MonoBehaviour
{
    public enum State { Browsing, Pulling, Reading, PuttingBack }

    public static BookInteractionManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Transform where the book is positioned for reading (child of camera).")]
    [SerializeField] private Transform readingAnchor;

    [Tooltip("Camera component used for screen-to-world raycasting.")]
    [SerializeField] private UnityEngine.Camera mainCamera;

    [Tooltip("BookcaseBrowseCamera for enabling/disabling look during reading.")]
    [SerializeField] private BookcaseBrowseCamera browseCamera;

    [Header("UI")]
    [Tooltip("Root panel for the title hint shown on hover.")]
    [SerializeField] private GameObject titleHintPanel;

    [Tooltip("TMP_Text for displaying the hovered book's title.")]
    [SerializeField] private TMP_Text titleHintText;

    [Header("Raycast")]
    [Tooltip("Layer mask for the Books layer.")]
    [SerializeField] private LayerMask booksLayerMask;

    [Tooltip("Maximum raycast distance from camera.")]
    [SerializeField] private float maxRayDistance = 10f;

    [Header("Audio")]
    [Tooltip("SFX played when pulling a book out (optional).")]
    [SerializeField] private AudioClip pullOutSFX;

    [Tooltip("SFX played when putting a book back (optional).")]
    [SerializeField] private AudioClip putBackSFX;

    public State CurrentState { get; private set; } = State.Browsing;

    private InputAction _mousePositionAction;
    private InputAction _clickAction;
    private InputAction _cancelAction;

    private BookVolume _hoveredBook;
    private BookVolume _activeBook;

    private void Awake()
    {
        // Scene-scoped singleton
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BookInteractionManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _mousePositionAction = new InputAction("MousePosition", InputActionType.Value,
            "<Mouse>/position");

        _clickAction = new InputAction("Click", InputActionType.Button,
            "<Mouse>/leftButton");

        _cancelAction = new InputAction("Cancel", InputActionType.Button);
        _cancelAction.AddBinding("<Keyboard>/escape");
        _cancelAction.AddBinding("<Mouse>/rightButton");

        if (titleHintPanel != null)
            titleHintPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        _mousePositionAction.Enable();
        _clickAction.Enable();
        _cancelAction.Enable();
    }

    private void OnDisable()
    {
        _mousePositionAction.Disable();
        _clickAction.Disable();
        _cancelAction.Disable();
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case State.Browsing:
                UpdateBrowsing();
                break;
            case State.Pulling:
                UpdatePulling();
                break;
            case State.Reading:
                UpdateReading();
                break;
            case State.PuttingBack:
                UpdatePuttingBack();
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Browsing — raycast for hover, click to pull out
    // ──────────────────────────────────────────────────────────────

    private void UpdateBrowsing()
    {
        if (mainCamera == null) return;

        Vector2 mousePos = _mousePositionAction.ReadValue<Vector2>();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, booksLayerMask))
        {
            var book = hit.collider.GetComponent<BookVolume>();
            if (book != null && book != _hoveredBook)
            {
                ClearHover();
                _hoveredBook = book;
                _hoveredBook.OnHoverEnter();
                ShowTitleHint(_hoveredBook);
            }
        }
        else
        {
            ClearHover();
        }

        // Click to pull out hovered book
        if (_clickAction.WasPressedThisFrame() && _hoveredBook != null)
        {
            _activeBook = _hoveredBook;
            ClearHover();
            BeginPullOut();
        }
    }

    private void ClearHover()
    {
        if (_hoveredBook != null)
        {
            _hoveredBook.OnHoverExit();
            _hoveredBook = null;
        }
        HideTitleHint();
    }

    private void ShowTitleHint(BookVolume book)
    {
        if (titleHintPanel == null || titleHintText == null) return;
        if (book.Definition == null) return;

        titleHintText.text = book.Definition.title;
        titleHintPanel.SetActive(true);
    }

    private void HideTitleHint()
    {
        if (titleHintPanel != null)
            titleHintPanel.SetActive(false);
    }

    // ──────────────────────────────────────────────────────────────
    // Pull Out
    // ──────────────────────────────────────────────────────────────

    private void BeginPullOut()
    {
        CurrentState = State.Pulling;

        if (pullOutSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(pullOutSFX);

        _activeBook.PullOut(readingAnchor);
    }

    private void UpdatePulling()
    {
        // Wait for BookVolume to finish animating
        if (_activeBook != null && _activeBook.CurrentState == BookVolume.State.Reading)
            CurrentState = State.Reading;
    }

    // ──────────────────────────────────────────────────────────────
    // Reading — click or cancel to put back
    // ──────────────────────────────────────────────────────────────

    private void UpdateReading()
    {
        if (_clickAction.WasPressedThisFrame() || _cancelAction.WasPressedThisFrame())
        {
            BeginPutBack();
        }
    }

    private void BeginPutBack()
    {
        CurrentState = State.PuttingBack;

        if (putBackSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(putBackSFX);

        _activeBook.PutBack();
    }

    private void UpdatePuttingBack()
    {
        // Wait for BookVolume to finish animating
        if (_activeBook != null && _activeBook.CurrentState == BookVolume.State.OnShelf)
        {
            _activeBook = null;
            CurrentState = State.Browsing;
        }
    }
}
