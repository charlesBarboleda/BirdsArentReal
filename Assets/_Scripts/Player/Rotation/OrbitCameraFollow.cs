using UnityEngine;
using InputSystem;

/// <summary>
/// Third-person orbit/chase camera with player-controlled look-around.
/// Reads look input for itself only — never writes to Target or any gameplay
/// state, so it cannot affect the thing it's following.
/// </summary>
public class OrbitCameraFollow : MonoBehaviour
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

    float _yawOffset;
    float _pitch;

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
}