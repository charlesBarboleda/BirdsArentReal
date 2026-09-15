using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NPCSpawner : NetworkBehaviour
{
    [SerializeField] GameObject _npcParent;
    [SerializeField] List<GameObject> _npcPrefabs = new();
    [SerializeField] List<NAVAgentController> _npcList = new();
    [SerializeField] Vector2 _npcSpeedRange = new(1f, 3f);
    [SerializeField] int _maxNPCCount = 10;
    [SerializeField] float _spawnInterval = 5f;
    [SerializeField] List<Transform> _spawnPoints = new();

    float _spawnTimer = 0f;

    void Update()
    {
        // When NetworkManager is present, only spawn if the server/host is running
        if (NetworkManager.Singleton != null && (!NetworkManager.Singleton.IsListening || !NetworkManager.Singleton.IsServer)) return;

        _spawnTimer += Time.deltaTime;

        _npcList.RemoveAll(item => item == null);

        if (_spawnTimer >= _spawnInterval && _npcList.Count < _maxNPCCount)
        {
            SpawnNPC();
            _spawnTimer = 0f;
        }
    }

    void SpawnNPC()
    {
        if (_npcPrefabs.Count == 0 || _spawnPoints.Count == 0)
        {
            Debug.LogWarning("No NPC prefabs or spawn points assigned.");
            return;
        }

        int randomPrefabIndex = Random.Range(0, _npcPrefabs.Count);
        int randomSpawnPointIndex = Random.Range(0, _spawnPoints.Count);

        GameObject npcInstance = Instantiate(_npcPrefabs[randomPrefabIndex], _spawnPoints[randomSpawnPointIndex].position, Quaternion.identity);

        if (npcInstance.TryGetComponent<NetworkObject>(out var netObj))
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
            {
                netObj.Spawn(true);
            }

            if (_npcParent != null)
            {
                if (netObj.IsSpawned && _npcParent.TryGetComponent<NetworkObject>(out var parentNetObj))
                {
                    netObj.TrySetParent(parentNetObj);
                }
                else
                {
                    npcInstance.transform.SetParent(_npcParent.transform);
                }
            }
        }
        else if (_npcParent != null)
        {
            npcInstance.transform.SetParent(_npcParent.transform);
        }

        if (npcInstance.TryGetComponent<NAVAgentController>(out var agentController))
        {
            agentController.SetSpeed(Random.Range(_npcSpeedRange.x, _npcSpeedRange.y));
            _npcList.Add(agentController);
        }
        else
        {
            Debug.LogWarning("Spawned NPC does not have a NAVAgentController component.");
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
            else
            {
                Destroy(npcInstance);
            }
        }
    }
}
