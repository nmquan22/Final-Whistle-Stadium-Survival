using UnityEngine;
public class WorldBootstrap : MonoBehaviour
{
    void Start(){
        // Ball t? Part C
        var ball = GameObject.FindWithTag("Ball") ?? GameObject.Find("Ball");
        if (ball){
            WorldContext.BallTransform = ball.transform;
            if (ball.TryGetComponent<Rigidbody>(out var rb)) WorldContext.BallBody = rb;
        }
        // Goals có s?n
        var gh = GameObject.Find("Goal_Home") ?? GameObject.Find("HomeGoal");
        var ga = GameObject.Find("Goal_Away") ?? GameObject.Find("AwayGoal");
        if (gh) WorldContext.GoalHome = gh.transform;
        if (ga) WorldContext.GoalAway = ga.transform;
    }
}