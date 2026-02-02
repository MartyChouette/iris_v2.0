/**
 * @file TMP_FocusBlur.cs
 * @brief TMP_FocusBlur script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup ui
 */

using UnityEngine;
using TMPro;
/**
 * @class TMP_FocusBlur
 * @brief TMP_FocusBlur component.
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

public class TMP_FocusBlur : MonoBehaviour
{
    [Header("Morph Settings")]
    [Tooltip("How fast the vertices move around.")]
    public float morphSpeed = 3.0f;

    [Tooltip("How far the vertices travel from their original spot.")]
    public float jitterIntensity = 2.0f;

    [Tooltip("The scale of the noise. Higher = more nervous/jagged. Lower = smoother waves.")]
    public float noiseScale = 10f;

    [Header("Targeting")]
    [Tooltip("If true, all vertices move together. If false, text looks like it's shredding.")]
    public bool preserveCharacterShape = false;

    [Header("Performance")]
    [Tooltip("Minimum seconds between mesh updates. 0.033 = ~30fps visual updates.")]
    public float updateInterval = 0.033f;

    private TMP_Text textMesh;
    private float _timeSinceUpdate;

    void Start()
    {
        textMesh = GetComponent<TMP_Text>();
    }

    void Update()
    {
        // PERF: Throttle updates - mesh deformation doesn't need to run every frame
        _timeSinceUpdate += Time.deltaTime;
        if (_timeSinceUpdate < updateInterval) return;
        _timeSinceUpdate = 0f;

        // 1. Force TMP to generate the latest geometry
        // (Important so we don't drift away from the original shape over time)
        textMesh.ForceMeshUpdate();

        var textInfo = textMesh.textInfo;

        // 2. Loop through every character
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];

            // Skip invisible characters (spaces, etc.)
            if (!charInfo.isVisible) continue;

            // Get the index of the vertex array
            int vertexIndex = charInfo.vertexIndex;
            int materialIndex = charInfo.materialReferenceIndex;

            // Get the actual vertices
            Vector3[] sourceVertices = textInfo.meshInfo[materialIndex].vertices;

            // 3. Create a unique offset for this character (if preserving shape)
            Vector3 charOffset = Vector3.zero;
            if (preserveCharacterShape)
            {
                charOffset = GetPerlinOffset(Time.time, i);
            }

            // 4. Loop through the 4 vertices of the character (TopLeft, TopRight, etc.)
            for (int v = 0; v < 4; v++)
            {
                Vector3 original = sourceVertices[vertexIndex + v];
                Vector3 offset;

                if (preserveCharacterShape)
                {
                    // Move the whole letter as one unit
                    offset = charOffset;
                }
                else
                {
                    // Move every corner independently (The "Shredding/Morphing" effect)
                    // We use the vertex index + time to generate unique noise for each corner
                    offset = GetPerlinOffset(Time.time, (i * 4) + v);
                }

                // Apply the offset
                sourceVertices[vertexIndex + v] = original + offset;
            }
        }

        // 5. Upload the changes to the mesh
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textMesh.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }

    // Helper to generate smooth random noise based on time and index
    Vector3 GetPerlinOffset(float time, int uniqueID)
    {
        // We use PerlinNoise to get a value between 0 and 1, then subtract 0.5 to get -0.5 to 0.5
        float xNoise = Mathf.PerlinNoise(time * morphSpeed, uniqueID * noiseScale) - 0.5f;
        float yNoise = Mathf.PerlinNoise(uniqueID * noiseScale, time * morphSpeed) - 0.5f;

        return new Vector3(xNoise, yNoise, 0) * jitterIntensity;
    }
}