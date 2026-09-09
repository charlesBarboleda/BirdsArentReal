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
        // Hook for stand-specific behaviour later - e.g. spawn a held food
        // prop or play a "browsing" idle animation.
    }

    /// <summary>Always face the stand itself, regardless of how each slot happens to be rotated.</summary>
    public override Quaternion GetFacingRotation(Transform slot)
    {
        Vector3 direction = transform.position - slot.position;
        direction.y = 0f; // stay upright - only rotate around the vertical axis

        return direction.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(direction)
            : slot.rotation; // degenerate case (slot sitting right on the stand's pivot) - fall back
    }
}