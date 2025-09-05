using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class BallInteractor : MonoBehaviour
{
    public float interactRadius = 2.2f;
    public float kickPower = 10f;
    public float passPower = 7f;
    public Transform kickPoint;

    void Reset(){ if(!kickPoint) kickPoint = transform.Find("KickPoint"); }

    public void KickButton(){
        var ball = FindNearestBall();
        if (!ball) return;
        Vector3 origin = kickPoint ? kickPoint.position : transform.position + transform.forward*0.6f + Vector3.up*0.3f;
        Vector3 dir = (ball.transform.position - origin); dir.y = 0; dir = dir.sqrMagnitude>0.001f ? dir.normalized : transform.forward;
        var rb = ball.GetComponent<Rigidbody>(); if(!rb) return;
        rb.AddForce(dir * kickPower, ForceMode.Impulse);
    }

    public void PassButton(){
        var ball = FindNearestBall();
        if (!ball) return;
        var mate = FindNearestTeammate();
        Vector3 dir = mate ? (mate.position - (kickPoint?kickPoint.position:transform.position)) : transform.forward;
        dir.y = 0; dir = dir.sqrMagnitude>0.001f ? dir.normalized : transform.forward;
        var rb = ball.GetComponent<Rigidbody>(); if(!rb) return;
        rb.AddForce(dir * passPower, ForceMode.Impulse);
    }

    GameObject FindNearestBall(){
        GameObject best = null; float d2best = float.MaxValue;
        foreach (var b in GameObject.FindGameObjectsWithTag("Ball")){
            float d2 = (b.transform.position - (kickPoint?kickPoint.position:transform.position)).sqrMagnitude;
            if (d2 < interactRadius*interactRadius && d2 < d2best){ d2best = d2; best = b; }
        }
        return best;
    }

    Transform FindNearestTeammate(){
        Transform best = null; float d2best = 99999f;
        foreach (var p in GameObject.FindGameObjectsWithTag("Player")){
            if (p == gameObject) continue;
            float d2 = (p.transform.position - transform.position).sqrMagnitude;
            if (d2 < d2best){ d2best = d2; best = p.transform; }
        }
        return best;
    }
}