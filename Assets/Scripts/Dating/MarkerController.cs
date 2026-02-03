using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class MarkerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The visual object representing the Sharpie marker tip.")]
    [SerializeField] private Transform markerVisual;

    [Tooltip("LineRenderer used to draw the circle.")]
    [SerializeField] private LineRenderer circleLine;

    [Tooltip("Camera used for raycasting. Auto-finds MainCamera if null.")]
    [SerializeField] private Camera cam;

    [Header("Raycast")]
    [Tooltip("Layer mask for the newspaper surface.")]
    [SerializeField] private LayerMask newspaperLayer;

    [Tooltip("Small offset above the newspaper surface to prevent z-fighting.")]
    [SerializeField] private float surfaceOffset = 0.001f;

    [Header("Circle Drawing")]
    [Tooltip("Number of points in the circle (more = smoother).")]
    [SerializeField] private int circlePointCount = 48;

    [Tooltip("How far past 360 degrees the circle overshoots (degrees).")]
    [SerializeField] private float overshootDegrees = 30f;

    [Tooltip("Duration of the circle-drawing animation in seconds.")]
    [SerializeField] private float circleDuration = 0.6f;

    [Tooltip("Amplitude of Perlin noise radial wobble (fraction of radius).")]
    [SerializeField] private float wobbleAmplitude = 0.12f;

    [Tooltip("Frequency of Perlin noise along the circle.")]
    [SerializeField] private float wobbleFrequency = 3f;

    [Tooltip("Ellipse axis variation (fraction). 0 = perfect circle.")]
    [SerializeField] private float ellipseVariation = 0.08f;

    private InputAction _mousePosition;
    private InputAction _mouseClick;

    private bool _isEnabled = true;
    private bool _isDrawing;
    private Coroutine _drawCoroutine;
    private PersonalListing _activeListing;
    private bool _isOnSurface;

    // Pre-computed circle points (populated per draw)
    private Vector3[] _circlePoints;

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        // Inline InputAction fallback (same pattern as SimpleTestCharacter)
        _mousePosition = new InputAction("MousePos", InputActionType.Value, "<Mouse>/position");
        _mouseClick = new InputAction("MouseClick", InputActionType.Button, "<Mouse>/leftButton");

        if (circleLine != null)
            circleLine.positionCount = 0;

        if (markerVisual != null)
            markerVisual.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        _mousePosition.Enable();
        _mouseClick.Enable();
    }

    private void OnDisable()
    {
        _mousePosition.Disable();
        _mouseClick.Disable();
    }

    private void Update()
    {
        if (!_isEnabled) return;

        UpdateMarkerPosition();

        if (_isDrawing)
        {
            // If mouse released during drawing, cancel
            if (_mouseClick.WasReleasedThisFrame())
                CancelDraw();
        }
        else
        {
            if (_mouseClick.WasPressedThisFrame() && _isOnSurface)
                TryStartCircle();
        }
    }

    private void UpdateMarkerPosition()
    {
        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, newspaperLayer))
        {
            _isOnSurface = true;
            Vector3 pos = hit.point + hit.normal * surfaceOffset;

            if (markerVisual != null)
            {
                markerVisual.position = pos;
                if (!markerVisual.gameObject.activeSelf)
                    markerVisual.gameObject.SetActive(true);
            }
        }
        else
        {
            _isOnSurface = false;
            if (markerVisual != null && markerVisual.gameObject.activeSelf)
                markerVisual.gameObject.SetActive(false);
        }
    }

    private void TryStartCircle()
    {
        Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, newspaperLayer))
            return;

        // Check if we hit a listing collider
        var listing = hit.collider.GetComponent<PersonalListing>();
        if (listing == null)
            listing = hit.collider.GetComponentInParent<PersonalListing>();

        if (listing == null || !listing.TryBeginCircle())
            return;

        _activeListing = listing;
        _drawCoroutine = StartCoroutine(DrawCircleCoroutine(listing));
    }

    private IEnumerator DrawCircleCoroutine(PersonalListing listing)
    {
        _isDrawing = true;

        Vector3 center = listing.CircleAnchor.position;
        float radius = listing.CircleRadius;

        // Pre-compute wobbled ellipse points
        float totalAngle = 360f + overshootDegrees;
        int totalPoints = circlePointCount + Mathf.CeilToInt(circlePointCount * (overshootDegrees / 360f));
        _circlePoints = new Vector3[totalPoints];

        // Random seed per circle for variety
        float noiseSeed = Random.Range(0f, 1000f);

        // Slight ellipse variation
        float axisA = radius * (1f + Random.Range(-ellipseVariation, ellipseVariation));
        float axisB = radius * (1f + Random.Range(-ellipseVariation, ellipseVariation));

        // Slight random rotation for the ellipse
        float ellipseRotation = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < totalPoints; i++)
        {
            float t = (float)i / (totalPoints - 1);
            float angle = t * totalAngle * Mathf.Deg2Rad;

            // Base ellipse point
            float localX = axisA * Mathf.Cos(angle);
            float localZ = axisB * Mathf.Sin(angle);

            // Rotate the ellipse
            float rotX = localX * Mathf.Cos(ellipseRotation) - localZ * Mathf.Sin(ellipseRotation);
            float rotZ = localX * Mathf.Sin(ellipseRotation) + localZ * Mathf.Cos(ellipseRotation);

            // Perlin wobble on the radius
            float noise = Mathf.PerlinNoise(noiseSeed + t * wobbleFrequency, noiseSeed * 0.7f);
            float wobble = 1f + (noise - 0.5f) * 2f * wobbleAmplitude;

            _circlePoints[i] = center + new Vector3(rotX * wobble, surfaceOffset, rotZ * wobble);
        }

        // Animate: reveal points over time with ease-in-out
        circleLine.positionCount = 0;

        float elapsed = 0f;
        while (elapsed < circleDuration)
        {
            elapsed += Time.deltaTime;
            float raw = Mathf.Clamp01(elapsed / circleDuration);

            // Ease-in-out (smoothstep)
            float eased = raw * raw * (3f - 2f * raw);

            int pointsToShow = Mathf.Max(1, Mathf.CeilToInt(eased * totalPoints));
            circleLine.positionCount = pointsToShow;

            for (int i = 0; i < pointsToShow; i++)
                circleLine.SetPosition(i, _circlePoints[i]);

            yield return null;
        }

        // Ensure all points are shown
        circleLine.positionCount = totalPoints;
        for (int i = 0; i < totalPoints; i++)
            circleLine.SetPosition(i, _circlePoints[i]);

        // Circle complete
        _isDrawing = false;
        listing.CompleteCircle();
        _activeListing = null;

        // Notify manager
        if (NewspaperManager.Instance != null)
            NewspaperManager.Instance.OnListingSelected(listing);
    }

    private void CancelDraw()
    {
        if (_drawCoroutine != null)
        {
            StopCoroutine(_drawCoroutine);
            _drawCoroutine = null;
        }

        _isDrawing = false;

        if (circleLine != null)
            circleLine.positionCount = 0;

        if (_activeListing != null)
        {
            _activeListing.CancelCircle();
            _activeListing = null;
        }
    }

    /// <summary>
    /// Enable or disable marker interaction. Called by NewspaperManager during state transitions.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;

        if (!enabled)
        {
            if (_isDrawing) CancelDraw();

            if (markerVisual != null)
                markerVisual.gameObject.SetActive(false);
        }
    }
}
