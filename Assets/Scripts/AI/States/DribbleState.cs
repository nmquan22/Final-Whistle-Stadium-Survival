using UnityEngine;
public class DribbleState : IState
{
    readonly AIPlayer ai; float timer;
    public DribbleState(AIPlayer a){ ai = a; }
    public void Enter(){ timer = Random.Range(0.6f, 1.0f); }
    public void Tick(){
        // move v? phía khung: ví d? hư?ng t?i goal đ?i phương
        var goal = ai.isHome ? WorldContext.GoalAway : WorldContext.GoalHome;
        if (goal) ai.agent.SetDestination(goal.position);

        timer -= Time.deltaTime;
        if (timer <= 0f){
            timer = Random.Range(0.6f, 1.0f);
            if (goal) ai.KickToward(goal.position); // gentle push
        }
    }
    public void Exit(){}
}