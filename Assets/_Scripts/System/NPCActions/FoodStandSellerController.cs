using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attach to Seller NPCs at FoodStands (ServingCounters & MarketStalls).
/// When customers are occupying the FoodStand, the Seller plays randomized
/// looping talk animations. When the FoodStand is empty, the Seller idles.
/// In networked multiplayer, the occupation check runs on the server and animates
/// through the synchronized NPCAnimationController.
/// </summary>
public class FoodStandSellerController : NetworkBehaviour
{
    [Header("Setup")]
    [SerializeField] FoodStandManager _foodStandManager;
    [SerializeField] NPCAnimationController _animationController;

    bool _isTalking;

    void Awake()
    {
        if (_animationController == null) TryGetComponent(out _animationController);
        if (_foodStandManager == null) _foodStandManager = GetComponentInParent<FoodStandManager>();
    }

    void Start()
    {
        if (_foodStandManager == null)
        {
            Debug.LogWarning($"[{nameof(FoodStandSellerController)}] No FoodStandManager found in parent for '{name}'.", this);
        }
    }

    void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;
        if (_foodStandManager == null || _animationController == null) return;

        bool isOccupied = _foodStandManager.HasOccupants;

        if (isOccupied && !_isTalking)
        {
            _isTalking = true;
            _animationController.StartContinuousTalk();
        }
        else if (!isOccupied && _isTalking)
        {
            _isTalking = false;
            _animationController.StopContinuousTalk();
        }
    }

    public override void OnNetworkDespawn()
    {
        _isTalking = false;
        if (_animationController != null)
        {
            _animationController.StopContinuousTalk();
        }

        base.OnNetworkDespawn();
    }

    void OnDisable()
    {
        _isTalking = false;
        if (_animationController != null)
        {
            _animationController.StopContinuousTalk();
        }
    }
}
