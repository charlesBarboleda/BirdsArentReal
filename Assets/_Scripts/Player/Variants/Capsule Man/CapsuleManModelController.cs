using System.Collections.Generic;
using UnityEngine;
using InputSystem;
using Unity.Netcode;

public class CapsuleManModelController : NetworkBehaviour
{
    [Header("Script References")]
    [SerializeField] InputManager _inputManager;

    [Header("Main Body")]
    [SerializeField] Transform _body;

    [Header("Right Arm References")]
    [SerializeField] Transform _rightShoulder;
    [SerializeField] Transform _rightShoulderAnchor;
    [SerializeField] Transform _rightForearm;
    [SerializeField] Transform _rightForearmAnchor;
    [SerializeField] Transform _rightHand;
    [SerializeField] Transform _rightHandAnchor;
    [SerializeField] List<Transform> _rightHandVariants;

    [Header("Left Arm References")]
    [SerializeField] Transform _leftShoulder;
    [SerializeField] Transform _leftShoulderAnchor;
    [SerializeField] Transform _leftForearm;
    [SerializeField] Transform _leftForearmAnchor;
    [SerializeField] Transform _leftHand;
    [SerializeField] Transform _leftHandAnchor;
    [SerializeField] List<Transform> _leftHandVariants;


    [Header("Arms - Wobble Settings")]
    [SerializeField] float _avgArmsWobbleStrength = 1f;
    [SerializeField] float _avgArmsWobbleSpeed = 1f;
    [SerializeField] float _xOffsetWobbleSpeedFactor = 2f;
    [SerializeField] float _xOffsetWobbleStrengthFactor = 2f;

    [Header("Body - Wobble Settings")]
    [SerializeField] float _bodyWobbleStrength = 1f;
    [SerializeField] float _bodyWobbleSpeed = 1f;

    [Header("Body - Ground Hover Settings")]
    [SerializeField, Min(0)] float _distanceFromGround;

    readonly Dictionary<Transform, Vector3> _wobbleOrigins = new();

    [Header("Model Tilt Settings")]
    [SerializeField] float _tiltAngle = 15f;
    public float GetTiltAngle() => _tiltAngle;
    [SerializeField] float _tiltSpeed = 10f;
    public float GetTiltSpeed() => _tiltSpeed;

    [Header("Model Direction Rotation Settings")]
    [SerializeField] float _rotationSpeed = 10f;
    [SerializeField] float _resetRotationSpeed = 5f;

    override public void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (_inputManager == null)
            _inputManager = InputManager.Instance;

