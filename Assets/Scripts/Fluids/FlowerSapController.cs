/**
 * @file FlowerSapController.cs
 * @brief FlowerSapController script.
 * @ingroup thirdparty
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class FlowerSapController : MonoBehaviour
{
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

    // START WITH THIS MANY (Pre-loaded for performance)
    public int initialPoolSize = 25;

    // NEVER GO ABOVE THIS (Safety net to prevent crashing)
    public int maxPoolSize = 50;

    public Transform poolRoot;

    [Header("Stem Cut Settings")]
    public float stemEndOffset = 0.02f;
    public SapBurstProfile stemTopBurst = new SapBurstProfile { speed = 18f, duration = 0.25f, angleJitter = 8f };
    public SapBurstProfile stemBottomBurst = new SapBurstProfile { speed = 12f, duration = 0.20f, angleJitter = 6f };

    [Header("Leaf / Petal Tear Settings")]
    public SapBurstProfile leafTearBurst = new SapBurstProfile { speed = 8f, duration = 0.18f, angleJitter = 12f };
    public SapBurstProfile petalTearBurst = new SapBurstProfile { speed = 6f, duration = 0.15f, angleJitter = 15f };

    [Header("Global Gore / Intensity")]
    [Min(0f)] public float sapIntensity = 1f;

    private List<ObiEmitter> emitterPool;

    // PERF: Track free emitters in a queue for O(1) lookup instead of O(n) linear search
    private Queue<ObiEmitter> freeEmitterQueue;

    private void Awake()
    {
        Instance = this;
        InitializePool();
    }

    // NEW: Force the fluid engine to "warm up" during the first frame
    private IEnumerator Start()
    {
        yield return null; // Wait for Obi to initialize

        // Dry Fire: Turn everyone on for 1 frame to force Mesh compilation
        foreach (var emitter in emitterPool)
        {
            emitter.speed = 1f;
        }

        yield return new WaitForEndOfFrame(); // Wait for Mesher to panic and build meshes

        // Reset everyone
        foreach (var emitter in emitterPool)
        {
            emitter.speed = 0f;
            emitter.KillAll();
        }
    }

    private void InitializePool()
    {
        emitterPool = new List<ObiEmitter>();
        freeEmitterQueue = new Queue<ObiEmitter>(maxPoolSize);

        if (emitterPrefab == null) return;
        if (poolRoot == null) poolRoot = transform;

        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewEmitter();
        }
    }

    // Helper method to create one emitter and add it to the list
    private ObiEmitter CreateNewEmitter()
    {
        ObiEmitter newEmitter = Instantiate(emitterPrefab, poolRoot);

        // CRITICAL FIX: Always Active, never False
        newEmitter.gameObject.SetActive(true);

        // Hide in Dungeon
        newEmitter.transform.position = new Vector3(0, -5000f, 0);
        newEmitter.speed = 0f;

        newEmitter.gameObject.name = $"SapEmitter_{emitterPool.Count}";
        emitterPool.Add(newEmitter);

        // PERF: Add to free queue for O(1) retrieval
        freeEmitterQueue.Enqueue(newEmitter);

        return newEmitter;
    }

    private ObiEmitter GetFreeEmitter()
    {
        // PERF: O(1) lookup from queue instead of O(n) linear search
        // 1. Try to get from the free queue
        while (freeEmitterQueue.Count > 0)
        {
            ObiEmitter emitter = freeEmitterQueue.Dequeue();
            // Double-check it's actually free (speed is 0)
            if (emitter != null && emitter.speed < 0.01f)
                return emitter;
        }

        // 2. Pool is empty! Can we expand?
        if (emitterPool.Count < maxPoolSize)
        {
            ObiEmitter newEmitter = CreateNewEmitter();
            // Remove from queue since we're returning it immediately
            if (freeEmitterQueue.Count > 0 && freeEmitterQueue.Peek() == newEmitter)
                freeEmitterQueue.Dequeue();
            return newEmitter;
        }

        // 3. Hard limit reached - fallback to linear search
        // (This shouldn't happen often if timing is right)
        foreach (var emitter in emitterPool)
        {
            if (emitter.speed < 0.01f) return emitter;
        }

        Debug.LogWarning("Sap Pool Exhausted! Consider increasing Max Pool Size.");
        return null;
    }

    public void EmitStemCut(Vector3 planePoint, Vector3 planeNormal, FlowerStemRuntime stem)
    {
        if (sapIntensity <= 0f) return;

        Vector3 exactPos = planePoint;
        if (stem != null)
        {
            exactPos = stem.GetClosestPointOnStem(planePoint);
        }

        Vector3 dir = planeNormal.normalized;

        ObiEmitter top = GetFreeEmitter();
        if (top != null) StartCoroutine(Burst(top, exactPos + dir * stemEndOffset, dir, stemTopBurst));

        ObiEmitter bottom = GetFreeEmitter();
        if (bottom != null) StartCoroutine(Burst(bottom, exactPos - dir * stemEndOffset, -dir, stemBottomBurst));
    }

    public void EmitLeafTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;
        ObiEmitter e = GetFreeEmitter();
        if (e != null) StartCoroutine(Burst(e, pos, normal.normalized, leafTearBurst));
    }

    public void EmitPetalTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;
        ObiEmitter e = GetFreeEmitter();
        if (e != null) StartCoroutine(Burst(e, pos, normal.normalized, petalTearBurst));
    }

    private IEnumerator Burst(ObiEmitter emitter, Vector3 worldPos, Vector3 mainDir, SapBurstProfile profile)
    {
        // 1. Teleport from Dungeon to Leaf
        emitter.transform.position = worldPos;

        // 2. Rotation
        Vector3 dir = mainDir.sqrMagnitude > 0.0001f ? mainDir.normalized : Vector3.up;
        if (profile.angleJitter > 0f)
        {
            Quaternion jitter = Quaternion.AngleAxis(Random.Range(-profile.angleJitter, profile.angleJitter), Random.onUnitSphere);
            dir = jitter * dir;
        }
        emitter.transform.rotation = Quaternion.LookRotation(dir);

        // 3. Clear old particles (instant)
        emitter.KillAll();

        // 4. FIRE
        emitter.speed = profile.speed * sapIntensity;

        yield return new WaitForSeconds(profile.duration);

        // 5. Stop Firing
        emitter.speed = 0f;

        // 6. Wait for trail to fall naturally
        yield return new WaitForSeconds(0.5f);

        // 7. Send back to Dungeon (Do NOT SetActive false)
        emitter.transform.position = new Vector3(0, -5000f, 0);

        // PERF: Return emitter to free queue for O(1) retrieval next time
        freeEmitterQueue.Enqueue(emitter);
    }
}