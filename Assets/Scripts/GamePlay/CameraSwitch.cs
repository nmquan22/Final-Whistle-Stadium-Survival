using UnityEngine;
using Cinemachine;

public class CameraSwitcher : MonoBehaviour
{
    [Header("Assign either or both")]
    public CinemachineVirtualCamera vcam;
    public Camera normalCamera;

    [Header("VCam priorities")]
    public int onPriority = 20;
    public int offPriority = 0;

    public bool UsingVCam { get; private set; } = true;

    void Awake()
    {
        ApplyState(UsingVCam, true);
    }

    public void Toggle()
    {
        SetUseVCam(!UsingVCam);
    }

    public void SetUseVCam(bool use)
    {
        if (UsingVCam == use) return;
        UsingVCam = use;
        ApplyState(UsingVCam, true);
    }

    void ApplyState(bool useVcam, bool force)
    {
        if (vcam) vcam.Priority = useVcam ? onPriority : offPriority;
        if (normalCamera) normalCamera.enabled = !useVcam;
    }
}
