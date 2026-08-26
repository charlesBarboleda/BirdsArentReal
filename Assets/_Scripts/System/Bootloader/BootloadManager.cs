using System;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class BootloadManager : MonoBehaviour
{
    [Tooltip("Order of initialization is executed from the top element to the bottom.")]
    [SerializeField] MonoBehaviour[] _initializables;

    void Awake()
    {
        _ = BootloadAsync();
    }

    async Awaitable BootloadAsync()
    {
        try
        {
            if (_initializables == null) return;

            for (int i = 0; i < _initializables.Length; i++)
            {
                if (_initializables[i] != null)
                {
                    if (_initializables[i] is IInitializable initializable)
                    {
                        await initializable.InitializeAsync();
                    }
                    else
                    {
                        Debug.LogError($"[BootloadManager] Invalid _initializables element: {_initializables[i].name}. The object must inherit IInitializable.");
                        continue;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Debug.Log($"[BootloadManager] Initialization failed.");
            Debug.LogException(exception);
        }
    }
}
