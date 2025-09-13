using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using Cinemachine;

public class TeamInputLocal : MonoBehaviour
{
    [Header("Camera (một trong hai)")]
    public CinemachineVirtualCamera vcam;      // Kéo VCam vào đây (khuyên dùng)
    public Camera fallbackCamera;              // Để trống cũng được; dùng khi không có VCam

    [Header("VCam bones (Mixamo)")]
    public string followBone = "mixamorig:Hips";
    public string lookBone = "mixamorig:Head";

    [Header("Switch player")]
    public KeyCode nextKey = KeyCode.R;
    public KeyCode prevKey = KeyCode.Q;
    public bool useMouseWheelToSwitch = true;
    public KeyCode selectFirstKey = KeyCode.Alpha1;
    public KeyCode selectSecondKey = KeyCode.Alpha2;

    [Header("Pass")]
    public KeyCode passKey = KeyCode.J;
    public float passPower = 5.5f;

    // --- runtime ---
    List<PlayerControllerNet> myPlayers = new();
    int active = 0;
    BallNetwork ball;
    int localTeamId = -1; // 0 = Home, 1 = Away

    void Start()
    {
        InvokeRepeating(nameof(RefreshPlayers), 0.2f, 0.5f);
        ball = FindObjectOfType<BallNetwork>();
    }

    void RefreshPlayers()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsClient) return;

        // 1) Xác định team của người chơi local
        var nm = NetworkManager.Singleton;
        if (localTeamId < 0)
        {
            if (nm.IsHost && nm.ConnectedClientsIds.Count == 1)
            {
                // SOLO: bạn luôn là Home
                localTeamId = 0;
            }
            else
            {
                // PvP: lấy team từ bất kỳ player mà mình own
                var anyMine = FindObjectsOfType<PlayerMeta>()
                    .FirstOrDefault(m => m && m.TryGetComponent<NetworkObject>(out var no) && no.IsOwner);
                localTeamId = anyMine ? anyMine.teamId.Value : 0; // fallback Home
            }
        }

        // 2) Lọc cầu thủ thuộc team của mình + controller đang bật
        myPlayers = FindObjectsOfType<PlayerControllerNet>()
            .Where(p =>
            {
                if (!p || !p.enabled) return false;
                var m = p.GetComponent<PlayerMeta>();
                return m && m.teamId.Value == localTeamId;
            })
            .OrderBy(p => p.GetComponent<PlayerMeta>()?.indexInTeam.Value ?? 0)
            .ToList();

        ClampActive();
        ApplyCameraToActive();
    }

    void Update()
    {
        if (myPlayers.Count == 0) return;

        // Đổi người
        if (Input.GetKeyDown(nextKey) || (useMouseWheelToSwitch && Input.GetAxis("Mouse ScrollWheel") > 0f)) Cycle(+1);
        if (Input.GetKeyDown(prevKey) || (useMouseWheelToSwitch && Input.GetAxis("Mouse ScrollWheel") < 0f)) Cycle(-1);
        if (Input.GetKeyDown(selectFirstKey)) SetActive(0);
        if (Input.GetKeyDown(selectSecondKey)) SetActive(1);

        // ---- Input di chuyển (camera-relative) + Sprint/Jump ----
        Transform camT = (vcam && vcam.VirtualCameraGameObject) ? vcam.VirtualCameraGameObject.transform
                          : (fallbackCamera ? fallbackCamera.transform
                          : (Camera.main ? Camera.main.transform : null));

        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (camT)
        {
            fwd = camT.forward; right = camT.right;
            fwd.y = 0f; right.y = 0f; fwd.Normalize(); right.Normalize();
        }

        Vector2 raw = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 wish = right * raw.x + fwd * raw.y;             // hướng thế giới (XZ)
        Vector2 worldDir = new(wish.x, wish.z);

        bool isSprinting = Input.GetKey(KeyCode.LeftShift);
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space);

        // Gửi input cho cầu thủ đang active (KHÔNG dựa vào IsOwner)
        var ctl = myPlayers[active];
        if (ctl) ctl.SetOwnerInput(worldDir, isSprinting, jumpPressed);

        // Clear input những cầu thủ còn lại
        for (int i = 0; i < myPlayers.Count; i++)
        {
            if (i == active) continue;
            var other = myPlayers[i];
            if (other) other.SetOwnerInput(Vector2.zero, false, false);
        }

        // Chuyền bóng cho đồng đội còn lại
        if (Input.GetKeyDown(passKey) && ball && myPlayers.Count >= 2)
        {
            int target = (active + 1) % myPlayers.Count;
            var targetNo = myPlayers[target].GetComponent<NetworkObject>();
            if (targetNo) ball.PassToTargetServerRpc(new NetworkObjectReference(targetNo), passPower);
        }
    }

    // ===== camera follow =====
    void Cycle(int dir)
    {
        active = (active + dir) % myPlayers.Count;
        if (active < 0) active += myPlayers.Count;
        ApplyCameraToActive();
    }
    void SetActive(int idx)
    {
        if (idx >= 0 && idx < myPlayers.Count) { active = idx; ApplyCameraToActive(); }
    }
    void ClampActive()
    {
        if (active >= myPlayers.Count) active = myPlayers.Count - 1;
        if (active < 0) active = 0;
    }

    void ApplyCameraToActive()
    {
        if (myPlayers.Count == 0 || !myPlayers[active]) return;

        // Ưu tiên Cinemachine
        if (vcam)
        {
            var root = myPlayers[active].transform;
            var followT = FindChildByName(root, followBone) ?? root;
            var lookT = FindChildByName(root, lookBone) ?? root;
            vcam.Follow = followT;
            vcam.LookAt = lookT;
        }
        else if (fallbackCamera)
        {
            var t = myPlayers[active].transform;
            fallbackCamera.transform.position = t.position + new Vector3(0, 5.5f, -7f);
            fallbackCamera.transform.LookAt(t.position + Vector3.up * 1.5f);
        }
    }

    Transform FindChildByName(Transform root, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
