using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class CameraTransitionController : NetworkBehaviour
{
    [SerializeField] RectTransform _transitionOverlay;
    [SerializeField] float _transitionDuration = 0.5f;

    bool _isTransitioning;

    public bool IsTransitioning => _isTransitioning;

    public override void OnNetworkSpawn()
    {
        _transitionOverlay = TransitionOverlayUI.Instance.TransitionPanel;

        // Start completely retracted.
        SetHeight(0f);
    }

    public IEnumerator Expand()
    {
        if (_isTransitioning)
            yield break;

        _isTransitioning = true;

        float startHeight = _transitionOverlay.rect.height;
        float targetHeight = Screen.height;

        float elapsed = 0f;

        while (elapsed < _transitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / _transitionDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            SetHeight(Mathf.Lerp(startHeight, targetHeight, t));

            yield return null;
        }

        SetHeight(targetHeight);
    }

    public IEnumerator Retract()
    {
        float startHeight = _transitionOverlay.rect.height;
        float targetHeight = 0f;

        float elapsed = 0f;

        while (elapsed < _transitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / _transitionDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            SetHeight(Mathf.Lerp(startHeight, targetHeight, t));

            yield return null;
        }

        SetHeight(targetHeight);

        _isTransitioning = false;
    }

    void SetHeight(float height)
    {
        Vector2 size = _transitionOverlay.sizeDelta;
        size.y = height;
        _transitionOverlay.sizeDelta = size;
    }
}
