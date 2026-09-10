using UnityEngine;

/// <summary>
/// Attach to every picnic spot in the world. Populate the "Slots" array (on the
/// base class) with child transforms positioned on the picnic blanket, each
/// rotated to face inward toward the blanket center/food.
/// </summary>
public class PicnicSpotManager : InteractableStationBase
{
    public override StationType StationType => StationType.PicnicSpot;

    public override void OnNPCEnter(NPCActionController npc, Transform slot)
    {
        base.OnNPCEnter(npc, slot);
        if (npc != null)
        {
            npc.StartGroundSitting();
        }
    }

    public override void OnNPCExit(NPCActionController npc, Transform slot)
    {
        base.OnNPCExit(npc, slot);
        if (npc != null)
        {
            npc.StopSitting();
        }
    }
}
