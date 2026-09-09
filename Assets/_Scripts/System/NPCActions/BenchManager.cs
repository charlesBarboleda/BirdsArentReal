using UnityEngine;

/// <summary>
/// Attach to every bench in the world. Populate the "Slots" array (on the
/// base class) with one child transform per seat, positioned and rotated
/// where an NPC should sit and face. Optionally wire up "Seat Animators",
/// index-matched to the slots, if you want sitting to trigger an animation.
/// </summary>
public class BenchManager : InteractableStationBase
{
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    [Header("Bench")]
    [Tooltip("Optional, index-matched to the Slots array above. Leave empty if benches don't need a sit animation.")]
    [SerializeField] Animator[] _seatAnimators;

    public override StationType StationType => StationType.Bench;

    public override void OnNPCEnter(NPCActionController npc, Transform slot)
    {
        base.OnNPCEnter(npc, slot);
        GetSeatAnimator(slot).SetBool(IsSittingHash, true);
    }

    public override void OnNPCExit(NPCActionController npc, Transform slot)
    {
        base.OnNPCExit(npc, slot);
        GetSeatAnimator(slot).SetBool(IsSittingHash, false);
    }

    Animator GetSeatAnimator(Transform slot)
    {
        if (_seatAnimators == null || _seatAnimators.Length == 0) return null;

        int index = System.Array.IndexOf(_slots, slot);
        if (index < 0 || index >= _seatAnimators.Length) return null;

        return _seatAnimators[index];
    }
}