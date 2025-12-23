/**
 * @file BacklightPulse.cs
 * @brief BacklightPulse script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup tools
 */

using UnityEngine;
/**
 * @class BacklightPulse
 * @brief BacklightPulse component.
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
 * @ingroup tools
 */

public class BacklightPulse : MonoBehaviour
{
    [Header("Glow Settings")]
    [ColorUsage(true, true)] // Allows HDR (High Intensity) color picking
    public Color dimColor;

    [ColorUsage(true, true)]
    public Color brightColor;

    public float pulseSpeed = 2.0f;

    private Material targetMaterial;
    private int emissionColorID;

    void Start()
    {
        // 1. Get the material currently attached to the SpriteRenderer
        // usage of .material (vs .sharedMaterial) automatically creates a unique instance
        // so this specific sprite pulses without affecting others.
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            targetMaterial = rend.material;

            // 2. Enable Emission keyword (Required for code to control emission on URP/Lit)
            targetMaterial.EnableKeyword("_EMISSION");

            // Cache the shader property ID for performance
            emissionColorID = Shader.PropertyToID("_EmissionColor");
        }
    }

    void Update()
    {
        if (targetMaterial == null) return;

        // 3. Create the sine wave (0 to 1)
        float wave = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;

        // 4. Blend the colors
        Color finalColor = Color.Lerp(dimColor, brightColor, wave);

        // 5. Apply to the Emission Channel
        targetMaterial.SetColor(emissionColorID, finalColor);
    }
}