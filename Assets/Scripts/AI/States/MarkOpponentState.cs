using UnityEngine;
public class MarkOpponentState : IState
{
    readonly AIPlayer ai; Transform markTarget;
    public MarkOpponentState(AIPlayer a, Transform opp){ ai=a; markTarget=opp; }
    public void Enter(){}
    public void Tick(){
        if(markTarget) ai.agent.SetDestination(markTarget.position);
    }
    public void Exit(){}
}