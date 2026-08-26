using UnityEngine;
using System;

public interface IInitializable
{
    bool IsInitialized { get; }

    event Action OnInitializationStart;
    event Action<bool> OnInitializationFinish;

    Awaitable InitializeAsync();
}