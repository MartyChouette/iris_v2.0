using UnityEngine;

[CreateAssetMenu(menuName = "Iris/Date Personal")]
public class DatePersonalDefinition : ScriptableObject
{
    // ──────────────────────────────────────────────────────────────
    // Identity
    // ──────────────────────────────────────────────────────────────
    [Header("Identity")]
    [Tooltip("Character name displayed in the personal ad and calling UI.")]
    public string characterName = "Mystery Date";

    [TextArea(2, 5)]
    [Tooltip("The personal ad text shown in the newspaper listing.")]
    public string adText = "Seeking someone who appreciates long walks and existential dread.";

    // ──────────────────────────────────────────────────────────────
    // Timing
    // ──────────────────────────────────────────────────────────────
    [Header("Timing")]
    [Tooltip("Seconds until the date arrives after being called.")]
    public float arrivalTimeSec = 30f;

    // ──────────────────────────────────────────────────────────────
    // Visuals
    // ──────────────────────────────────────────────────────────────
    [Header("Visuals")]
    [Tooltip("Portrait sprite shown in the arrived UI (optional).")]
    public Sprite portrait;

    [Tooltip("Prefab to spawn when the date arrives (optional, for future use).")]
    public GameObject characterPrefab;
}
