using System;
using Unity.Netcode;
using UnityEngine;
using InputSystem;

/// <summary>
/// Handles pickup and dropping of DropObjects.
///
/// The player does NOT network-parent objects to the hold socket.
/// Instead:
///
/// DropObject
///     -> Network-parented to this player's NetworkObject.
///     -> Visually follows the non-networked _holdSocket.
///
/// All pickup/drop decisions are server-authoritative.
/// </summary>
public class DropController : NetworkBehaviour, IInitializable
{
    [Header("References")]
    [SerializeField] InputManager _inputManager;
    [SerializeField] Transform _pickupPoint;
    [SerializeField] Transform _holdSocket;

    [Header("Pickup")]
    [SerializeField] float _pickupRadius = 2f;
    [SerializeField] LayerMask _droppableLayers;

    [Header("Poop Fallback")]
    [SerializeField] GameObject _poopPrefab;
    [SerializeField] Transform _poopDropPoint;
    [SerializeField] float _poopCooldown = 10f;

    [Header("IInitializable")]
    public bool IsInitialized { get; private set; }

    public event Action OnInitializationStart;
    public event Action<bool> OnInitializationFinish;

    /// <summary>
    /// The object currently held by this player.
    /// This is a local reference on each network instance.
    /// </summary>
    public DropObject HeldObject => _heldObject;

    public bool IsHolding => _heldObject != null;

    DropObject _heldObject;
    float _nextPoopTime;

    public override void OnNetworkSpawn()
    {
        _ = InitializeAsync();
    }

    public override void OnNetworkDespawn()
    {
        if (_inputManager != null)
        {
            _inputManager.PickupPerformed -= HandlePickupPerformed;
            _inputManager.DropPerformed -= HandleDropPerformed;
        }

        base.OnNetworkDespawn();
    }

    void HandlePickupPerformed()
    {
        RequestPickupRpc();
    }

    void HandleDropPerformed()
    {
        RequestDropRpc();
    }

    // ---------------------------------------------------------------------
    // NETWORK REQUESTS
    // ---------------------------------------------------------------------

    [Rpc(SendTo.Server)]
    void RequestPickupRpc()
    {
        if (!IsServer)
            return;

        if (IsHolding)
            return;

        TryPickup();
    }

    [Rpc(SendTo.Server)]
    void RequestDropRpc()
    {
        if (!IsServer)
            return;

        if (IsHolding)
            DropHeldObject();
        else
            TryDropPoop();
    }

    // ---------------------------------------------------------------------
    // PICKUP
    // ---------------------------------------------------------------------

    void TryPickup()
    {
        if (!IsServer)
            return;

        if (_pickupPoint == null)
            return;

        Collider[] candidates = new Collider[16];

        int count = Physics.OverlapSphereNonAlloc(
            _pickupPoint.position,
            _pickupRadius,
            candidates,
            _droppableLayers);

        if (count == 0)
            return;

        DropObject closest = null;
        float closestSqrDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = candidates[i];

            if (candidate == null)
                continue;

            DropObject dropObject =
                candidate.GetComponentInParent<DropObject>();

            if (dropObject == null)
                continue;

            if (!dropObject.IsSpawned)
                continue;

            if (dropObject.State != DropObject.DropState.Available)
                continue;

            float sqrDist =
                (candidate.transform.position - _pickupPoint.position)
                .sqrMagnitude;

            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = dropObject;
            }
        }

        if (closest == null)
            return;

        // -----------------------------------------------------------------
        // NETWORK PARENTING
        //
        // Parent to the PLAYER'S NetworkObject, not the hold socket.
        // The hold socket is a regular Transform and does not need to
        // be a NetworkObject.
        // -----------------------------------------------------------------

        if (!closest.NetworkObject.TrySetParent(
                NetworkObject,
                worldPositionStays: false))
        {
            Debug.LogWarning(
                $"[{nameof(DropController)}] Failed to network-parent " +
                $"'{closest.name}' to player.",
                this);

            return;
        }

        // Set the gameplay state first.
        closest.Pickup();

        // Track the held object explicitly.
        _heldObject = closest;

        // Visually attach it to the local hold socket.
        closest.SetFollowTarget(_holdSocket);
    }

    // ---------------------------------------------------------------------
    // DROP
    // ---------------------------------------------------------------------

    void DropHeldObject()
    {
        if (!IsServer)
            return;

        if (_heldObject == null)
            return;

        DropObject held = _heldObject;

        // Clear the local reference first.
        _heldObject = null;

        // Stop following the hold socket.
        held.ClearFollowTarget();

        // Remove the network parent while preserving world position.
        held.NetworkObject.TrySetParent(
            (Transform)null,
            worldPositionStays: true);

        // Re-enable physics.
        held.Drop();
    }

    // ---------------------------------------------------------------------
    // POOP FALLBACK
    // ---------------------------------------------------------------------

    void TryDropPoop()
    {
        if (!IsServer)
            return;

        if (Time.time < _nextPoopTime)
        {
            Debug.Log(
                $"[DropController] Poop on cooldown for " +
                $"{_nextPoopTime - Time.time:F1}s more.",
                this);

            return;
        }

        if (_poopPrefab == null)
        {
            Debug.LogError(
                "[DropController] No poop prefab assigned.",
                this);

            return;
        }

        Transform spawnPoint =
            _poopDropPoint != null
                ? _poopDropPoint
                : _holdSocket;

        if (spawnPoint == null)
        {
            Debug.LogError(
                "[DropController] No poop spawn point assigned.",
                this);

            return;
        }

        GameObject poopInstance = Instantiate(
            _poopPrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        if (!poopInstance.TryGetComponent(
                out NetworkObject poopNetworkObject))
        {
            Debug.LogError(
                "[DropController] Poop prefab has no NetworkObject.",
                this);

            Destroy(poopInstance);
            return;
        }

        poopNetworkObject.Spawn();

        if (poopInstance.TryGetComponent(
                out DropObject poopDropObject))
        {
            poopDropObject.Drop();
        }

        _nextPoopTime = Time.time + _poopCooldown;
    }

    // ---------------------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------------------

    public async Awaitable InitializeAsync()
    {
        OnInitializationStart?.Invoke();

        if (_pickupPoint == null || _holdSocket == null)
        {
            Debug.LogError(
                "[DropController] Pickup point or hold socket not assigned.",
                this);

            IsInitialized = false;
            enabled = false;

            OnInitializationFinish?.Invoke(false);
            return;
        }

        if (_inputManager == null)
            _inputManager = InputManager.Instance;

        if (_inputManager == null)
        {
            Debug.LogError(
                "[DropController] No InputManager found or assigned.",
                this);

            IsInitialized = false;
            enabled = false;

            OnInitializationFinish?.Invoke(false);
            return;
        }

        // Only the owning player should process local input.
        if (IsOwner)
        {
            _inputManager.PickupPerformed += HandlePickupPerformed;
            _inputManager.DropPerformed += HandleDropPerformed;
        }

        IsInitialized = true;
        enabled = true;

        OnInitializationFinish?.Invoke(true);

        Debug.Log(
            $"[DropController] Initialized. " +
            $"NetworkObjectId: {NetworkObjectId}, " +
            $"IsOwner: {IsOwner}");
    }
}