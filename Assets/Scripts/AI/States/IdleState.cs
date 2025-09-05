using UnityEngine;
public class IdleState : IState
{
    readonly AIPlayer ai;
    float _repathTime;
    public IdleState(AIPlayer a){ ai = a; }
    public void Enter(){ _repathTime = 0f; }
    public void Tick(){
        _repathTime -= Time.deltaTime;
        if (_repathTime <= 0f){
            _repathTime = 0.5f;
            if (ai.homeSpot) ai.agent.SetDestination(ai.homeSpot.position);
        }
        // ví d? chuy?n tr?ng thái: n?u m?nh g?n bóng nh?t -> ChaseBall (b?n s? cài ? Part F chi ti?t)
        // ai.fsm.Set(new ChaseBallState(ai));  // khi đ? vi?t logic phân công pressing
    }
    public void Exit(){}
}