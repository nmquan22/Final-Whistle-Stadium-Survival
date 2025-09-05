using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(PlayerController))]
public class PlayerUserControl : MonoBehaviour
{
    PlayerController pc; public bool isUserControlled;
    void Awake(){ pc = GetComponent<PlayerController>(); }
    void Update(){ if(GameManager.I) pc.enabled = isUserControlled && !GameManager.I.InputLocked; }
    public void OnSwitchPlayer(InputValue v){
        if(!v.isPressed) return;
        var team = GetComponentInParent<TeamManager>();
        var ball = GameObject.FindWithTag("Ball");
        var target = team?.GetNearestToBall(ball?ball.transform.position:Vector3.zero);
        if(target && target!=pc){
            var old = team.players.Find(p=>p.GetComponent<PlayerUserControl>()?.isUserControlled==true);
            if(old) old.GetComponent<PlayerUserControl>().isUserControlled=false;
            target.GetComponent<PlayerUserControl>().isUserControlled=true;
        }
    }
}