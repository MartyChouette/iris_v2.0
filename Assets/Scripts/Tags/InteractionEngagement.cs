// File: InteractionEngagement.cs
using UnityEngine;

[DisallowMultipleComponent]
public class InteractionEngagement : MonoBehaviour
{
    [Tooltip("True while the player is actively grabbing / interacting with this object.")]
    public bool isEngaged = false;

    [Range(0f, 1f)]
    [Tooltip("How much intensity this object should feel when NOT directly engaged.")]
    public float passiveIntensity = 0.25f;

    public float GetIntensity()
    {
        return isEngaged ? 1f : passiveIntensity;
    }
}