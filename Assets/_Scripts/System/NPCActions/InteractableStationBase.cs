using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for any world object NPCs can walk to and occupy for a while
/// (a bench, a food stand, a fire pit, etc). Handles slot reservation so
/// multiple NPCs never target the same spot, and exposes enter/exit hooks
/// for subclasses to layer on visuals (animations, props) without touching
/// the reservation logic itself.
/// </summary>
public abstract class InteractableStationBase : MonoBehaviour
{
    [Header("Slots")]
    [Tooltip("Each transform is a spot an NPC can occupy. Position = where they stand/sit, rotation = the direction they'll face once there.")]
    [SerializeField] protected Transform[] _slots;

    [Header("Timing")]
    [Tooltip("How long (seconds) an NPC occupies a slot once it arrives, picked at random within this range.")]
    [SerializeField] protected Vector2 _occupyDurationRange = new Vector2(5f, 15f);

    readonly Dictionary<int, NPCActionController> _occupants = new();

    public abstract StationType StationType { get; }
    public bool HasAvailableSlot => _slots != null && _occupants.Count < _slots.Length;
    public int OccupantCount => _occupants.Count;
    public bool HasOccupants => _occupants.Count > 0;

    protected virtual void OnEnable()
    {
        if (_slots == null || _slots.Length == 0)
        {
            Debug.LogWarning($"[{GetType().Name}] '{name}' has no slots assigned - NPCs will never be able to use it.", this);
        }

        StationRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        StationRegistry.Unregister(this);
    }

    /// <summary>Attempts to claim a free slot for the given NPC. Returns false if the station is full.</summary>
    public bool TryReserveSlot(NPCActionController npc, out Transform slot, out float occupyDuration)
    {
        slot = null;
        occupyDuration = 0f;

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;
            if (_occupants.ContainsKey(i)) continue;

            _occupants[i] = npc;
            slot = _slots[i];
            occupyDuration = Random.Range(_occupyDurationRange.x, _occupyDurationRange.y);
            return true;
        }

        return false;
    }

    /// <summary>Frees whichever slot the given NPC is holding, if any. Safe to call even if it holds none.</summary>
    public void ReleaseSlot(NPCActionController npc)
    {
        int key = -1;

        foreach (var kvp in _occupants)
        {
            if (kvp.Value != npc) continue;
            key = kvp.Key;
            break;
        }

        if (key != -1) _occupants.Remove(key);
    }

    /// <summary>Called once the NPC has physically arrived at its reserved slot. Override to play animations, spawn props, etc.</summary>
    public virtual void OnNPCEnter(NPCActionController npc, Transform slot) { }

    /// <summary>Called right before the slot is released, once the NPC's occupy duration has elapsed.</summary>
    public virtual void OnNPCExit(NPCActionController npc, Transform slot) { }

    /// <summary>
    /// The rotation an NPC should snap to once it arrives at the given slot.
    /// Defaults to the slot's own rotation (hand-set per slot in the editor -
    /// useful for benches, where a seat should face away from the bench, not
    /// toward it). Override to compute it dynamically instead, e.g. always
    /// facing the station itself.
    /// </summary>
    public virtual Quaternion GetFacingRotation(Transform slot) => slot.rotation;
}