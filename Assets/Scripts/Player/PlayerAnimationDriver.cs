using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerAnimatorDriver : MonoBehaviour
{
    CharacterController cc;
    Animator anim;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();   // <- Animator ở X Bot
    }

    void Update()
    {
        // Speed cho Blend Tree (phẳng XZ, có damping + deadzone)
        float planar = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;
        if (planar < 0.05f) planar = 0f;
        anim.SetFloat("Speed", planar, 0.1f, Time.deltaTime);

        // TEST nhanh bằng phím (dù bạn dùng Input System mới, cứ để test trước):
        if (Input.GetKeyDown(KeyCode.Space)) anim.SetTrigger("Jump");
        if (Input.GetKeyDown(KeyCode.E)) anim.SetTrigger("Pass");
        if (Input.GetMouseButtonDown(0)) anim.SetTrigger("Kick");
        anim.SetBool("IsDribbling", Input.GetKey(KeyCode.LeftShift));
    }
}
