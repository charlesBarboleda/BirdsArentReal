using UnityEngine;
using Unity.Netcode;

public class BirdModelController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] Animator _animator;
    [SerializeField] Transform _visualModel;


    [Header("Visual Tilt & Banking")]
    [SerializeField] float _maxBankAngle = 30f;
    [SerializeField] float _maxPitchAngle = 20f;
    [SerializeField] float _bankSpeed = 6f;
    [SerializeField] float _pitchSpeed = 6f;

    [Header("Animation Smoothing")]
    [SerializeField] float _animDampTime = 0.15f;

    // Animator parameter hashes
    static readonly int FlyingHash = Animator.StringToHash("flying");
    static readonly int FlyingDirectionXHash = Animator.StringToHash("flyingDirectionX");
    static readonly int FlyingDirectionYHash = Animator.StringToHash("flyingDirectionY");
    static readonly int LandingHash = Animator.StringToHash("landing");

    float _currentBank;
    float _currentPitch;

    void Awake()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        if (_visualModel == null && _animator != null)
            _visualModel = _animator.transform;
    }


    public void UpdateFlightVisuals(float turnInput, float forwardInput, float verticalInput, bool isFlying)
    {
        // Update Animator parameters
        if (_animator != null)
        {
            _animator.SetBool(FlyingHash, isFlying);

            if (isFlying)
            {
                _animator.SetFloat(FlyingDirectionXHash, turnInput, _animDampTime, Time.deltaTime);
                _animator.SetFloat(FlyingDirectionYHash, verticalInput, _animDampTime, Time.deltaTime);
            }
        }

        // Apply visual banking (roll on Z) and pitch (tilt on X) to visual child
        if (_visualModel != null)
        {
            float targetBank = isFlying ? -turnInput * _maxBankAngle : 0f;
            float targetPitch = isFlying ? -verticalInput * _maxPitchAngle : 0f;

            _currentBank = Mathf.Lerp(_currentBank, targetBank, Time.deltaTime * _bankSpeed);
            _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, Time.deltaTime * _pitchSpeed);

            _visualModel.localRotation = Quaternion.Euler(_currentPitch, 0f, _currentBank);
        }
    }

    public void SetFlying(bool flying)
    {
        if (_animator != null)
        {
            _animator.SetBool(FlyingHash, flying);
        }
    }

    public void TriggerLanding()
    {
        if (_animator != null)
        {
            _animator.SetBool(LandingHash, true);
            _animator.SetBool(FlyingHash, false);
        }
    }
}
