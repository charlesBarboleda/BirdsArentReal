using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Approves incoming NGO connections and assigns each player's spawn pose via
/// SpawnPointRegistry. Must exist and be enabled before StartHost()/StartServer()
/// is called, or clients connecting in that window bypass this entirely.
/// </summary>
[RequireComponent(typeof(SpawnPointRegistry))]
public class PlayerSpawnConnectionApprover : MonoBehaviour
{
    [SerializeField] NetworkManager _networkManager;
    SpawnPointRegistry _spawnPointRegistry;

    void Awake()
    {
        if (_networkManager == null)
            _networkManager = NetworkManager.Singleton;

        _spawnPointRegistry = GetComponent<SpawnPointRegistry>();
    }

    void OnEnable()
    {
        if (_networkManager == null)
        {
            Debug.LogError("[PlayerSpawnConnectionApprover] No NetworkManager found.", this);
            return;
        }

        _networkManager.NetworkConfig.ConnectionApproval = true;
        _networkManager.ConnectionApprovalCallback += ApprovalCheck;
    }

    void OnDisable()
    {
        if (_networkManager != null)
            _networkManager.ConnectionApprovalCallback -= ApprovalCheck;
    }

    void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                        NetworkManager.ConnectionApprovalResponse response)
    {
        var (position, rotation) = _spawnPointRegistry.GetSpawnPoint(request.ClientNetworkId);

        response.CreatePlayerObject = true;
        response.Position = position;
        response.Rotation = rotation;
        response.Approved = true;
        response.Pending = false;
    }
}