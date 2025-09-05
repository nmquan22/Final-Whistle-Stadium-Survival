using UnityEngine;
using System.Linq;
using System.Collections.Generic;
public class TeamManager : MonoBehaviour
{
    public Formation formation;
    public Transform goal;
    public bool isHome=true;
    public GameObject playerPrefab;
    public List<PlayerController> players = new();
    public PlayerController goalkeeper;

    public void Setup(){
        ClearChildren();
        if (!playerPrefab || formation==null || formation.homeSpawns==null) return;
        for(int i=0;i<formation.homeSpawns.Length;i++){
            var pos = formation.homeSpawns[i]; if(!isHome) pos.x=-pos.x;
            var go = Instantiate(playerPrefab, pos, Quaternion.identity, transform);
            var pc = go.GetComponent<PlayerController>(); players.Add(pc);
            if (formation.roles!=null && i<formation.roles.Length && formation.roles[i]=="GK") goalkeeper=pc;
        }
        if (!goalkeeper && players.Count>0) goalkeeper = players.OrderByDescending(p=>Vector3.Distance(p.transform.position, goal?goal.position:Vector3.zero)).First();
    }
    public void ResetForKickoff(bool weKickoff, Vector3 center){
        for(int i=0;i<players.Count;i++){
            var pos=formation.homeSpawns[Mathf.Clamp(i,0,formation.homeSpawns.Length-1)]; if(!isHome) pos.x=-pos.x;
            players[i].transform.position=pos; players[i].transform.rotation=Quaternion.LookRotation((center-pos).normalized);
        }
    }
    public void MirrorTeam(Vector3 around){
        foreach(var p in players){ var pos=p.transform.position; pos.x=-pos.x; p.transform.position=pos; p.transform.rotation=Quaternion.LookRotation((around-pos).normalized); }
        isHome=!isHome;
    }
    public PlayerController GetNearestToBall(Vector3 ballPos){
        float best=float.MaxValue; PlayerController bestP=null;
        foreach(var p in players){ float d=(p.transform.position-ballPos).sqrMagnitude; if(d<best){best=d; bestP=p;} }
        return bestP;
    }
    void ClearChildren(){ for(int i=transform.childCount-1;i>=0;i--) DestroyImmediate(transform.GetChild(i).gameObject); players.Clear(); goalkeeper=null; }
}