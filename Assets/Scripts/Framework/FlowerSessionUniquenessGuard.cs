using UnityEngine;

[DisallowMultipleComponent]
public class FlowerSessionUniquenessGuard : MonoBehaviour
{
    private static int _activeCount = 0;

    private void Awake()
    {
        _activeCount++;
        if (_activeCount > 1)
        {
            Debug.LogError($"[FlowerSessionGuard] DUPLICATE session detected. count={_activeCount}  this={name}", this);
        }
        else
        {
            Debug.Log($"[FlowerSessionGuard] Session registered: {name}", this);
        }
    }

    private void OnDestroy()
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);
        Debug.Log($"[FlowerSessionGuard] Session unregistered: {name} count={_activeCount}", this);
    }
}