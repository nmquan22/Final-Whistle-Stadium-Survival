using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using Cinemachine;

public class TeamInputLocal : MonoBehaviour
{
    // ===================== Camera =====================
    [Header("Camera (một trong hai)")]
    public CinemachineVirtualCamera vcam;      // Kéo VCam vào đây (khuyên dùng)
    public Camera fallbackCamera;              // Nếu không có VCam

    [Header("VCam bones (Mixamo)")]
    public string followBone = "mixamorig:Hips";
    public string lookBone = "mixamorig:Head";

    // ===================== Switch player =====================
    [Header("Switch player")]
    public KeyCode nextKey = KeyCode.R;
    public KeyCode prevKey = KeyCode.Q;
    public bool useMouseWheelToSwitch = true;
    public KeyCode selectFirstKey = KeyCode.Alpha1;
    public KeyCode selectSecondKey = KeyCode.Alpha2;

    // ===================== Keyboard Pass =====================
    [Header("Pass by keyboard")]
    public KeyCode passKey = KeyCode.J;
    public float passPower = 5.5f;

    // ===================== Hand Input (no PlayerFromHand) =====================
    public enum InputMode { Keyboard, Hand, Both }
    public enum ControlPolicy { LastActiveWins, HandPriority, KeyboardPriority, MixedAverage }

    [Header("Hand Input (merge cùng Keyboard)")]
    public InputMode inputMode = InputMode.Both;                 // Giữ cả bàn phím & hand
    public ControlPolicy controlPolicy = ControlPolicy.LastActiveWins;
    public HandGestureDetector hand;                             // Kéo HandGestureDetector (scene)
    [Range(0f, 0.5f)] public float handDeadzone = 0.06f;         // deadzone joystick từ cổ tay
    public float handMoveGain = 1.0f;                            // độ nhạy move từ tay
    [Tooltip("Thời gian giữ quyền lái sau lần hoạt động gần nhất (LastActiveWins)")]
    public float handHoldAfterActivity = 0.6f;                   // s

    [Header("Shoot/Pass (fallback physics nếu không có RPC)")]
    public float shootRadius = 2.0f;
    public float shootForce = 8.0f;
    public float passForceLocal = 5.0f;
    public LayerMask ballMask;                                   // Layer quả bóng (cho fallback physics)

    // ===================== Logging =====================
    [Header("Logging")]
    public bool logInput = true;                 // Log vector move + nguồn chọn
    [Range(0.05f, 2f)] public float inputLogEvery = 0.5f;
    public bool logSprintToggle = true;          // Sprint ON/OFF
    public bool logShootPass = true;             // SHOOT/PASS triggers
    public bool logSwitchActive = true;          // Đổi cầu thủ
    public bool logOwnerChange = true;           // LastActiveWins: đổi quyền điều khiển
    public KeyCode forceKeyboardKey = KeyCode.K; // ép Keyboard thắng
    public KeyCode forceHandKey = KeyCode.H; // ép Hand thắng

    float _lastInputLog;
    bool _prevSprint;
    int _prevSource = -1; // 0=KB,1=Hand,2=Mixed
    int _prevActiveLogged = -1;

    // --- runtime ---
    List<PlayerControllerNet> myPlayers = new();
    int active = 0;
    BallNetwork ball;
    int localTeamId = -1; // 0 = Home, 1 = Away

    // hand edge-detect
    HandGesture _prevHandG = HandGesture.None;

    // merge-owner (LastActiveWins)
    enum ControlBy { Keyboard, Hand }
    ControlBy _owner = ControlBy.Keyboard;
    float _ownerUntil = 0f;
    ControlBy _ownerLogged = (ControlBy)(-1);

    // ===================== LIFECYCLE =====================
    void Start()
    {
        InvokeRepeating(nameof(RefreshPlayers), 0.2f, 0.5f);
        ball = FindObjectOfType<BallNetwork>();
    }

    void RefreshPlayers()
    {
        if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsClient) return;

        // 1) Team local
        var nm = NetworkManager.Singleton;
        if (localTeamId < 0)
        {
            if (nm.IsHost && nm.ConnectedClientsIds.Count == 1) localTeamId = 0;
            else
            {
                var anyMine = FindObjectsOfType<PlayerMeta>()
                    .FirstOrDefault(m => m && m.TryGetComponent<NetworkObject>(out var no) && no.IsOwner);
                localTeamId = anyMine ? anyMine.teamId.Value : 0;
            }
        }

        // 2) Lọc cầu thủ của mình
        myPlayers = FindObjectsOfType<PlayerControllerNet>()
            .Where(p =>
            {
                if (!p || !p.enabled) return false;
                var meta = p.GetComponent<PlayerMeta>();
                return meta && meta.teamId.Value == localTeamId;
            })
            .OrderBy(p => p.GetComponent<PlayerMeta>()?.indexInTeam.Value ?? 0)
            .ToList();

