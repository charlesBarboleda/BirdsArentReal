using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NAVAgentController : MonoBehaviour
{
    [SerializeField] NavMeshAgent _agent;

    void Awake()
    {
        if (_agent == null && !TryGetComponent(out _agent))
        {
            Debug.LogError($"[{nameof(NAVAgentController)}] NavMeshAgent component is missing from '{name}'. Add one or assign it in the inspector.", this);
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

    /// <summary>Re-enables the agent and places it safely on the nearest walkable NavMesh point.</summary>
    public void ResumeNavigation()
    {
        if (_agent == null) return;

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