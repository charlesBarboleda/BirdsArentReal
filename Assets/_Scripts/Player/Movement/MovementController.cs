using System;
using InputSystem;
using Unity.Netcode;
using UnityEngine;

public class MovementController : NetworkBehaviour, IInitializable
{
    [Header("References")]
    [SerializeField] InputManager _inputManager;
    [SerializeField] Rigidbody _rigidbody;
    [SerializeField] BirdModelController _birdModelController;

    [Header("Flight Speeds")]
    [SerializeField] float _minSpeed = 2f;
    [SerializeField] float _cruiseSpeed = 7f;
    [SerializeField] float _maxSpeed = 14f;
    [SerializeField] float _acceleration = 8f;
    [SerializeField] float _deceleration = 10f;

    [Header("Turn & Vertical Speeds")]
    [SerializeField] float _turnSpeed = 90f;
    [SerializeField] float _ascendSpeed = 6f;
    [SerializeField] float _descendSpeed = 6f;
    [SerializeField] float _verticalDamping = 6f;

    [Header("Flight State")]
    [SerializeField] bool _movementLocked;
    [SerializeField] float _currentSpeed = 7f;
    [SerializeField] float _currentVerticalSpeed;

    [Header("IInitializable")]
    public bool IsInitialized { get; private set; }

    public event Action OnInitializationStart;
    public event Action<bool> OnInitializationFinish;

    readonly NetworkVariable<Vector2> _moveInput = new(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<float> _verticalInput = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<bool> _movementLockedNetwork = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsMovementLocked
    {
        get => _movementLockedNetwork.Value;
        set
        {
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
        if (!IsInitialized)
            return;

        if (IsOwner)
        {
            HandleLocalInput();
        }

        // Update visuals & animation on all instances (owner and remotes)
        UpdateVisuals();
    }

    void FixedUpdate()
    {
        if (!IsInitialized || !IsOwner)
            return;

        HandleOwnerFlight();
    }

    void HandleLocalInput()
    {
        if (_movementLockedNetwork.Value || _inputManager == null)
        {
            _moveInput.Value = Vector2.zero;
            _verticalInput.Value = 0f;
            return;
        }

        _moveInput.Value = _inputManager.MoveInput;

        float vert = 0f;
        if (_inputManager.AscendPressed) vert += 1f;
        if (_inputManager.DescendPressed) vert -= 1f;
        _verticalInput.Value = vert;
    }

    void HandleOwnerFlight()
    {
        if (_movementLockedNetwork.Value)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            return;
        }

        Vector2 input = _moveInput.Value;
        float vertInput = _verticalInput.Value;

        // 1. Calculate Target Forward Speed based on W / S input
        // W (input.y > 0): accelerate to maxSpeed
        // S (input.y < 0): slow down to minSpeed
        // neutral (input.y == 0): cruise at cruiseSpeed
        float targetSpeed;
        if (input.y > 0.1f)
            targetSpeed = Mathf.Lerp(_cruiseSpeed, _maxSpeed, input.y);
        else if (input.y < -0.1f)
            targetSpeed = Mathf.Lerp(_cruiseSpeed, _minSpeed, -input.y);
        else
            targetSpeed = _cruiseSpeed;

        float speedRate = (targetSpeed > _currentSpeed) ? _acceleration : _deceleration;
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, speedRate * Time.fixedDeltaTime);

        // 2. Turning (A = -1, D = +1)
        float turnAmount = input.x;
        if (Mathf.Abs(turnAmount) > 0.01f)
        {
            float yawDelta = turnAmount * _turnSpeed * Time.fixedDeltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, yawDelta, 0f);
            _rigidbody.MoveRotation(_rigidbody.rotation * turnRotation);
        }

        // 3. Vertical Speed (Space = ascend, Ctrl = descend)
        float targetVertSpeed = 0f;
        if (vertInput > 0.1f)
            targetVertSpeed = _ascendSpeed * vertInput;
        else if (vertInput < -0.1f)
            targetVertSpeed = _descendSpeed * vertInput;

        _currentVerticalSpeed = Mathf.MoveTowards(_currentVerticalSpeed, targetVertSpeed, _verticalDamping * Time.fixedDeltaTime);

        // 4. Apply Linear Velocity to Rigidbody
        Vector3 forwardVelocity = _rigidbody.transform.forward * _currentSpeed;
        Vector3 verticalVelocity = Vector3.up * _currentVerticalSpeed;
        _rigidbody.linearVelocity = forwardVelocity + verticalVelocity;
    }

    void UpdateVisuals()
    {
        if (_birdModelController == null)
        {
            _birdModelController = transform.root.GetComponentInChildren<BirdModelController>();
            if (_birdModelController == null) return;
        }

        Vector2 input = _moveInput.Value;
        float vertInput = _verticalInput.Value;
        _birdModelController.UpdateFlightVisuals(input.x, input.y, vertInput, isFlying: true);
    }

    public async Awaitable InitializeAsync()
    {
        OnInitializationStart?.Invoke();

        if (_rigidbody == null && !TryGetComponent(out _rigidbody))
        {
            _rigidbody = GetComponentInParent<Rigidbody>();
        }

        if (_rigidbody == null)
        {
            Debug.LogError("[MovementController] No Rigidbody found.", this);
            IsInitialized = false;
            enabled = false;
            OnInitializationFinish?.Invoke(false);
            return;
        }

        // Configure Rigidbody for flight
        _rigidbody.useGravity = false;
        _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (_birdModelController == null)
        {
            _birdModelController = GetComponentInChildren<BirdModelController>();
            if (_birdModelController == null)
                _birdModelController = transform.root.GetComponentInChildren<BirdModelController>();
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

        _currentSpeed = _cruiseSpeed;
        _currentVerticalSpeed = 0f;

        IsInitialized = true;
        enabled = true;

        OnInitializationFinish?.Invoke(true);
        Debug.Log($"[MovementController] Bird Flight Initialized. NetworkObjectId: {NetworkObjectId}, IsOwner: {IsOwner}");
    }
}