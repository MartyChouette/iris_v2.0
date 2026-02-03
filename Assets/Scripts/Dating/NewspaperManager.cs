using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class NewspaperManager : MonoBehaviour
{
    public enum State { Browsing, Calling, Waiting, DateArrived }

    public static NewspaperManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("The marker controller to enable/disable during state transitions.")]
    [SerializeField] private MarkerController markerController;

    [Header("Calling Phase")]
    [Tooltip("Duration of the 'Calling...' overlay in seconds.")]
    [SerializeField] private float callingDuration = 2f;

    [Tooltip("SFX played when calling starts (optional).")]
    [SerializeField] private AudioClip phoneRingSFX;

    [Header("UI Panels")]
    [Tooltip("Root GameObject for the 'Calling [Name]...' UI.")]
    [SerializeField] private GameObject callingUI;

    [Tooltip("TMP_Text for the calling message.")]
    [SerializeField] private TMP_Text callingText;

    [Tooltip("Root GameObject for the countdown timer UI.")]
    [SerializeField] private GameObject timerUI;

    [Tooltip("TMP_Text for the countdown display.")]
    [SerializeField] private TMP_Text timerText;

    [Tooltip("Root GameObject for the '[Name] has arrived!' UI.")]
    [SerializeField] private GameObject arrivedUI;

    [Tooltip("TMP_Text for the arrived message.")]
    [SerializeField] private TMP_Text arrivedText;

    [Header("Events")]
    public UnityEvent OnDateArrived;

    public State CurrentState { get; private set; } = State.Browsing;

    private DatePersonalDefinition _selectedDefinition;
    private float _timeRemaining;

    private void Awake()
    {
        // Scene-scoped singleton (same pattern as HorrorCameraManager)
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NewspaperManager] Duplicate instance destroyed.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        HideAllUI();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (CurrentState != State.Waiting) return;

        _timeRemaining -= Time.deltaTime;

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            UpdateTimerDisplay();
            EnterDateArrived();
        }
        else
        {
            UpdateTimerDisplay();
        }
    }

    /// <summary>
    /// Called by MarkerController when a listing's circle animation completes.
    /// </summary>
    public void OnListingSelected(PersonalListing listing)
    {
        if (CurrentState != State.Browsing) return;
        if (listing == null || listing.Definition == null) return;

        _selectedDefinition = listing.Definition;
        StartCoroutine(CallingSequence());
    }

    private IEnumerator CallingSequence()
    {
        // ── Enter Calling state ──
        CurrentState = State.Calling;

        if (markerController != null)
            markerController.SetEnabled(false);

        // Play phone ring SFX
        if (phoneRingSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(phoneRingSFX);

        // Show calling UI
        if (callingUI != null) callingUI.SetActive(true);
        if (callingText != null)
            callingText.text = $"Calling {_selectedDefinition.characterName}...";

        yield return new WaitForSeconds(callingDuration);

        // ── Transition to Waiting state ──
        if (callingUI != null) callingUI.SetActive(false);
        EnterWaiting();
    }

    private void EnterWaiting()
    {
        CurrentState = State.Waiting;
        _timeRemaining = _selectedDefinition.arrivalTimeSec;

        if (timerUI != null) timerUI.SetActive(true);
        UpdateTimerDisplay();
    }

    private void EnterDateArrived()
    {
        CurrentState = State.DateArrived;

        if (timerUI != null) timerUI.SetActive(false);

        if (arrivedUI != null) arrivedUI.SetActive(true);
        if (arrivedText != null)
            arrivedText.text = $"{_selectedDefinition.characterName} has arrived!";

        OnDateArrived?.Invoke();
    }

    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(_timeRemaining);
        int min = seconds / 60;
        int sec = seconds % 60;
        // Use SetText with format args to avoid string allocation
        timerText.SetText("{0}:{1:00}", min, sec);
    }

    private void HideAllUI()
    {
        if (callingUI != null) callingUI.SetActive(false);
        if (timerUI != null) timerUI.SetActive(false);
        if (arrivedUI != null) arrivedUI.SetActive(false);
    }
}
