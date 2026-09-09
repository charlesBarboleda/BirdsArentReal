using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central lookup so NPCs can find nearby available stations without an
/// expensive FindObjectsOfType call every time they pick a new action.
/// Stations register/unregister themselves in OnEnable/OnDisable.
/// </summary>
public static class StationRegistry
{
    static readonly Dictionary<StationType, List<InteractableStationBase>> _stations = new();
    static readonly List<InteractableStationBase> _candidateBuffer = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnLoad()
    {
        // Scene unloads already clean this up naturally (stations unregister
        // via OnDisable), but if "Enter Play Mode Without Domain Reload" is
        // on, static fields survive between Editor play sessions. This is a
        // cheap safety net against a stale reference leaking across sessions.
        _stations.Clear();
    }

    public static void Register(InteractableStationBase station)
    {
        if (!_stations.TryGetValue(station.StationType, out var list))
        {
            list = new List<InteractableStationBase>();
            _stations[station.StationType] = list;
        }

        if (!list.Contains(station)) list.Add(station);
    }

    public static void Unregister(InteractableStationBase station)
    {
        if (_stations.TryGetValue(station.StationType, out var list)) list.Remove(station);
    }

    /// <summary>
    /// Returns a random available station of the given type within maxDistance
    /// of origin (pass 0 or less to ignore distance entirely).
    /// </summary>
    public static bool TryGetRandomAvailableStation(StationType type, Vector3 origin, float maxDistance, out InteractableStationBase station)
    {
        station = null;
        _candidateBuffer.Clear();

        if (!_stations.TryGetValue(type, out var list) || list.Count == 0) return false;

        bool limitDistance = maxDistance > 0f;
        float sqrMaxDistance = maxDistance * maxDistance;

        foreach (var s in list)
        {
            if (s == null || !s.HasAvailableSlot) continue;
            if (limitDistance && (s.transform.position - origin).sqrMagnitude > sqrMaxDistance) continue;

            _candidateBuffer.Add(s);
        }

        if (_candidateBuffer.Count == 0) return false;

        station = _candidateBuffer[Random.Range(0, _candidateBuffer.Count)];
        return true;
    }
}