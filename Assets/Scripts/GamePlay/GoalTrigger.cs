using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    public string teamId = "Home"; // hoặc "Away"
    bool armed = true;
    public float cooldown = 1.0f;

    void OnTriggerEnter(Collider other)
    {
        if (!armed) return;
        if (other.CompareTag("Ball"))
        {
            armed = false;
            Debug.Log($"GOAL for {teamId}!");
            // Cộng điểm
            if (ScoreBoard.Instance) ScoreBoard.Instance.AddGoal(teamId);
            // TODO: Reset đội hình / reset bóng nếu muốn
            Invoke(nameof(Rearm), cooldown);
        }
    }
    void Rearm() { armed = true; }
}
