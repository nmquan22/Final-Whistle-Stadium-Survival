using UnityEngine;
public class GoalTrigger : MonoBehaviour
{
    public bool isHomeGoal = true;
    void OnTriggerEnter(Collider other){
        if (!other.CompareTag("Ball")) return;
        GameManager.I.Goal(homeScored: !isHomeGoal);
    }
}