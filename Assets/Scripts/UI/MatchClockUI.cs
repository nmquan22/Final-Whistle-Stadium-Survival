using UnityEngine;
using TMPro;
public class MatchClockUI : MonoBehaviour
{
    public TMP_Text clockText; public TMP_Text scoreText;
    void OnEnable(){ if(GameManager.I){ GameManager.I.OnScoreChanged += RefreshScore; GameManager.I.OnStateChanged += RefreshClock; } }
    void OnDisable(){ if(GameManager.I){ GameManager.I.OnScoreChanged -= RefreshScore; GameManager.I.OnStateChanged -= RefreshClock; } }
    void Update(){ if(GameManager.I && GameManager.I.State==MatchState.Playing) RefreshClock(); }
    void RefreshClock(){ float t=Mathf.Max(0, GameManager.I.timeLeft); int m=Mathf.FloorToInt(t/60f), s=Mathf.FloorToInt(t%60); if(clockText) clockText.text=$"{m:00}:{s:00}"; }
    void RefreshScore(){ if(scoreText) scoreText.text=$"{GameManager.I.home} - {GameManager.I.away}"; }
}