using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central lookup so NPCs can find a nearby, available conversation partner
/// without scanning the scene each time. Every NPCActionController
/// self-registers on enable/disable; availability itself is checked at
/// query time via IsAvailableForConversation, the same pattern
/// StationRegistry uses for station slots.
/// </summary>
public static class NPCSocialRegistry
{
    static readonly List<NPCActionController> _npcs = new();
    static readonly List<NPCActionController> _candidateBuffer = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnLoad() => _npcs.Clear();

    public static void Register(NPCActionController npc)
    {
        if (!_npcs.Contains(npc)) _npcs.Add(npc);
    }

    public static void Unregister(NPCActionController npc) => _npcs.Remove(npc);

    /// <summary>
    /// Returns a random other available NPC within maxDistance of requester
    /// (0 = no limit). Never returns the requester itself.
    /// </summary>
    public static bool TryGetRandomAvailablePartner(NPCActionController requester, float maxDistance, out NPCActionController partner)
    {
        partner = null;
        _candidateBuffer.Clear();

        bool limitDistance = maxDistance > 0f;
        float sqrMaxDistance = maxDistance * maxDistance;
        Vector3 origin = requester.transform.position;

        foreach (var npc in _npcs)
        {
            if (npc == null || npc == requester) continue;
            if (!npc.IsAvailableForConversation) continue;
            if (limitDistance && (npc.transform.position - origin).sqrMagnitude > sqrMaxDistance) continue;

            _candidateBuffer.Add(npc);
        }

        if (_candidateBuffer.Count == 0) return false;

        partner = _candidateBuffer[Random.Range(0, _candidateBuffer.Count)];
        return true;
    }
}