using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;
using System.Linq;

public class AutoRigSetup : MonoBehaviour
{
    [MenuItem("Tools/Pose/Auto-Setup Rig (Arms + Head)")]
    static void AutoSetup()
    {
        var sel = Selection.activeGameObject;
        if (sel == null) { Debug.LogError("Chọn root nhân vật (có Animator Humanoid) trước."); return; }

        var animator = sel.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("Animator không phải Humanoid."); return;
        }

        // 1) RigBuilder
        var rb = sel.GetComponent<RigBuilder>();
        if (rb == null) rb = sel.AddComponent<RigBuilder>();

        // 2) Rig layer
        var rigGO = new GameObject("Rig");
        rigGO.transform.SetParent(sel.transform, false);
        var rig = rigGO.AddComponent<Rig>();

        if (!rb.layers.Any(l => l.rig == rig))
        {
            var layers = rb.layers.ToList();
            layers.Add(new RigLayer(rig));
            rb.layers = layers;
        }

        // 3) Targets / Hints
        Transform Make(string n, Vector3 localPos)
        {
            var go = new GameObject(n);
            go.transform.SetParent(rigGO.transform, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        var headAim = Make("Head_Aim_Target", new Vector3(0f, 1.6f, 0.6f));
        var lHandT = Make("L_Hand_Target", new Vector3(-0.3f, 1.2f, 0.6f));
        var rHandT = Make("R_Hand_Target", new Vector3(0.3f, 1.2f, 0.6f));
        var lHint = Make("L_Elbow_Hint", new Vector3(-0.5f, 1.1f, 0.3f));
        var rHint = Make("R_Elbow_Hint", new Vector3(0.5f, 1.1f, 0.3f));

        // 4) Lấy xương từ Humanoid
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        if (!head || !lUpper || !lLower || !lHand || !rUpper || !rLower || !rHand)
        {
            Debug.LogError("Thiếu bone Humanoid (kiểm tra Avatar)."); return;
        }

        // 5) TwoBoneIK - Left
        var lIK = rigGO.AddComponent<TwoBoneIKConstraint>();
        lIK.data.root = lUpper;
        lIK.data.mid = lLower;
        lIK.data.tip = lHand;
        lIK.data.target = lHandT;
        lIK.data.hint = lHint;
        lIK.weight = 1f;

        // 6) TwoBoneIK - Right
        var rIK = rigGO.AddComponent<TwoBoneIKConstraint>();
        rIK.data.root = rUpper;
        rIK.data.mid = rLower;
        rIK.data.tip = rHand;
        rIK.data.target = rHandT;
        rIK.data.hint = rHint;
        rIK.weight = 1f;

        // 7) MultiAimConstraint cho đầu
        var aim = rigGO.AddComponent<MultiAimConstraint>();
        var aimData = aim.data;
        aimData.constrainedObject = head;

        // chọn trục nào là "hướng nhìn" của đầu trong rig (thường +Z hoặc +X)
        // thử thay đổi nếu xoay sai
        aimData.aimAxis = MultiAimConstraintData.Axis.Z;
        aimData.upAxis = MultiAimConstraintData.Axis.Y;

        aimData.sourceObjects = new WeightedTransformArray();
        aimData.sourceObjects.Add(new WeightedTransform(headAim, 1f));
        aim.data = aimData;

        aim.weight = 1f;

        // chọn nhanh targets để thấy trong Scene
        Selection.objects = new Object[] { headAim.gameObject, lHandT.gameObject, rHandT.gameObject };

        Debug.Log("Auto-setup Rig xong. Kéo các Target vào PoseTargetsDriver2D.");
    }
}
