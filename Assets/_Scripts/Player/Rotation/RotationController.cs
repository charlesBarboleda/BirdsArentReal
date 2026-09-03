using System;
using InputSystem;
using Unity.Netcode;
using UnityEngine;

public class RotationController : NetworkBehaviour, IInitializable
{
    [Header("References")]
    [SerializeField] InputManager _inputManager;
    [SerializeField] Transform _parentTransform;
    [SerializeField] Camera _playerCamera;

    [Header("Yaw")]
    [SerializeField] float _yawSensitivity = 3f;

    [Header("Pitch")]
    [SerializeField] float _pitchSensitivity = 3f;
    [SerializeField] bool _invertVerticalInput;
    [SerializeField] float _minPitch = -20f;
    [SerializeField] float _maxPitch = 80f;
    [SerializeField] float _startingPitch = 20f;

    [Header("Orbit")]
    [SerializeField, Min(0.1f)] float _distance = 4f;
    [SerializeField] Vector3 _lookAtOffset = new(0f, 1.6f, 0f);

    [Header("Rotation State")]
    [SerializeField] float _yaw;
    [SerializeField] float _pitch;

    [Header("IInitializable")]
    public bool IsInitialized { get; private set; }

    public event Action OnInitializationStart;
    public event Action<bool> OnInitializationFinish;

    // Networked purely so remote copies (head-look IK, turn animations, etc.)
    // can react to look input - actual yaw/pitch application below is local
    // per-client. Yaw is replicated to observers via a NetworkTransform on
    // the _parentTransform's prefab (same pattern as the body Rigidbody in
    // MovementController); pitch never needs to leave this client since
    // non-owners never see their own copy of playerCamera (disabled below).
    readonly NetworkVariable<Vector2> _mouseInput = new(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    // Server controls this. Clients can read it.
    readonly NetworkVariable<bool> _rotationLockedNetwork = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsRotationLocked
    {
        get => _rotationLockedNetwork.Value;
        set
        {
            // Only the server should authoritatively change this.
            if (IsServer)
                _rotationLockedNetwork.Value = value;
        }
    }

    public override void OnNetworkSpawn()
    {
        // Only the local owner should ever render from their own camera -
        // remote copies would otherwise fight for the audio listener/output.
        if (!IsOwner)
        {
            _playerCamera.gameObject.SetActive(false);
        }

        _ = InitializeAsync();
    }

    void Update()
    {
        if (!IsInitialized || !IsOwner)
            return;

        HandleLocalInput();
    }

    // Camera work happens in LateUpdate so it reads a final, settled
    // _parentTransform position for this frame (movement/animation have
    // already run in Update/FixedUpdate), avoiding one-frame camera jitter.
    void LateUpdate()
    {
        if (!IsInitialized || !IsOwner)
            return;

        HandleOwnerRotation();
    }

    void HandleLocalInput()
    {
        if (_rotationLockedNetwork.Value)
        {
            _mouseInput.Value = Vector2.zero;
            return;
        }

        _mouseInput.Value = _inputManager.LookInput;
    }

    void HandleOwnerRotation()
    {
        if (_rotationLockedNetwork.Value)
            return;

        Vector2 input = _mouseInput.Value;

        // Horizontal look only ever touches _parentTransform's Y euler - X
        // and Z are rebuilt as 0 every time, so Y is the only value that
        // can change on that transform.
        _yaw = Mathf.Repeat(_yaw + input.x * _yawSensitivity, 360f);
        _parentTransform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        // Mouse down -> camera arcs up and in; mouse up -> camera arcs down
        // toward the ground and in. Flip _invertVerticalInput if your
        // InputManager's LookInput.y convention comes in reversed.
        float verticalDelta = input.y * (_invertVerticalInput ? 1f : -1f);
        _pitch = Mathf.Clamp(_pitch + verticalDelta * _pitchSensitivity, _minPitch, _maxPitch);

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        // Orbits playerCamera around a fixed-radius vertical arc centered on
        // the pivot. Height and horizontal offset are the sin/cos components
        // of that same radius, so pitching toward either limit simultaneously
        // raises/lowers the camera AND pulls it closer to the pivot - the
        // "up and in" / "down and in" behavior, with no separate distance
        // tuning needed.
        float pitchRad = _pitch * Mathf.Deg2Rad;
        float horizontalDistance = _distance * Mathf.Cos(pitchRad);
        float verticalOffset = _distance * Mathf.Sin(pitchRad);

        Vector3 localOffset = new(0f, verticalOffset, -horizontalDistance);
        Vector3 pivotPoint = _parentTransform.position + _lookAtOffset;
        Vector3 desiredPosition = pivotPoint + (_parentTransform.rotation * localOffset);

        _playerCamera.transform.SetPositionAndRotation(
            desiredPosition,
            Quaternion.LookRotation((pivotPoint - desiredPosition).normalized, Vector3.up));
    }

    public async Awaitable InitializeAsync()
    {
        OnInitializationStart?.Invoke();

        if (_parentTransform == null)
        {
            Debug.LogError("[RotationController] No parent transform assigned.", this);

            IsInitialized = false;
            enabled = false;

            OnInitializationFinish?.Invoke(false);
            return;
        }

        if (_playerCamera == null && !TryGetComponent(out _playerCamera))
        {
            Debug.LogError("[RotationController] No Camera found.", this);

            IsInitialized = false;
            enabled = false;

            OnInitializationFinish?.Invoke(false);
            return;
        }

        if (_inputManager == null)
            _inputManager = InputManager.Instance;

        if (_inputManager == null)
        {
            Debug.LogError("[RotationController] No InputManager found or assigned to _inputManager.", this);

            IsInitialized = false;
            enabled = false;

            OnInitializationFinish?.Invoke(false);
            return;
        }

        _yaw = _parentTransform.eulerAngles.y;
        _pitch = Mathf.Clamp(_startingPitch, _minPitch, _maxPitch);

        if (IsOwner)
        {
            _parentTransform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            UpdateCameraPosition(); // snap to the correct start position immediately, no first-frame pop
        }

        IsInitialized = true;
        enabled = true;

        OnInitializationFinish?.Invoke(true);

        Debug.Log($"[RotationController] Initialized. " + $"NetworkObjectId: {NetworkObjectId}, " + $"IsOwner: {IsOwner}");
    }
}