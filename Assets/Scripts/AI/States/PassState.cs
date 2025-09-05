using UnityEngine;
public class PassState : IState
{
    readonly AIPlayer ai; Transform target;
    public PassState(AIPlayer a, Transform mate){ ai=a; target=mate; }
    public void Enter(){}
    public void Tick(){
        if (target){ ai.KickToward(target.position); }
        // r?i quay l?i Idle/ReturnHome
    }
    public void Exit(){}
}