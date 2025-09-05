using UnityEngine;
[RequireComponent(typeof(LineRenderer))]
public class PossessionIndicator : MonoBehaviour
{
    public TeamManager home, away; public float maxDist=2f;
    LineRenderer lr; Transform ball;
    void Awake(){ lr=GetComponent<LineRenderer>(); var b=GameObject.FindWithTag("Ball"); if(b) ball=b.transform; }
    void Update(){
        if(!ball){ var b=GameObject.FindWithTag("Ball"); if(b) ball=b.transform; else { lr.enabled=false; return; } }
        Transform best=null; float bestD=maxDist*maxDist;
        void Try(TeamManager t){ if(t==null) return; foreach(var p in t.players){ float d=(p.transform.position-ball.position).sqrMagnitude; if(d<bestD){bestD=d; best=p.transform;} } }
        Try(home); Try(away);
        if(!best){ lr.enabled=false; return; }
        lr.enabled=true; lr.positionCount=2; lr.widthMultiplier=0.05f;
        lr.SetPosition(0, ball.position); lr.SetPosition(1, best.position+Vector3.up*1.6f);
    }
}