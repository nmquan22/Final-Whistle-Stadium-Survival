using UnityEngine;
public class ReturnHomeState : IState
{
    readonly AIPlayer ai;
    public ReturnHomeState(AIPlayer a){ ai=a; }
    public void Enter(){}
    public void Tick(){ if (ai.homeSpot) ai.agent.SetDestination(ai.homeSpot.position); }
    public void Exit(){}
}