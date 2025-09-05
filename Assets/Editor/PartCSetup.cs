// Assets/Editor/PartCSetup.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Cinemachine;

public static class PartCSetup
{
    [MenuItem("Tools/Stadium Setup/Run Part C Setup")]
    public static void Run()
    {
        // --- Create/Find Ball ---
        var ball = GameObject.Find("Ball");
        if (!ball)
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); // has SphereCollider + MeshRenderer
            ball.name = "Ball";
            ball.transform.position = Vector3.zero;     // giữa sân (giả sử sân đặt quanh (0,0,0))
            ball.transform.localScale = Vector3.one * 0.22f; // đường kính 0.22m => bán kính ~0.11m
        }
        // Tag/Layer nếu có
        try { ball.tag = "Ball"; } catch { Debug.Log("ℹ️ Tag 'Ball' chưa có. Bạn có thể thêm trong Project Settings → Tags & Layers."); }
        TrySetLayer(ball, "Ball");

        // --- Rigidbody ---
        var rb = ball.GetComponent<Rigidbody>();
        if (!rb) rb = ball.AddComponent<Rigidbody>();
        rb.mass = 0.43f;
        rb.drag = 0.03f;
        rb.angularDrag = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.maxAngularVelocity = 50f;

        // --- SphereCollider + Physic Material ---
        var sc = ball.GetComponent<SphereCollider>();
        if (sc)
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/Ball_Phys.physicMaterial");
            if (!mat)
            {
                mat = new PhysicMaterial("Ball_Phys")
                {
                    bounciness = 0.3f,
                    bounceCombine = PhysicMaterialCombine.Multiply,
                    dynamicFriction = 0.2f,
                    staticFriction = 0.2f,
                    frictionCombine = PhysicMaterialCombine.Average
                };
                AssetDatabase.CreateAsset(mat, "Assets/Ball_Phys.physicMaterial");
            }
            sc.material = mat;
        }

        // --- Possession child trigger ---
        var poss = ball.transform.Find("PossessionZone");
        if (!poss)
        {
            var go = new GameObject("PossessionZone");
            go.transform.SetParent(ball.transform, false);
            var pc = go.AddComponent<SphereCollider>();
            pc.isTrigger = true;
            pc.radius = 0.35f; // 0.35–0.4m là vừa
        }

        // --- Add BallController & BallDebugKick (runtime scripts) ---
        if (!ball.GetComponent<BallController>()) ball.AddComponent<BallController>();
        if (!ball.GetComponent<BallDebugKick>()) ball.AddComponent<BallDebugKick>();

        // --- Hook Cinemachine VCam to Ball if exists ---
        var vcam = Object.FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam)
        {
            if (!vcam.Follow) vcam.Follow = ball.transform;
            if (!vcam.LookAt) vcam.LookAt = ball.transform;
        }

        // --- Inform about GoalTrigger script presence ---
        Debug.Log("ℹ️ Part C: Ball đã tạo/cấu hình. Nhớ gắn script GoalTrigger cho 2 GoalTrigger_Home/Away và gọi ScoreBoard.Instance.AddGoal(teamId) khi bóng vào lưới.");

        EditorUtility.DisplayDialog("Part C Setup", "Đã tạo/cấu hình Ball + Possession + Rigidbody + DebugKick. VCam đã follow Ball (nếu có).", "OK");
    }

    static void TrySetLayer(GameObject go, string layerName)
    {
        int idx = LayerMask.NameToLayer(layerName);
        if (idx >= 0) go.layer = idx;
        // nếu layer chưa có thì thôi, chỉ log gợi ý:
        else Debug.Log("ℹ️ Layer '" + layerName + "' chưa tồn tại. Bạn có thể thêm trong Project Settings → Tags & Layers.");
    }
}
#endif
