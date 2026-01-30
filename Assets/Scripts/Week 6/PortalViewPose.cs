using UnityEngine;

/// <summary>
/// Drives the portal's viewpoint camera pose relative to the portal.
/// Edit Offset/Euler in Inspector, or move the camera and use "Capture From Current".
/// </summary>
[DisallowMultipleComponent]
public class PortalViewPose : MonoBehaviour
{
    [Header("References")]
    public Camera viewpointCamera;

    [Header("Pose (local to the portal)")]
    public Vector3 localOffset = new Vector3(0f, 0f, 1.5f);
    public Vector3 localEuler = Vector3.zero;

    [Header("Behavior")]
    [Tooltip("If true, enforce the pose every frame. If false, only when you press 'Apply Now' (context menu).")]
    public bool applyEveryFrame = true;

    void Reset()
    {
        // sensible default
        localOffset = new Vector3(0f, 0f, 1.5f);
        localEuler = Vector3.zero;
        applyEveryFrame = true;
    }

    void LateUpdate()
    {
        if (!applyEveryFrame || !viewpointCamera) return;
        ApplyNow();
    }

    [ContextMenu("Apply Now")]
    public void ApplyNow()
    {
        if (!viewpointCamera) return;
        var t = viewpointCamera.transform;
        t.localPosition = localOffset;
        t.localRotation = Quaternion.Euler(localEuler);
    }

    [ContextMenu("Capture From Current")]
    public void CaptureFromCurrent()
    {
        if (!viewpointCamera) return;
        var t = viewpointCamera.transform;
        localOffset = t.localPosition;
        localEuler = t.localRotation.eulerAngles;
    }
}
