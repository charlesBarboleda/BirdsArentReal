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

    [Header("Look Sensitivity")]
    [SerializeField] float _lookSensitivityX = 2f;
    [SerializeField] float _lookSensitivityY = 2f;
    [SerializeField] bool _invertVerticalInput;

    [Header("Pitch Limits")]
    [SerializeField] float _minPitch = -30f;
    [SerializeField] float _maxPitch = 70f;
    [SerializeField] float _startingPitch = 12f;

    [Header("Camera Orbit")]
    [SerializeField, Min(0.1f)] float _distance = 3.2f;
    [SerializeField] Vector3 _lookAtOffset = new(0f, 0.25f, 0f);
    [SerializeField] float _positionFollowSpeed = 30f;
    [SerializeField] float _rotationFollowSpeed = 25f;

    [Header("Rotation State")]
    [SerializeField] float _cameraYawOffset;
    [SerializeField] float _currentPitch;

    Vector3 _currentCameraPos;
    Quaternion _currentCameraRot;

    [Header("IInitializable")]
    public bool IsInitialized { get; private set; }

    public event Action OnInitializationStart;
    public event Action<bool> OnInitializationFinish;

    readonly NetworkVariable<Vector2> _mouseInput = new(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<bool> _rotationLockedNetwork = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsRotationLocked
    {
        get => _rotationLockedNetwork.Value;
        set
        {
            if (IsServer)
                _rotationLockedNetwork.Value = value;
        }
    }

    Camera _sceneMainCam;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Deactivate static scene camera so PlayerCamera becomes the primary view
            var mainCams = Camera.allCameras;
            foreach (var c in mainCams)
            {
                if (c != _playerCamera && c.CompareTag("MainCamera"))
                {
                    _sceneMainCam = c;
                    _sceneMainCam.gameObject.SetActive(false);
                }
            }

            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(true);
                _playerCamera.tag = "MainCamera";
            }
        }
        else if (_playerCamera != null)
        {
            _playerCamera.gameObject.SetActive(false);
        }

        _ = InitializeAsync();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && _sceneMainCam != null)
        {
            _sceneMainCam.gameObject.SetActive(true);
        }

        base.OnNetworkDespawn();
    }

    void Update()
    {
        if (!IsInitialized || !IsOwner)
            return;

        HandleLocalInput();
    }

    void LateUpdate()
    {
        if (!IsInitialized || !IsOwner)
            return;

        UpdateCameraTransform();
    }

    void HandleLocalInput()
    {
        if (_rotationLockedNetwork.Value || _inputManager == null)
        {
            _mouseInput.Value = Vector2.zero;
            return;
        }

        _mouseInput.Value = _inputManager.LookInput;

        Vector2 input = _mouseInput.Value;

        // Optional mouse look to orbit around the bird
        if (Mathf.Abs(input.x) > 0.01f)
        {
            _cameraYawOffset += input.x * _lookSensitivityX;
            _cameraYawOffset = Mathf.Clamp(_cameraYawOffset, -80f, 80f);
        }
        else
        {
            // Smoothly return camera to center behind the bird when not looking around
            _cameraYawOffset = Mathf.MoveTowards(_cameraYawOffset, 0f, 60f * Time.deltaTime);
        }

        float verticalDelta = input.y * (_invertVerticalInput ? 1f : -1f);
        _currentPitch = Mathf.Clamp(_currentPitch + verticalDelta * _lookSensitivityY, _minPitch, _maxPitch);
    }

    void UpdateCameraTransform()
    {
        if (_playerCamera == null || _parentTransform == null)
            return;

        // The camera should orbit directly behind the bird's facing direction
        float targetYaw = _parentTransform.eulerAngles.y + _cameraYawOffset;
        Quaternion orbitRotation = Quaternion.Euler(_currentPitch, targetYaw, 0f);

        float pitchRad = _currentPitch * Mathf.Deg2Rad;
        float horizontalDistance = _distance * Mathf.Cos(pitchRad);
        float verticalOffset = _distance * Mathf.Sin(pitchRad);

        Vector3 localOffset = new(0f, verticalOffset, -horizontalDistance);
        Vector3 pivotPoint = _parentTransform.position + _lookAtOffset;

        // Calculate desired world position behind the bird
        Quaternion yawOnlyRot = Quaternion.Euler(0f, targetYaw, 0f);
        Vector3 desiredPosition = pivotPoint + (yawOnlyRot * localOffset);
        Quaternion desiredRotation = Quaternion.LookRotation((pivotPoint - desiredPosition).normalized, Vector3.up);

        // Smooth follow to eliminate any physics step choppiness
        float t = 1f - Mathf.Exp(-_positionFollowSpeed * Time.deltaTime);
        _currentCameraPos = Vector3.Lerp(_playerCamera.transform.position, desiredPosition, t);

        float rotT = 1f - Mathf.Exp(-_rotationFollowSpeed * Time.deltaTime);
        _currentCameraRot = Quaternion.Slerp(_playerCamera.transform.rotation, desiredRotation, rotT);

        _playerCamera.transform.SetPositionAndRotation(_currentCameraPos, _currentCameraRot);
    }

    public async Awaitable InitializeAsync()
    {
        OnInitializationStart?.Invoke();

        if (_parentTransform == null)
            _parentTransform = transform.root;

        if (_playerCamera == null)
            _playerCamera = GetComponentInChildren<Camera>(true);

        if (_playerCamera == null)
            _playerCamera = transform.root.GetComponentInChildren<Camera>(true);

        if (_playerCamera == null)
        {
            Debug.LogError("[RotationController] No Camera found.", this);
            IsInitialized = false;
            enabled = false;
            OnInitializationFinish?.Invoke(false);
            return;
        }

        if (_inputManager == null)
            _inputManager = InputManager.Instance;

        _cameraYawOffset = 0f;
        _currentPitch = Mathf.Clamp(_startingPitch, _minPitch, _maxPitch);

        if (IsOwner)
        {
            Vector3 pivotPoint = _parentTransform.position + _lookAtOffset;
            float pitchRad = _currentPitch * Mathf.Deg2Rad;
            float horizontalDistance = _distance * Mathf.Cos(pitchRad);
            float verticalOffset = _distance * Mathf.Sin(pitchRad);
            Vector3 localOffset = new(0f, verticalOffset, -horizontalDistance);
            Quaternion yawOnlyRot = Quaternion.Euler(0f, _parentTransform.eulerAngles.y, 0f);

            _currentCameraPos = pivotPoint + (yawOnlyRot * localOffset);
            _currentCameraRot = Quaternion.LookRotation((pivotPoint - _currentCameraPos).normalized, Vector3.up);
            _playerCamera.transform.SetPositionAndRotation(_currentCameraPos, _currentCameraRot);
        }

        IsInitialized = true;
        enabled = true;

        OnInitializationFinish?.Invoke(true);
        Debug.Log($"[RotationController] Initialized. NetworkObjectId: {NetworkObjectId}, IsOwner: {IsOwner}");
    }
}