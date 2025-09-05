using UnityEngine;
[RequireComponent(typeof(Rigidbody))]
public class Ball : MonoBehaviour
{
    Rigidbody rb;
    void Awake(){ rb = GetComponent<Rigidbody>(); }
    public void ResetBall(Vector3 pos){
        rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        transform.position = pos + Vector3.up*0.11f;
    }
}