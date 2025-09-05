using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BallController : MonoBehaviour
{
    public Rigidbody rb;
    public float maxSpeed = 25f;
    public Vector3 startPos;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = 50f;
        startPos = transform.position;
    }

    void FixedUpdate()
    {
        var v = rb.velocity;
        float s = v.magnitude;
        if (s > maxSpeed) rb.velocity = v * (maxSpeed / s);
    }

    public void Kick(Vector3 force) { rb.AddForce(force, ForceMode.Impulse); }

    public void ResetBall()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = startPos;
    }
}