        if (_inputManager == null)
            Debug.LogError("InputManager reference is missing in CapsuleManModelController.");
    }

    void Update()
    {
        WobbleBothArms(avgWobbleStrength: _avgArmsWobbleStrength, avgWobbleSpeed: _avgArmsWobbleSpeed);
        WobbleBody(wobbleStrength: _bodyWobbleStrength, wobbleSpeed: _bodyWobbleSpeed);

        if (_inputManager.MoveInput.sqrMagnitude > 0f)
        {
            TiltModel(tiltAngle: _tiltAngle, tiltSpeed: _tiltSpeed);
            RotateModelTowardsMoveDirection(moveInput: _inputManager.MoveInput);
        }
        else
        {
            TiltModel(tiltAngle: 0f, tiltSpeed: _tiltSpeed);
            RotateModelTowardsMoveDirection(moveInput: Vector2.zero);
        }
    }

    void RotateModelTowardsMoveDirection(Vector2 moveInput)
    {
        if (_body == null) return;
        if (moveInput.sqrMagnitude <= 0f)
        {
            Quaternion _targetRotation = Quaternion.Euler(0f, _body.localRotation.eulerAngles.y, 0f);
            _body.localRotation = Quaternion.Slerp(_body.localRotation, _targetRotation, _resetRotationSpeed * Time.deltaTime);
        }

        Vector3 localTargetDirection = new(moveInput.x, 0f, moveInput.y);
        if (localTargetDirection.sqrMagnitude > 0f)
        {
            Quaternion localTargetRotation = Quaternion.LookRotation(localTargetDirection, Vector3.up);
            Vector3 currentEuler = _body.localRotation.eulerAngles;
            Quaternion targetRotation = Quaternion.Euler(currentEuler.x, localTargetRotation.eulerAngles.y, currentEuler.z);

            _body.localRotation = Quaternion.Slerp(_body.localRotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

    }

    void WobbleBody(float wobbleStrength, float wobbleSpeed)
    {
        BodyWobble(_body, wobbleStrength, wobbleSpeed);
    }

    void WobbleBothArms(float avgWobbleStrength, float avgWobbleSpeed)
    {
        WobbleLeftArm(avgWobbleStrength, avgWobbleSpeed);
        WobbleRightArm(avgWobbleStrength, avgWobbleSpeed);
    }

    void WobbleLeftArm(float avgWobbleStrength, float avgWobbleSpeed)
    {
        // Shoulder
        ArmWobble(_leftShoulder,
        wobbleStrength: avgWobbleStrength,
        wobbleSpeed: avgWobbleSpeed);
        // Forearm
        ArmWobble(_leftForearm,
        wobbleStrength: avgWobbleStrength * 0.9f,
        wobbleSpeed: avgWobbleSpeed * 0.9f);
        // Hand
        ArmWobble(_leftHand,
        wobbleStrength: avgWobbleStrength * 0.8f,
        wobbleSpeed: avgWobbleSpeed * 0.8f);
    }

    void WobbleRightArm(float avgWobbleStrength, float avgWobbleSpeed)
    {
        // Shoulder
        ArmWobble(_rightShoulder,
        wobbleStrength: avgWobbleStrength,
        wobbleSpeed: avgWobbleSpeed);
        // Forearm
        ArmWobble(_rightForearm,
        wobbleStrength: avgWobbleStrength * 0.9f,
        wobbleSpeed: avgWobbleSpeed * 0.9f);
        // Hand
        ArmWobble(_rightHand,
        wobbleStrength: avgWobbleStrength * 0.8f,
        wobbleSpeed: avgWobbleSpeed * 0.8f);
    }

    void ArmWobble(Transform objectTransform, float wobbleStrength, float wobbleSpeed)
    {
        if (objectTransform == null) return;

        // Cache the starting position
        if (!_wobbleOrigins.TryGetValue(objectTransform, out Vector3 origin))
        {
            origin = objectTransform.localPosition;
            _wobbleOrigins[objectTransform] = origin;
        }

        float xOffset = Mathf.Sin(Time.fixedTime * wobbleSpeed * _xOffsetWobbleSpeedFactor) * wobbleStrength * _xOffsetWobbleStrengthFactor;
        float zOffset = Mathf.Sin(Time.fixedTime * wobbleSpeed) * wobbleStrength;

        objectTransform.localPosition = origin + new Vector3(xOffset, 0f, zOffset);
    }

    void BodyWobble(Transform objectTransform, float wobbleStrength, float wobbleSpeed)
    {
        if (objectTransform == null) return;

        // Cache the starting position
        if (!_wobbleOrigins.TryGetValue(objectTransform, out Vector3 origin))
        {
            origin = new Vector3(0f, _distanceFromGround, 0f);
            _wobbleOrigins[objectTransform] = origin;
            Debug.Log($"Origin: {origin}");
        }

        float yOffset = Mathf.Sin(Time.fixedTime * wobbleSpeed) * wobbleStrength;

        objectTransform.localPosition = origin + new Vector3(0, yOffset, 0);
    }

    public void TiltModel(float tiltAngle, float tiltSpeed)
    {
        if (_body == null) return;
        if (_inputManager == null)
        {
            _inputManager = InputManager.Instance;
            if (_inputManager == null) return;
        }

        Quaternion targetRotation = Quaternion.Euler(tiltAngle, _body.localRotation.eulerAngles.y, 0f);
        _body.localRotation = Quaternion.Slerp(_body.localRotation, targetRotation, tiltSpeed * Time.fixedDeltaTime);
    }

}

public enum MoveDirection
{
    Forward,
    Backward,
    Left,
    Right
}
