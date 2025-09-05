using UnityEngine;
public class ChaseBallState : IState
{
    readonly AIPlayer ai;
    public ChaseBallState(AIPlayer a){ ai = a; }
    public void Enter(){}
    public void Tick(){
        ai.agent.SetDestination(WorldContext.BallPos);
        // đi?u ki?n k?t thúc/đ?i state: khi ch?m bóng -> Dribble/Pass/Shoot...
    }
    public void Exit(){}
}