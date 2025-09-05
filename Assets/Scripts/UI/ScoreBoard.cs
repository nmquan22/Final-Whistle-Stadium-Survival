using UnityEngine;
using TMPro;

public class ScoreBoard : MonoBehaviour
{
    public static ScoreBoard Instance;
    public TMP_Text scoreText;
    public TMP_Text timerText;
    int home = 0, away = 0;
    float time; bool running = true;

    void Awake() { Instance = this; }
    void Start() { UpdateScore(); }

    void Update()
    {
        if (!running) return;
        time += Time.deltaTime;
        int m = Mathf.FloorToInt(time / 60f);
        int s = Mathf.FloorToInt(time % 60f);
        if (timerText) timerText.text = $"{m:00}:{s:00}";
    }

    public void AddGoal(string team)
    {
        if (team == "Home") home++; else away++;
        UpdateScore();
    }

    public void ResetMatch() { home = away = 0; time = 0; running = true; UpdateScore(); }

    void UpdateScore() { if (scoreText) scoreText.text = $"Home {home} - {away} Away"; }
}
