using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("Đây là khung thành của đội Nhà? (đội Nhà đang phòng ngự ở khung này)")]
    public bool isHomeGoal = true;

    [Tooltip("Hướng mũi tên chỉ ra ngoài sân. Dùng transform.forward nếu để trống.")]
    public Transform outward;

    [Tooltip("Chống double-count trong vài trăm ms khi bóng còn cọ vào lưới.")]
    public float rearmDelay = 0.8f;

    Collider _col;
    bool _armed = true;

    void Reset()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true; // BẮT BUỘC là trigger
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        TryScore(other);
    }

    void OnTriggerStay(Collider other)
    {
        // Dự phòng nếu Enter bị miss do timestep: vẫn cố bắt
        TryScore(other);
    }

    void TryScore(Collider other)
    {
        if (!_armed) return;

        // Lấy rigidbody/ball ở ROOT thay vì tin vào Tag của chính collider
        var rb = other.attachedRigidbody;
        if (!rb) return;

        var ball = rb.GetComponent<BallController>() ?? rb.GetComponentInParent<BallController>();
        if (!ball) return;

        // (Tuỳ chọn) Kiểm tra hướng di chuyển có lao VÀO khung không, tránh đếm khi bóng từ trong lưới lăn ra.
        Vector3 n = (outward ? outward.forward : transform.forward); // vector chỉ ra ngoài sân
        float goingIn = Vector3.Dot(ball.rb.velocity, -n); // >0 nghĩa là đang đi vào khung

        // Nếu bóng gần như đứng yên, vẫn cho ghi bàn (ball đã nằm sau vạch)
        if (goingIn <= 0f && ball.rb.velocity.sqrMagnitude > 0.02f) return;

        _armed = false; // chặn double
        GameManager.I?.Goal(homeScored: !isHomeGoal);
        Invoke(nameof(Rearm), rearmDelay);
    }

    void Rearm() => _armed = true;

    void OnValidate()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void OnDrawGizmosSelected()
    {
        // vẽ hướng OUTWARD cho dễ set
        Vector3 n = (outward ? outward.forward : transform.forward);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, n * 0.8f);
    }
}
