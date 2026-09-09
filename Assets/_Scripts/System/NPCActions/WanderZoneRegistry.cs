using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central lookup for WanderZones, mirroring StationRegistry, so NPCs can
/// find a nearby wanderable area without scanning the scene each time.
/// </summary>
public static class WanderZoneRegistry
{
    static readonly List<WanderZone> _zones = new();
    static readonly List<WanderZone> _candidateBuffer = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnLoad() => _zones.Clear();

    public static void Register(WanderZone zone)
    {
        if (!_zones.Contains(zone)) _zones.Add(zone);
    }

    public static void Unregister(WanderZone zone) => _zones.Remove(zone);

    /// <summary>
    /// Returns a random registered zone within maxDistance of origin (pass 0
    /// or less to ignore distance and consider every registered zone).
    /// </summary>
    public static bool TryGetRandomZone(Vector3 origin, float maxDistance, out WanderZone zone)
    {
        zone = null;
        _candidateBuffer.Clear();

        if (_zones.Count == 0) return false;

        bool limitDistance = maxDistance > 0f;
        float sqrMaxDistance = maxDistance * maxDistance;

        foreach (var z in _zones)
        {
            if (z == null) continue;
            if (limitDistance && (z.transform.position - origin).sqrMagnitude > sqrMaxDistance) continue;

            _candidateBuffer.Add(z);
        }

        if (_candidateBuffer.Count == 0) return false;

        zone = _candidateBuffer[Random.Range(0, _candidateBuffer.Count)];
        return true;
    }
}