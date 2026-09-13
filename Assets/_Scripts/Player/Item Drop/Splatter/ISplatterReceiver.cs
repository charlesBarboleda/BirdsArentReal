using UnityEngine;

namespace SplatterFX
{
    /// <summary>
    /// Optional: implement on a surface that needs custom logic for whether it
    /// currently accepts a splatter (already saturated, immune, etc).
    /// Surfaces without this component always accept.
    /// </summary>
    public interface ISplatterReceiver
    {
        bool CanReceiveSplatter(Vector3 worldPoint);
    }
}