using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
public class PlayerControllerNet : NetworkBehaviour
{
    [Header("Move (Arcade)")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9.5f;
    public float rotationSpeed = 1000f;   // deg/s

    [Header("Jump / Gravity")]
    public float gravity = -20f;          // âm (rơi xuống)
    public float jumpHeight = 1.1f;
    public float groundedStick = -2f;     // giữ dính đất nhẹ

    [Header("Animator")]
    public Animator animator;             // để trống -> tự tìm
    public string speedParam = "Speed";

    // ===== Inputs từ client (Owner) =====
    // world XZ: (x, z). Viết bởi Owner (client), đọc bởi Server
    public NetworkVariable<Vector2> wishDir = new(writePerm: NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> sprint = new(writePerm: NetworkVariableWritePermission.Owner);
    // jump là "trigger". Ta dùng sequence tăng dần để bắt cạnh
    public NetworkVariable<int> jumpSeq = new(writePerm: NetworkVariableWritePermission.Owner);

    CharacterController cc;
    Vector3 velocity;               // (x,z) phẳng + y trọng lực
    int lastJumpSeqProcessed = 0;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    // Cho TeamInputLocal gọi từ client sở hữu
    public void SetOwnerInput(Vector2 worldDir, bool isSprinting, bool jumpPressed)
    {
        if (!IsOwner) return;                  // chỉ chủ sở hữu mới được ghi NV
        wishDir.Value = worldDir;              // world-space XZ (đã camera-relative)
        sprint.Value = isSprinting;
        if (jumpPressed) jumpSeq.Value = jumpSeq.Value + 1; // trigger 1 lần
    }

    void FixedUpdate()
    {
        if (!IsServer) return;                 // server mô phỏng

        float dt = Time.fixedDeltaTime;

        // --- 1) Hướng & tốc độ mục tiêu (arcade) ---
        Vector2 d = wishDir.Value;
        float mag = Mathf.Clamp01(d.magnitude);
        Vector3 dir = new(d.x, 0f, d.y);
        if (dir.sqrMagnitude > 0.0001f) dir.Normalize();

        float speed = (sprint.Value ? sprintSpeed : walkSpeed) * mag;
        Vector3 planar = dir * speed;

        // --- 2) Jump/Gravity ---
        if (cc.isGrounded)
        {
            if (velocity.y < 0f) velocity.y = groundedStick;
            if (jumpSeq.Value != lastJumpSeqProcessed)
            {
                lastJumpSeqProcessed = jumpSeq.Value;
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            velocity.y += gravity * dt;
        }

        // --- 3) Move ---
        velocity.x = planar.x;
        velocity.z = planar.z;
        cc.Move(velocity * dt);

        // --- 4) Rotate theo hướng chạy ---
        if (dir.sqrMagnitude > 0.001f)
        {
            var targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * dt);
        }

        // --- 5) Animator BlendTree ---
        if (animator && !string.IsNullOrEmpty(speedParam))
        {
            float planarSpeed = planar.magnitude;
            if (planarSpeed < 0.05f) planarSpeed = 0f; // deadzone
            animator.SetFloat(speedParam, planarSpeed, 0.08f, dt);
        }
    }
}
