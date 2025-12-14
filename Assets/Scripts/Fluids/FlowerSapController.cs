using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class FlowerSapController : MonoBehaviour
{
    // --- SINGLETON ---
    public static FlowerSapController Instance;

    [System.Serializable]
    public class SapBurstProfile
    {
        public float speed = 10f;
        public float duration = 0.2f;
        public float angleJitter = 5f;
    }

    [Header("Pooling Settings")]
    public ObiEmitter emitterPrefab;
    public int poolSize = 20;
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

    // --- OPTIMIZED POOLING ---
    // A queue gives us the next available emitter instantly (O(1))
    private Queue<ObiEmitter> availableEmitters;

    // A linked list lets us track active emitters in order of age, 
    // so we can steal the oldest one if we run out (O(1) removal).
    private LinkedList<ObiEmitter> activeEmitters;

    // Dictionary to map emitter -> coroutine so we can stop specific ones
    private Dictionary<ObiEmitter, Coroutine> runningCoroutines;

    private void Awake()
    {
        Instance = this;
        InitializePool();
    }

    private void InitializePool()
    {
        availableEmitters = new Queue<ObiEmitter>(poolSize);
        activeEmitters = new LinkedList<ObiEmitter>();
        runningCoroutines = new Dictionary<ObiEmitter, Coroutine>(poolSize);

        if (emitterPrefab == null)
        {
            Debug.LogError("FlowerSapController: No Emitter Prefab assigned!");
            return;
        }

        // Auto-find solver if poolRoot isn't set
        if (poolRoot == null)
        {
            var solver = Object.FindFirstObjectByType<Obi.ObiSolver>();
            if (solver != null) poolRoot = solver.transform;
            else poolRoot = this.transform;
        }

        for (int i = 0; i < poolSize; i++)
        {
            ObiEmitter newEmitter = Instantiate(emitterPrefab, poolRoot);

            // IMPORTANT: Force speed to 0 so it doesn't leak on spawn
            newEmitter.speed = 0f;
            newEmitter.gameObject.name = $"SapEmitter_{i}";

            // Ensure it's active in hierarchy so it's "ready to fire"
            newEmitter.gameObject.SetActive(true);

            availableEmitters.Enqueue(newEmitter);
        }
    }

    /// <summary>
    /// optimized retrieval: O(1) pop from queue, or O(1) steal from linked list.
    /// </summary>
    private ObiEmitter GetReadyEmitter()
    {
        ObiEmitter candidate = null;

        // Case A: We have fresh emitters in the pool
        if (availableEmitters.Count > 0)
        {
            candidate = availableEmitters.Dequeue();
        }
        // Case B: Pool is empty! We must recycle the oldest active burst (Balancing Usage)
        else if (activeEmitters.Count > 0)
        {
            // Steal the oldest one (first in linked list)
            candidate = activeEmitters.First.Value;
            activeEmitters.RemoveFirst();

            // Force stop its current routine
            if (runningCoroutines.TryGetValue(candidate, out Coroutine running))
            {
                StopCoroutine(running);
                runningCoroutines.Remove(candidate);
            }
        }

        return candidate;
    }

    // ───────────────────────── Public API ─────────────────────────

    public void EmitStemCut(Vector3 planePoint, Vector3 planeNormal)
    {
        if (sapIntensity <= 0f) return;

        var dir = planeNormal.normalized;
        Vector3 topPos = planePoint + dir * stemEndOffset;
        Vector3 bottomPos = planePoint - dir * stemEndOffset;

        FireEmitter(topPos, dir, stemTopBurst);
        FireEmitter(bottomPos, -dir, stemBottomBurst);
    }

    public void EmitLeafTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;
        FireEmitter(pos, normal.normalized, leafTearBurst);
    }

    public void EmitPetalTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;
        FireEmitter(pos, normal.normalized, petalTearBurst);
    }

    // ───────────────────────── FIRE LOGIC ─────────────────────────

    private void FireEmitter(Vector3 pos, Vector3 dir, SapBurstProfile profile)
    {
        ObiEmitter emitter = GetReadyEmitter();
        if (emitter == null) return; // Should allow pool expansion if this happens often, or increase poolSize

        // Track as active (add to end of list -> newest)
        activeEmitters.AddLast(emitter);

        // Start routine and store reference
        Coroutine c = StartCoroutine(BurstRoutine(emitter, pos, dir, profile));
        runningCoroutines[emitter] = c;
    }

    private IEnumerator BurstRoutine(ObiEmitter emitter, Vector3 worldPos, Vector3 mainDir, SapBurstProfile profile)
    {
        // 1. Kill old particles to prevent teleporting artifacts (Essential for recycling)
        if (emitter.activeParticleCount > 0)
        {
            emitter.KillAll();
        }

        Transform t = emitter.transform;
        t.position = worldPos;

        // 2. Jitter
        Vector3 dir = mainDir.sqrMagnitude > 0.0001f ? mainDir.normalized : Vector3.up;
        if (profile.angleJitter > 0f)
        {
            Quaternion jitter = Quaternion.AngleAxis(Random.Range(-profile.angleJitter, profile.angleJitter), Random.onUnitSphere);
            dir = jitter * dir;
        }
        t.rotation = Quaternion.LookRotation(dir);

        // 3. Speed
        float targetSpeed = profile.speed * sapIntensity;
        if (maxEffectiveSpeed > 0f) targetSpeed = Mathf.Min(targetSpeed, maxEffectiveSpeed);

        // 4. FIRE (Stream Mode ON)
        emitter.speed = targetSpeed;

        yield return new WaitForSeconds(profile.duration);

        // 5. STOP (Stream Mode OFF)
        emitter.speed = 0f;

        // 6. Return to pool
        ReturnToPool(emitter);
    }

    private void ReturnToPool(ObiEmitter emitter)
    {
        // Remove from tracking
        if (activeEmitters.Contains(emitter)) activeEmitters.Remove(emitter);
        if (runningCoroutines.ContainsKey(emitter)) runningCoroutines.Remove(emitter);

        // Reset state
        emitter.speed = 0f;

        // Push back to available queue
        availableEmitters.Enqueue(emitter);
    }
}