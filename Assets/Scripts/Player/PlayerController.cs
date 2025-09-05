using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 6f, sprint = 9f, rotationSpeed = 720f;
    CharacterController cc; Vector2 moveInput; bool sprinting;

    void Awake(){ cc = GetComponent<CharacterController>(); }

    public void OnMove(InputValue v){ moveInput = v.Get<Vector2>(); }
    public void OnSprint(InputValue v){ sprinting = v.isPressed; }

    void Update(){
        Vector3 dir = new Vector3(moveInput.x, 0, moveInput.y);
        if(dir.sqrMagnitude > 0.0001f){
            float spd = sprinting ? sprint : speed;
            var cam = Camera.main ? Camera.main.transform : null;
            Vector3 world = cam ? cam.TransformDirection(dir) : dir;
            world.y = 0; world.Normalize();
            cc.Move(world * spd * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(world), rotationSpeed * Time.deltaTime);
        } else {
            cc.Move(Physics.gravity * Time.deltaTime);
        }
    }
}