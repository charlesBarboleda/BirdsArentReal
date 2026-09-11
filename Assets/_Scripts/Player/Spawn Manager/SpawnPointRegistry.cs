using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Server-side registry of available spawn points. Hands them out round-robin
/// (wrapping once exhausted) so concurrent connections don't stack on the same spot.
/// Not a NetworkBehaviour — this only ever needs to run on the host/server.
/// </summary>
public class SpawnPointRegistry : MonoBehaviour
{
    [SerializeField] Transform _spawnPointContainer;
    [SerializeField] List<Transform> _spawnPoints = new();

    int _nextIndex;

    void Awake()
    {
        if (_spawnPointContainer != null && _spawnPoints.Count == 0)
        {
            foreach (Transform child in _spawnPointContainer)
                _spawnPoints.Add(child);
        }

        if (_spawnPoints.Count == 0)
            Debug.LogWarning("[SpawnPointRegistry] No spawn points assigned — falling back to Vector3.zero.", this);
    }

    public (Vector3 position, Quaternion rotation) GetSpawnPoint(ulong clientId)
    {
        if (_spawnPoints.Count == 0)
            return (Vector3.zero, Quaternion.identity);

        Transform point = _spawnPoints[_nextIndex % _spawnPoints.Count];
        _nextIndex++;

        return (point.position, point.rotation);
    }
}