        ClampActive();
        ApplyCameraToActive();
    }

    void Update()
    {
        if (myPlayers.Count == 0) return;

        // ===== Đổi người =====
        bool switched = false;
        if (Input.GetKeyDown(nextKey) || (useMouseWheelToSwitch && Input.GetAxis("Mouse ScrollWheel") > 0f)) { Cycle(+1); switched = true; }
        if (Input.GetKeyDown(prevKey) || (useMouseWheelToSwitch && Input.GetAxis("Mouse ScrollWheel") < 0f)) { Cycle(-1); switched = true; }
        if (Input.GetKeyDown(selectFirstKey)) { SetActive(0); switched = true; }
        if (Input.GetKeyDown(selectSecondKey)) { SetActive(1); switched = true; }

        if (logSwitchActive && switched)
        {
            var name = SafeActiveName();
            Debug.Log($"[TeamInput] Active switched → #{active} {name}");
        }

        // ===== Camera basis =====
        Transform camT = (vcam && vcam.VirtualCameraGameObject) ? vcam.VirtualCameraGameObject.transform
                          : (fallbackCamera ? fallbackCamera.transform
                          : (Camera.main ? Camera.main.transform : null));
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (camT)
        {
            fwd = camT.forward; right = camT.right;
            fwd.y = 0f; right.y = 0f; fwd.Normalize(); right.Normalize();
        }

        // ===== Keyboard sample =====
        InputSample kb = SampleKeyboard(fwd, right);

        // ===== Hand sample =====
        InputSample hd = SampleHand(fwd, right); // nếu hand==null → valid=false

        // ===== Chính sách trộn =====
        if (inputMode == InputMode.Keyboard) hd.valid = false;
        else if (inputMode == InputMode.Hand) kb.valid = false;

        if (Input.GetKeyDown(forceKeyboardKey)) { _owner = ControlBy.Keyboard; _ownerUntil = Time.time + handHoldAfterActivity; }
        if (Input.GetKeyDown(forceHandKey)) { _owner = ControlBy.Hand; _ownerUntil = Time.time + handHoldAfterActivity; }

        InputSample use = ChooseInput(kb, hd);

        // Log đổi quyền điều khiển (LastActiveWins)
        if (logOwnerChange && controlPolicy == ControlPolicy.LastActiveWins && _owner != _ownerLogged)
        {
            _ownerLogged = _owner;
            Debug.Log($"[TeamInput] Control owner → {(_owner == ControlBy.Hand ? "Hand" : "Keyboard")}");
        }

        // ===== Đẩy input cho cầu thủ active =====
        var ctl = GetActiveController();
        if (ctl) ctl.SetOwnerInput(use.move, use.sprint, use.jump);

        // Sprint ON/OFF
        if (logSprintToggle && use.sprint != _prevSprint)
        {
            Debug.Log(use.sprint ? "[TeamInput] Sprint ON" : "[TeamInput] Sprint OFF");
            _prevSprint = use.sprint;
        }

        // Input log (throttle)
        if (logInput && Time.time - _lastInputLog >= inputLogEvery)
        {
            _lastInputLog = Time.time;
            string src = SourceName(use.source);
            Debug.Log($"[TeamInput] src={src} move=({use.move.x:F2},{use.move.y:F2}) sprint={(use.sprint ? 1 : 0)}");
            _prevSource = use.source;
        }

        // Shoot (Pinch rising edge hoặc chuột trái/K)
        if (use.shoot)
        {
            if (logShootPass) Debug.Log("[TeamInput] SHOOT");
            TryShootPhysicsOrRPC();
        }

        // Pass – phím J hoặc TwoPinch
        bool passPressed = Input.GetKeyDown(passKey) || use.pass;
        if (passPressed)
        {
            if (logShootPass) Debug.Log("[TeamInput] PASS");
            if (!TryPassRPCToNext()) TryPassPhysicsForward();
        }

        // Clear input những cầu thủ còn lại
        for (int i = 0; i < myPlayers.Count; i++)
        {
            if (i == active) continue;
            var other = myPlayers[i];
            if (other) other.SetOwnerInput(Vector2.zero, false, false);
        }
    }

    // ===================== Input Sampling =====================
    struct InputSample
    {
        public Vector2 move; public bool sprint; public bool jump; public bool shoot; public bool pass;
        public bool valid; public float activity; public int source; // 0=KB,1=Hand,2=Mixed
    }

    InputSample SampleKeyboard(Vector3 fwd, Vector3 right)
    {
        InputSample s = new InputSample { source = 0 };

        Vector2 raw = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 wish = right * raw.x + fwd * raw.y;
        s.move = new Vector2(wish.x, wish.z);

        s.sprint = Input.GetKey(KeyCode.LeftShift);
        s.jump = Input.GetKeyDown(KeyCode.Space);

        // Demo: shoot chuột trái/K
        s.shoot = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.K);
        s.pass = false; // pass = J xử lý riêng

        s.valid = (raw.sqrMagnitude > 0.0001f) || s.sprint || s.jump || s.shoot;
        s.activity = s.move.magnitude + (s.shoot ? 1 : 0);
        return s;
    }

    InputSample SampleHand(Vector3 fwd, Vector3 right)
    {
        InputSample s = new InputSample { source = 1, valid = false, activity = 0f };

        if (!hand) return s;
        if (!hand.TryGet(out var g, out var palm, out var idx)) return s;

        // Joystick ảo từ cổ tay
        Vector2 axis = new(palm.x - 0.5f, 0.6f - palm.y);
        if (axis.magnitude < handDeadzone) axis = Vector2.zero;
        axis = Vector2.ClampMagnitude(axis, 1f);

        Vector3 wish = right * axis.x + fwd * axis.y;
        s.move = new Vector2(wish.x, wish.z) * handMoveGain;

        // Map gesture → action (edge detect)
        bool shootEdge = (g == HandGesture.Pinch && _prevHandG != HandGesture.Pinch);
        bool passEdge = (g == HandGesture.TwoPinch && _prevHandG != HandGesture.TwoPinch);
        _prevHandG = g;

        s.sprint = (g == HandGesture.Fist);
        s.jump = false;
        s.shoot = shootEdge;
        s.pass = passEdge;

        s.valid = (axis.sqrMagnitude > 0.0001f) || s.sprint || s.shoot || s.pass;
        s.activity = s.move.magnitude + (s.shoot ? 1 : 0) + (s.pass ? 1 : 0);
        return s;
    }

    InputSample ChooseInput(InputSample k, InputSample h)
    {
        InputSample use = default;

        switch (controlPolicy)
        {
            case ControlPolicy.HandPriority: use = h.valid ? h : k; break;
            case ControlPolicy.KeyboardPriority: use = k.valid ? k : h; break;

            case ControlPolicy.MixedAverage:
                use.source = 2;
                use.move = k.move + h.move;
                float mag = Mathf.Clamp(use.move.magnitude, 0f, 1.5f);
                if (mag > 1f) use.move = use.move.normalized;
                use.sprint = k.sprint || h.sprint;
                use.jump = k.jump || h.jump;
                use.shoot = k.shoot || h.shoot;
                use.pass = k.pass || h.pass;
                use.valid = k.valid || h.valid;
                break;

            default: // LastActiveWins
                if (h.activity > 0.001f) { _owner = ControlBy.Hand; _ownerUntil = Time.time + handHoldAfterActivity; }
                if (k.activity > 0.001f) { _owner = ControlBy.Keyboard; _ownerUntil = Time.time + handHoldAfterActivity; }
                if (Time.time > _ownerUntil)
                    _owner = h.valid ? ControlBy.Hand : ControlBy.Keyboard;

                use = (_owner == ControlBy.Hand) ? h : k;
                break;
        }

        return use;
    }

    // ===================== Shoot / Pass =====================
    bool TryPassRPCToNext()
    {
        if (!ball || myPlayers.Count < 2) return false;
        int target = (active + 1) % myPlayers.Count;
        var targetNo = myPlayers[target].GetComponent<NetworkObject>();
        if (!targetNo) return false;
        ball.PassToTargetServerRpc(new NetworkObjectReference(targetNo), passPower);
        return true;
    }

    void TryPassPhysicsForward()
    {
        var me = myPlayers[active];
        if (!me) return;
        var t = me.transform;
        var cols = Physics.OverlapSphere(t.position + t.forward * 1.0f, shootRadius, ballMask);
        foreach (var c in cols)
        {
            var r = c.attachedRigidbody; if (!r) continue;
            r.AddForce(t.forward * passForceLocal, ForceMode.VelocityChange);
            break;
        }
    }

    void TryShootPhysicsOrRPC()
    {
        var me = myPlayers[active];
        if (!me) return;
        var t = me.transform;

        var cols = Physics.OverlapSphere(t.position + t.forward * 1.0f, shootRadius, ballMask);
        Rigidbody best = null; float bestDot = 0.5f;
        foreach (var c in cols)
        {
            var r = c.attachedRigidbody; if (!r) continue;
            var dir = (r.worldCenterOfMass - t.position).normalized;
            float dot = Vector3.Dot(t.forward, dir);
            if (dot > bestDot) { bestDot = dot; best = r; }
        }
        if (best)
        {
            var dir = (best.worldCenterOfMass - t.position).normalized;
            best.AddForce((t.forward * 0.7f + dir * 0.3f).normalized * shootForce, ForceMode.VelocityChange);
        }
    }

    // ===================== Camera / Switch helpers =====================
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
        if (myPlayers.Count == 0) { active = 0; return; }
        if (active >= myPlayers.Count) active = myPlayers.Count - 1;
        if (active < 0) active = 0;
    }

    void ApplyCameraToActive()
    {
        if (myPlayers.Count == 0 || !myPlayers[active]) return;

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

    public PlayerControllerNet GetActiveController()
        => (myPlayers.Count > 0) ? myPlayers[active] : null;

    // ===================== Utils =====================
    string SourceName(int s) => (s == 0 ? "Keyboard" : s == 1 ? "Hand" : "Mixed");
    string SafeActiveName()
    {
        if (active < 0 || active >= myPlayers.Count || !myPlayers[active]) return "<none>";
        return myPlayers[active].name;
    }
}
