using System;
using InputSystem;
using Unity.Netcode;
using UnityEngine;

public class MovementController : NetworkBehaviour, IInitializable
{
    [Header("References")]
    [SerializeField] InputManager _inputManager;
    [SerializeField] Rigidbody _rigidbody;
    [SerializeField] Transform _groundCheck;
    [SerializeField] CapsuleManModelController _modelController;

    [Header("Movement")]
    [SerializeField] float _moveSpeed = 5f;
    [SerializeField] float _jumpForce = 6f;

    [Header("Ground Check")]
    [SerializeField] float _groundCheckRadius = 0.25f;
    [SerializeField] LayerMask _groundLayers = ~0;

    [Header("Movement State")]
    [SerializeField] bool _movementLocked;

    [Header("IInitializable")]
    public bool IsInitialized { get; private set; }

    public event Action OnInitializationStart;
    public event Action<bool> OnInitializationFinish;

    // Networked purely so remote copies (animation, VFX, etc.) can react to
    // input - the position/rotation itself is replicated by NetworkRigidbody
    // + ClientNetworkTransform on the prefab, not by this variable.
    readonly NetworkVariable<Vector2> _moveInput = new(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<bool> _movementLockedNetwork = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    bool _jumpRequested;
    [SerializeField] bool _isGrounded;

    public bool IsMovementLocked
    {
        get => _movementLockedNetwork.Value;
        set
        {
            // Only the server should authoritatively change this.
            if (IsServer)
                _movementLockedNetwork.Value = value;
        }
    }

    public override void OnNetworkSpawn()
    {
        _ = InitializeAsync();
    }

    void Update()
    {
        if (!IsInitialized || !IsOwner)
            return;

        HandleLocalInput();
    }

    void FixedUpdate()
    {
        if (!IsInitialized || !IsOwner)
            return;

        // Rigidbody/NetworkRigidbody is paired with a ClientNetworkTransform,
        // i.e. authority sits with the owner - so only the owner simulates
        // physics here. Non-owner copies are kept kinematic by NetworkRigidbody
        // itself and just play back the synced transform; this script never
        // touches their Rigidbody.
        HandleOwnerMovement();
    }

    void HandleLocalInput()
    {
        if (_movementLockedNetwork.Value)
        {
            _moveInput.Value = Vector2.zero;
            return;
        }

        _moveInput.Value = _inputManager.MoveInput;

        // JumpPerformed is handled through an event; consumed in FixedUpdate.
    }

    void HandleJumpPerformed()
    {
        if (!IsOwner)
            return;

        if (_movementLockedNetwork.Value)
            return;

        _jumpRequested = true;
    }

    void HandleOwnerMovement()
    {
        UpdateGroundedState();

        if (_movementLockedNetwork.Value)
        {
            _jumpRequested = false;
            return;
        }

        Vector2 input = _moveInput.Value;
        Vector3 desiredVelocity = GetDesiredVelocity(input);

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = desiredVelocity.x;
        velocity.z = desiredVelocity.z;
        _rigidbody.linearVelocity = velocity;

        if (_jumpRequested && _isGrounded)
            _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.VelocityChange);

        _jumpRequested = false;
    }

    // Builds world-space velocity from local input (x = strafe, y = forward/
    // back) relative to this transform's current facing, rather than raw
    // world axes - so W always moves the character toward wherever it's
    // currently facing. Forward/right are flattened to the horizontal plane
    // before use so movement stays level even if the Rigidbody's rotation
    // ever picks up pitch/roll (e.g. from a collision), rather than assuming
    // a guaranteed yaw-only transform.
    Vector3 GetDesiredVelocity(Vector2 input)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDirection = (right * input.x) + (forward * input.y);
        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        return moveDirection * _moveSpeed;
    }

    void UpdateGroundedState()
    {
        Vector3 origin = _groundCheck ? _groundCheck.position : transform.position;
        _isGrounded = Physics.CheckSphere(origin, _groundCheckRadius, _groundLayers, QueryTriggerInteraction.Ignore);
    }

    public async Awaitable InitializeAsync()
    {
        OnInitializationStart?.Invoke();

        if (_rigidbody == null && !TryGetComponent(out _rigidbody))
        {
            Debug.LogError("[MovementController] No Rigidbody found.", this);

            IsInitialized = false;
            enabled = false;

            OnInitializationFinish?.Invoke(false);
            return;

        }

        if (_inputManager == null)
            _inputManager = InputManager.Instance;

        if (_inputManager == null)
        {
            Debug.LogError("[MovementController] No InputManager found or assigned to _inputManager.", this);

            IsInitialized = false;
            enabled = false;

            OnInitializationFinish?.Invoke(false);
            return;
        }

        // Only the owning player should subscribe to local input.
        if (IsOwner)
        {
            _inputManager.JumpPerformed += HandleJumpPerformed;
        }

        IsInitialized = true;
        enabled = true;

        OnInitializationFinish?.Invoke(true);

        Debug.Log($"[MovementController] Initialized. " + $"NetworkObjectId: {NetworkObjectId}, " + $"IsOwner: {IsOwner}");
    }

    public override void OnNetworkDespawn()
    {
        if (_inputManager != null && IsOwner)
            _inputManager.JumpPerformed -= HandleJumpPerformed;

        base.OnNetworkDespawn();
    }
}