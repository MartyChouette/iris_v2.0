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

    // Internal Pooling Logic
    private List<ObiEmitter> emitterPool;
    private HashSet<ObiEmitter> activeEmitters;

    private void Awake()
    {
        Instance = this;
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

        if (poolRoot == null)
        {
            var solver = GameObject.FindFirstObjectByType<Obi.ObiSolver>();
            if (solver != null) poolRoot = solver.transform;
            else poolRoot = this.transform;
        }

        for (int i = 0; i < poolSize; i++)
        {
            ObiEmitter newEmitter = Instantiate(emitterPrefab, poolRoot);

            // IMPORTANT: Force speed to 0 so it doesn't leak on spawn
            newEmitter.speed = 0f;
            newEmitter.gameObject.name = $"SapEmitter_{i}";

            emitterPool.Add(newEmitter);
        }
    }

    private ObiEmitter GetFreeEmitter()
    {
        foreach (var emitter in emitterPool)
        {
            if (!activeEmitters.Contains(emitter)) return emitter;
        }
        return null;
    }

    // ───────────────────────── Public API ─────────────────────────

    public void EmitStemCut(Vector3 planePoint, Vector3 planeNormal)
    {
        if (sapIntensity <= 0f) return;

        var dir = planeNormal.normalized;
        Vector3 topPos = planePoint + dir * stemEndOffset;
        Vector3 bottomPos = planePoint - dir * stemEndOffset;

        ObiEmitter topEmitter = GetFreeEmitter();
        if (topEmitter != null) activeEmitters.Add(topEmitter);

        ObiEmitter bottomEmitter = GetFreeEmitter();
        if (bottomEmitter != null) activeEmitters.Add(bottomEmitter);

        if (topEmitter != null) StartCoroutine(Burst(topEmitter, topPos, dir, stemTopBurst));
        if (bottomEmitter != null) StartCoroutine(Burst(bottomEmitter, bottomPos, -dir, stemBottomBurst));
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

    // ───────────────────────── BURST LOGIC ─────────────────────────
    // This is the part you were missing!

    private IEnumerator Burst(ObiEmitter emitter, Vector3 worldPos, Vector3 mainDir, SapBurstProfile profile)
    {
        if (emitter == null) yield break;

        // 1. Kill old particles to prevent teleporting artifacts
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

        activeEmitters.Remove(emitter);
    }
}