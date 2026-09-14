using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum NPCState
{
    Idle,
    Moving,
    PerformingAction
}

enum NPCActionType
{
    Wander,
    Bench,
    FoodStand,
    Talk,
    PicnicSpot
}

/// <summary>
/// Drives one NPC's behaviour loop: repeatedly pick a random action (wander,
/// use a bench, use a food stand, talk to another NPC), carry it out start
/// to finish, then pick another. Talks to stations only through
/// InteractableStationBase and to other NPCs only through
/// NPCSocialRegistry, so adding a new station type or social behaviour
/// never requires touching this class's public surface.
/// In networked multiplayer, the action loop runs exclusively on the server.
/// </summary>
public class NPCActionController : NetworkBehaviour, ISplatterReactable
{
    [Header("Setup")]
    [SerializeField] NAVAgentController _navAgentController;
    [SerializeField] NPCAnimationController _animationController;

    [Header("Action Weights (relative - don't need to sum to 1)")]
    [SerializeField] float _wanderWeight = 0.35f;
    [SerializeField] float _benchWeight = 0.2f;
    [SerializeField] float _foodStandWeight = 0.15f;
    [SerializeField] float _talkWeight = 0.15f;
    [SerializeField] float _picnicWeight = 0.15f;

    [Header("Wander Settings")]
    [Tooltip("Max distance to look for a WanderZone. 0 = no limit (any registered zone is fair game).")]
    [SerializeField] float _wanderZoneSearchRadius = 0f;
    [SerializeField] Vector2 _wanderIdleDuration = new Vector2(2f, 5f);

    [Header("Station Search")]
    [Tooltip("Max distance to look for a bench/food stand. 0 = no limit.")]
    [SerializeField] float _stationSearchRadius = 25f;

    [Header("Conversation Settings")]
    [Tooltip("Max distance to look for another NPC to talk to. 0 = no limit.")]
    [SerializeField] float _conversationSearchRadius = 15f;
    [Tooltip("How far apart to stand from a conversation partner.")]
    [SerializeField] float _conversationDistance = 1.2f;
    [Tooltip("Floor for how long a conversation lasts even if a talk clip is very short or missing.")]
    [SerializeField] float _minConversationDuration = 2f;

    [Header("Splatter Reaction Settings")]
    [Tooltip("Distance the NPC moves backward away from the splatter during the reaction animation.")]
    [SerializeField] float _splatterStepBackDistance = 1.0f;

    InteractableStationBase _reservedStation;
    NPCActionController _currentPartner;
    bool _isBusy; // true while reserved for a conversation, either as initiator or partner
    Coroutine _actionLoopCoroutine;
    Coroutine _reactionCoroutine;

    public NPCState CurrentState { get; private set; } = NPCState.Idle;
    public NPCAnimationController AnimationController => _animationController;

    /// <summary>True when this NPC can be pulled into a conversation right now.</summary>
    public bool IsAvailableForConversation => !_isBusy && CurrentState == NPCState.Idle;

    void Awake()
    {
        if (_navAgentController == null) TryGetComponent(out _navAgentController);
        if (_animationController == null) TryGetComponent(out _animationController);
    }

