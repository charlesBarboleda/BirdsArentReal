using System;
using UnityEngine;

public class EventManager : MonoBehaviour, IInitializable
{
    public static EventManager Instance { get; private set; }

    [Header("IInitializable")]
    public bool IsInitialized { get; private set; }
    public event Action OnInitializationStart;
    public event Action<bool> OnInitializationFinish;

    public async Awaitable InitializeAsync()
    {
        OnInitializationStart?.Invoke();

        if (Instance != null && Instance != this)
        {
            Debug.LogError("[EventManager] An EventManager singleton instance already exists. Failed initialization.");

            IsInitialized = false;
            OnInitializationFinish?.Invoke(IsInitialized);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(this);

        IsInitialized = true;
        enabled = true;
        OnInitializationFinish?.Invoke(IsInitialized);
        Debug.Log("[EventManager] Initialized.");
    }
}
