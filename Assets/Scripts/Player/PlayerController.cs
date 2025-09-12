using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Move (Arcade)")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9.5f;
    public float rotationSpeed = 1000f;   // deg/s, cho cảm giác quay nhanh kiểu mobile soccer
    public bool cameraRelative = true;    // di chuyển theo hướng camera

    [Header("Jump / Gravity")]
    public float gravity = -20f;          // âm (rơi xuống)
    public float jumpHeight = 1.1f;
    public float groundedStick = -2f;     // giữ dính đất nhẹ để không bồng bềnh

    [Header("Animator (optional)")]
    public Animator animator;             // để trống -> tự tìm ở child (XBot)
    public string speedParam = "Speed";   // parameter cho Blend Tree

    // input
    Vector2 moveInput;    // x = A/D → X ; y = W/S → Z
    bool sprinting, jumpPressed;

    // state
    CharacterController cc;
    Vector3 velocity;     // (x,z)=phẳng; y=độ cao

    public float CurrentPlanarSpeed => new Vector3(velocity.x, 0f, velocity.z).magnitude;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    // ====== Input System callbacks ======
    public void OnMove(InputValue v) => moveInput = v.Get<Vector2>();
    public void OnSprint(InputValue v) => sprinting = v.isPressed;
    public void OnJump(InputValue v) { if (v.isPressed) jumpPressed = true; }

    void Update()
    {
        float dt = Time.deltaTime;

        // ---- 1) Tính trục camera-relative trên mặt phẳng XZ ----
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (cameraRelative && Camera.main)
        {
            var cam = Camera.main.transform;
            fwd = cam.forward; right = cam.right;
            fwd.y = 0f; right.y = 0f;
            fwd.Normalize(); right.Normalize();
        }

        // ---- 2) Map Vector2 -> XZ (x giữ nguyên, y -> z) ----
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);   // XZ plane
        Vector3 wishDir = right * inputDir.x + fwd * inputDir.z;
        float inputMag = Mathf.Clamp01(inputDir.magnitude);
        if (wishDir.sqrMagnitude > 0.0001f) wishDir.Normalize();

        // ---- 3) Arcade speed (không inertia: cầm là chạy, nhả là dừng) ----
        float speed = (sprinting ? sprintSpeed : walkSpeed) * inputMag;
        Vector3 planar = wishDir * speed;

        // ---- 4) Jump & Gravity (dùng CharacterController.Move) ----
        if (cc.isGrounded)
        {
            if (velocity.y < 0f) velocity.y = groundedStick;
            if (jumpPressed)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpPressed = false;
                // animator?.SetTrigger("Jump"); // nếu bạn có state Jump
            }
        }
        else
        {
            velocity.y += gravity * dt;
        }

        // ---- 5) Gộp vận tốc & Move ----
        velocity.x = planar.x;
        velocity.z = planar.z;
        cc.Move(velocity * dt);

        // ---- 6) Xoay mặt theo hướng chạy (cực responsive) ----
        if (wishDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * dt);
        }

        // ---- 7) (Optional) cập nhật Animator Speed cho BlendTree ----
        if (animator && !string.IsNullOrEmpty(speedParam))
        {
            float planarSpeed = planar.magnitude;        // dùng speed mục tiêu cho mượt
            if (planarSpeed < 0.05f) planarSpeed = 0f;   // deadzone
            animator.SetFloat(speedParam, planarSpeed, 0.08f, dt); // damping nhẹ
        }
    }
}
