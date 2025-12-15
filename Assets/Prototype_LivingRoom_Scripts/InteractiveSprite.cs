using UnityEngine;

public class InteractiveSprite : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite defaultSprite;
    public Sprite hoverSprite;

    [Header("Position Offset")]
    public Vector2 hoverOffset = new Vector2(0.2f, 0.2f);

    [Header("Scene Destination")]
    public string targetScene = ""; // Optional scene for 2D objects

    private SpriteRenderer spriteRenderer;
    private Vector3 initialLocalPosition;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        initialLocalPosition = transform.localPosition;
        if (spriteRenderer != null && defaultSprite != null)
        {
            spriteRenderer.sprite = defaultSprite;
        }
    }

    public void OnHoverEnter()
    {
        if (spriteRenderer != null && hoverSprite != null)
        {
            spriteRenderer.sprite = hoverSprite;
            Vector3 targetPos = initialLocalPosition + new Vector3(hoverOffset.x, hoverOffset.y, 0);
            transform.localPosition = targetPos;
        }
    }

    public void OnHoverExit()
    {
        if (spriteRenderer != null && defaultSprite != null)
        {
            spriteRenderer.sprite = defaultSprite;
            transform.localPosition = initialLocalPosition;
        }
    }

    public string GetTargetSceneName()
    {
        return targetScene;
    }
}