    void Start()
    {
        if (_navAgentController == null && !TryGetComponent(out _navAgentController))
        {
            Debug.LogError($"[{nameof(NPCActionController)}] NAVAgentController component is missing on '{name}'. Add one or assign it in the inspector.", this);
            enabled = false;
            return;
        }

        if (_animationController == null) TryGetComponent(out _animationController);

        // Fallback for offline singleplayer when NetworkManager is not present in the scene
        if (NetworkManager.Singleton == null)
        {
            NPCSocialRegistry.Register(this);
            StartActionLoop();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            NPCSocialRegistry.Register(this);
            StartActionLoop();
        }
        else
        {
            StopActionLoop();
            NPCSocialRegistry.Unregister(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        StopActionLoop();
        CleanupReservations();
        NPCSocialRegistry.Unregister(this);

        base.OnNetworkDespawn();
    }

    void OnEnable()
    {
        if (NetworkManager.Singleton == null || (IsSpawned && IsServer))
        {
            NPCSocialRegistry.Register(this);
        }
    }

    void OnDisable()
    {
        NPCSocialRegistry.Unregister(this);
        CleanupReservations();
        StopActionLoop();
    }

    void StartActionLoop()
    {
        if (_actionLoopCoroutine != null) return;
        _actionLoopCoroutine = StartCoroutine(ActionLoop());
    }

    void StopActionLoop()
    {
        if (_actionLoopCoroutine != null)
        {
            StopCoroutine(_actionLoopCoroutine);
            _actionLoopCoroutine = null;
        }
    }

    void CleanupReservations()
    {
        if (_reservedStation != null)
        {
            StopSitting();
            _navAgentController?.ResumeNavigation();
            _reservedStation.ReleaseSlot(this);
            _reservedStation = null;
        }

        if (_currentPartner != null)
        {
            _currentPartner.ReleaseConversationReservation();
            _currentPartner = null;
        }

        _isBusy = false;
    }

    IEnumerator ActionLoop()
    {
        while (true)
        {
            // Paused while another NPC has reserved us for a conversation.
            while (_isBusy) yield return null;

            switch (PickRandomAction())
            {
                case NPCActionType.Bench:
                    yield return PerformStationAction(StationType.Bench);
                    break;
                case NPCActionType.FoodStand:
                    yield return PerformStationAction(StationType.FoodStand);
                    break;
                case NPCActionType.PicnicSpot:
                    yield return PerformStationAction(StationType.PicnicSpot);
                    break;
                case NPCActionType.Talk:
                    yield return PerformTalkToNPC();
                    break;
                default:
                    yield return PerformWander();
                    break;
            }
        }
    }

    NPCActionType PickRandomAction()
    {
        float total = _wanderWeight + _benchWeight + _foodStandWeight + _talkWeight + _picnicWeight;
        if (total <= 0f) return NPCActionType.Wander;

        float roll = Random.Range(0f, total);

        if (roll < _wanderWeight) return NPCActionType.Wander;
        roll -= _wanderWeight;

        if (roll < _benchWeight) return NPCActionType.Bench;
        roll -= _benchWeight;

        if (roll < _foodStandWeight) return NPCActionType.FoodStand;
        roll -= _foodStandWeight;

        return roll < _talkWeight ? NPCActionType.Talk : NPCActionType.PicnicSpot;
    }

    IEnumerator PerformStationAction(StationType type)
    {
        if (!StationRegistry.TryGetRandomAvailableStation(type, transform.position, _stationSearchRadius, out var station)
            || !station.TryReserveSlot(this, out var slot, out var duration))
        {
            // Nothing free right now - don't idle in place, just wander instead
            // and try again next loop.
            yield return PerformWander();
            yield break;
        }

        _reservedStation = station;
        CurrentState = NPCState.Moving;
        _navAgentController.GoTo(slot.position);

        yield return WaitUntilArrived();

        _navAgentController.StopAndSnap(slot.position, station.GetFacingRotation(slot));
        CurrentState = NPCState.PerformingAction;
        station.OnNPCEnter(this, slot);

        yield return new WaitForSeconds(duration);

        station.OnNPCExit(this, slot);
        _navAgentController.ResumeNavigation();
        station.ReleaseSlot(this);
        _reservedStation = null;

        CurrentState = NPCState.Idle;
    }

    IEnumerator PerformTalkToNPC()
    {
        if (!NPCSocialRegistry.TryGetRandomAvailablePartner(this, _conversationSearchRadius, out var partner)
            || !partner.TryReserveForConversation())
        {
            // Nobody free to talk to right now - wander instead and try again next loop.
            yield return PerformWander();
            yield break;
        }

        _currentPartner = partner;
        _isBusy = true;
        CurrentState = NPCState.Moving;

        Vector3 toPartner = partner.transform.position - transform.position;
        Vector3 approachDirection = toPartner.sqrMagnitude > 0.0001f ? toPartner.normalized : Vector3.forward;
        Vector3 approachPoint = partner.transform.position - approachDirection * _conversationDistance;

        _navAgentController.GoTo(approachPoint);
        yield return WaitUntilArrived();

        // Face each other - the partner never moves, it's frozen in place by
        // its own ActionLoop's _isBusy check, so only rotation is needed.
        FaceToward(partner.transform.position);
        partner.FaceToward(transform.position);

        CurrentState = NPCState.PerformingAction;

        float myTalkLength = PlayTalkAnimation();
        float theirTalkLength = partner.PlayTalkAnimation();
        float duration = Mathf.Max(myTalkLength, theirTalkLength, _minConversationDuration);

        yield return new WaitForSeconds(duration);

        if (_currentPartner != null)
        {
            _currentPartner.ReleaseConversationReservation();
            _currentPartner = null;
        }
        _isBusy = false;

        CurrentState = NPCState.Idle;
    }

    IEnumerator PerformWander()
    {
        CurrentState = NPCState.Moving;

        if (WanderZoneRegistry.TryGetRandomZone(transform.position, _wanderZoneSearchRadius, out var zone)
            && zone.TryGetRandomPoint(out var point))
        {
            _navAgentController.GoTo(point);
            yield return WaitUntilArrived();
        }

        CurrentState = NPCState.Idle;
        yield return new WaitForSeconds(Random.Range(_wanderIdleDuration.x, _wanderIdleDuration.y));
    }

    IEnumerator WaitUntilArrived()
    {
        yield return null; // let the agent process the new destination before checking

        while (!_navAgentController.HasReachedDestination())
        {
            yield return null;
        }
    }

    /// <summary>
    /// Called when a splatter lands within range of this NPC.
    /// Turns toward the impact center, plays a random splatter reaction animation,
    /// and moves backwards away from the splatter during the animation.
    /// </summary>
    public void ReactToSplatter(Vector3 sourcePosition)
    {
        Debug.Log($"[SPLATTER] {name} received reaction request at {sourcePosition}", this);

        // In a networked game, only the server controls NPC reactions.
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !IsServer)
        {
            Debug.Log($"[SPLATTER] {name} rejected request because this is a client.", this);
            return;
        }

        if (_reactionCoroutine != null)
        {
            StopCoroutine(_reactionCoroutine);
            _reactionCoroutine = null;
        }

        StopActionLoop();
        CleanupReservations();

        _animationController.StopAllActions();

        _reactionCoroutine = StartCoroutine(
            PerformSplatterReaction(sourcePosition)
        );
    }

    IEnumerator PerformSplatterReaction(Vector3 sourcePosition)
    {
        CurrentState = NPCState.PerformingAction;

        // Stop navigation before applying reaction movement.
        _navAgentController?.StopPath();

        // Face the source of the splatter.
        FaceToward(sourcePosition);

        // Play a randomized splatter animation.
        float clipLength = PlaySplatterAnimation();

        // Fallback duration if no valid animation was assigned.
        if (clipLength <= 0f)
        {
            clipLength = 1.5f;
        }

        // Calculate the horizontal direction away from the splatter.
        Vector3 awayDirection = transform.position - sourcePosition;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude > 0.0001f)
        {
            awayDirection.Normalize();
        }
        else
        {
            // If the NPC is directly on the impact position,
            // move backward relative to its current facing.
            awayDirection = -transform.forward;
            awayDirection.y = 0f;
            awayDirection.Normalize();
        }

        float elapsed = 0f;
        float previousDistance = 0f;

        while (elapsed < clipLength)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsed / clipLength);

