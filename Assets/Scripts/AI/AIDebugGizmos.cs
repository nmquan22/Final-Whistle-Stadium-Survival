using UnityEngine;
public class AIDebugGizmos : MonoBehaviour
{
    public static bool Enabled;
    void Update(){ if (Input.GetKeyDown(KeyCode.F8)) Enabled = !Enabled; }
    void OnDrawGizmos(){
        if (!Enabled) return;
        if (WorldContext.BallTransform){
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(WorldContext.BallPos + Vector3.up*0.1f, 0.1f);
        }
    }
}