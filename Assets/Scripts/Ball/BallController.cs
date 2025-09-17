using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BallController : MonoBehaviour
{
    [Header("Physics")]
    public Rigidbody rb;
    public SphereCollider sphere;
    public PhysicMaterial ballMaterial;      // gán từ Inspector (bounciness ~0.35–0.5, friction low-vừa)
    public float mass = 0.43f;               // size 5 ~0.43kg
    public float linearDrag = 0.02f;         // nhỏ để bóng lăn một lúc rồi chậm
    public float angularDrag = 0.05f;

    [Header("Limits")]
    public float maxSpeed = 28f;             // m/s ~ 100km/h (shot mạnh)
    public float maxAngularVel = 60f;        // cho phép xoáy nhanh

    [Header("Defaults")]
    public float officialRadius = 0.11f;     // ~22cm diameter
    public Vector3 startPos;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        sphere = GetComponent<SphereCollider>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.mass = mass;
        rb.drag = linearDrag;
        rb.angularDrag = angularDrag;
        rb.maxAngularVelocity = maxAngularVel;

        sphere.radius = officialRadius / Mathf.Max(transform.lossyScale.x, 0.0001f);
        if (ballMaterial) sphere.material = ballMaterial;
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (!sphere) sphere = GetComponent<SphereCollider>();
        startPos = transform.position;

        rb.mass = mass;
        rb.drag = linearDrag;
        rb.angularDrag = angularDrag;
        rb.maxAngularVelocity = maxAngularVel;
        if (ballMaterial) sphere.material = ballMaterial;
    }

    void FixedUpdate()
    {
        float spd = rb.velocity.magnitude;
        if (spd > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;

        if (rb.angularVelocity.magnitude > maxAngularVel)
            rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVel;
    }

    /// <summary>
    /// Đẩy bóng theo impulse (đã clamp ở FixedUpdate).
    /// </summary>
    public void Kick(Vector3 impulse, float loft = 0f, float spin = 0f)
    {
        // Tách impulse phẳng + độ bổng
        Vector3 flat = new Vector3(impulse.x, 0f, impulse.z);
        Vector3 k = flat + Vector3.up * loft;
        rb.AddForce(k, ForceMode.Impulse);

        // Xoáy (spin>0: topspin, <0: backspin); hướng xoáy vuông góc hướng đi phẳng
        if (spin != 0f && flat.sqrMagnitude > 1e-6f)
        {
            Vector3 sideAxis = Vector3.Cross(Vector3.up, flat.normalized); // trục tạo topspin/backspin
            rb.AddTorque(sideAxis * spin, ForceMode.Impulse);
        }
    }

    public void ResetBall()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = startPos;
    }

    public void ResetBall(Vector3 pos)
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = pos;
        startPos = pos; // để lần sau ResetBall() không tham số cũng về đúng chấm giữa
    }

}
