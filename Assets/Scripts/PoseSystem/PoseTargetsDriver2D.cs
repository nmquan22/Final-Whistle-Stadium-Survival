using System.Collections.Generic;
using UnityEngine;

public class PoseTargetsDriver2D : MonoBehaviour
{
    public BarracudaPoseSource source;
    public Animator anim;
    public Transform characterRoot;
    public float planeDepth = 0.6f;
    public Transform L_Hand_Target, R_Hand_Target, L_Elbow_Hint, R_Elbow_Hint, Head_Aim_Target;
    public float smoothTau = 0.08f;
    Dictionary<string, Vector3> vel = new();

    const int NOSE = 0; const int L_SH = 5, R_SH = 6, L_EL = 7, R_EL = 8, L_WR = 9, R_WR = 10, L_HIP = 11, R_HIP = 12;
    Transform _hips, _lSh, _rSh;

    void Awake()
    {
        if (!anim) anim = GetComponentInParent<Animator>();
        if (!characterRoot) characterRoot = anim.transform;
        _hips = anim.GetBoneTransform(HumanBodyBones.Hips);
        _lSh = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
        _rSh = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
    }

    void LateUpdate()
    {
        if (source == null || !source.TryGet2D(out var kps)) return;
        Vector2 KP(int id) { var p = kps[id]; return new Vector2(p.x, p.y); }

        Vector2 shL = KP(L_SH), shR = KP(R_SH), hipL = KP(L_HIP), hipR = KP(R_HIP);
        Vector2 hipMid = 0.5f * (hipL + hipR);
        float srcShoulder = Vector2.Distance(shL, shR);
        float dstShoulder = Vector3.Distance(_lSh.position, _rSh.position);
        float scale = srcShoulder > 1e-5f ? dstShoulder / srcShoulder : 1f;

        var up = characterRoot.up; var fwd = characterRoot.forward; var right = Vector3.Cross(up, fwd);
        Vector3 origin = _hips.position + fwd * planeDepth;

        Vector3 Map2D(Vector2 p)
        {
            Vector2 q = new(p.x - hipMid.x, (1f - p.y) - (1f - hipMid.y));
            return origin + (q.x * scale) * right + (q.y * scale) * up;
        }

        Move(L_Hand_Target, Map2D(KP(L_WR)), "Lw");
        Move(R_Hand_Target, Map2D(KP(R_WR)), "Rw");
        Move(L_Elbow_Hint, Map2D(KP(L_EL)), "Le");
        Move(R_Elbow_Hint, Map2D(KP(R_EL)), "Re");
        Move(Head_Aim_Target, Map2D(KP(NOSE)), "Hd");
    }

    void Move(Transform t, Vector3 target, string key)
    {
        if (!t) return;
        if (!vel.ContainsKey(key)) vel[key] = Vector3.zero;
        var v = vel[key];
        t.position = Vector3.SmoothDamp(t.position, target, ref v, smoothTau);
        vel[key] = v;
    }
}
