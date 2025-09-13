using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using Cinemachine;

public class TeamInputLocal_Actions : MonoBehaviour
{
    [Header("References")]
    public PlayerInput playerInput;               // kéo PlayerInput (trên GameInput)
    public CinemachineVirtualCamera vcam;         // VCam để follow
    public Camera fallbackCamera;                 // optional

    [Header("Action names (phải trùng asset)")]
    public string moveAction = "Move";
    public string sprintAction = "Sprint";
    public string jumpAction = "Jump";
    public string passAction = "Pass";
    public string nextAction = "NextPlayer";
    public string prevAction = "PrevPlayer";
    public string select1Action = "Select1";
    public string select2Action = "Select2";

    [Header("VCam bones (Mixamo)")]
    public string followBone = "";                // "" = root, hoặc "mixamorig:Hips"
    public string lookBone = "";                // "" = null LookAt, hoặc "mixamorig:Head"

    [Header("Gameplay")]
    public float passPower = 5.5f;
    public bool blockWhenPointerOverUI = true;    // chặn input khi đang chấm vào UI

    // runtime
    readonly List<PlayerControllerNet> myPlayers = new();
    int active = 0;
    BallNetwork ball;
    int localTeamId = -1;

    // cached actions
    InputAction aMove, aSprint, aJump, aPass, aNext, aPrev, aSel1, aSel2;

    void Awake()
    {
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
        var map = playerInput ? playerInput.actions : null;
        aMove = map?[moveAction];
        aSprint = map?[sprintAction];
        aJump = map?[jumpAction];
        aPass = map?[passAction];
        aNext = !string.IsNullOrEmpty(nextAction) ? map[nextAction] : null;
        aPrev = !string.IsNullOrEmpty(prevAction) ? map[prevAction] : null;
        aSel1 = !string.IsNullOrEmpty(select1Action) ? map[select1Action] : null;
        aSel2 = !string.IsNullOrEmpty(select2Action) ? map[select2Action] : null;
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
            if (nm.IsHost && nm.ConnectedClientsIds.Count == 1)
                localTeamId = 0; // SOLO: bạn là Home
            else
            {
                var anyMine = FindObjectsOfType<PlayerMeta>()
                    .FirstOrDefault(m => m && m.TryGetComponent(out NetworkObject no) && no.IsOwner);
                localTeamId = anyMine ? anyMine.teamId.Value : 0;
            }
        }

        // Lọc cầu thủ thuộc đội của mình + controller đang bật
        myPlayers.Clear();
        foreach (var p in FindObjectsOfType<PlayerControllerNet>())
        {
            if (!p || !p.enabled) continue;
            var m = p.GetComponent<PlayerMeta>();
            if (m && m.teamId.Value == localTeamId) myPlayers.Add(p);
        }
        myPlayers.Sort((a, b) =>
            (a.GetComponent<PlayerMeta>()?.indexInTeam.Value ?? 0)
          .CompareTo(b.GetComponent<PlayerMeta>()?.indexInTeam.Value ?? 0));

        if (active >= myPlayers.Count) active = Mathf.Max(0, myPlayers.Count - 1);
        ApplyCameraToActive();
    }

    void Update()
    {
        if (myPlayers.Count == 0) return;

        if (blockWhenPointerOverUI && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            return;

        // Switch
        if (aNext != null && aNext.WasPressedThisFrame()) Cycle(+1);
        if (aPrev != null && aPrev.WasPressedThisFrame()) Cycle(-1);
        if (aSel1 != null && aSel1.WasPressedThisFrame()) SetActive(0);
        if (aSel2 != null && aSel2.WasPressedThisFrame()) SetActive(1);

        // Read actions
        Vector2 mv = aMove?.ReadValue<Vector2>() ?? Vector2.zero;
        bool isSprint = aSprint != null && aSprint.IsPressed();
        bool jump = aJump != null && aJump.WasPressedThisFrame();

        // Camera-relative world dir
        Transform camT = (vcam && vcam.VirtualCameraGameObject) ? vcam.VirtualCameraGameObject.transform
                          : (fallbackCamera ? fallbackCamera.transform
                          : (Camera.main ? Camera.main.transform : null));
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (camT) { fwd = camT.forward; right = camT.right; fwd.y = right.y = 0f; fwd.Normalize(); right.Normalize(); }
        Vector3 wish = right * mv.x + fwd * mv.y;
        Vector2 worldDir = new(wish.x, wish.z);

        // Apply to active player
        var ctl = myPlayers[active];
        if (ctl) ctl.SetOwnerInput(worldDir, isSprint, jump);

        // Zero input for others
        for (int i = 0; i < myPlayers.Count; i++)
            if (i != active && myPlayers[i]) myPlayers[i].SetOwnerInput(Vector2.zero, false, false);

        // Pass
        if (aPass != null && aPass.WasPressedThisFrame())
        {
            if (!ball) ball = FindObjectOfType<BallNetwork>();
            if (ball && myPlayers.Count >= 2)
            {
                int target = (active + 1) % myPlayers.Count;
                var no = myPlayers[target].GetComponent<NetworkObject>();
                if (no) ball.PassToTargetServerRpc(new NetworkObjectReference(no), passPower);
            }
        }
    }

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

    void ApplyCameraToActive()
    {
        if (!vcam || myPlayers.Count == 0 || !myPlayers[active]) return;
        var root = myPlayers[active].transform;

        var followT = FindChildByName(root, followBone) ?? root;
        var lookT = string.IsNullOrEmpty(lookBone) ? null : (FindChildByName(root, lookBone) ?? root);

        vcam.Follow = followT;
        vcam.LookAt = lookT; // hoặc null để Aim=Do Nothing
    }

    Transform FindChildByName(Transform root, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
