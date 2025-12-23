/**
 * @file MotionJitter.cs
 * @brief MotionJitter script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup ui
 */

using UnityEngine;
/**
 * @class MotionJitter
 * @brief MotionJitter component.
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

public class MotionJitter : MonoBehaviour
{
    public enum InputMode
    {
        ObjectPosition, // Jitter depends on where the object is in the world
        MousePosition   // Jitter depends on where the mouse cursor is
    }

    [Header("Control")]
    public InputMode inputMode = InputMode.ObjectPosition;

    [Header("Noise Settings")]
    [Tooltip("How 'zoomed in' the noise map is. \nLow (0.1) = Smooth wobbles. \nHigh (50) = Chaotic static.")]
    public float noiseFrequency = 10f;

    [Header("Jitter Strength")]
    public float positionStrength = 0.1f;
    public float rotationStrength = 5.0f; // Degrees
    public float scaleStrength = 0.1f;

    // Internal
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private Vector3 initialScale;

    // Offsets to make sure X, Y, and Rotation don't look identical
    private float seedX, seedY, seedRot, seedScale;

    void Start()
    {
        // Cache rest pose
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;
        initialScale = transform.localScale;

        // Generate random seeds once so every object looks slightly different
        // (But consistent within its own life)
        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(0f, 100f);
        seedRot = Random.Range(0f, 100f);
        seedScale = Random.Range(0f, 100f);
    }

    void Update()
    {
        // 1. Get the Driver Coordinate (The "Input")
        Vector2 coord = Vector2.zero;

        if (inputMode == InputMode.ObjectPosition)
        {
            // Use World Position
            coord = new Vector2(transform.position.x, transform.position.y);
        }
        else
        {
            // Use Mouse Position (Normalized 0-1 to be resolution independent)
            coord = new Vector2(
                Input.mousePosition.x / Screen.width,
                Input.mousePosition.y / Screen.height
            );

            // Multiply by aspect ratio to keep noise square
            coord.x *= (float)Screen.width / Screen.height;
        }

        // Apply Frequency
        coord *= noiseFrequency;

        // 2. Calculate Deterministic Noise
        // We subtract 0.5 to make the range -0.5 to 0.5 (centering the distortion)

        // Position Noise
        float noiseX = (Mathf.PerlinNoise(coord.x + seedX, coord.y + seedX) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(coord.x + seedY, coord.y + seedY) - 0.5f) * 2f;

        // Rotation Noise
        float noiseRot = (Mathf.PerlinNoise(coord.x + seedRot, coord.y + seedRot) - 0.5f) * 2f;

        // Scale Noise
        float noiseScaleVal = (Mathf.PerlinNoise(coord.x + seedScale, coord.y + seedScale) - 0.5f) * 2f;

        // 3. Apply to Transform

        // Apply Position
        Vector3 posOffset = new Vector3(noiseX * positionStrength, noiseY * positionStrength, 0f);
        transform.localPosition = initialLocalPos + posOffset;

        // Apply Rotation
        float rotOffset = noiseRot * rotationStrength;
        transform.localRotation = initialLocalRot * Quaternion.Euler(0, 0, rotOffset);

        // Apply Scale
        float scaleMod = 1f + (noiseScaleVal * scaleStrength);
        transform.localScale = new Vector3(
            initialScale.x * scaleMod,
            initialScale.y * scaleMod,
            initialScale.z
        );
    }
}