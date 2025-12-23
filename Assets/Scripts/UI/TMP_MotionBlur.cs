/**
 * @file TMP_MotionBlur.cs
 * @brief TMP_MotionBlur script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup ui
 */

using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
/**
 * @class TMP_MotionBlur
 * @brief TMP_MotionBlur component.
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
public class TMP_MotionBlur : MonoBehaviour
{
    [Header("Settings")]
    public float blurSensitivity = 0.5f; // How easily it blurs
    public float maxBlur = 0.8f;
    public float recoverySpeed = 5f; // How fast it snaps back to sharp

    private Material fontMat;
    private int softnessID;
    private Vector3 lastMousePos;
    private float currentBlur = 0f;

    void Start()
    {
        fontMat = GetComponent<TextMeshProUGUI>().fontMaterial;
        softnessID = Shader.PropertyToID("_UnderlaySoftness");
        lastMousePos = Input.mousePosition;
    }

    void Update()
    {
        // 1. Calculate how fast the mouse is moving
        float mouseSpeed = Vector3.Distance(Input.mousePosition, lastMousePos);

        // 2. Normalize speed slightly (adjust divisor to tune sensitivity)
        float targetBlur = Mathf.Clamp01(mouseSpeed * blurSensitivity * 0.1f);

        // 3. Smoothly interpolate towards the target blur
        // (We blur instantly, but recover slowly)
        if (targetBlur > currentBlur)
        {
            currentBlur = Mathf.Lerp(currentBlur, targetBlur, Time.deltaTime * 10f);
        }
        else
        {
            currentBlur = Mathf.Lerp(currentBlur, 0f, Time.deltaTime * recoverySpeed);
        }

        // 4. Clamp to max limit
        float finalBlur = Mathf.Min(currentBlur, maxBlur);

        // 5. Apply
        fontMat.SetFloat(softnessID, finalBlur);

        lastMousePos = Input.mousePosition;
    }

    void OnDestroy()
    {
        if (fontMat != null) Destroy(fontMat);
    }
}