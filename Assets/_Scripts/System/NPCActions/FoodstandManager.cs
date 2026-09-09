using UnityEngine;

/// <summary>
/// Attach to every food stand in the world. Populate the "Slots" array (on
/// the base class) with standing points in a loose ring around the stand,
/// each rotated to face inward toward it.
/// </summary>
public class FoodStandManager : InteractableStationBase
{
    public override StationType StationType => StationType.FoodStand;

    public override void OnNPCEnter(NPCActionController npc, Transform slot)
    {
        base.OnNPCEnter(npc, slot);
        npc.PlayTalkAnimation();
    }

    /// <summary>Always face the stand itself, regardless of how each slot happens to be rotated.</summary>
    public override Quaternion GetFacingRotation(Transform slot)
    {
        return FacingUtility.LookAtFlat(slot.position, transform.position, slot.rotation);
    }
}