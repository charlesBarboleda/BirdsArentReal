using UnityEngine;

public class NPCPortraitSource : MonoBehaviour
{
    [Header("Portrait Camera")]
    [SerializeField] Transform _portraitCameraAnchor;

    [Header("Portrait Look Target")]
    [SerializeField] Transform _portraitLookTarget;

    [Header("Profile UI")]
    [SerializeField] Transform _profileUIAnchor;

    public Transform PortraitCameraAnchor => _portraitCameraAnchor;
    public Transform PortraitLookTarget => _portraitLookTarget;
    public Transform ProfileUIAnchor => _profileUIAnchor;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_portraitCameraAnchor == null)
        {
            Transform anchor = transform.Find("PortraitCameraAnchor");

            if (anchor != null)
                _portraitCameraAnchor = anchor;
        }

        if (_portraitLookTarget == null)
        {
            Transform lookTarget = transform.Find("PortraitLookTarget");

            if (lookTarget != null)
                _portraitLookTarget = lookTarget;
        }

        if (_profileUIAnchor == null)
        {
            Transform profileUIAnchor = transform.Find("ProfileUIAnchor");

            if (profileUIAnchor != null) _profileUIAnchor = profileUIAnchor;

        }
    }
#endif
}