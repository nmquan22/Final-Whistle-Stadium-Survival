using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PoseTargetsDriver2D : MonoBehaviour
{
    [Header("Pose Source (drag any component that implements IPoseSource2D)")]
    public MonoBehaviour sourceBehaviour;   // Kéo PoseSmoother hoặc TFLitePoseSource
    IPoseSource2D source;

    [Header("Character")]
    public Animator anim;
    public Transform characterRoot;

    [Header("Targets / Hints")]
    public Transform L_Hand_Target, R_Hand_Target;
    public Transform L_Elbow_Hint, R_Elbow_Hint;
    public Transform Head_Aim_Target;

    [Header("Mapping Options")]
    [Tooltip("Đảo trái/phải nếu đầu vào pose bị mirror")]
    public bool swapLR = false;
    [Tooltip("Độ sâu mặt phẳng trước NGỰC (m)")]
    [Range(0.1f, 1.0f)]
    public float planeDepth = 0.35f;

    [Header("Smoothing")]
    [Tooltip("Thời hằng cho SmoothDamp")]
    public float smoothTau = 0.08f;

    [Header("Confidence & Limits")]
    [Range(0, 1)] public float minScore = 0.25f; // bỏ qua kp thấp
    [Tooltip("Clamp khoảng cách vai→target: min = armLen * armMin")]
    public float armMin = 0.40f;
    [Tooltip("Clamp khoảng cách vai→target: max = armLen * armMax")]
    public float armMax = 1.10f;
    [Tooltip("Đẩy khuỷu tay lệch ngang ra ngoài hông (theo bề rộng vai)")]
    public float hintSideBias = 0.18f;

    // COCO indices
    const int NOSE = 0; const int L_SH = 5, R_SH = 6, L_EL = 7, R_EL = 8, L_WR = 9, R_WR = 10, L_HIP = 11, R_HIP = 12;

    // cached bones
    Transform _hips, _lSh, _rSh, _lElbow, _rElbow, _lHand, _rHand;

    // smoothing velocities
    readonly Dictionary<string, Vector3> vel = new();

    void Awake()
    {
        // 1) Auto-wire source nếu chưa gán
        if (sourceBehaviour == null)
        {
            var any = FindObjectsOfType<MonoBehaviour>(true).FirstOrDefault(m => m is IPoseSource2D);
            if (any != null) sourceBehaviour = any;
        }
        source = sourceBehaviour as IPoseSource2D;
        if (source == null)
        {
            Debug.LogError("PoseTargetsDriver2D: Không tìm thấy IPoseSource2D (PoseSmoother/TFLitePoseSource).");
            enabled = false; return;
        }

        // 2) Animator & root
        if (!anim) anim = GetComponentInParent<Animator>();
        if (!anim) { Debug.LogError("PoseTargetsDriver2D: Không tìm thấy Animator."); enabled = false; return; }
        if (!characterRoot) characterRoot = anim.transform;

        // 3) Cache bones (Humanoid)
        _hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        _lSh = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
        _rSh = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
        _lElbow = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _rElbow = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _lHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
        _rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);

        if (_hips == null || _lSh == null || _rSh == null || _lElbow == null || _rElbow == null || _lHand == null || _rHand == null)
            Debug.LogWarning("PoseTargetsDriver2D: Thiếu xương Humanoid (Hips/Shoulders/Elbows/Hands). Hãy kiểm tra Avatar & Optimize/Strip Bones.");
    }

    void Update()
    {
        if (source == null || !source.TryGet2D(out var kps)) return;

        // Cho phép đảo trái/phải ở tầng mapping
        int LW = swapLR ? R_WR : L_WR;
        int RW = swapLR ? L_WR : R_WR;
        int LE = swapLR ? R_EL : L_EL;
        int RE = swapLR ? L_EL : R_EL;
        int LSh = swapLR ? R_SH : L_SH;
        int RSh = swapLR ? L_SH : R_SH;

        Vector2 KP(int id) { var p = kps[id]; return new Vector2(p.x, p.y); }
        float Score(int id) => Mathf.Clamp01(kps[id].score);

        // --- shoulders & scale ---
        Vector2 shL = KP(LSh), shR = KP(RSh);
        float srcShoulder = Vector2.Distance(shL, shR);
        float dstShoulder = Vector3.Distance(_lSh.position, _rSh.position);
        float scale = srcShoulder > 1e-5f ? dstShoulder / srcShoulder : 1f;

        // --- trục tham chiếu của nhân vật ---
        var up = characterRoot.up;
        var fwd = characterRoot.forward;
        var right = characterRoot.right;

        // --- mặt phẳng trước NGỰC ---
        Vector3 chest = (_lSh.position + _rSh.position) * 0.5f;
        Vector3 origin = chest + fwd * Mathf.Clamp(planeDepth, 0.1f, 1.0f);

        // --- gốc quy chiếu 2D (dựa trên hông) ---
        Vector2 hipMid = 0.5f * (KP(L_HIP) + KP(R_HIP));

        Vector3 Map2D(Vector2 p)
        {
            // trừ đi hipMid, đảo trục y (ảnh gốc 0..1, (0,0) ở góc trên trái)
            Vector2 q = new Vector2(p.x - hipMid.x, hipMid.y - p.y);
            return origin + (q.x * scale) * right + (q.y * scale) * up;
        }

        // --- clamp khoảng cách vai→target ---
        Vector3 ClampArm(Vector3 shoulder, float armLen, Vector3 target)
        {
            float minD = armLen * armMin, maxD = armLen * armMax;
            Vector3 dir = target - shoulder; float d = dir.magnitude;
            if (d < 1e-5f) return shoulder + right * 0.01f;
            d = Mathf.Clamp(d, minD, maxD);
            return shoulder + dir.normalized * d;
        }

        float armLenL = Vector3.Distance(_lSh.position, _lElbow.position) + Vector3.Distance(_lElbow.position, _lHand.position);
        float armLenR = Vector3.Distance(_rSh.position, _rElbow.position) + Vector3.Distance(_rElbow.position, _rHand.position);

        // --- LEFT WRIST ---
        if (Score(LW) >= minScore && L_Hand_Target)
        {
            var t = ClampArm(_lSh.position, armLenL, Map2D(KP(LW)));
            Move(L_Hand_Target, t, "Lw");
            SetHandRot(L_Hand_Target, t, _lSh, L_Elbow_Hint);   // ổn định rotation
        }

        // --- RIGHT WRIST ---
        if (Score(RW) >= minScore && R_Hand_Target)
        {
            var t = ClampArm(_rSh.position, armLenR, Map2D(KP(RW)));
            Move(R_Hand_Target, t, "Rw");
            SetHandRot(R_Hand_Target, t, _rSh, R_Elbow_Hint);
        }

        // --- ELBOW HINTS (đẩy lệch ngang để tránh “ôm mặt”) ---
        if (L_Elbow_Hint)
        {
            var t = Map2D(KP(LE)) + right * (+hintSideBias * dstShoulder) + Vector3.up * 0.03f;
            Move(L_Elbow_Hint, t, "Le");
        }
        if (R_Elbow_Hint)
        {
            var t = Map2D(KP(RE)) + right * (-hintSideBias * dstShoulder) + Vector3.up * 0.03f;
            Move(R_Elbow_Hint, t, "Re");
        }

        // --- HEAD (optional) ---
        if (Head_Aim_Target)
        {
            var t = Map2D(KP(NOSE));
            Move(Head_Aim_Target, t, "Hd");
        }
    }

    // ===== Helpers =====
    void Move(Transform t, Vector3 target, string key)
    {
        if (!t) return;
        if (!vel.ContainsKey(key)) vel[key] = Vector3.zero;
        var v = vel[key];
        t.position = Vector3.SmoothDamp(t.position, target, ref v, smoothTau);
        vel[key] = v;
    }

    // Khóa rotation của target theo hướng vai→target và “pole” từ elbow hint
    void SetHandRot(Transform target, Vector3 targetPos, Transform shoulder, Transform elbowHint)
    {
        if (!target || !shoulder) return;
        Vector3 dir = (targetPos - shoulder.position).normalized;
        Vector3 pole = (elbowHint ? (elbowHint.position - shoulder.position).normalized : characterRoot.right);
        // up = cross(pole, dir) để cố định roll quanh trục dir
        Vector3 up = Vector3.Cross(pole, dir);
        if (up.sqrMagnitude < 1e-6f) up = characterRoot.up; // fallback
        target.rotation = Quaternion.LookRotation(dir, up);
    }
}
