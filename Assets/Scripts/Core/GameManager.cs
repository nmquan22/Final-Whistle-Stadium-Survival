using UnityEngine;
using System;
using System.Collections;

public enum MatchState { PreMatch, Kickoff, Playing, GoalScored, HalfTime, FullTime }

public class GameManager : MonoBehaviour
{
    public static GameManager I;
    [Header("Match")] public float halfDuration = 180f; public float kickoffDelay = 2f; public int home=0, away=0;
    [Header("Refs")] public TeamManager homeTeam, awayTeam; public Transform centerSpot; public Ball ball; public AudioSource sfx; public AudioClip whistleKickoff, whistleGoal, whistleFulltime;
    public MatchState State {get; private set;} = MatchState.PreMatch;
    public float timeLeft {get; private set;}
    public bool InputLocked {get; private set;} = true;
    public event Action OnKickoff, OnHalfTime, OnFullTime, OnGoal, OnScoreChanged, OnStateChanged;

    void Awake(){ I=this; }
    void Start(){ timeLeft = halfDuration; StartCoroutine(CoKickoff()); }

    void Update(){
        if (State==MatchState.Playing){
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f){ SetState(MatchState.HalfTime); StartCoroutine(CoHalfTime()); }
        }
    }

    public void Goal(bool homeScored){
        if(homeScored) home++; else away++;
        OnScoreChanged?.Invoke(); OnGoal?.Invoke();
        if (sfx && whistleGoal) sfx.PlayOneShot(whistleGoal);
        SetState(MatchState.GoalScored);
        StartCoroutine(CoKickoff());
    }

    IEnumerator CoKickoff(){
        LockInput(true); SetState(MatchState.Kickoff);
        homeTeam?.ResetForKickoff(true,  centerSpot? centerSpot.position : Vector3.zero);
        awayTeam?.ResetForKickoff(false, centerSpot? centerSpot.position : Vector3.zero);
        ball?.ResetBall(centerSpot? centerSpot.position : Vector3.zero);
        if (sfx && whistleKickoff) sfx.PlayOneShot(whistleKickoff);
        OnKickoff?.Invoke();
        yield return new WaitForSeconds(kickoffDelay);
        LockInput(false); SetState(MatchState.Playing);
    }

    IEnumerator CoHalfTime(){
        LockInput(true);
        var g = homeTeam.goal; homeTeam.goal = awayTeam.goal; awayTeam.goal = g;
        homeTeam.MirrorTeam(centerSpot?centerSpot.position:Vector3.zero);
        awayTeam.MirrorTeam(centerSpot?centerSpot.position:Vector3.zero);
        yield return new WaitForSeconds(2f);
        timeLeft = halfDuration;
        SetState(MatchState.Kickoff); OnKickoff?.Invoke();
        ball?.ResetBall(centerSpot?centerSpot.position:Vector3.zero);
        if (sfx && whistleKickoff) sfx.PlayOneShot(whistleKickoff);
        yield return new WaitForSeconds(kickoffDelay);
        LockInput(false); SetState(MatchState.Playing);
    }

    public void SetState(MatchState s){ State=s; OnStateChanged?.Invoke(); }
    public void EndMatch(){ LockInput(true); SetState(MatchState.FullTime); OnFullTime?.Invoke(); if (sfx && whistleFulltime) sfx.PlayOneShot(whistleFulltime); }
    public void LockInput(bool v){ InputLocked = v; }
}