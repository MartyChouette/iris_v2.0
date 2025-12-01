using UnityEngine;
using UnityEngine.UI;

public class IdealFlowerHUDIcon : MonoBehaviour
{
    [Header("References")]
    [Tooltip("HUD Image that will display the ideal flower picture (place this in the top corner).")]
    public Image targetImage;

    [Tooltip("Optional: reference to the current flower session. If left null, it will auto-find one in the scene.")]
    public FlowerSessionController session;

    [Header("Behaviour")]
    [Tooltip("Hide the Image GameObject if no ideal sprite is found.")]
    public bool hideIfNoSprite = true;

    [Tooltip("Update every frame (if the flower type can change at runtime). Otherwise, just set once on Start.")]
    public bool updateContinuously = false;

    private void Awake()
    {
        if (session == null)
        {
            session = FindObjectOfType<FlowerSessionController>();
        }

        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void Start()
    {
        ApplyIdealSprite();
    }

    private void Update()
    {
        if (updateContinuously)
        {
            ApplyIdealSprite();
        }
    }

    private void ApplyIdealSprite()
    {
        if (targetImage == null)
            return;

        Sprite spriteToUse = null;

        if (session != null && session.FlowerType != null)
        {
            spriteToUse = session.FlowerType.idealFlowerSprite;
        }

        targetImage.sprite = spriteToUse;

        if (hideIfNoSprite)
        {
            targetImage.gameObject.SetActive(spriteToUse != null);
        }
    }
}