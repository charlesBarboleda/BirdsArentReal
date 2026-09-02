using System.Collections.Generic;
using UnityEngine;

public class CapsuleManModelController : MonoBehaviour
{
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

    void FixedUpdate()
    {
        // TestRotateRightShoulder();
        WobbleBothArms(avgWobbleStrength: _avgArmsWobbleStrength, avgWobbleSpeed: _avgArmsWobbleSpeed);
        WobbleBody(wobbleStrength: _bodyWobbleStrength, wobbleSpeed: _bodyWobbleSpeed);
    }

    // [SerializeField] Vector3 _axis;
    // [SerializeField] float _rotationSpeed;
    // void TestRotateRightShoulder()
    // {
    //     if (!_rightShoulder || !_rightShoulderAnchor) return;

    //     _rightShoulder.RotateAround(_rightShoulderAnchor.position, _axis, _rotationSpeed);
    // }

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

}