            // Ease-out movement: moves quickly at first, then slows down.
            float targetDistance =
                _splatterStepBackDistance *
                (normalizedTime * (2f - normalizedTime));

            float distanceThisFrame = targetDistance - previousDistance;

            if (distanceThisFrame > 0f)
            {
                _navAgentController?.MoveDisplacement(
                    awayDirection * distanceThisFrame
                );

                previousDistance = targetDistance;
            }

            yield return null;
        }

        // Reaction is finished. Allow locomotion animation updates again.
        _animationController.ResumeLocomotion();

        // Resume normal navigation.
        _navAgentController.ResumeNavigation();

        CurrentState = NPCState.Idle;

        _reactionCoroutine = null;

        // Restart the normal NPC behaviour loop.
        StartActionLoop();
    }

    /// <summary>Attempts to reserve this NPC as a conversation partner. Fails if it's already busy or not idle.</summary>
    public bool TryReserveForConversation()
    {
        if (!IsAvailableForConversation) return false;

        _isBusy = true;
        return true;
    }

    /// <summary>Releases a conversation reservation made via TryReserveForConversation, letting this NPC's own loop resume.</summary>
    public void ReleaseConversationReservation() => _isBusy = false;

    /// <summary>Rotates in place to face a world position, ignoring height.</summary>
    public void FaceToward(Vector3 worldPosition) => transform.rotation = FacingUtility.LookAtFlat(transform.position, worldPosition, transform.rotation);

    /// <summary>Plays one random non-looping talk animation, if this NPC has an NPCAnimationController. Returns the clip length, or 0 if none played.</summary>
    public float PlayTalkAnimation() => _animationController != null ? _animationController.PlayRandomTalk() : 0f;

    /// <summary>Plays one random non-looping splatter reaction animation, if this NPC has an NPCAnimationController. Returns the clip length, or 0 if none played.</summary>
    public float PlaySplatterAnimation() => _animationController != null ? _animationController.PlayRandomSplatter() : 0f;

    /// <summary>Puts this NPC into the sitting animation state with a random sitting animation variant.</summary>
    public void StartSitting() => _animationController?.StartSitting();

    /// <summary>Puts this NPC into the ground sitting animation state with a random ground sitting animation variant.</summary>
    public void StartGroundSitting() => _animationController?.StartGroundSitting();

    /// <summary>Transitions this NPC out of the sitting animation state.</summary>
    public void StopSitting() => _animationController?.StopSitting();
}