using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AIPlayer : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform homeSpot;
    public bool isHome;
    public float kickPower = 8f;

    StateMachine fsm = new StateMachine();

    void Awake(){ if(!agent) agent = GetComponent<NavMeshAgent>(); }
    void Start(){ fsm.Set(new IdleState(this)); }
    void Update(){ fsm.Tick(); }

    public void KickToward(Vector3 target){
        Vector3 dir = (target - WorldContext.BallPos); dir.y = 0f;
        WorldContext.Kick(dir.normalized * kickPower);
    }
}