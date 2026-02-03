using UnityEngine;
using TMPro;

public class PersonalListing : MonoBehaviour
{
    public enum State { Available, BeingCircled, Circled }

    [Header("Data")]
    [Tooltip("Definition asset for this listing's date character.")]
    [SerializeField] private DatePersonalDefinition definition;

    [Header("UI References")]
    [Tooltip("TMP label displaying the character name.")]
    [SerializeField] private TMP_Text nameLabel;

    [Tooltip("TMP label displaying the ad text.")]
    [SerializeField] private TMP_Text adLabel;

    [Header("Circle Drawing")]
    [Tooltip("Transform at the center of this listing, used as the circle origin.")]
    [SerializeField] private Transform circleAnchor;

    [Tooltip("Radius of the circle drawn around this listing (world units).")]
    [SerializeField] private float circleRadius = 0.06f;

    public DatePersonalDefinition Definition => definition;
    public Transform CircleAnchor => circleAnchor;
    public float CircleRadius => circleRadius;
    public State CurrentState { get; private set; } = State.Available;

    private void Awake()
    {
        if (circleAnchor == null)
            circleAnchor = transform;

        PopulateText();
    }

    private void PopulateText()
    {
        if (definition == null) return;

        if (nameLabel != null)
            nameLabel.SetText(definition.characterName);

        if (adLabel != null)
            adLabel.SetText(definition.adText);
    }

    /// <summary>
    /// Attempt to begin circling this listing. Returns true if the listing is available.
    /// </summary>
    public bool TryBeginCircle()
    {
        if (CurrentState != State.Available) return false;
        CurrentState = State.BeingCircled;
        return true;
    }

    /// <summary>
    /// Mark the circle as complete. One-way transition — cannot be undone.
    /// </summary>
    public void CompleteCircle()
    {
        CurrentState = State.Circled;
    }

    /// <summary>
    /// Cancel an in-progress circle, returning the listing to Available.
    /// </summary>
    public void CancelCircle()
    {
        if (CurrentState == State.BeingCircled)
            CurrentState = State.Available;
    }
}
