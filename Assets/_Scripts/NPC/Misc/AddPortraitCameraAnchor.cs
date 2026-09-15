using UnityEngine;

public class AddPortraitCameraAnchor : MonoBehaviour
{
    [SerializeField] float y = 1.605f;
    [SerializeField] float z = 0.397f;

    [ContextMenu("Add Portrait Camera Anchor")]
    public void AddAnchor()
    {
        GameObject anchor = new("PortraitCameraAnchor");
        anchor.transform.SetParent(transform);
        anchor.transform.SetLocalPositionAndRotation(new Vector3(0f, y, z), Quaternion.identity);
    }

    [ContextMenu("Add Portrait Camera Look Target")]
    public void AddLookTarget()
    {
        GameObject lookTarget = new("PortraitLookTarget");
        lookTarget.transform.SetParent(transform);
        lookTarget.transform.SetLocalPositionAndRotation(new Vector3(0f, y, 0f), Quaternion.identity);

    }

    [ContextMenu("Add Profile UI Anchor")]
    public void AddProfileUIAnchor()
    {
        GameObject anchor = new("ProfileUIAnchor");
        anchor.transform.SetParent(transform);
        anchor.transform.SetLocalPositionAndRotation(new Vector3(0f, 2f, 0f), Quaternion.identity);
    }
}
