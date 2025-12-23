/**
 * @file FlowerSapController.cs
 * @brief FlowerSapController script.
 * @details
 * - Auto-generated Doxygen header. Expand @details with intent, invariants, and perf notes as needed.
*
 * @ingroup thirdparty
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;
/**
 * @class FlowerSapController
 * @brief FlowerSapController component.
 * @details
 * Responsibilities:
 * - (Documented) See fields and methods below.
 *
 * Unity lifecycle:
 * - Awake(): cache references / validate setup.
 * - OnEnable()/OnDisable(): hook/unhook events.
 * - Update(): per-frame behavior (if any).
 *
 * Gotchas:
 * - Keep hot paths allocation-free (Update/cuts/spawns).
 * - Prefer event-driven UI updates over per-frame string building.
 *
 * @ingroup thirdparty
 */

public class FlowerSapController : MonoBehaviour
{
    public static FlowerSapController Instance;

    [System.Serializable]
    /**
     * @class SapBurstProfile
     * @brief SapBurstProfile component.
     * @details
     * Responsibilities:
     * - (Documented) See fields and methods below.
     *
     * Unity lifecycle:
     * - Awake(): cache references / validate setup.
     * - OnEnable()/OnDisable(): hook/unhook events.
     * - Update(): per-frame behavior (if any).
     *
     * Gotchas:
     * - Keep hot paths allocation-free (Update/cuts/spawns).
     * - Prefer event-driven UI updates over per-frame string building.
     *
     * @ingroup thirdparty
     */
    public class SapBurstProfile
    {
        public float speed = 10f;
        public float duration = 0.2f;
        public float angleJitter = 5f;
    }

    [Header("Pooling Settings")]
    public ObiEmitter emitterPrefab;
    // OPTIMIZATION: Reduced from 20 to 6. This is enough for 1 cut + 4 tears.
    public int poolSize = 6;
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

        if (emitterPrefab == null) return;
        if (poolRoot == null) poolRoot = transform;

        for (int i = 0; i < poolSize; i++)
        {
            ObiEmitter newEmitter = Instantiate(emitterPrefab, poolRoot);
            newEmitter.speed = 0f;
            newEmitter.gameObject.SetActive(false); // Disable when not in use (Save CPU)
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
        return null; // Pool exhausted? Skip effect. Better than lag.
    }

    // --- UPDATED SIGNATURE: Requires Stem Runtime for accurate position ---
    public void EmitStemCut(Vector3 planePoint, Vector3 planeNormal, FlowerStemRuntime stem)
    {
        if (sapIntensity <= 0f) return;

        // FIX: Project the plane point onto the actual stem mesh line
        Vector3 exactPos = planePoint;
        if (stem != null)
        {
            exactPos = stem.GetClosestPointOnStem(planePoint);
        }

        Vector3 dir = planeNormal.normalized;

        // Fire Top (Held Piece)
        ObiEmitter top = GetFreeEmitter();
        if (top != null)
        {
            activeEmitters.Add(top);
            StartCoroutine(Burst(top, exactPos + dir * stemEndOffset, dir, stemTopBurst));
        }

        // Fire Bottom (Falling Piece)
        ObiEmitter bottom = GetFreeEmitter();
        if (bottom != null)
        {
            activeEmitters.Add(bottom);
            StartCoroutine(Burst(bottom, exactPos - dir * stemEndOffset, -dir, stemBottomBurst));
        }
    }

    // (Keep EmitLeafTear and EmitPetalTear as they were...)
    public void EmitLeafTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;
        ObiEmitter e = GetFreeEmitter();
        if (e != null) { activeEmitters.Add(e); StartCoroutine(Burst(e, pos, normal.normalized, leafTearBurst)); }
    }

    public void EmitPetalTear(Vector3 pos, Vector3 normal)
    {
        if (sapIntensity <= 0f) return;
        ObiEmitter e = GetFreeEmitter();
        if (e != null) { activeEmitters.Add(e); StartCoroutine(Burst(e, pos, normal.normalized, petalTearBurst)); }
    }

    private IEnumerator Burst(ObiEmitter emitter, Vector3 worldPos, Vector3 mainDir, SapBurstProfile profile)
    {
        emitter.gameObject.SetActive(true);
        emitter.KillAll(); // Clear old particles instantly to prevent teleporting trails

        Transform t = emitter.transform;
        t.position = worldPos;

        Vector3 dir = mainDir.sqrMagnitude > 0.0001f ? mainDir.normalized : Vector3.up;
        if (profile.angleJitter > 0f)
        {
            Quaternion jitter = Quaternion.AngleAxis(Random.Range(-profile.angleJitter, profile.angleJitter), Random.onUnitSphere);
            dir = jitter * dir;
        }
        t.rotation = Quaternion.LookRotation(dir);

        emitter.speed = profile.speed * sapIntensity;
        yield return new WaitForSeconds(profile.duration);

        emitter.speed = 0f;
        yield return new WaitForSeconds(0.5f); // Wait for trail to die

        emitter.gameObject.SetActive(false);
        activeEmitters.Remove(emitter);
    }
}