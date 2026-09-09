using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach alongside NAVAgentController on every NPC. On spawn, picks one
/// walk clip at random from the assigned list and builds a per-instance
/// AnimatorOverrideController so each NPC can walk differently without
/// needing a separate Animator Controller asset per variant. Drives a
/// "Speed" float from the NavMeshAgent's own velocity, so it never needs to
/// know anything about NPCActionController's state machine.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCAnimationController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] Animator _animator;
    [SerializeField] NAVAgentController _navAgentController;

    [Header("Base Controller")]
    [Tooltip("Shared Animator Controller with an Idle <-> Walk transition driven by the 'Speed' float parameter.")]
    [SerializeField] RuntimeAnimatorController _baseController;

    [Header("Walk Variants")]
    [SerializeField] List<AnimationClip> _walkAnimations = new();

    [Header("Idle Variants")]
    [SerializeField] List<AnimationClip> _idleAnimations = new();


    [Tooltip("Must match the exact name of the placeholder clip assigned to the Walk state in the base controller - that name is the key AnimatorOverrideController uses to swap it.")]
    [SerializeField] string _walkClipName = "Walk";
    [SerializeField] string _idleClipName = "Idle";

    [Header("Blending")]
    [Tooltip("Higher = snappier transition between idle and walk.")]
    [SerializeField] float _speedLerpSpeed = 8f;

    static readonly int SpeedParam = Animator.StringToHash("Speed");

    void Awake()
    {
        if (_animator == null && !TryGetComponent(out _animator))
        {
            Debug.LogError($"[{nameof(NPCAnimationController)}] Animator missing from '{name}'.", this);
        }

        if (_navAgentController == null && !TryGetComponent(out _navAgentController))
        {
            Debug.LogError($"[{nameof(NPCAnimationController)}] NAVAgentController missing from '{name}'.", this);
        }

        SetupAnimationOverrides();
    }

    void SetupAnimationOverrides()
    {
        if (_animator == null || _baseController == null) return;

        var overrideController = new AnimatorOverrideController(_baseController);

        // Swap Idle
        if (_idleAnimations.Count > 0)
        {
            var randomIdle = _idleAnimations[Random.Range(0, _idleAnimations.Count)];
            overrideController[_idleClipName] = randomIdle;
        }

        // Swap Walk
        if (_walkAnimations.Count > 0)
        {
            var randomWalk = _walkAnimations[Random.Range(0, _walkAnimations.Count)];
            overrideController[_walkClipName] = randomWalk;
        }

        // Assign the populated override controller to the animator once at the end
        _animator.runtimeAnimatorController = overrideController;
    }

    void Update()
    {
        if (_animator == null || _navAgentController == null) return;

        float target = _navAgentController.CurrentSpeed;
        float current = _animator.GetFloat(SpeedParam);

        _animator.SetFloat(SpeedParam, Mathf.Lerp(current, target, Time.deltaTime * _speedLerpSpeed));
    }
}