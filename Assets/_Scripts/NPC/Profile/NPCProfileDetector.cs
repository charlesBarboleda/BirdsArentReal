using InputSystem;
using Unity.Netcode;
using UnityEngine;

public class NPCProfileDetector : NetworkBehaviour
{
    [SerializeField] InputManager _inputManager;
    [SerializeField] Camera _camera;
    [SerializeField] NPCProfileUIController _uiController;
    [SerializeField] OrbitCameraFollow _orbitCameraFollow;

    [SerializeField] float _maxDistance = 20f;
    [SerializeField] float _detectionRadius = 0.35f;
    [SerializeField] LayerMask _detectionLayers;
    [SerializeField] LayerMask _obstacleLayers;

    [Header("Hold To Mark")]
    [SerializeField] float _markDuration = 2f;
    [SerializeField] NPCMarkProgressUI _markProgressUI;

    NPCProfile _markTarget;
    float _markProgress;
    bool _isMarking;
    bool _hasCompletedMark;

    NPCProfile _currentProfile;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        _uiController = NPCProfileUIController.Instance;
        if (_uiController == null)
        {
            Debug.LogError(
                "NPCProfileDetector could not find NPCProfileUIController.",
                this);

            return;
        }
        _uiController.AssignCamera(_camera);

        _inputManager = InputManager.Instance;
        if (_inputManager == null)
        {
            Debug.LogError(
                "NPCProfileDetector could not find InputManager.",
                this);

            return;
        }
        _inputManager.MarkStarted += BeginMarking;
        _inputManager.MarkCanceled += CancelMarking;

        _markProgressUI = NPCMarkProgressUI.Instance;
        if (_markProgressUI == null)
        {
            Debug.LogError(
                "NPCProfileDetector could not find NPCMarkProgressUI.",
                this);

            return;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        if (_inputManager != null)
        {
            _inputManager.MarkStarted -= BeginMarking;
            _inputManager.MarkCanceled -= CancelMarking;
        }

        CancelMarking();
    }

    void Update()
    {
        if (!IsOwner)
            return;

        if (_orbitCameraFollow.ViewMode != CameraViewMode.FirstPerson)
        {
            HideProfile();
            CancelMarking();
            return;
        }

        DetectNPC();
        UpdateMarkProgress();
    }

    void CompleteMarking()
    {
        if (_hasCompletedMark)
            return;

        if (_markTarget == null)
        {
            CancelMarking();
            return;
        }

        _hasCompletedMark = true;
        _isMarking = false;

        _markTarget.RequestToggleMarkRpc();

        if (_markProgressUI != null)
        {
            _markProgressUI.SetProgress(1f);
            _markProgressUI.Hide();
        }
    }

    public void CancelMarking()
    {
        _markTarget = null;
        _markProgress = 0f;
        _isMarking = false;
        _hasCompletedMark = false;

        if (_markProgressUI != null)
        {
            _markProgressUI.SetProgress(0f);
            _markProgressUI.Hide();
        }
    }

    public void BeginMarking()
    {
        if (!IsOwner)
            return;

        if (_currentProfile == null)
            return;

        if (_currentProfile.IsMarkedBy(NetworkManager.LocalClientId))
            return;

        if (_markProgressUI == null)
            return;

        _markTarget = _currentProfile;
        _markProgress = 0f;
        _isMarking = true;
        _hasCompletedMark = false;

        _markProgressUI.Show();
        _markProgressUI.SetProgress(0f);
    }

    void UpdateMarkProgress()
    {
        if (!_isMarking)
            return;

        if (_markTarget == null)
        {
            CancelMarking();
            return;
        }

        if (_currentProfile != _markTarget)
        {
            CancelMarking();
            return;
        }

        if (_markTarget.IsMarkedBy(NetworkManager.LocalClientId))
        {
            CancelMarking();
            return;
        }

        _markProgress += Time.deltaTime;

        float normalizedProgress =
            Mathf.Clamp01(_markProgress / _markDuration);

        if (_markProgressUI != null)
        {
            _markProgressUI.SetProgress(normalizedProgress);
        }

        if (_markProgress >= _markDuration)
        {
            CompleteMarking();
        }
    }

    void DetectNPC()
    {
        Ray ray = new Ray(
            _camera.transform.position,
            _camera.transform.forward);

        LayerMask raycastLayers =
            _detectionLayers | _obstacleLayers;

        if (Physics.SphereCast(
            ray,
            _detectionRadius,
            out RaycastHit hit,
            _maxDistance,
            raycastLayers))
        {
            NPCProfile profile =
                hit.collider.GetComponentInParent<NPCProfile>();

            if (profile != null)
            {
                ShowProfile(profile);
                return;
            }
        }

        HideProfile();
    }

    void ShowProfile(NPCProfile profile)
    {
        if (_currentProfile == profile)
            return;

        if (_isMarking)
        {
            CancelMarking();
        }

        _currentProfile = profile;

        _uiController.ShowAimedProfile(profile);
    }

    void HideProfile()
    {
        CancelMarking();

        if (_currentProfile == null)
            return;

        _currentProfile = null;

        _uiController.HideAimedProfile();
    }

    void TryMarkCurrentNPC()
    {
        if (_currentProfile == null)
            return;

        _currentProfile.RequestToggleMarkRpc();
    }
}