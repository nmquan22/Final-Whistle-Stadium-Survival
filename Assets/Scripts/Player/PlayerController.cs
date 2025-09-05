using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 9f;
    public float acceleration = 20f;
    public float deceleration = 30f;
    public float rotationSpeed = 720f;

    [Header("Jump / Gravity")]
    public float gravity = -20f;
    public float jumpHeight = 1.1f;
    public float groundedStick = -2f;

    [Header("Options")]
    public bool rotateByLook = true;
    public bool strafeOnSideways = true;

    CharacterController cc;
    Vector2 moveInput, lookInput;
    bool sprinting, jumpPressed;
    Vector3 velocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    // --- Input (PlayerInput -> Send Messages) ---
    public void OnMove(InputValue v) => moveInput = v.Get<Vector2>();
    public void OnSprint(InputValue v) => sprinting = v.isPressed;
    public void OnLook(InputValue v) => lookInput = v.Get<Vector2>();
    public void OnJump(InputValue v) { if (v.isPressed) jumpPressed = true; }

    void Update()
    {
        float dt = Time.deltaTime;

        // --- Camera-relative axes ---
        Transform cam = Camera.main ? Camera.main.transform : null;
        Vector3 fwd = cam ? cam.forward : Vector3.forward;
        Vector3 right = cam ? cam.right : Vector3.right;
        fwd.y = 0; right.y = 0; fwd.Normalize(); right.Normalize();

        // --- Desired direction (input) ---
        Vector3 in2 = new Vector3(moveInput.x, 0f, moveInput.y);   // x = A/D, z = W/S
        Vector3 wishDir = right * in2.x + fwd * in2.z;
        if (wishDir.sqrMagnitude > 1e-4f) wishDir.Normalize();

        // --- Target speed ---
        float targetSpeed = (sprinting ? sprintSpeed : walkSpeed) * Mathf.Clamp01(in2.magnitude);
        Vector3 targetPlanar = wishDir * targetSpeed;

        // --- Smooth acceleration / deceleration ---
        Vector3 currentPlanar = new Vector3(velocity.x, 0, velocity.z);
        float a = (targetPlanar.magnitude > currentPlanar.magnitude) ? acceleration : deceleration;
        currentPlanar = Vector3.MoveTowards(currentPlanar, targetPlanar, a * dt);

        // --- Gravity + Jump ---
        if (cc.isGrounded)
        {
            if (velocity.y < 0f) velocity.y = groundedStick;
            if (jumpPressed)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpPressed = false;
            }
        }
        else
        {
            velocity.y += gravity * dt;
        }

        // --- Apply velocity ---
        velocity.x = currentPlanar.x;
        velocity.z = currentPlanar.z;
        cc.Move(velocity * dt);

        // --- Rotation ---
        Vector3 faceDir = currentPlanar;
        if (rotateByLook && lookInput.sqrMagnitude > 0.01f && cam != null)
        {
            Vector3 lookForward = cam.forward;
            lookForward.y = 0; lookForward.Normalize();
            faceDir = Vector3.Slerp(faceDir.sqrMagnitude > 1e-4f ? faceDir : lookForward, lookForward, 0.5f);
        }

        // Nếu chỉ nhấn ngang (A/D) và bật chế độ strafe thì không xoay
        bool onlySideways = Mathf.Abs(in2.x) > 0.01f && Mathf.Abs(in2.z) < 0.01f;
        if (onlySideways && strafeOnSideways) return;

        if (faceDir.sqrMagnitude > 1e-4f)
        {
            Quaternion targetRot = Quaternion.LookRotation(faceDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * dt);
        }
    }
}
