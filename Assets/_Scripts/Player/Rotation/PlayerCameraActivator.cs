using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Activates this player's camera on spawn (deactivating whatever scene camera
/// was active) and hands it to an OrbitCameraFollow. Contains zero look/rotation
/// logic — purely ownership + activation.
/// </summary>
public class PlayerCameraActivator : NetworkBehaviour, IInitializable
{
    [Header("References")]
    [SerializeField] Camera _playerCamera;
    [SerializeField] OrbitCameraFollow _cameraFollow;
    [SerializeField] Transform _followTarget;

    [Header("IInitializable")]
    public bool IsInitialized { get; private set; }

    public event Action OnInitializationStart;
    public event Action<bool> OnInitializationFinish;

    Camera _previousMainCamera;

    public override void OnNetworkSpawn()
    {
        _ = InitializeAsync();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && _previousMainCamera != null)
            _previousMainCamera.gameObject.SetActive(true);

        base.OnNetworkDespawn();
    }

    public async Awaitable InitializeAsync()
    {
        OnInitializationStart?.Invoke();

        if (_playerCamera == null)
            _playerCamera = GetComponentInChildren<Camera>(true);

        if (_playerCamera == null)
        {
            Debug.LogError("[PlayerCameraActivator] No Camera found.", this);
            IsInitialized = false;
            enabled = false;
            OnInitializationFinish?.Invoke(false);
            return;
        }

        if (_cameraFollow == null)
            _cameraFollow = _playerCamera.GetComponent<OrbitCameraFollow>();

        if (_followTarget == null)
            _followTarget = transform.root;

        if (IsOwner)
            ActivateOwnerCamera();
        else
            _playerCamera.gameObject.SetActive(false);

        IsInitialized = true;
        enabled = true;

        OnInitializationFinish?.Invoke(true);
        Debug.Log($"[PlayerCameraActivator] Initialized. NetworkObjectId: {NetworkObjectId}, IsOwner: {IsOwner}");
    }

    void ActivateOwnerCamera()
    {
        foreach (var cam in Camera.allCameras)
        {
            if (cam != _playerCamera && cam.CompareTag("MainCamera"))
            {
                _previousMainCamera = cam;
                _previousMainCamera.gameObject.SetActive(false);
            }
        }

        _playerCamera.gameObject.SetActive(true);
        _playerCamera.tag = "MainCamera";

        if (_cameraFollow != null)
        {
            _cameraFollow.Target = _followTarget;
            _cameraFollow.SnapToTarget();
        }
    }
}