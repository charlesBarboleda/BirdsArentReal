using System.Collections;
using UnityEngine;

public class DropCameraController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera _dropCamera;

    [Header("UI")]
    [SerializeField] private GameObject _cameraPanel;

    [Header("Follow Settings")]
    [SerializeField] private Vector3 _cameraOffset = new Vector3(0f, 2f, -4f);
    [SerializeField] private Vector3 _lookAtOffset = new Vector3(0f, 0.5f, 0f);

    private Transform _target;

    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;

    private Coroutine _stopCoroutine;

    private bool _isInitialized;
    private bool _isFollowing;
    private bool _isFrozen;

    private void Awake()
    {
        if (_dropCamera == null)
        {
            _dropCamera = GetComponentInChildren<Camera>(true);
        }

        if (_dropCamera != null)
        {
            _originalParent = _dropCamera.transform.parent;
            _originalLocalPosition = _dropCamera.transform.localPosition;
            _originalLocalRotation = _dropCamera.transform.localRotation;
            _originalLocalScale = _dropCamera.transform.localScale;

            _dropCamera.enabled = false;
        }

        if (_cameraPanel != null)
        {
            _cameraPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Called only on the locally-owned player.
    /// Detaches the camera from the player hierarchy permanently
    /// while the player is alive.
    /// </summary>
    public void InitializeForLocalOwner()
    {
        if (_isInitialized)
            return;

        if (_dropCamera == null)
        {
            Debug.LogError($"{name}: Drop camera is not assigned.");
            return;
        }

        _isInitialized = true;

        // Preserve the camera's current world transform while detaching.
        _dropCamera.transform.SetParent(null, true);

        _dropCamera.enabled = false;

        if (_cameraPanel != null)
        {
            _cameraPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Begins following a dropped object.
    /// Must only be called on the owning client.
    /// </summary>
    public void Follow(Transform target)
    {
        if (!_isInitialized)
        {
            Debug.LogWarning($"{name}: Camera has not been initialized for the local owner.");
            return;
        }

        if (target == null)
            return;

        CancelStopCoroutine();

        // Defensive re-assertion: guarantees the camera is fully detached
        // from the player hierarchy right before it starts following,
        // regardless of what happened (or raced) between the spawn-time
        // detach in InitializeForLocalOwner() and now. NetworkBehaviour
        // OnNetworkSpawn order across components isn't guaranteed, so this
        // closes that gap instead of relying on spawn timing alone.
        if (_dropCamera.transform.parent != null)
        {
            _dropCamera.transform.SetParent(null, true);
        }

        _target = target;
        _isFollowing = true;
        _isFrozen = false;

        _dropCamera.enabled = true;

        if (_cameraPanel != null)
        {
            _cameraPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Freezes the camera at its current world-space transform.
    /// This does not depend on the target remaining alive.
    /// </summary>
    public void FreezeAtImpact()
    {
        if (!_isInitialized || _dropCamera == null)
            return;

        if (!_isFollowing)
            return;

        // First, stop following the target.
        // The camera's current world transform remains unchanged.
        _target = null;
        _isFollowing = false;
        _isFrozen = true;
    }

    /// <summary>
    /// Freezes the camera and disables it after the supplied delay.
    /// </summary>
    public void StopAfterImpact(float delay)
    {
        if (!_isInitialized)
            return;

        FreezeAtImpact();

        CancelStopCoroutine();
        _stopCoroutine = StartCoroutine(StopAfterDelayCoroutine(delay));
    }

    private IEnumerator StopAfterDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        StopCamera();
        _stopCoroutine = null;
    }

    /// <summary>
    /// Immediately stops and hides the camera.
    /// </summary>
    public void StopCamera()
    {
        CancelStopCoroutine();

        _target = null;
        _isFollowing = false;
        _isFrozen = false;

        if (_dropCamera != null)
        {
            _dropCamera.enabled = false;
        }

        if (_cameraPanel != null)
        {
            _cameraPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Called when the local player's network object is despawned.
    /// </summary>
    public void CleanupLocalOwnerCamera()
    {
        if (!_isInitialized)
            return;

        StopCamera();

        if (_dropCamera != null && _originalParent != null)
        {
            _dropCamera.transform.SetParent(_originalParent, false);
            _dropCamera.transform.localPosition = _originalLocalPosition;
            _dropCamera.transform.localRotation = _originalLocalRotation;
            _dropCamera.transform.localScale = _originalLocalScale;
        }

        _isInitialized = false;
    }

    private void LateUpdate()
    {
        if (!_isInitialized || !_isFollowing || _isFrozen)
            return;

        // If the target disappears before the impact RPC arrives,
        // freeze at the camera's current world transform instead of
        // trying to access the destroyed object.
        if (_target == null)
        {
            FreezeAtImpact();
            return;
        }

        Vector3 targetPosition = _target.position;

        Vector3 desiredPosition = targetPosition + _cameraOffset;
        Vector3 lookPosition = targetPosition + _lookAtOffset;

        // World-space assignment. The camera is no longer affected
        // by the player's position or rotation.
        _dropCamera.transform.position = desiredPosition;

        Vector3 lookDirection = lookPosition - _dropCamera.transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            _dropCamera.transform.rotation =
                Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }

    private void CancelStopCoroutine()
    {
        if (_stopCoroutine == null)
            return;

        StopCoroutine(_stopCoroutine);
        _stopCoroutine = null;
    }
}