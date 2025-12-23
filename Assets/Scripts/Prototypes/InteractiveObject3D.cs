using UnityEngine;

public class InteractiveObject3D : MonoBehaviour
{
    [Header("Visual Feedback")]
    public Color defaultColor = Color.white;
    public Color hoverColor = Color.yellow;

    [Header("Scene Destination")]
    public string targetScene = "Flower Scene";

    private Renderer meshRenderer;
    private Material defaultMaterial;
    private Vector3 initialScale;

    void Start()
    {
        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            defaultMaterial = meshRenderer.material;
            defaultMaterial.color = defaultColor;
        }
        initialScale = transform.localScale;
    }

    public void OnHoverEnter()
    {
        if (defaultMaterial != null)
        {
            defaultMaterial.color = hoverColor;
            transform.localScale = initialScale * 1.05f;
        }
    }

    public void OnHoverExit()
    {
        if (defaultMaterial != null)
        {
            defaultMaterial.color = defaultColor;
            transform.localScale = initialScale;
        }
    }

    public string GetTargetSceneName()
    {
        return targetScene;
    }
}