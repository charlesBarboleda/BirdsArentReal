using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach to an empty GameObject and size its BoxCollider to define an area
/// NPCs are allowed to wander into (a park, a plaza, etc). The collider is
/// forced to trigger-only on Awake since it's a logical area, not physical
/// geometry. Place as many of these as you have distinct walkable areas -
/// NPCs pick a random one in range each time they decide to wander.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class WanderZone : MonoBehaviour
{
    [SerializeField] BoxCollider _bounds;

    [Tooltip("How far to search on the NavMesh for a valid point near each random sample. Increase if the zone contains large non-walkable gaps (ponds, buildings).")]
    [SerializeField] float _navMeshSampleRadius = 2f;

    void Awake()
    {
        if (_bounds == null && !TryGetComponent(out _bounds))
        {
            Debug.LogError($"[{nameof(WanderZone)}] BoxCollider is missing from '{name}'. Add one or assign it in the inspector.", this);
            return;
        }

        _bounds.isTrigger = true; // this defines an area, not something to collide with
    }

    void OnEnable() => WanderZoneRegistry.Register(this);
    void OnDisable() => WanderZoneRegistry.Unregister(this);

    /// <summary>Samples a random reachable point inside this zone's box, snapped to the NavMesh.</summary>
    public bool TryGetRandomPoint(out Vector3 result)
    {
        result = transform.position;
        if (_bounds == null) return false;

        for (int i = 0; i < 5; i++)
        {
            Vector3 localPoint = _bounds.center + new Vector3(
                Random.Range(-0.5f, 0.5f) * _bounds.size.x,
                Random.Range(-0.5f, 0.5f) * _bounds.size.y,
                Random.Range(-0.5f, 0.5f) * _bounds.size.z
            );

            // TransformPoint accounts for this object's position, rotation
            // and scale, so a rotated/scaled box samples correctly.
            Vector3 worldPoint = transform.TransformPoint(localPoint);

            if (NavMesh.SamplePosition(worldPoint, out var hit, _navMeshSampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var box = _bounds != null ? _bounds : GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.DrawWireCube(box.center, box.size);
    }
#endif
}