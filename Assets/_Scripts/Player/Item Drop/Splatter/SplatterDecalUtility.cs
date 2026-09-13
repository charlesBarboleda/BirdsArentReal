using System.Collections.Generic;
using UnityEngine;
using CoreUtilities;

namespace SplatterFX
{
    /// <summary>
    /// Pure local logic: resolves which surfaces inside a sphere should be coated
    /// and spawns pooled decals for them. Contains no networking - call this
    /// identically on every client (also works fine in a singleplayer build).
    /// </summary>
    public static class SplatterDecalUtility
    {
        private static readonly Dictionary<GameObject, ComponentPool<SplatterDecalInstance>> Pools = new();
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        public static void SpawnSplatters(Vector3 impactPoint, Vector3 impactNormal, SplatterProfile profile)
        {
            if (profile == null || profile.decalPrefabs == null || profile.decalPrefabs.Length == 0) return;

            int count = Physics.OverlapSphereNonAlloc(
                impactPoint, profile.splatterRadius, OverlapBuffer, profile.surfaceLayers, QueryTriggerInteraction.Ignore);

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (profile.maxSurfacesPerImpact > 0 && spawned >= profile.maxSurfacesPerImpact) break;

                Collider surface = OverlapBuffer[i];
                if (!TryResolveSurfaceHit(impactPoint, impactNormal, surface, out Vector3 point, out Vector3 normal))
                    continue;

                if (surface.TryGetComponent(out ISplatterReceiver receiver) && !receiver.CanReceiveSplatter(point))
                    continue;

                SpawnSingleDecal(point, normal, profile);
                spawned++;
            }
        }

        private static bool TryResolveSurfaceHit(Vector3 impactPoint, Vector3 impactNormal, Collider surface, out Vector3 point, out Vector3 normal)
        {
            Vector3 closest = surface.ClosestPoint(impactPoint);

            // Impact point is on (or inside) this collider - trust the original contact
            // normal rather than raycasting from a point that's already touching it.
            if ((closest - impactPoint).sqrMagnitude < 0.0001f)
            {
                point = impactPoint;
                normal = impactNormal;
                return true;
            }

            // For other nearby surfaces caught by the sphere, raycast toward the closest
            // point to get an accurate normal for that specific surface.
            Vector3 direction = (closest - impactPoint).normalized;
            float distance = Vector3.Distance(impactPoint, closest) + 0.05f;

            if (Physics.Raycast(impactPoint, direction, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider == surface)
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }

            point = default;
            normal = default;
            return false;
        }

        private static void SpawnSingleDecal(Vector3 point, Vector3 normal, SplatterProfile profile)
        {
            GameObject prefab = profile.decalPrefabs[Random.Range(0, profile.decalPrefabs.Length)];
            ComponentPool<SplatterDecalInstance> pool = GetOrCreatePool(prefab, profile.poolPrewarmCount);
            if (pool == null) return;

            Quaternion rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            Vector3 spawnPosition = point + normal * profile.surfaceOffset;
            SplatterDecalInstance decal = pool.Get(spawnPosition, rotation);

            float size = Random.Range(profile.sizeRange.x, profile.sizeRange.y);
            float lifetime = Random.Range(profile.fadeDelayRange.x, profile.fadeDelayRange.y);
            decal.Activate(new Vector2(size, size), lifetime, profile.fadeDuration, instance => pool.Release(instance));
        }

        private static ComponentPool<SplatterDecalInstance> GetOrCreatePool(GameObject prefab, int prewarmCount)
        {
            if (Pools.TryGetValue(prefab, out ComponentPool<SplatterDecalInstance> pool))
                return pool;

            SplatterDecalInstance instanceComponent = prefab.GetComponent<SplatterDecalInstance>();
            if (instanceComponent == null)
            {
                Debug.LogError($"Splatter decal prefab '{prefab.name}' needs a {nameof(SplatterDecalInstance)} component on its root.", prefab);
                return null;
            }

            pool = new ComponentPool<SplatterDecalInstance>(instanceComponent, prewarmCount);
            Pools[prefab] = pool;
            return pool;
        }
    }
}