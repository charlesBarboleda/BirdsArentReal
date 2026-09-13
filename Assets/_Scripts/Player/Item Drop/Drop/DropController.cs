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

    [Header("Drop Camera")]
    [SerializeField] DropCameraController _dropCameraController;
    [SerializeField] float _cameraDeactivateDelayAfterImpact = 1.5f;
    DropObject _cameraTrackedObject;
    ulong _cameraTrackedClientId;

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

        if (IsOwner)
        {
            _dropCameraController.InitializeForLocalOwner();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_inputManager != null)
        {
            _inputManager.PickupPerformed -= HandlePickupPerformed;
            _inputManager.DropPerformed -= HandleDropPerformed;
        }

        if (IsOwner)
        {
            _dropCameraController.CleanupLocalOwnerCamera();
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
    void RequestDropRpc(RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        NetworkObjectReference droppedObjectReference;

        if (IsHolding)
        {
            droppedObjectReference = DropHeldObject();
        }
        else
        {
            droppedObjectReference = TryDropPoop();
        }

        // The drop may have failed, for example:
        // - No held object
        // - Poop cooldown
        // - Missing prefab
        // - Spawn failure
        if (!droppedObjectReference.TryGet(
                out NetworkObject droppedNetworkObject))
        {
            return;
        }

        DropObject droppedObject =
            droppedNetworkObject.GetComponent<DropObject>();

        if (droppedObject == null)
            return;

        // Track this object's impact on the server.
        RegisterCameraTracking(
            droppedObject,
            senderClientId);

        // Tell ONLY the player who requested the drop
        // to activate their local camera.
        ActivateDropCameraRpc(
            droppedObjectReference,
            RpcTarget.Single(
                senderClientId,
                RpcTargetUse.Temp));
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
    [Rpc(SendTo.SpecifiedInParams)]
    void StopDropCameraRpc(
        float delay,
        RpcParams rpcParams = default)
    {
        if (_dropCameraController == null)
            return;

        _dropCameraController.StopAfterImpact(delay);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void ActivateDropCameraRpc(
    NetworkObjectReference droppedObjectReference,
    RpcParams rpcParams = default)
    {
        if (!droppedObjectReference.TryGet(
                out NetworkObject droppedNetworkObject))
        {
            Debug.LogWarning(
                "[DropController] Could not resolve dropped object " +
                "on camera client.");

            return;
        }

        DropObject droppedObject =
            droppedNetworkObject.GetComponent<DropObject>();

        if (droppedObject == null)
            return;

        if (_dropCameraController == null)
        {
            Debug.LogWarning(
                "[DropController] No DropCameraController assigned.",
                this);

            return;
        }

        _dropCameraController.Follow(droppedObject.transform);
    }

    void HandleTrackedObjectImpact(Vector3 impactPoint)
    {
        if (!IsServer)
            return;

        if (_cameraTrackedObject == null)
            return;

        StopDropCameraRpc(
            _cameraDeactivateDelayAfterImpact,
            RpcTarget.Single(
                _cameraTrackedClientId,
                RpcTargetUse.Temp));

        UnregisterCameraTracking();
    }
    void RegisterCameraTracking(DropObject droppedObject, ulong clientId)
    {
        if (!IsServer)
            return;

        if (droppedObject == null)
            return;

        // Clean up any previous subscription.
        UnregisterCameraTracking();

        _cameraTrackedObject = droppedObject;
        _cameraTrackedClientId = clientId;

        droppedObject.OnImpact += HandleTrackedObjectImpact;
    }

    void UnregisterCameraTracking()
    {
        if (_cameraTrackedObject != null)
        {
            _cameraTrackedObject.OnImpact -= HandleTrackedObjectImpact;
        }

        _cameraTrackedObject = null;
    }

    NetworkObjectReference DropHeldObject()
    {
        if (!IsServer)
            return default;

        if (_heldObject == null)
            return default;

        DropObject held = _heldObject;

        NetworkObjectReference droppedObjectReference =
            held.NetworkObject;

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

        return droppedObjectReference;
    }

    // ---------------------------------------------------------------------
    // POOP FALLBACK
    // ---------------------------------------------------------------------

    NetworkObjectReference TryDropPoop()
    {
        if (!IsServer)
            return default;

        if (Time.time < _nextPoopTime)
        {
            Debug.Log(
                $"[DropController] Poop on cooldown for " +
                $"{_nextPoopTime - Time.time:F1}s more.",
                this);

            return default;
        }

        if (_poopPrefab == null)
        {
            Debug.LogError(
                "[DropController] No poop prefab assigned.",
                this);

            return default;
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

            return default;
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
            return default;
        }

        poopNetworkObject.Spawn();

        if (poopInstance.TryGetComponent(
                out DropObject poopDropObject))
        {
            poopDropObject.Drop();
        }

        _nextPoopTime = Time.time + _poopCooldown;

        return poopNetworkObject;
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