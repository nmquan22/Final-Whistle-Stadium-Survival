using UnityEngine;
public class GoalkeeperState : IState
{
    readonly AIPlayer ai;
    public GoalkeeperState(AIPlayer a){ ai=a; }
    public void Enter(){}
    public void Tick(){
        // đơn gi?n: bám X theo bóng, đ?ng g?n goalLine
        var g = ai.isHome ? WorldContext.GoalHome : WorldContext.GoalAway;
        if (!g) return;
        var p = g.position; p.x = WorldContext.BallPos.x;
        ai.agent.SetDestination(p);
    }
    public void Exit(){}
}