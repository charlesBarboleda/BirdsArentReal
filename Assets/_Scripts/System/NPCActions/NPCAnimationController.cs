using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Attach alongside NAVAgentController on every NPC. On spawn, picks one
/// walk clip and one idle clip at random and builds a per-instance
/// AnimatorOverrideController so each NPC can look different without
/// needing a separate Animator Controller asset per variant. Talk clips are
/// swapped in on demand via PlayRandomTalk() instead, since they should
/// vary every time an NPC talks rather than being fixed once for its
/// lifetime.
/// Synchronized across the network via NGO NetworkVariables and NetworkAnimator.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCAnimationController : NetworkBehaviour
{
    [Header("Setup")]
    [SerializeField] Animator _animator;
    [SerializeField] NAVAgentController _navAgentController;
    [SerializeField] NetworkAnimator _networkAnimator;

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
    Coroutine _continuousTalkCoroutine;

    // Network-synchronized animation selection indices so all clients display identical animations
    readonly NetworkVariable<int> _idleIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<int> _walkIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<int> _talkIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<int> _sitIndex = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> _isGroundSitting = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        if (_animator == null && !TryGetComponent(out _animator))
        {
            Debug.LogError($"[{nameof(NPCAnimationController)}] Animator missing from '{name}'.", this);
        }

        if (_networkAnimator == null)
        {
            TryGetComponent(out _networkAnimator);
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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _idleIndex.OnValueChanged += OnIdleIndexChanged;
        _walkIndex.OnValueChanged += OnWalkIndexChanged;
        _talkIndex.OnValueChanged += OnTalkIndexChanged;
        _sitIndex.OnValueChanged += OnSitIndexChanged;
        _isGroundSitting.OnValueChanged += OnGroundSittingChanged;

        if (IsServer)
        {
            if (_idleAnimations.Count > 0 && _idleIndex.Value < 0)
            {
                _idleIndex.Value = Random.Range(0, _idleAnimations.Count);
            }

            if (_walkAnimations.Count > 0 && _walkIndex.Value < 0)
            {
                _walkIndex.Value = Random.Range(0, _walkAnimations.Count);
            }
        }

        // Apply synchronized state on spawn
        if (_idleIndex.Value >= 0 && _idleIndex.Value < _idleAnimations.Count)
        {
            ApplyIdleClip(_idleIndex.Value);
        }

        if (_walkIndex.Value >= 0 && _walkIndex.Value < _walkAnimations.Count)
        {
            ApplyWalkClip(_walkIndex.Value);
        }

        if (_talkIndex.Value >= 0 && _talkIndex.Value < _talkAnimations.Count)
        {
            ApplyTalkClip(_talkIndex.Value);
        }

        if (_sitIndex.Value >= 0)
        {
            ApplySitClip(_sitIndex.Value, _isGroundSitting.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        _idleIndex.OnValueChanged -= OnIdleIndexChanged;
        _walkIndex.OnValueChanged -= OnWalkIndexChanged;
        _talkIndex.OnValueChanged -= OnTalkIndexChanged;
        _sitIndex.OnValueChanged -= OnSitIndexChanged;
        _isGroundSitting.OnValueChanged -= OnGroundSittingChanged;

        StopContinuousTalk();

        base.OnNetworkDespawn();
    }

    void OnIdleIndexChanged(int previousValue, int newValue) => ApplyIdleClip(newValue);
    void OnWalkIndexChanged(int previousValue, int newValue) => ApplyWalkClip(newValue);
    void OnTalkIndexChanged(int previousValue, int newValue) => ApplyTalkClip(newValue);
    void OnSitIndexChanged(int previousValue, int newValue) => ApplySitClip(newValue, _isGroundSitting.Value);
    void OnGroundSittingChanged(bool previousValue, bool newValue) => ApplySitClip(_sitIndex.Value, newValue);

    void ApplyIdleClip(int index)
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_overrideController != null && index >= 0 && index < _idleAnimations.Count)
        {
            _overrideController[_idleClipName] = _idleAnimations[index];
        }
    }

    void ApplyWalkClip(int index)
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_overrideController != null && index >= 0 && index < _walkAnimations.Count)
        {
            _overrideController[_walkClipName] = _walkAnimations[index];
        }
    }

    void ApplyTalkClip(int index)
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_overrideController != null && index >= 0 && index < _talkAnimations.Count)
        {
            _overrideController[_talkClipName] = _talkAnimations[index];
        }
    }

    void ApplySitClip(int index, bool groundSitting)
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_overrideController == null || index < 0) return;

        if (groundSitting)
        {
            if (index < _groundSittingAnimations.Count)
            {
                _overrideController[_sittingClipName] = _groundSittingAnimations[index];
            }
        }
        else
        {
            if (index < _sittingAnimations.Count)
            {
                _overrideController[_sittingClipName] = _sittingAnimations[index];
            }
        }
    }

    void SetupAnimationOverrides()
    {
        if (_overrideController != null) return;
        if (_animator == null && !TryGetComponent(out _animator)) return;
        if (_baseController == null) return;

        _overrideController = new AnimatorOverrideController(_baseController);

        if (_idleAnimations.Count > 0)
        {
            _overrideController[_idleClipName] = _idleAnimations[0];
        }

        if (_walkAnimations.Count > 0)
        {
            _overrideController[_walkClipName] = _walkAnimations[0];
        }

        _animator.runtimeAnimatorController = _overrideController;
    }

    /// <summary>
    /// Swaps in a random talk clip and fires the one-shot Idle -> Talk ->
    /// Idle transition. Returns the chosen clip's length in seconds (0 if
    /// there are no talk clips assigned), so callers know how long to wait.
    /// </summary>
    public float PlayRandomTalk()
    {
        if (_overrideController == null) SetupAnimationOverrides();
        if (_animator == null || _overrideController == null || _talkAnimations.Count == 0) return 0f;

        int selectedIndex = Random.Range(0, _talkAnimations.Count);

        if (IsSpawned && IsServer)
        {
            _talkIndex.Value = selectedIndex;
        }

        var clip = _talkAnimations[selectedIndex];
        _overrideController[_talkClipName] = clip;

        if (_networkAnimator != null && IsSpawned)
        {
            _networkAnimator.SetTrigger(TalkTrigger);
        }
        else
        {
            _animator.SetTrigger(TalkTrigger);
        }

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

    IEnumerator ContinuousTalkRoutine()
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

        if (_sittingAnimations.Count > 0)
        {
            int selectedIndex = Random.Range(0, _sittingAnimations.Count);
            if (IsSpawned && IsServer)
            {
                _isGroundSitting.Value = false;
                _sitIndex.Value = selectedIndex;
            }

            var clip = _sittingAnimations[selectedIndex];
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

        if (_groundSittingAnimations.Count > 0)
        {
            int selectedIndex = Random.Range(0, _groundSittingAnimations.Count);
            if (IsSpawned && IsServer)
            {
                _isGroundSitting.Value = true;
                _sitIndex.Value = selectedIndex;
            }

            var clip = _groundSittingAnimations[selectedIndex];
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

        // If networked and listening, only server drives parameter updates; client receives via NetworkAnimator
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        float target = _navAgentController.CurrentSpeed;
        float current = _animator.GetFloat(SpeedParam);

        _animator.SetFloat(SpeedParam, Mathf.Lerp(current, target, Time.deltaTime * _speedLerpSpeed));
    }
}