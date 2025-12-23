/**
 * @file TextDissolveButton.cs
 * @brief TextDissolveButton script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup ui
 */

using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
/**
 * @class TextDissolveButton
 * @brief TextDissolveButton component.
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

public class TextDissolveButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Animation Settings")]
    public float animationSpeed = 5f;

    [Header("Blur / Softness")]
    [Range(0f, 1f)] public float normalSoftness = 0f;
    [Range(0f, 1f)] public float blurredSoftness = 1f;

    [Header("Dissolve / Dilate")]
    [Range(-1f, 1f)] public float normalDilate = 0f;
    [Range(-1f, 1f)] public float dissolvedDilate = -0.5f; // Don't go to -1, stick to -0.5 for better blur

    [Header("Scale (The Blur Booster)")]
    public float normalScale = 1.0f;
    public float blurredScale = 1.2f; // Expanding makes the blur look bigger

    [Header("Alpha Fade")]
    [Range(0f, 1f)] public float normalAlpha = 1f;
    [Range(0f, 1f)] public float blurredAlpha = 0f; // Fade out to hide hard edges

    private TextMeshProUGUI textMesh;
    private Material textMat;
    private Transform textTransform;

    // Current & Target Values
    private float targetSoftness, currentSoftness;
    private float targetDilate, currentDilate;
    private float targetScale, currentScale;
    private float targetAlpha, currentAlpha;

    // Shader IDs
    private int softnessID;
    private int faceDilateID;

    void Start()
    {
        textMesh = GetComponentInChildren<TextMeshProUGUI>();

        if (textMesh != null)
        {
            textTransform = textMesh.transform;

            // Create material instance
            textMat = new Material(textMesh.fontMaterial);
            textMesh.fontMaterial = textMat;

            // Cache IDs
            softnessID = Shader.PropertyToID("_OutlineSoftness");
            faceDilateID = Shader.PropertyToID("_FaceDilate");

            // Initialize Defaults
            currentSoftness = normalSoftness;
            currentDilate = normalDilate;
            currentScale = normalScale;
            currentAlpha = normalAlpha;

            SetTargets(normalSoftness, normalDilate, normalScale, normalAlpha);
        }
    }

    void Update()
    {
        if (textMat == null) return;

        float dt = Time.deltaTime * animationSpeed;

        // 1. Interpolate Values
        currentSoftness = Mathf.Lerp(currentSoftness, targetSoftness, dt);
        currentDilate = Mathf.Lerp(currentDilate, targetDilate, dt);
        currentScale = Mathf.Lerp(currentScale, targetScale, dt);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, dt);

        // 2. Apply Shader Properties
        textMat.SetFloat(softnessID, currentSoftness);
        textMat.SetFloat(faceDilateID, currentDilate);

        // 3. Apply Transform Scale (Creates the "Super Blur" illusion)
        textTransform.localScale = Vector3.one * currentScale;

        // 4. Apply Alpha (Hides artifacts)
        textMesh.alpha = currentAlpha;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Go to Blurry/Invisible State
        SetTargets(blurredSoftness, dissolvedDilate, blurredScale, blurredAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Return to Normal State
        SetTargets(normalSoftness, normalDilate, normalScale, normalAlpha);
    }

    private void SetTargets(float soft, float dilate, float scale, float alpha)
    {
        targetSoftness = soft;
        targetDilate = dilate;
        targetScale = scale;
        targetAlpha = alpha;
    }
}