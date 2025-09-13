using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NavMeshAgent))]
public class AIPlayerController : NetworkBehaviour
{
    [Header("Tuning")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 7.5f;
    public float rotationSpeed = 720f;
    public float arriveDist = 0.25f;

    [HideInInspector] public int teamId = 1; // Away
    [HideInInspector] public bool isChaser = false;
    [HideInInspector] public Vector3 supportPoint;

    CharacterController cc;
    NavMeshAgent agent;
    AITeamManager manager;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.radius = cc.radius;
        agent.height = cc.height;
        agent.speed = sprintSpeed;
        agent.acceleration = 40f;
        agent.angularSpeed = 720f;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }
        manager = FindObjectOfType<AITeamManager>();
        if (manager) manager.Register(this);
    }

    void OnDestroy()
    {
        if (IsServer && manager) manager.Unregister(this);
    }

    void Update()
    {
        if (!IsServer) return;
        if (!manager) return;

        Vector3 target = isChaser ? manager.BallPos : supportPoint;

        // cập nhật đích navmesh
        agent.SetDestination(target);

        // velocity mong muốn trên mặt phẳng
        Vector3 wish = agent.desiredVelocity;
        wish.y = 0f;
        float dist = Vector3.Distance(transform.position, target);
        float spd = (dist > 3f ? sprintSpeed : walkSpeed);

        Vector3 move = Vector3.zero;
        if (wish.sqrMagnitude > 0.0004f)
        {
            Vector3 dir = wish.normalized;
            move = dir * spd;
            // xoay mặt
            Quaternion t = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, t, rotationSpeed * Time.deltaTime);
        }

        // Gravity nhẹ
        float vy = cc.isGrounded ? -2f : Physics.gravity.y;
        move.y = vy;

        cc.Move(move * Time.deltaTime);
        agent.nextPosition = transform.position;

        // nếu đã tới support point thì chậm lại
        if (!isChaser && dist < arriveDist) { /*idle nhẹ*/ }
    }
}
