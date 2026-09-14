using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NAVAgentController : NetworkBehaviour
{
    [SerializeField] NavMeshAgent _agent;

    bool CanExecuteLogic => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || IsServer;

    void Awake()
    {
        if (_agent == null && !TryGetComponent(out _agent))
        {
            Debug.LogError($"[{nameof(NAVAgentController)}] NavMeshAgent component is missing from '{name}'. Add one or assign it in the inspector.", this);
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Clients should not run NavMeshAgent simulations; position/rotation is synced via NetworkTransform
        if (!IsServer && _agent != null)
        {
            _agent.enabled = false;
        }
    }

    public void GoTo(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"[{nameof(NAVAgentController)}] GoTo called with a null target on '{name}'.", this);
            return;
        }

        GoTo(target.position);
    }

    public void GoTo(Vector3 destination)
    {
        if (!CanExecuteLogic) return;

        if (_agent == null)
        {
            Debug.LogError($"[{nameof(NAVAgentController)}] NavMeshAgent reference is missing on '{name}'.", this);
            return;
        }

        if (!_agent.enabled)
        {
            _agent.enabled = true;
        }

        _agent.isStopped = false;
        _agent.SetDestination(destination);
    }

    /// <summary>True once the agent's current path has resolved and it has arrived within stopping distance.</summary>
    public bool HasReachedDestination()
    {
        if (_agent == null || !_agent.enabled) return true; // don't let a broken agent hang the action loop forever
        if (_agent.pathPending) return false;
        if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) return true; // give up rather than block

        return _agent.remainingDistance <= _agent.stoppingDistance
            && (!_agent.hasPath || _agent.velocity.sqrMagnitude < 0.01f);
    }

    /// <summary>Stops navigation, disables the agent to prevent NavMesh clamping, and snaps precisely to target transform.</summary>
    public void StopAndSnap(Vector3 position, Quaternion rotation)
    {
        if (!CanExecuteLogic) return;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
            _agent.enabled = false;
        }
        else if (_agent != null)
        {
            _agent.enabled = false;
        }

        transform.position = position;
        transform.rotation = rotation;
    }

    /// <summary>Stops pathfinding without destroying agent state so displacement can be applied.</summary>
    public void StopPath()
    {
        if (!CanExecuteLogic || _agent == null) return;

        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }
    }

    /// <summary>Moves the agent by a displacement vector, respecting NavMesh bounds if active.</summary>
    public void MoveDisplacement(Vector3 displacement)
    {
        if (!CanExecuteLogic || _agent == null)
        {
            transform.position += displacement;
            return;
        }

        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.Move(displacement);
        }
        else
        {
            transform.position += displacement;
        }
    }

    /// <summary>Re-enables the agent and places it safely on the nearest walkable NavMesh point.</summary>
    public void ResumeNavigation()
    {
        if (!CanExecuteLogic || _agent == null) return;

        if (!_agent.enabled)
        {
            _agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 2.0f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
        }
    }

    /// <summary>Current movement speed in units/second. Useful for driving an Animator's locomotion blend.</summary>
    public float CurrentSpeed => (_agent != null && _agent.enabled) ? _agent.velocity.magnitude : 0f;

    public void SetSpeed(float speed)
    {
        if (_agent == null)
        {
            Debug.LogError($"[{nameof(NAVAgentController)}] NavMeshAgent reference is missing on '{name}'.", this);
            return;
        }

        _agent.speed = speed;
    }
}