using UnityEngine;

/// <summary>
/// Implemented by anything that should react when a splatter impact lands within range —
/// typically an NPC's controller. Decouples DropObject from any specific NPC implementation.
/// </summary>
public interface ISplatterReactable
{
    /// <param name="sourcePosition">World position of the splatter impact to react toward (e.g. rotate to face).</param>
    void ReactToSplatter(Vector3 sourcePosition);
}