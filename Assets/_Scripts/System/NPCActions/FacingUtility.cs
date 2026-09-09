using UnityEngine;

/// <summary>
/// Shared helper for computing a horizontal-only look rotation - used
/// anywhere an NPC needs to face a point without tilting up or down.
/// </summary>
public static class FacingUtility
{
    /// <summary>Rotation that faces "to" from "from", ignoring height. Returns fallback if the two points coincide.</summary>
    public static Quaternion LookAtFlat(Vector3 from, Vector3 to, Quaternion fallback)
    {
        Vector3 direction = to - from;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : fallback;
    }
}