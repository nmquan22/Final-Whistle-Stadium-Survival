using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BallController : MonoBehaviour
{
    public Rigidbody rb;
    public float maxSpeed = 25f;
    public Vector3 startPos;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        var col = GetComponent<SphereCollider>();
        col.material = null;          // gán PhysicMaterial nếu muốn
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = 0.43f;              // bóng size 5 ~0.43kg
        rb.drag = 0.03f;              // giảm trôi lâu
        rb.angularDrag = 0.05f;
    }

    void Awake() { if (!rb) rb = GetComponent<Rigidbody>(); startPos = transform.position; }

    void FixedUpdate()
    {
        float spd = rb.velocity.magnitude;
        if (spd > maxSpeed) rb.velocity = rb.velocity.normalized * maxSpeed;
    }

    public void Kick(Vector3 impulse)
    { 
        rb.AddForce(impulse, ForceMode.Impulse);
    }

    public void ResetBall()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = startPos;
    }
}
