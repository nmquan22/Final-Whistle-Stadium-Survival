using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BallInteractor : MonoBehaviour
{
    [Header("Dribble Follow (PD)")]
    public float holdDistance = 0.6f;      // điểm giữ trước mũi chân
    public float followKp = 14f;           // Proportional gain (tăng/giảm để bám nhanh/chậm)
    public float followKd = 2.5f;          // Derivative gain (giảm rung)
    public float maxAccel = 45f;           // giới hạn gia tốc bám
    public float moveThreshold = 0.12f;    // chỉ dribble khi đang di chuyển

    [Header("Refs")]
    public Transform kickPoint;            // empty trên chân
    public BallController overrideBall;
    public Animator animator;

    [Header("Interaction")]
    public float interactRadius = 2.0f;
    public float kickForce = 12f;
    public float kickLoft = 0.6f;          // bổng nhẹ khi sút thường
    public float kickSpin = 0.0f;          // xoáy sút thường
    public float passForce = 9f;
    public float passLoft = 0.3f;
    public float passSpin = 0.0f;
    public float tapWhileDribble = 0.4f;   // chạm nhịp nhỏ
    public float dribbleTapInterval = 0.35f;
    public LayerMask ballMask = ~0;

    [Header("Aiming")]
    public bool passTowardCamera = true;
    public Transform passTarget;

    CharacterController _cc;
    BallController _ball;
    float _dribTimer;

    void Reset()
    {
        if (!kickPoint)
        {
            kickPoint = new GameObject("KickPoint").transform;
            kickPoint.SetParent(transform, false);
            kickPoint.localPosition = new Vector3(0, 0.2f, 0.45f);
        }
    }

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        _cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Input chỉ trigger hành động; tính lực để FixedUpdate xử lý
        if (Input.GetMouseButtonDown(0)) _queuedKick = true;
        if (Input.GetKeyDown(KeyCode.J)) _queuedPass = true;

        _holding = Input.GetKey(KeyCode.C); // giữ C để dribble
    }

    bool _queuedKick, _queuedPass, _holding;

    void FixedUpdate()
    {
        if (_queuedKick) { DoKick(); _queuedKick = false; }
        if (_queuedPass) { DoPass(); _queuedPass = false; }
        DoDribble(_holding);
    }

    public void DoKick()
    {
        var ball = GetBallInRange();
        if (!ball) return;

        // hướng từ chân → bóng rồi đẩy theo hướng nhìn phẳng của player
        Vector3 toBall = (ball.transform.position - KickOrigin());
        Vector3 dirFlat = transform.forward; dirFlat.y = 0f; dirFlat.Normalize();
        Vector3 impulse = dirFlat * kickForce;

        ball.Kick(impulse, kickLoft, kickSpin);
        if (animator) animator.SetTrigger("Kick");
    }

    public void DoPass()
    {
        var ball = GetBallInRange();
        if (!ball) return;

        Vector3 dir = AimDirection();
        Vector3 impulse = dir * passForce;

        ball.Kick(impulse, passLoft, passSpin);
        if (animator) animator.SetTrigger("Pass");
    }

    public void DoDribble(bool isHolding)
    {
        if (!isHolding) { _dribTimer = 0f; return; }
        var ball = GetBallInRange(); if (!ball) return;

        Vector3 planarVel = _cc ? new Vector3(_cc.velocity.x, 0f, _cc.velocity.z) : Vector3.zero;
        if (planarVel.magnitude < moveThreshold) return;

        Vector3 holdPoint = KickOrigin() + transform.forward * holdDistance;
        Vector3 ePos = (holdPoint - ball.transform.position); ePos.y = 0f;

        // PD: a = Kp*e + Kd*(v_desired - v_ball)
        Vector3 vDesired = ePos * followKp;
        Vector3 vErr = vDesired - ball.rb.velocity;
        Vector3 accel = vErr * followKd;

        // hạn chế bạo lực
        accel = Vector3.ClampMagnitude(accel, maxAccel);
        accel.y = Mathf.Clamp(accel.y, -10f, 10f);

        ball.rb.AddForce(accel, ForceMode.Acceleration);

        // Tap nhỏ theo nhịp để bóng luôn lăn phía trước
        _dribTimer -= Time.fixedDeltaTime;
        if (_dribTimer <= 0f)
        {
            Vector3 tap = transform.forward * tapWhileDribble;
            ball.Kick(tap, 0f, 0f);
            _dribTimer = dribbleTapInterval;
        }
    }

    // --- Helpers ---
    BallController GetBallInRange()
    {
        if (overrideBall) return overrideBall;
        if (_ball && (_ball.transform.position - transform.position).sqrMagnitude <= interactRadius * interactRadius)
            return _ball;

        float best = float.MaxValue; BallController bestBall = null;
        var hits = Physics.OverlapSphere(transform.position, interactRadius, ballMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            var b = h.GetComponentInParent<BallController>();
            if (!b) continue;
            float d = (b.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; bestBall = b; }
        }
        _ball = bestBall;
        return bestBall;
    }

    Vector3 KickOrigin()
    {
        return kickPoint ? kickPoint.position
            : (transform.position + transform.forward * 0.45f + Vector3.up * 0.2f);
    }

    Vector3 AimDirection()
    {
        if (passTarget)
        {
            Vector3 d = passTarget.position - KickOrigin(); d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) return d.normalized;
        }
        if (passTowardCamera && Camera.main)
        {
            Vector3 d = Camera.main.transform.forward; d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) return d.normalized;
        }
        Vector3 f = transform.forward; f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
        if (kickPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(kickPoint.position, 0.05f);
        }
    }
}
