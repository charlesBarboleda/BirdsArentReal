using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Networked droppable prop.
///
/// Responsibilities:
/// - Server-authoritative pickup/drop state.
/// - Server-authoritative physics and impact detection.
/// - Network parenting to a player's NetworkObject.
/// - Optional visual following of a non-networked hold socket.
/// - Cosmetic splatter effects replicated through an RPC.
///
/// The hold socket itself does NOT need to be a NetworkObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class DropObject : NetworkBehaviour
{
    public enum DropState
    {
        Available,
        Held,
        Armed,
        Impacted
    }

    [Header("References")]
    [SerializeField] Rigidbody _rigidbody;
    [SerializeField] Collider _collider;

    [Header("Splatter")]
    [SerializeField] float _splatterRadius = 2f;
    [SerializeField] LayerMask _reactableLayers;

    [Header("Effect (Optional)")]
    [SerializeField] GameObject _splatterEffectPrefab;

    [Header("Lifecycle")]
    [SerializeField] float _despawnDelayAfterImpact = 2f;

    public DropState State { get; private set; } = DropState.Available;

    /// <summary>
    /// Server-only event. Fired once when the object impacts something.
    /// </summary>
    public event Action<Vector3> OnImpact;

    /// <summary>
    /// Server-only event. Fired for each reactable collider found.
    /// </summary>
    public event Action<Collider> OnReactableHit;

    Transform _followTarget;

    void Awake()
    {
        if (_rigidbody == null && !TryGetComponent(out _rigidbody))
        {
            Debug.LogError(
                "[DropObject] No Rigidbody found.",
                this);

            enabled = false;
            return;
        }

        if (_collider == null && !TryGetComponent(out _collider))
        {
            Debug.LogError(
                "[DropObject] No Collider found.",
                this);

            enabled = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        // The server owns the gameplay state.
        if (IsServer)
            State = DropState.Available;
    }

    void LateUpdate()
    {
        // The socket is a regular Transform.
        // We visually follow it rather than network-parenting to it.
        if (_followTarget == null)
            return;

        if (State != DropState.Held)
            return;

        transform.SetPositionAndRotation(
            _followTarget.position,
            _followTarget.rotation);
    }

    /// <summary>
    /// Assigns the local visual socket this object should follow.
    /// This does not change network parenting.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        _followTarget = target;

        if (_followTarget == null)
            return;

        transform.SetPositionAndRotation(
            _followTarget.position,
            _followTarget.rotation);
    }

    /// <summary>
    /// Clears the visual follow target.
    /// </summary>
    public void ClearFollowTarget()
    {
        _followTarget = null;
    }

    /// <summary>
    /// Server-only. Marks this object as held.
    /// </summary>
    public void Pickup()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[DropObject] Pickup() called on a non-server instance.",
                this);

            return;
        }

        State = DropState.Held;

        _rigidbody.isKinematic = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Server-only. Releases this object and enables physics.
    /// </summary>
    public void Drop()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "[DropObject] Drop() called on a non-server instance.",
                this);

            return;
        }

        State = DropState.Armed;

        _rigidbody.isKinematic = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsServer)
            return;

        if (State != DropState.Armed)
            return;

        if (collision.contactCount == 0)
            return;

        TriggerSplatter(collision.GetContact(0).point);
    }

    void TriggerSplatter(Vector3 impactPoint)
    {
        if (!IsServer)
            return;

        // Prevent multiple impacts from triggering multiple times.
        if (State == DropState.Impacted)
            return;

        State = DropState.Impacted;

        OnImpact?.Invoke(impactPoint);

        Collider[] hits = Physics.OverlapSphere(
            impactPoint,
            _splatterRadius,
            _reactableLayers);

        foreach (Collider hit in hits)
        {
            OnReactableHit?.Invoke(hit);

            if (hit.TryGetComponent(out ISplatterReactable reactable))
                reactable.ReactToSplatter(impactPoint);
        }

        PlaySplatterEffectRpc(impactPoint);

        if (_despawnDelayAfterImpact >= 0f)
            Invoke(nameof(DespawnSelf), _despawnDelayAfterImpact);
    }

    void DespawnSelf()
    {
        if (!IsServer)
            return;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PlaySplatterEffectRpc(Vector3 impactPoint)
    {
        // Cosmetic-only. Every client creates its own local VFX.
        if (_splatterEffectPrefab != null)
        {
            Instantiate(
                _splatterEffectPrefab,
                impactPoint,
                Quaternion.identity);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);

        Gizmos.DrawWireSphere(
            transform.position,
            _splatterRadius);
    }
}