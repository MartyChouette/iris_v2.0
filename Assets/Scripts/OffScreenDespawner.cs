using UnityEngine;

/// <summary>
/// Despawns this GameObject when it goes off-screen (below a certain Y threshold).
/// Useful for falling stem pieces that should disappear when they fall off screen.
/// </summary>
public class OffScreenDespawner : MonoBehaviour
{
    [Header("Despawn Settings")]
    [Tooltip("Y position threshold below which the object will be despawned.")]
    public float despawnYThreshold = -10f;

    [Tooltip("Delay before checking if off-screen (allows object to settle).")]
    public float checkDelay = 0.5f;

    [Tooltip("How often to check if off-screen (in seconds).")]
    public float checkInterval = 0.2f;

    [Header("Debug")]
    public bool debugLogs = false;

    private float _checkTimer = 0f;
    private bool _hasStartedChecking = false;

    private void Start()
    {
        // Wait for the delay before starting checks
        _checkTimer = checkDelay;
    }

    private void Update()
    {
        if (!_hasStartedChecking)
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f)
            {
                _hasStartedChecking = true;
                _checkTimer = checkInterval;
            }
            return;
        }

        _checkTimer -= Time.deltaTime;
        if (_checkTimer <= 0f)
        {
            _checkTimer = checkInterval;
            CheckIfOffScreen();
        }
    }

    private void CheckIfOffScreen()
    {
        if (transform.position.y < despawnYThreshold)
        {
            if (debugLogs)
                Debug.Log($"[OffScreenDespawner] Despawning '{gameObject.name}' at Y={transform.position.y:F2} (threshold={despawnYThreshold})", this);

            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw a line showing the despawn threshold
        Gizmos.color = Color.red;
        Vector3 thresholdPos = new Vector3(transform.position.x, despawnYThreshold, transform.position.z);
        Gizmos.DrawLine(
            thresholdPos + Vector3.left * 5f,
            thresholdPos + Vector3.right * 5f
        );
    }
}
