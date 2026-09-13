using UnityEngine;

namespace SplatterFX
{
    [CreateAssetMenu(menuName = "Splatter FX/Splatter Profile", fileName = "NewSplatterProfile")]
    public class SplatterProfile : ScriptableObject
    {
        [Header("Detection")]
        [Tooltip("Radius of the overlap sphere cast at the impact point.")]
        public float splatterRadius = 1.5f;

        [Tooltip("Physics layers considered valid splatter surfaces.")]
        public LayerMask surfaceLayers = ~0;

        [Tooltip("Max surfaces coated per impact. 0 = unlimited.")]
        public int maxSurfacesPerImpact = 8;

        [Header("Decal Appearance")]
        [Tooltip("Prefabs must have a DecalProjector + SplatterDecalInstance component on the root. One is picked at random per surface hit.")]
        public GameObject[] decalPrefabs;

        [Tooltip("Offsets the decal's origin slightly off the surface along its normal, so the projector's depth range doesn't sit exactly on the surface and flicker.")]
        public float surfaceOffset = 0.02f;

        public Vector2 sizeRange = new Vector2(0.3f, 0.8f);

        [Tooltip("Random delay range (seconds) before a decal starts fading out.")]
        public Vector2 fadeDelayRange = new Vector2(8f, 15f);

        public float fadeDuration = 2f;

        [Header("Pooling")]
        [Tooltip("Decals pre-instantiated per prefab the first time that prefab is used.")]
        public int poolPrewarmCount = 16;
    }
}