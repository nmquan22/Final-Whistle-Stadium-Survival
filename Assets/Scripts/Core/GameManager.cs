using UnityEngine;
using System;
using System.Collections;

public enum MatchState { PreMatch, Kickoff, Playing, GoalScored, HalfTime, FullTime }

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    [Header("Match")]
    public float halfDuration = 180f;
    public float kickoffDelay = 2f;
    public float goalDelay = 1.5f;
    public int home = 0, away = 0;

    [Header("Refs")]
    public TeamManager homeTeam, awayTeam;
    public Transform centerSpot;
    public BallController ball; // <— unify với hệ bóng trước đó
    public AudioSource sfx;
    public AudioClip whistleKickoff, whistleGoal, whistleFulltime;

    public MatchState State { get; private set; } = MatchState.PreMatch;
    public float timeLeft { get; private set; }
    public bool InputLocked { get; private set; } = true;

    public event Action OnKickoff, OnHalfTime, OnFullTime, OnGoal, OnScoreChanged, OnStateChanged;

    bool _secondHalf = false;
    Coroutine _phaseRoutine;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
    }

    void Start()
    {
        timeLeft = halfDuration;
        StartPhase(CoKickoff());
    }

    void Update()
    {
        if (State == MatchState.Playing)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                if (!_secondHalf)
                {
                    SetState(MatchState.HalfTime);
                    StartPhase(CoHalfTime());
                }
                else
                {
                    EndMatch();
                }
            }
        }
    }

    public void Goal(bool homeScored)
    {
        if (State != MatchState.Playing && State != MatchState.GoalScored) return;

        if (homeScored) home++; else away++;
        OnScoreChanged?.Invoke();
        OnGoal?.Invoke();
        if (sfx && whistleGoal) sfx.PlayOneShot(whistleGoal);

        SetState(MatchState.GoalScored);
        StartPhase(CoKickoff(goalDelay));
    }

    void StartPhase(IEnumerator routine)
    {
        if (_phaseRoutine != null) StopCoroutine(_phaseRoutine);
        _phaseRoutine = StartCoroutine(routine);
    }

    IEnumerator CoKickoff(float extraDelay = 0f)
    {
        LockInput(true);
        if (extraDelay > 0f) yield return new WaitForSeconds(extraDelay);

        SetState(MatchState.Kickoff);
        var center = centerSpot ? centerSpot.position : Vector3.zero;

        homeTeam?.ResetForKickoff(true, center);
        awayTeam?.ResetForKickoff(false, center);
        ball?.ResetBall(center); // cần overload ResetBall(Vector3)

        if (sfx && whistleKickoff) sfx.PlayOneShot(whistleKickoff);
        OnKickoff?.Invoke();

        yield return new WaitForSeconds(kickoffDelay);

        LockInput(false);
        SetState(MatchState.Playing);
    }

    IEnumerator CoHalfTime()
    {
        LockInput(true);
        _secondHalf = true;

        // đổi sân
        var g = homeTeam.goal; homeTeam.goal = awayTeam.goal; awayTeam.goal = g;

        var c = centerSpot ? centerSpot.position : Vector3.zero;
        homeTeam.MirrorTeam(c);
        awayTeam.MirrorTeam(c);

        OnHalfTime?.Invoke();
        yield return new WaitForSeconds(2f);

        timeLeft = halfDuration;

        SetState(MatchState.Kickoff);
        ball?.ResetBall(c);
        if (sfx && whistleKickoff) sfx.PlayOneShot(whistleKickoff);

        yield return new WaitForSeconds(kickoffDelay);

        LockInput(false);
        SetState(MatchState.Playing);
    }

    public void SetState(MatchState s)
    {
        if (State == s) return;
        State = s;
        OnStateChanged?.Invoke();
    }

    public void EndMatch()
    {
        LockInput(true);
        SetState(MatchState.FullTime);
        OnFullTime?.Invoke();
        if (sfx && whistleFulltime) sfx.PlayOneShot(whistleFulltime);
    }

    public void LockInput(bool v) { InputLocked = v; }
}
