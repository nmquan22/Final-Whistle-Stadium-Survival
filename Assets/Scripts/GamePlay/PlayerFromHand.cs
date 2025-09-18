using UnityEngine;

public class PlayerFromHand : MonoBehaviour
{
    public HandGestureDetector hand;
    public Camera cam;
    public Rigidbody rb;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float sprintMult = 1.6f;
    public float turnSpeed = 720f;
    [Range(0, 0.5f)] public float deadzone = 0.06f;
    [Range(0, 1f)] public float smooth = 0.15f;

    [Header("Action / Kick")]
    public float kickCooldown = 0.35f;
    public float kickForce = 8f;
    public float kickRadius = 2f;
    public LayerMask kickMask;

    [Header("Debug")]
    public bool logActions = true;
    public float moveLogEvery = 0.5f;

    Vector3 vel; HandGesture prev = HandGesture.None; float lastKick = -999f; float lostT = 0f;
    const float LOST_HOLD = 0.3f;
    bool sprinting = false;
    float lastMoveLog = -999f;

    void Reset() { cam = Camera.main; rb = GetComponent<Rigidbody>(); }

    void Update()
    {
        if (hand != null && hand.TryGet(out var g, out var palm, out var idx))
        {
            lostT = 0f;

            // move từ palm
            Vector2 axis = new Vector2(palm.x - 0.5f, 0.6f - palm.y);
            if (axis.magnitude < deadzone) axis = Vector2.zero;
            axis = Vector2.ClampMagnitude(axis, 1f);
            Vector3 wish = AxisToWorld(axis);
            vel = Vector3.Lerp(vel, wish, 1f - Mathf.Exp(-10f * (1f - smooth) * Time.deltaTime));

            // aim theo indexDir
            Vector3 look = AxisToWorld(new Vector2(idx.x, -idx.y)); look.y = 0;
            if (look.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(look), turnSpeed * Time.deltaTime);

            // sprint state log
            bool nowSprint = (g == HandGesture.Fist);
            if (logActions && nowSprint != sprinting)
                Debug.Log(nowSprint ? "[HandCtrl] Sprint ON (Fist)" : "[HandCtrl] Sprint OFF");
            sprinting = nowSprint;

            float spd = moveSpeed * (sprinting ? sprintMult : 1f);
            Move(spd);

            // movement log (throttle)
            if (logActions && Time.time - lastMoveLog >= moveLogEvery)
            {
                lastMoveLog = Time.time;
                Debug.Log($"[HandCtrl] Move axis=({axis.x:F2},{axis.y:F2}) vel=({vel.x:F2},{vel.z:F2}) spd={spd:F1}");
            }

            // actions
            if (g == HandGesture.Pinch && prev != HandGesture.Pinch) { if (logActions) Debug.Log("[HandCtrl] SHOOT (Pinch)"); TryKick(); }
            if (g == HandGesture.TwoPinch && prev != HandGesture.TwoPinch) { if (logActions) Debug.Log("[HandCtrl] PASS (TwoPinch)"); TryPass(); }

            prev = g;
        }
        else
        {
            if (lostT == 0f && logActions) Debug.Log("[HandCtrl] LOST hand – holding input for a moment");
            lostT += Time.deltaTime;
            if (lostT > LOST_HOLD) vel = Vector3.Lerp(vel, Vector3.zero, 10f * Time.deltaTime);
            Move(moveSpeed);
        }
    }

    Vector3 AxisToWorld(Vector2 a)
    {
        if (!cam) return new Vector3(a.x, 0, a.y);
        Vector3 f = cam.transform.forward; f.y = 0; f.Normalize();
        Vector3 r = cam.transform.right; r.y = 0; r.Normalize();
        return r * a.x + f * a.y;
    }

    void Move(float spd)
    {
        Vector3 v = vel * spd;
        if (rb) rb.velocity = new Vector3(v.x, rb.velocity.y, v.z);
        else transform.position += v * Time.deltaTime;
    }

    void TryKick()
    {
        if (Time.time - lastKick < kickCooldown) return;
        lastKick = Time.time;

        Collider[] cols = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, kickRadius, kickMask);
        Rigidbody best = null; float bestDot = 0.5f;
        foreach (var c in cols)
        {
            var r = c.attachedRigidbody; if (!r) continue;
            var dir = (r.worldCenterOfMass - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dir);
            if (dot > bestDot) { bestDot = dot; best = r; }
        }
        if (best)
        {
            var dir = (best.worldCenterOfMass - transform.position).normalized;
            best.AddForce((transform.forward * 0.7f + dir * 0.3f).normalized * kickForce, ForceMode.VelocityChange);
        }
    }

    void TryPass()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, kickRadius, kickMask);
        foreach (var c in cols)
        {
            var r = c.attachedRigidbody; if (!r) continue;
            r.AddForce(transform.forward * (kickForce * 0.6f), ForceMode.VelocityChange);
            break;
        }
    }
}
