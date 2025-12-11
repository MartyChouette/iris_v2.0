using UnityEngine;
using TMPro; // Required for TextMeshPro
using UnityEngine.EventSystems; // Required for Mouse Detection

public class TextDissolveButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Settings")]
    public float animationSpeed = 5f;
    
    [Header("Blur Settings")]
    [Range(0f, 1f)] public float normalSoftness = 0f;
    [Range(0f, 1f)] public float blurredSoftness = 1f; // 1 is fully blurry

    [Header("Dissolve Settings")]
    [Range(-1f, 1f)] public float normalDilate = 0f;
    [Range(-1f, 1f)] public float dissolvedDilate = -1f; // -1 makes text invisible (eroded)

    private TextMeshProUGUI textMesh;
    private Material textMat;
    
    // Target values
    private float targetSoftness;
    private float targetDilate;
    private float currentSoftness;
    private float currentDilate;

    // Shader Property IDs (Optimization)
    private int softnessID;
    private int faceDilateID;

    void Start()
    {
        textMesh = GetComponentInChildren<TextMeshProUGUI>();

        if (textMesh != null)
        {
            // Create a material instance so we don't effect ALL text in the game
            textMat = new Material(textMesh.fontMaterial); 
            textMesh.fontMaterial = textMat;

            // Cache Shader IDs
            softnessID = Shader.PropertyToID("_OutlineSoftness");
            faceDilateID = Shader.PropertyToID("_FaceDilate");

            // Set Initial State
            currentSoftness = normalSoftness;
            currentDilate = normalDilate;
            SetTargets(normalSoftness, normalDilate);
        }
    }

    void Update()
    {
        if (textMat == null) return;

        // Smoothly interpolate current values to target values
        currentSoftness = Mathf.Lerp(currentSoftness, targetSoftness, Time.deltaTime * animationSpeed);
        currentDilate = Mathf.Lerp(currentDilate, targetDilate, Time.deltaTime * animationSpeed);

        // Apply to the material
        textMat.SetFloat(softnessID, currentSoftness);
        textMat.SetFloat(faceDilateID, currentDilate);
        
        // Update the mesh to reflect changes
        textMesh.SetAllDirty();
    }

    // Triggered when Mouse Enters
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetTargets(blurredSoftness, dissolvedDilate);
    }

    // Triggered when Mouse Exits
    public void OnPointerExit(PointerEventData eventData)
    {
        SetTargets(normalSoftness, normalDilate);
    }

    private void SetTargets(float soft, float dilate)
    {
        targetSoftness = soft;
        targetDilate = dilate;
    }
}