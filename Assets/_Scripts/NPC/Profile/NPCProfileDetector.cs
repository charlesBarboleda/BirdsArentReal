using Unity.Netcode;
using UnityEngine;

public class NPCProfileDetector : NetworkBehaviour
{

    [SerializeField] Camera _camera;
    [SerializeField] NPCProfileUIController _uiController;

    [SerializeField] float _maxDistance = 20f;
    [SerializeField] float _detectionRadius = 0.35f;
    [SerializeField] LayerMask _detectionLayers;
    [SerializeField] LayerMask _obstacleLayers;


    NPCProfile _currentProfile;

    public override void OnNetworkSpawn()
    {
        _uiController = NPCProfileUIController.Instance;
    }

    void Update()
    {
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

        _uiController.Show(profile);
    }

    void HideProfile()
    {
        if (_currentProfile == null)
            return;

        _currentProfile = null;

        _uiController.Hide();
    }
}