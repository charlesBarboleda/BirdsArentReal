using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SplatterFX
{
    [RequireComponent(typeof(DecalProjector))]
    public class SplatterDecalInstance : MonoBehaviour
    {
        private DecalProjector _projector;
        private float _fadeDuration;
        private float _fadeTimer;
        private bool _fading;
        private Action<SplatterDecalInstance> _releaseCallback;

        private void Awake() => _projector = GetComponent<DecalProjector>();

        public void Activate(Vector2 size, float lifetime, float fadeDuration, Action<SplatterDecalInstance> releaseCallback)
        {
            Vector3 currentSize = _projector.size;
            _projector.size = new Vector3(size.x, size.y, currentSize.z);
            _projector.fadeFactor = 1f;

            _fadeDuration = Mathf.Max(0.01f, fadeDuration);
            _releaseCallback = releaseCallback;
            _fading = false;
            _fadeTimer = 0f;

            CancelInvoke(nameof(BeginFade));
            Invoke(nameof(BeginFade), Mathf.Max(0f, lifetime));
        }

        private void BeginFade() => _fading = true;

        private void Update()
        {
            if (!_fading) return;

            _fadeTimer += Time.deltaTime;
            float t = _fadeTimer / _fadeDuration;
            _projector.fadeFactor = 1f - Mathf.Clamp01(t);

            if (t >= 1f)
            {
                _fading = false;
                _releaseCallback?.Invoke(this);
            }
        }
    }
}