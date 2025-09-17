using UnityEngine;

public class HandGameInput : MonoBehaviour
{
    public HandGestureDetector detector;

    [Header("Wiring")]
    public Transform player;     // Root player
    public Transform yawPivot;   // Chỗ xoay hướng (player hoặc camera rig)

    [Header("Movement")]
    public float moveGain = 6f;  // m/s ở biên
    [Range(0f, 0.5f)] public float deadZone = 0.08f;
    public float accel = 8f, decel = 12f;

    Vector3 vel;

    void Update()
    {
        if (!detector || !detector.TryGet(out var g, out var palm, out var idxDir))
        {
            // mất tay → hãm dần
            vel = Vector3.MoveTowards(vel, Vector3.zero, decel * Time.deltaTime);
            if (player) player.position += new Vector3(vel.x, 0, vel.z) * Time.deltaTime;
            return;
        }

        // 1) Palm (wrist) → joystick ảo (trái/phải/lên/xuống so với tâm ảnh)
        Vector2 delta = new Vector2(palm.x - 0.5f, 0.5f - palm.y);
        if (delta.magnitude < deadZone) delta = Vector2.zero;
        else delta = (delta - delta.normalized * deadZone) / (1f - deadZone);

        Vector3 wish = (yawPivot.right * delta.x + yawPivot.forward * delta.y) * moveGain;
        vel = Vector3.MoveTowards(vel, wish, accel * Time.deltaTime);
        if (player) player.position += new Vector3(vel.x, 0, vel.z) * Time.deltaTime;

        // 2) Aim / xoay người từ hướng ngón trỏ
        float yaw = Mathf.Atan2(idxDir.x, -idxDir.y) * Mathf.Rad2Deg; // ảnh: y up là dương
        if (yawPivot) yawPivot.rotation = Quaternion.Euler(0f, yaw, 0f);

        // 3) Gesture → hành động
        switch (g)
        {
            case HandGesture.Pinch: OnShoot(); break;   // sút
            case HandGesture.TwoPinch: OnPass(); break;   // chuyền
            case HandGesture.Fist: OnStop(); break;   // dừng
            case HandGesture.Open: OnSprint(true); break; // chạy nhanh
            case HandGesture.Point: OnAimAssist(); break;  // hỗ trợ aim
        }
    }

    // ==== gắn vào gameplay của bạn ====
    void OnShoot() { /* TODO: trigger shoot (animation + ball force) */ }
    void OnPass() {  /* TODO: trigger pass  (find teammate + ball pass) */ }
    void OnStop() { vel = Vector3.zero; /* TODO: stop dribble / state */ }
    void OnSprint(bool on = true) { /* TODO: toggled sprint flag → tăng moveGain tạm thời */ }
    void OnAimAssist() { /* TODO: highlight mục tiêu chuyền/sút */ }
}
