using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach alongside NAVAgentController on every NPC. On spawn, picks one
/// walk clip and one idle clip at random and builds a per-instance
/// AnimatorOverrideController so each NPC can look different without
/// needing a separate Animator Controller asset per variant. Talk clips are
/// swapped in on demand via PlayRandomTalk() instead, since they should
/// vary every time an NPC talks rather than being fixed once for its
/// lifetime.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCAnimationController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] Animator _animator;
    [SerializeField] NAVAgentController _navAgentController;

    [Header("Base Controller")]
    [Tooltip("Shared Animator Controller with an Idle <-> Walk transition driven by 'Speed', plus a one-shot Idle -> Talk -> Idle transition driven by the 'Talk' trigger.")]
    [SerializeField] RuntimeAnimatorController _baseController;

    [Header("Walk Variants")]
    [SerializeField] List<AnimationClip> _walkAnimations = new();

    [Header("Idle Variants")]
    [SerializeField] List<AnimationClip> _idleAnimations = new();

    [Header("Talk Variants")]
    [Tooltip("Picked at random every time PlayRandomTalk() is called, not just once on spawn.")]
    [SerializeField] List<AnimationClip> _talkAnimations = new();

    [Header("Sitting Variants")]
    [Tooltip("Picked at random every time StartSitting() is called.")]
    [SerializeField] List<AnimationClip> _sittingAnimations = new();

    [Header("Ground Sitting Variants")]
    [Tooltip("Picked at random every time StartGroundSitting() is called.")]
    [SerializeField] List<AnimationClip> _groundSittingAnimations = new();

    [Tooltip("Must match the exact name of each placeholder clip in the base controller - that name is the key AnimatorOverrideController uses to swap it.")]
    [SerializeField] string _walkClipName = "Walk";
    [SerializeField] string _idleClipName = "Idle";
    [SerializeField] string _talkClipName = "Talk";
    [SerializeField] string _sittingClipName = "Sit";

    [Header("Blending")]
    [Tooltip("Higher = snappier transition between idle and walk.")]
    [SerializeField] float _speedLerpSpeed = 8f;

    [Header("Static NPC")]
    [SerializeField] bool _isStaticNPC = false;

    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int TalkTrigger = Animator.StringToHash("Talk");
    static readonly int IsSittingParam = Animator.StringToHash("IsSitting");

    AnimatorOverrideController _overrideController;

    void Awake()
    {
        if (_animator == null && !TryGetComponent(out _animator))
        {
            Debug.LogError($"[{nameof(NPCAnimationController)}] Animator missing from '{name}'.", this);
        }

        if (!_isStaticNPC)
        {
            if (_navAgentController == null && !TryGetComponent(out _navAgentController))
            {
                Debug.LogError($"[{nameof(NPCAnimationController)}] NAVAgentController missing from '{name}'.", this);
            }
        }

        SetupAnimationOverrides();
    }

    void SetupAnimationOverrides()
    {
        if (_overrideController != null) return;
        if (_animator == null && !TryGetComponent(out _animator)) return;
        if (_baseController == null) return;

        _overrideController = new AnimatorOverrideController(_baseController);

        if (_idleAnimations.Count > 0)
        {
            _overrideController[_idleClipName] = _idleAnimations[Random.Range(0, _idleAnimations.Count)];
        }

        if (_walkAnimations.Count > 0)
        {
            _overrideController[_walkClipName] = _walkAnimations[Random.Range(0, _walkAnimations.Count)];
        }

        _animator.runtimeAnimatorController = _overrideController;
    }

    Coroutine _continuousTalkCoroutine;

    /// <summary>
    /// Swaps in a random talk clip and fires the one-shot Idle -> Talk ->
    /// Idle transition. Returns the chosen clip's length in seconds (0 if
    /// there are no talk clips assigned), so callers know how long to wait.
    /// </summary>
    public float PlayRandomTalk()
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_animator == null || _overrideController == null || _talkAnimations.Count == 0) return 0f;

        var clip = _talkAnimations[Random.Range(0, _talkAnimations.Count)];
        _overrideController[_talkClipName] = clip;
        _animator.SetTrigger(TalkTrigger);

        return clip.length;
    }

    /// <summary>
    /// Starts playing randomized talk animations continuously in a loop until StopContinuousTalk() is called.
    /// </summary>
    public void StartContinuousTalk()
    {
        if (_continuousTalkCoroutine != null) return;
        _continuousTalkCoroutine = StartCoroutine(ContinuousTalkRoutine());
    }

    /// <summary>
    /// Stops playing continuous randomized talk animations, returning to idle.
    /// </summary>
    public void StopContinuousTalk()
    {
        if (_continuousTalkCoroutine != null)
        {
            StopCoroutine(_continuousTalkCoroutine);
            _continuousTalkCoroutine = null;
        }
    }

    System.Collections.IEnumerator ContinuousTalkRoutine()
    {
        while (true)
        {
            float duration = PlayRandomTalk();
            if (duration <= 0f) duration = 3f;
            yield return new WaitForSeconds(duration);
        }
    }

    /// <summary>
    /// Swaps in a random sitting clip and transitions into the sitting state.
    /// </summary>
    public void StartSitting()
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_animator == null) return;

        if (_overrideController != null && _sittingAnimations.Count > 0)
        {
            var clip = _sittingAnimations[Random.Range(0, _sittingAnimations.Count)];
            _overrideController[_sittingClipName] = clip;
        }

        _animator.SetBool(IsSittingParam, true);
    }

    /// <summary>
    /// Swaps in a random ground sitting clip and transitions into the sitting state.
    /// </summary>
    public void StartGroundSitting()
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_animator == null) return;

        if (_overrideController != null && _groundSittingAnimations.Count > 0)
        {
            var clip = _groundSittingAnimations[Random.Range(0, _groundSittingAnimations.Count)];
            _overrideController[_sittingClipName] = clip;
        }

        _animator.SetBool(IsSittingParam, true);
    }

    /// <summary>
    /// Transitions out of the sitting state back to idle.
    /// </summary>
    public void StopSitting()
    {
        if (_animator == null) return;
        _animator.SetBool(IsSittingParam, false);
    }

    void Update()
    {
        if (_animator == null || _navAgentController == null) return;

        float target = _navAgentController.CurrentSpeed;
        float current = _animator.GetFloat(SpeedParam);

        _animator.SetFloat(SpeedParam, Mathf.Lerp(current, target, Time.deltaTime * _speedLerpSpeed));
    }
}