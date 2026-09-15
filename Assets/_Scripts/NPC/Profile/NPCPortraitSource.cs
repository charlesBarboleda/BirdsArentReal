using UnityEngine;

public class NPCPortraitSource : MonoBehaviour
{
    [Header("Portrait Camera")]
    [SerializeField] Transform _portraitCameraAnchor;

    [Header("Portrait Look Target")]
    [SerializeField] Transform _portraitLookTarget;

    public Transform PortraitCameraAnchor => _portraitCameraAnchor;
    public Transform PortraitLookTarget => _portraitLookTarget;

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
    }
#endif
}