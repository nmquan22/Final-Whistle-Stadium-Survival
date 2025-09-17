using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Linq;
using System.Collections.Generic;
using Cinemachine;

[DefaultExecutionOrder(-50)]
public class TeamInputLocal_Actions : MonoBehaviour
{
    [Header("Refs")]
    public PlayerInput playerInput;                   // kéo PlayerInput trên GameInput vào
    public CinemachineVirtualCamera vcam;            // VCam follow
    public Camera fallbackCamera;                    // optional khi không dùng VCam

    [Header("Action names (đúng với asset của bạn)")]
    public string moveAction = "Move";             // Vector2
    public string sprintAction = "Sprint";           // Button
    public string jumpAction = "Jump";             // Button
    public string lookAction = "Look";             // (không dùng cho di chuyển, giữ lại nếu sau này cần)

    [Header("Fallback phím (vì asset chưa có các action này)")]
    public KeyCode passKey = KeyCode.J;
    public KeyCode nextKey = KeyCode.R;
    public KeyCode prevKey = KeyCode.Q;
    public KeyCode select1Key = KeyCode.Alpha1;
    public KeyCode select2Key = KeyCode.Alpha2;
    public bool useMouseWheelToSwitch = true;
    public float passPower = 5.5f;

    [Header("VCam bones (Mixamo)")]
    public string followBone = "mixamorig:Hips";
    public string lookBone = "mixamorig:Head";

    // runtime
    InputAction aMove, aSprint, aJump;               // các action có trong asset hiện tại
    List<PlayerControllerNet> myPlayers = new();
    int active = 0;
    BallNetwork ball;
    int localTeamId = -1;

    void Awake()
    {
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        var actions = playerInput ? playerInput.actions : null;

        // Lấy action theo tên; nếu thiếu -> null (sẽ dùng fallback)
        aMove = actions?.FindAction(moveAction, throwIfNotFound: false);
        aSprint = actions?.FindAction(sprintAction, throwIfNotFound: false);
        aJump = actions?.FindAction(jumpAction, throwIfNotFound: false);

        // Server-only build thì tắt input router
        var nm = NetworkManager.Singleton;
        if (nm && nm.IsServer && !nm.IsClient) enabled = false;
    }

    void Start()
    {
        InvokeRepeating(nameof(RefreshPlayers), 0.2f, 0.5f);
        ball = FindObjectOfType<BallNetwork>();
    }

    void RefreshPlayers()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsClient) return;

        // Xác định team local 1 lần
        if (localTeamId < 0)
        {
            if (nm.IsHost && nm.ConnectedClientsIds.Count == 1) localTeamId = 0;       // solo → Home
            else
            {
                var owned = FindObjectsOfType<PlayerMeta>()
                    .FirstOrDefault(m => m && m.TryGetComponent(out NetworkObject no) && no.IsOwner);
                localTeamId = owned ? owned.teamId.Value : 0;
            }
        }

        // Lọc đúng 2 cầu thủ của đội mình (teamId)
        myPlayers = FindObjectsOfType<PlayerControllerNet>()
                    .Where(p => p && p.enabled && p.GetComponent<PlayerMeta>()?.teamId.Value == localTeamId)
                    .OrderBy(p => p.GetComponent<PlayerMeta>()?.indexInTeam.Value ?? 0)
                    .ToList();

        if (active >= myPlayers.Count) active = Mathf.Max(0, myPlayers.Count - 1);
        ApplyCameraToActive();
    }

    void Update()
    {
        if (myPlayers.Count == 0) return;

        // Đổi người (fallback phím + cuộn chuột)
        if (Input.GetKeyDown(nextKey) || (useMouseWheelToSwitch && Input.mouseScrollDelta.y > 0f)) Cycle(+1);
        if (Input.GetKeyDown(prevKey) || (useMouseWheelToSwitch && Input.mouseScrollDelta.y < 0f)) Cycle(-1);
        if (Input.GetKeyDown(select1Key)) SetActive(0);
        if (Input.GetKeyDown(select2Key)) SetActive(1);

        // ---- Đọc Move/Sprint/Jump từ Input Actions (nếu thiếu thì fallback) ----
        Vector2 mv = aMove != null ? aMove.ReadValue<Vector2>()
                                     : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        bool sprint = aSprint != null ? aSprint.IsPressed() : Input.GetKey(KeyCode.LeftShift);
        bool jump = aJump != null ? aJump.WasPressedThisFrame() : Input.GetKeyDown(KeyCode.Space);

        // Camera-relative
        Transform camT = vcam && vcam.VirtualCameraGameObject
            ? vcam.VirtualCameraGameObject.transform
            : (fallbackCamera ? fallbackCamera.transform : (Camera.main ? Camera.main.transform : null));

        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (camT) { fwd = camT.forward; right = camT.right; fwd.y = right.y = 0f; fwd.Normalize(); right.Normalize(); }
        Vector3 wish = right * mv.x + fwd * mv.y;
        Vector2 worldDir = new Vector2(wish.x, wish.z);

        // Gửi input cho cầu thủ đang active
        var ctl = myPlayers[active];
        if (ctl) ctl.SetOwnerInput(worldDir, sprint, jump);

        // Dừng các cầu thủ còn lại
        for (int i = 0; i < myPlayers.Count; i++)
            if (i != active && myPlayers[i]) myPlayers[i].SetOwnerInput(Vector2.zero, false, false);

        // Chuyền bóng (fallback J)
        if (Input.GetKeyDown(passKey) && ball && myPlayers.Count >= 2)
        {
            int target = (active + 1) % myPlayers.Count;
            var no = myPlayers[target].GetComponent<NetworkObject>();
            if (no) ball.PassToTargetServerRpc(new NetworkObjectReference(no), passPower);
        }
    }

    // ===== camera follow =====
    void Cycle(int dir) { active = (active + dir + myPlayers.Count) % myPlayers.Count; ApplyCameraToActive(); }
    void SetActive(int idx) { if (idx >= 0 && idx < myPlayers.Count) { active = idx; ApplyCameraToActive(); } }

    void ApplyCameraToActive()
    {
        if (!vcam || myPlayers.Count == 0 || !myPlayers[active]) return;
        var root = myPlayers[active].transform;

        var followT = string.IsNullOrEmpty(followBone) ? root : FindChild(root, followBone) ?? root;
        var lookT = string.IsNullOrEmpty(lookBone) ? null : (FindChild(root, lookBone) ?? root);

        vcam.Follow = followT;
        vcam.LookAt = lookT;
    }

    Transform FindChild(Transform r, string n)
    {
        foreach (var t in r.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }
}
