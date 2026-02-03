using System.Collections;
using UnityEngine;
using TMPro;

public class BookVolume : MonoBehaviour
{
    public enum State { OnShelf, PullingOut, Reading, PuttingBack }

    [Header("Definition")]
    [Tooltip("ScriptableObject defining this book's content and appearance.")]
    [SerializeField] private BookDefinition definition;

    [Header("Pages")]
    [Tooltip("Parent GameObject containing the 3 page quads (activated during Reading).")]
    [SerializeField] private GameObject pagesRoot;

    [Tooltip("TMP_Text components for left, center, and right pages.")]
    [SerializeField] private TMP_Text[] pageLabels = new TMP_Text[3];

    public BookDefinition Definition => definition;
    public State CurrentState { get; private set; } = State.OnShelf;

    private Vector3 _shelfPosition;
    private Quaternion _shelfRotation;
    private Material _instanceMaterial;
    private Color _baseColor;
    private bool _isHovered;

    private const float HoverSlideDistance = 0.03f;
    private const float PullOutDuration = 0.25f;
    private const float PutBackDuration = 0.2f;

    private void Awake()
    {
        _shelfPosition = transform.position;
        _shelfRotation = transform.rotation;

        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _instanceMaterial = rend.material; // creates instance
            _baseColor = _instanceMaterial.color;
        }

        if (pagesRoot != null)
            pagesRoot.SetActive(false);
    }

    public void SetDefinition(BookDefinition def)
    {
        definition = def;
    }

    public void SetPagesRoot(GameObject root)
    {
        pagesRoot = root;
    }

    public void SetPageLabels(TMP_Text[] labels)
    {
        pageLabels = labels;
    }

    public void OnHoverEnter()
    {
        if (CurrentState != State.OnShelf || _isHovered) return;
        _isHovered = true;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor * 1.2f;

        // Slide toward camera (negative Z in local space = toward viewer)
        transform.position = _shelfPosition - transform.forward * HoverSlideDistance;
    }

    public void OnHoverExit()
    {
        if (!_isHovered) return;
        _isHovered = false;

        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        if (CurrentState == State.OnShelf)
            transform.position = _shelfPosition;
    }

    public void PullOut(Transform readingAnchor)
    {
        if (CurrentState != State.OnShelf) return;

        // Clear hover state
        _isHovered = false;
        if (_instanceMaterial != null)
            _instanceMaterial.color = _baseColor;

        StartCoroutine(PullOutRoutine(readingAnchor));
    }

    public void PutBack()
    {
        if (CurrentState != State.Reading) return;
        StartCoroutine(PutBackRoutine());
    }

    private IEnumerator PullOutRoutine(Transform readingAnchor)
    {
        CurrentState = State.PullingOut;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < PullOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / PullOutDuration);

            transform.position = Vector3.Lerp(startPos, readingAnchor.position, t);
            transform.rotation = Quaternion.Slerp(startRot, readingAnchor.rotation, t);

            yield return null;
        }

        transform.position = readingAnchor.position;
        transform.rotation = readingAnchor.rotation;

        // Parent to reading anchor so it follows camera
        transform.SetParent(readingAnchor, true);

        EnterReading();
    }

    private void EnterReading()
    {
        CurrentState = State.Reading;

        // Populate page text from definition
        if (definition != null && pageLabels != null)
        {
            for (int i = 0; i < pageLabels.Length && i < 3; i++)
            {
                if (pageLabels[i] == null) continue;

                if (definition.pageTexts != null && i < definition.pageTexts.Length)
                    pageLabels[i].text = definition.pageTexts[i] ?? "";
                else
                    pageLabels[i].text = "";
            }
        }

        if (pagesRoot != null)
            pagesRoot.SetActive(true);
    }

    private IEnumerator PutBackRoutine()
    {
        CurrentState = State.PuttingBack;

        if (pagesRoot != null)
            pagesRoot.SetActive(false);

        // Unparent from reading anchor
        transform.SetParent(null, true);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;

        while (elapsed < PutBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smoothstep(elapsed / PutBackDuration);

            transform.position = Vector3.Lerp(startPos, _shelfPosition, t);
            transform.rotation = Quaternion.Slerp(startRot, _shelfRotation, t);

            yield return null;
        }

        transform.position = _shelfPosition;
        transform.rotation = _shelfRotation;

        CurrentState = State.OnShelf;
    }

    private static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
