using UnityEngine;
public class Bootstrap : MonoBehaviour
{
    public TeamManager home, away;
    void Start(){ if(home) home.Setup(); if(away) away.Setup(); }
}