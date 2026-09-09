using System.Collections;
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
    FoodStand
}

/// <summary>
/// Drives one NPC's behaviour loop: repeatedly pick a random action (wander,
/// use a bench, use a food stand), carry it out start to finish, then pick
/// another. Talks to stations only through InteractableStationBase, so
/// adding a new station type never requires touching this class.
/// </summary>
public class NPCActionController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] NAVAgentController _navAgentController;

    [Header("Action Weights (relative - don't need to sum to 1)")]
    [SerializeField] float _wanderWeight = 0.4f;
    [SerializeField] float _benchWeight = 0.3f;
    [SerializeField] float _foodStandWeight = 0.3f;

    [Header("Wander Settings")]
    [Tooltip("Max distance to look for a WanderZone. 0 = no limit (any registered zone is fair game).")]
    [SerializeField] float _wanderZoneSearchRadius = 0f;
    [SerializeField] Vector2 _wanderIdleDuration = new Vector2(2f, 5f);

    [Header("Station Search")]
    [Tooltip("Max distance to look for a bench/food stand. 0 = no limit.")]
    [SerializeField] float _stationSearchRadius = 25f;

    InteractableStationBase _reservedStation;

    public NPCState CurrentState { get; private set; } = NPCState.Idle;

    void Start()
    {
        if (_navAgentController == null && !TryGetComponent(out _navAgentController))
        {
            Debug.LogError($"[{nameof(NPCActionController)}] NAVAgentController component is missing on '{name}'. Add one or assign it in the inspector.", this);
            enabled = false;
            return;
        }

        StartCoroutine(ActionLoop());
    }

    void OnDisable()
    {
        // If the NPC gets despawned/disabled mid-action, don't leave its
        // reserved slot permanently unavailable to everyone else.
        if (_reservedStation == null) return;

        _reservedStation.ReleaseSlot(this);
        _reservedStation = null;
    }

    IEnumerator ActionLoop()
    {
        while (true)
        {
            switch (PickRandomAction())
            {
                case NPCActionType.Bench:
                    yield return PerformStationAction(StationType.Bench);
                    break;
                case NPCActionType.FoodStand:
                    yield return PerformStationAction(StationType.FoodStand);
                    break;
                default:
                    yield return PerformWander();
                    break;
            }
        }
    }

    NPCActionType PickRandomAction()
    {
        float total = _wanderWeight + _benchWeight + _foodStandWeight;
        if (total <= 0f) return NPCActionType.Wander;

        float roll = Random.Range(0f, total);

        if (roll < _wanderWeight) return NPCActionType.Wander;
        roll -= _wanderWeight;

        return roll < _benchWeight ? NPCActionType.Bench : NPCActionType.FoodStand;
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

        transform.rotation = station.GetFacingRotation(slot);
        CurrentState = NPCState.PerformingAction;
        station.OnNPCEnter(this, slot);

        yield return new WaitForSeconds(duration);

        station.OnNPCExit(this, slot);
        station.ReleaseSlot(this);
        _reservedStation = null;

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
}