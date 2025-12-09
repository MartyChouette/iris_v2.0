using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi; // Ensure Obi namespace is available

/// <summary>
/// Updated Controller: Uses a POOL of emitters to handle multiple cuts/tears simultaneously.
/// </summary>
public class FlowerSapController : MonoBehaviour
{
    // --- SINGLETON (Critical for the Responder script to find this) ---
    public static FlowerSapController Instance;

    [System.Serializable]
    public class SapBurstProfile
    {
        [Tooltip("Base emitter.speed used for this burst.")]
        public float speed = 10f;

        [Tooltip("How long the burst should last.")]
        public float duration = 0.2f;

        [Tooltip("Random cone angle (degrees) added to the main direction.")]
        public float angleJitter = 5f;
    }

    [Header("Pooling Settings")]
    [Tooltip("The ObiEmitter prefab to clone. Make sure it's set up correctly (Blueprints, etc).")]
    public ObiEmitter emitterPrefab;

    [Tooltip("How many emitters to create at start.")]
    public int poolSize = 20;

    [Tooltip("Where to parent the pooled emitters? Usually the Obi Solver.")]
    public Transform poolRoot;

    [Header("Stem Cut Settings")]
    public float stemEndOffset = 0.01f;
    public SapBurstProfile stemTopBurst = new SapBurstProfile { speed = 18f, duration = 0.25f, angleJitter = 8f };
    public SapBurstProfile stemBottomBurst = new SapBurstProfile { speed = 12f, duration = 0.20f, angleJitter = 6f };

    [Header("Leaf / Petal Tear Settings")]
    public SapBurstProfile leafTearBurst = new SapBurstProfile { speed = 8f, duration = 0.18f, angleJitter = 12f };
    public SapBurstProfile petalTearBurst = new SapBurstProfile { speed = 6f, duration = 0.15f, angleJitter = 15f };

    [Header("Global Gore / Intensity")]
    [Min(0f)] public float sapIntensity = 1f;
    public float maxEffectiveSpeed = 200f;

    // Internal Pooling Logic
    private List<ObiEmitter> emitterPool;
    private HashSet<ObiEmitter> activeEmitters; // Tracks which ones are currently spraying

    private void Awake()
    {
        Instance = this; // Set the global reference so Responders can find it
        InitializePool();
    }

    private void InitializePool()
    {
        emitterPool = new List<ObiEmitter>();
        activeEmitters = new HashSet<ObiEmitter>();

        if (emitterPrefab == null)
        {
            Debug.LogError("FlowerSapController: No Emitter Prefab assigned!");
            return;
        }

        // If the poolRoot is empty (which happens on spawned prefabs), find the Solver automatically.
        if (poolRoot == null)
        {
            // Unity 6 / 2023+ syntax
            var solver = GameObject.FindFirstObjectByType<Obi.ObiSolver>();

            // Fallback for older Unity versions if the above errors out:
            // var solver = GameObject.FindObjectOfType<Obi.ObiSolver>();

            if (solver != null)
            {
                poolRoot = solver.transform;
            }
            else
            {
                // Fallback: Stick them on the flower, but warn the user.
                poolRoot = this.transform;
                Debug.LogWarning("FlowerSapController: Could not find an Obi Solver in the scene! Fluid won't show up.");
            }
        }

        for (int i = 0; i < poolSize; i++)
        {
            ObiEmitter newEmitter = Instantiate(emitterPrefab, poolRoot);

            // Ensure it starts "off"
            newEmitter.speed = 0f;
            newEmitter.gameObject.name = $"SapEmitter_{i}";

            emitterPool.Add(newEmitter);
        }
    }

    /// <summary>
    /// Finds an emitter that isn't currently busy spraying.
    /// </summary>
    private ObiEmitter GetFreeEmitter()
    {
        foreach (var emitter in emitterPool)
        {
            if (!activeEmitters.Contains(emitter))
            {
                return emitter;
            }
        }

        Debug.LogWarning("FlowerSapController: Pool exhausted! Increase pool size.");
        return null;
    }

    // ───────────────────────── Public API ─────────────────────────

    public void EmitStemCut(Vector3 planePoint, Vector3 planeNormal)
    {
        if (sapIntensity <= 0f) return;

        var dir = planeNormal.normalized;
        Vector3 topPos = planePoint + dir * stemEndOffset;
        Vector3 bottomPos = planePoint - dir * stemEndOffset;

        // Get two separate emitters from the pool
        ObiEmitter topEmitter = GetFreeEmitter();
        // Mark as active immediately so the next GetFreeEmitter call doesn't grab it
        if (topEmitter != null) activeEmitters.Add(topEmitter);

        ObiEmitter bottomEmitter = GetFreeEmitter();
        if (bottomEmitter != null) activeEmitters.Add(bottomEmitter);

        // Fire them
        if (topEmitter != null)
            StartCoroutine(Burst(topEmitter, topPos, dir, stemTopBurst));

        if (bottomEmitter != null)
            StartCoroutine(Burst(bottomEmitter, bottomPos, -dir, stemBottomBurst));
    }

    public void EmitLeafTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;

        ObiEmitter e = GetFreeEmitter();
        if (e != null)
        {
            activeEmitters.Add(e);
            StartCoroutine(Burst(e, pos, normal.normalized, leafTearBurst));
        }
    }

    public void EmitPetalTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;

        ObiEmitter e = GetFreeEmitter();
        if (e != null)
        {
            activeEmitters.Add(e);
            StartCoroutine(Burst(e, pos, normal.normalized, petalTearBurst));
        }
    }

    // ───────────────────────── Core Burst Logic ─────────────────────────

    private IEnumerator Burst(ObiEmitter emitter, Vector3 worldPos, Vector3 mainDir, SapBurstProfile profile)
    {
        // Safety check
        if (emitter == null) yield break;

        Transform t = emitter.transform;

        // Snap to position
        t.position = worldPos;

        // Calculate Direction with Jitter
        Vector3 dir = mainDir.sqrMagnitude > 0.0001f ? mainDir.normalized : Vector3.up;
        if (profile.angleJitter > 0f)
        {
            Quaternion jitter = Quaternion.AngleAxis(Random.Range(-profile.angleJitter, profile.angleJitter), Random.onUnitSphere);
            dir = jitter * dir;
        }
        t.rotation = Quaternion.LookRotation(dir);

        // Calculate Speed
        float targetSpeed = profile.speed * sapIntensity;
        if (maxEffectiveSpeed > 0f) targetSpeed = Mathf.Min(targetSpeed, maxEffectiveSpeed);

        // Turn ON
        emitter.speed = targetSpeed;

        yield return new WaitForSeconds(profile.duration);

        // Turn OFF
        emitter.speed = 0f;

        // Return to pool (mark as free)
        activeEmitters.Remove(emitter);
    }
}