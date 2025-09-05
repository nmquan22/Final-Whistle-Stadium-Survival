using UnityEngine;
public class ShootState : IState
{
    readonly AIPlayer ai;
    public ShootState(AIPlayer a){ ai=a; }
    public void Enter(){}
    public void Tick(){
        var goal = ai.isHome ? WorldContext.GoalAway : WorldContext.GoalHome;
        if (goal){ ai.KickToward(goal.position); }
        // đ?i v? ReturnHome sau khi sút
    }
    public void Exit(){}
}