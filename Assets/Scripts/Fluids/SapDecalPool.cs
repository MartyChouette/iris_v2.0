/**
 * @file SapDecalPool.cs
 * @brief Object pool for sap decals to avoid runtime instantiation.
 *
 * @details
 * Manages a pool of SapDecal objects. Decals are reused instead of
 * instantiated/destroyed to minimize GC pressure.
 *
 * Features:
 * - Pre-warms pool on startup
 * - Auto-expands up to max limit
 * - Oldest decal recycling when pool is exhausted
 *
 * @ingroup fluids
 */

using System.Collections.Generic;
using UnityEngine;

public class SapDecalPool : MonoBehaviour
{
    public static SapDecalPool Instance { get; private set; }

    [Header("Prefab")]
    [Tooltip("Decal prefab. Should have SapDecal component.")]
    public GameObject decalPrefab;

    [Header("Pool Settings")]
    public int initialPoolSize = 50;
    public int maxPoolSize = 200;

    [Header("Auto-Create Prefab")]
    [Tooltip("If no prefab assigned, create a simple quad decal")]
    public bool autoCreatePrefab = true;
    public Material decalMaterial;
    public Sprite decalSprite;

    private List<SapDecal> _pool;
    private Queue<SapDecal> _freeQueue;
    private Queue<SapDecal> _activeQueue; // For recycling oldest when pool exhausted
    private Transform _poolRoot;

    private void Awake()
    {
        Instance = this;
        InitializePool();
    }

    private void InitializePool()
    {
        _pool = new List<SapDecal>(maxPoolSize);
        _freeQueue = new Queue<SapDecal>(maxPoolSize);
        _activeQueue = new Queue<SapDecal>(maxPoolSize);

        // Create pool root
        var rootObj = new GameObject("SapDecalPool");
        rootObj.transform.SetParent(transform);
        _poolRoot = rootObj.transform;

        // Create prefab if needed
        if (decalPrefab == null && autoCreatePrefab)
        {
            CreateDefaultPrefab();
        }

        if (decalPrefab == null)
        {
            Debug.LogError("[SapDecalPool] No decal prefab available!");
            return;
        }

        // Pre-warm pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateDecal();
        }
    }

    private void CreateDefaultPrefab()
    {
        // Create a simple quad-based decal
        var go = new GameObject("DefaultSapDecal");

        // Use sprite renderer for simplicity (works well for decals)
        var sr = go.AddComponent<SpriteRenderer>();

        if (decalSprite != null)
        {
            sr.sprite = decalSprite;
        }
        else
        {
            // Create a simple circle sprite procedurally
            // For now, use a basic setup - user should assign proper sprite
            sr.color = new Color(0.2f, 0.7f, 0.1f, 0.8f);
        }

        // Set sorting order to render on top of surfaces
        sr.sortingOrder = 100;

        // Add SapDecal component
        var decal = go.AddComponent<SapDecal>();
        decal.decalColor = new Color(0.2f, 0.7f, 0.1f, 0.8f);

        go.SetActive(false);
        decalPrefab = go;
    }

    private SapDecal CreateDecal()
    {
        var go = Instantiate(decalPrefab, _poolRoot);
        go.name = $"SapDecal_{_pool.Count}";
        go.SetActive(false);

        var decal = go.GetComponent<SapDecal>();
        if (decal == null)
        {
            decal = go.AddComponent<SapDecal>();
        }

        _pool.Add(decal);
        _freeQueue.Enqueue(decal);

        return decal;
    }

    /// <summary>
    /// Get a decal from the pool.
    /// </summary>
    public SapDecal Get()
    {
        SapDecal decal = null;

        // Try free queue first
        while (_freeQueue.Count > 0)
        {
            decal = _freeQueue.Dequeue();
            if (decal != null && !decal.gameObject.activeInHierarchy)
            {
                _activeQueue.Enqueue(decal);
                return decal;
            }
        }

        // Try to expand pool
        if (_pool.Count < maxPoolSize)
        {
            decal = CreateDecal();
            _freeQueue.Dequeue(); // Remove the one we just added
            _activeQueue.Enqueue(decal);
            return decal;
        }

        // Pool exhausted - recycle oldest active decal
        if (_activeQueue.Count > 0)
        {
            decal = _activeQueue.Dequeue();
            if (decal != null)
            {
                decal.Deactivate();
                _activeQueue.Enqueue(decal);
                return decal;
            }
        }

        Debug.LogWarning("[SapDecalPool] Pool exhausted and no decals to recycle!");
        return null;
    }

    /// <summary>
    /// Return a decal to the pool.
    /// </summary>
    public void Return(SapDecal decal)
    {
        if (decal == null) return;

        decal.gameObject.SetActive(false);
        _freeQueue.Enqueue(decal);
    }

    /// <summary>
    /// Clear all active decals (useful for scene reset).
    /// </summary>
    public void ClearAll()
    {
        foreach (var decal in _pool)
        {
            if (decal != null)
            {
                decal.gameObject.SetActive(false);
            }
        }

        _freeQueue.Clear();
        _activeQueue.Clear();

        foreach (var decal in _pool)
        {
            if (decal != null)
            {
                _freeQueue.Enqueue(decal);
            }
        }
    }
}
