using UnityEngine;

public class BallInteractor : MonoBehaviour
{
    [Header("Dribble Follow")]
    public float holdDistance = 0.6f;       
    public float followGain = 12f;        
    public float maxAccel = 40f;        
    public float moveThreshold = 0.15f;     
    CharacterController _cc;               

    [Header("Refs")]
    public Transform kickPoint;               // kéo empty "KickPoint" vào đây
    public BallController overrideBall;       
    public Animator animator;                 

    [Header("Interaction")]
    public float interactRadius = 2.0f;       
    public float kickForce = 12f;             
    public float passForce = 9f;              
    public float dribbleForce = 2.2f;         
    public float dribbleInterval = 0.75f;     
    public LayerMask ballMask = ~0;           

    [Header("Aiming")]
    public bool passTowardCamera = true;      
    public Transform passTarget;             

    float _dribTimer;
    BallController _ball;

    void Reset()
    {
        if (!kickPoint)
        {
            kickPoint = new GameObject("KickPoint").transform;
            kickPoint.SetParent(transform, false);
            kickPoint.localPosition = new Vector3(0, 0.25f, 0.5f);
        }
    }

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        _cc = GetComponent<CharacterController>();   

    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) DoKick();
        if (Input.GetKeyDown(KeyCode.E)) DoPass();
        if (Input.GetKey(KeyCode.C)) // đổi từ Shift sang phím C
        {
            DoDribble(true);
        }
        else
        {
            DoDribble(false);
        }
    }

    public void DoKick()
    {
        var ball = GetBallInRange();
        if (!ball) return;

        Vector3 dir = (ball.transform.position - KickOrigin()).normalized;
        dir.y = 0f;
        ball.Kick(dir * kickForce);
        if (animator) animator.SetTrigger("Kick");
    }

    public void DoPass()
    {
        var ball = GetBallInRange();
        if (!ball) return;

        Vector3 dir = AimDirection();
        ball.Kick(dir * passForce);
        if (animator) animator.SetTrigger("Pass");
    }

    public void DoDribble(bool isHolding)
    {
        if (!isHolding) { _dribTimer = 0f; return; }
        var ball = GetBallInRange(); if (!ball) return;

        // chỉ dribble khi player đang di chuyển
        Vector3 planarVel = _cc ? new Vector3(_cc.velocity.x, 0, _cc.velocity.z) : Vector3.zero;
        if (planarVel.magnitude < moveThreshold) return;

        // điểm giữ bóng trước mũi chân
        Vector3 holdPoint = KickOrigin() + transform.forward * holdDistance;
        Vector3 toTarget = (holdPoint - ball.transform.position);
        toTarget.y = 0f;

        // vận tốc mong muốn để bóng về điểm giữ
        Vector3 desiredVel = toTarget * followGain;                 
        Vector3 velError = desiredVel - ball.rb.velocity;         // cần Rigidbody public trong BallController
        Vector3 accel = Vector3.ClampMagnitude(velError, maxAccel);

        ball.rb.AddForce(accel, ForceMode.Acceleration);

        // (tuỳ chọn) chạm nhịp nhỏ để đẩy bóng lăn phía trước một tí
        _dribTimer -= Time.deltaTime;
        if (_dribTimer <= 0f)
        {
            ball.Kick(transform.forward * 0.5f); // impulse rất nhỏ
            _dribTimer = 0.35f;
        }
    }

    // --- Helpers ---
    BallController GetBallInRange()
    {
        if (overrideBall) return overrideBall;
        if (_ball && Vector3.SqrMagnitude(_ball.transform.position - transform.position) <= interactRadius * interactRadius)
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
        return kickPoint ? kickPoint.position : (transform.position + transform.forward * 0.5f + Vector3.up * 0.2f);
    }

    Vector3 AimDirection()
    {
        if (passTarget)
        {
            Vector3 d = (passTarget.position - KickOrigin()); d.y = 0f;
            if (d.sqrMagnitude > 0.001f) return d.normalized;
        }
        if (passTowardCamera && Camera.main)
        {
            Vector3 d = Camera.main.transform.forward; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) return d.normalized;
        }
        var f = transform.forward; f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
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
