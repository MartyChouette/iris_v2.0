/**
 * @file SapDecalSpawner.cs
 * @brief Spawns decals when sap particles collide with surfaces.
 *
 * @details
 * Attached to ParticleSystem objects. Uses OnParticleCollision to detect
 * when particles hit surfaces and spawns pooled decals at impact points.
 *
 * Features:
 * - Efficient decal pooling
 * - Configurable spawn chance (not every particle needs a decal)
 * - Size variation based on particle velocity
 * - Normal-aligned decals that conform to surfaces
 *
 * @ingroup fluids
 */

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class SapDecalSpawner : MonoBehaviour
{
    [Header("Decal Settings")]
    [Tooltip("Prefab for sap decal. If null, uses SapDecalPool.Instance.")]
    public GameObject decalPrefab;

    [Tooltip("Chance (0-1) that a collision spawns a decal. Lower = better perf.")]
    [Range(0f, 1f)]
    public float spawnChance = 0.3f;

    [Tooltip("Minimum decal size")]
    public float minSize = 0.05f;

    [Tooltip("Maximum decal size")]
    public float maxSize = 0.15f;

    [Tooltip("Size scales with particle velocity")]
    public bool scaleWithVelocity = true;

    [Tooltip("Velocity at which decal reaches max size")]
    public float maxVelocityForSize = 15f;

    [Header("Placement")]
    [Tooltip("Offset decal slightly from surface to prevent z-fighting")]
    public float surfaceOffset = 0.001f;

    [Tooltip("Random rotation around surface normal")]
    public bool randomRotation = true;

    private ParticleSystem _particleSystem;
    private List<ParticleCollisionEvent> _collisionEvents;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        _collisionEvents = new List<ParticleCollisionEvent>(16);
    }

    private void OnParticleCollision(GameObject other)
    {
        if (SapDecalPool.Instance == null) return;

        int eventCount = _particleSystem.GetCollisionEvents(other, _collisionEvents);

        for (int i = 0; i < eventCount; i++)
        {
            // Random chance check - skip most collisions for performance
            if (Random.value > spawnChance) continue;

            var collision = _collisionEvents[i];
            Vector3 pos = collision.intersection;
            Vector3 normal = collision.normal;
            float velocity = collision.velocity.magnitude;

            // Calculate size based on velocity
            float size = minSize;
            if (scaleWithVelocity && maxVelocityForSize > 0f)
            {
                float t = Mathf.Clamp01(velocity / maxVelocityForSize);
                size = Mathf.Lerp(minSize, maxSize, t);
            }
            else
            {
                size = Random.Range(minSize, maxSize);
            }

            // Spawn decal
            SpawnDecal(pos, normal, size);
        }
    }

    private void SpawnDecal(Vector3 position, Vector3 normal, float size)
    {
        SapDecal decal = SapDecalPool.Instance.Get();
        if (decal == null) return;

        // Position with slight offset to prevent z-fighting
        decal.transform.position = position + normal * surfaceOffset;

        // Align to surface normal
        if (normal != Vector3.zero)
        {
            decal.transform.rotation = Quaternion.LookRotation(-normal);

            // Random rotation around the normal
            if (randomRotation)
            {
                decal.transform.Rotate(0f, 0f, Random.Range(0f, 360f), Space.Self);
            }
        }

        // Apply size
        decal.transform.localScale = Vector3.one * size;

        // Activate the decal
        decal.Activate();
    }
}
