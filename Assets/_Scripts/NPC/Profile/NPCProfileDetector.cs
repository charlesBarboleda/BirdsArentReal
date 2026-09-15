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
        _inputManager.TryMarkNPC += TryMarkCurrentNPC;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        if (_inputManager != null)
        {
            _inputManager.TryMarkNPC -= TryMarkCurrentNPC;
        }
    }

    void Update()
    {
        if (!IsOwner)
            return;

        if (_orbitCameraFollow.ViewMode != CameraViewMode.FirstPerson)
        {
            HideProfile();
            return;
        }

        DetectNPC();
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

        _currentProfile = profile;

        _uiController.ShowAimedProfile(profile);
    }

    void HideProfile()
    {
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