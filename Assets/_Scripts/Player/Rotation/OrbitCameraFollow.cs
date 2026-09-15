using UnityEngine;
using InputSystem;
using System.Collections;
using Unity.Netcode;

public enum CameraViewMode
{
    ThirdPerson,
    FirstPerson
}

/// <summary>
/// Third-person orbit/chase camera with player-controlled look-around.
/// Reads look input for itself only — never writes to Target or any gameplay
/// state, so it cannot affect the thing it's following.
/// </summary>
public class OrbitCameraFollow : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] InputManager _inputManager;

    [Header("Target")]
    [SerializeField] Transform _target;
    [SerializeField] Vector3 _targetOffset = new(0f, 0.25f, 0f);

    [Header("Look Sensitivity")]
    [SerializeField] float _lookSensitivityX = 2f;
    [SerializeField] float _lookSensitivityY = 2f;
    [SerializeField] bool _invertVerticalInput;

    [Header("Pitch Limits")]
    [SerializeField] float _minPitch = -30f;
    [SerializeField] float _maxPitch = 70f;
    [SerializeField] float _startingPitch = 12f;

    [Header("Yaw Orbit")]
    [SerializeField] float _maxYawOffset = 80f;
    [SerializeField] float _yawRecenterSpeed = 60f;

    [Header("Orbit")]
    [SerializeField, Min(0.1f)] float _distance = 3.2f;

    [Header("Follow Smoothing")]
    [SerializeField] float _positionFollowSpeed = 30f;
    [SerializeField] float _rotationFollowSpeed = 25f;

    [Header("First Person")]
    [Tooltip("Empty child transform placed at the bird's eye position, facing forward.")]
    [SerializeField] Transform _firstPersonAnchor;

    [Header("Crosshair")]
    [SerializeField] GameObject _crosshair;

    [Header("Field of View")]
    [SerializeField] Camera _camera;
    [SerializeField] float _thirdPersonFOV = 60f;
    [SerializeField] float _firstPersonFOV = 70f;

    [Header("Camera Transition")]
    [SerializeField] CameraTransitionController _transitionController;

    float _yawOffset;
    float _pitch;

    CameraViewMode _viewMode = CameraViewMode.ThirdPerson;
    public CameraViewMode ViewMode => _viewMode;

    public Transform Target { get => _target; set => _target = value; }

    /// <summary>Look input (e.g. mouse delta or right-stick axis). Set externally if no InputManager is assigned.</summary>
    public Vector2 LookInput { get; set; }

    void Awake()
    {
        if (_inputManager == null)
            _inputManager = InputManager.Instance;

        _pitch = Mathf.Clamp(_startingPitch, _minPitch, _maxPitch);
    }

    void Update()
    {
        Vector2 input = _inputManager != null ? _inputManager.LookInput : LookInput;

        if (Mathf.Abs(input.x) > 0.01f)
        {
            _yawOffset = Mathf.Clamp(_yawOffset + input.x * _lookSensitivityX, -_maxYawOffset, _maxYawOffset);
        }
        else
        {
            // Recenter horizontally behind the bird when the player isn't actively looking around
            // _yawOffset = Mathf.MoveTowards(_yawOffset, 0f, _yawRecenterSpeed * Time.deltaTime);
        }

        float verticalDelta = input.y * (_invertVerticalInput ? 1f : -1f);
        _pitch = Mathf.Clamp(_pitch + verticalDelta * _lookSensitivityY, _minPitch, _maxPitch);
    }

    void LateUpdate()
    {
        if (_target == null)
            return;

        var (position, rotation) = CalculateDesiredPose();

        float posT = 1f - Mathf.Exp(-_positionFollowSpeed * Time.deltaTime);
        float rotT = 1f - Mathf.Exp(-_rotationFollowSpeed * Time.deltaTime);

        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, position, posT),
            Quaternion.Slerp(transform.rotation, rotation, rotT));
    }

    public override void OnNetworkSpawn()
    {
        _crosshair = CrosshairUI.Instance.Crosshair;
        _crosshair.SetActive(false);

        InputManager.Instance.SetOrbitCameraFollow(this);
    }

    /// <summary>Snaps instantly to directly-behind the target and resets look offsets — call after (re)assigning Target.</summary>
    public void SnapToTarget()
    {
        if (_target == null)
            return;

        _yawOffset = 0f;
        _pitch = Mathf.Clamp(_startingPitch, _minPitch, _maxPitch);

        var (position, rotation) = CalculateDesiredPose();
        transform.SetPositionAndRotation(position, rotation);
    }

    (Vector3 position, Quaternion rotation) CalculateDesiredPose()
    {
        if (_viewMode == CameraViewMode.FirstPerson && _firstPersonAnchor != null)
        {
            float fpYaw = _target.eulerAngles.y + _yawOffset;
            Quaternion fpRotation = Quaternion.Euler(_pitch, fpYaw, 0f);
            return (_firstPersonAnchor.position, fpRotation);
        }

        // --- existing third-person calculation below, unchanged ---
        float targetYaw = _target.eulerAngles.y + _yawOffset;
        float pitchRad = _pitch * Mathf.Deg2Rad;
        float horizontalDistance = _distance * Mathf.Cos(pitchRad);
        float verticalOffset = _distance * Mathf.Sin(pitchRad);
        Vector3 localOffset = new(0f, verticalOffset, -horizontalDistance);

        Quaternion yawRotation = Quaternion.Euler(0f, targetYaw, 0f);
        Vector3 pivotPoint = _target.position + _targetOffset;
        Vector3 desiredPosition = pivotPoint + (yawRotation * localOffset);
        Quaternion desiredRotation = Quaternion.LookRotation((pivotPoint - desiredPosition).normalized, Vector3.up);

        return (desiredPosition, desiredRotation);
    }

    void OnEnable()
    {
        if (_inputManager == null) _inputManager = InputManager.Instance;
        if (_inputManager != null) _inputManager.ToggleCameraViewPerformed += ToggleViewMode;
    }

    void OnDisable()
    {
        if (_inputManager != null) _inputManager.ToggleCameraViewPerformed -= ToggleViewMode;
    }

    public void ToggleViewMode()
    {
        if (_transitionController == null) return;
        if (_transitionController.IsTransitioning) return;
        StartCoroutine(ToggleViewModeRoutine());
    }

    IEnumerator ToggleViewModeRoutine()
    {
        CameraViewMode newMode =
            _viewMode == CameraViewMode.ThirdPerson
                ? CameraViewMode.FirstPerson
                : CameraViewMode.ThirdPerson;

        // Hide crosshair immediately when transition begins.
        if (_crosshair != null)
        {
            _crosshair.SetActive(false);
        }

        yield return StartCoroutine(_transitionController.Expand());

        SetViewMode(newMode);

        if (SecurityCameraRendererFeature.Instance != null)
        {
            SecurityCameraRendererFeature.Instance.SetActive(
                newMode == CameraViewMode.FirstPerson);
        }

        yield return StartCoroutine(_transitionController.Retract());

        // Show crosshair after returning to first person.
        if (newMode == CameraViewMode.FirstPerson && _crosshair != null)
        {
            _crosshair.SetActive(true);
        }
    }

    public void SetViewMode(CameraViewMode mode)
    {
        if (mode == CameraViewMode.FirstPerson && _firstPersonAnchor == null)
        {
            Debug.LogWarning(
                $"[{nameof(OrbitCameraFollow)}] No first-person anchor assigned on '{name}' - staying in third-person.",
                this);

            return;
        }

        _viewMode = mode;

        bool isFirstPerson = mode == CameraViewMode.FirstPerson;

        if (_camera != null)
        {
            _camera.fieldOfView =
                isFirstPerson
                    ? _firstPersonFOV
                    : _thirdPersonFOV;
        }

        if (_crosshair != null)
        {
            _crosshair.SetActive(isFirstPerson);
        }
    }
}