using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallNetwork : NetworkBehaviour
{
    Rigidbody rb;
    void Awake() { rb = GetComponent<Rigidbody>(); }

    [ServerRpc(RequireOwnership = false)]
    public void PassToTargetServerRpc(NetworkObjectReference targetRef, float power)
    {
        if (!IsServer) return;
        if (!targetRef.TryGet(out NetworkObject target)) return;

        Vector3 to = target.transform.position - rb.position;
        to.y = 0f;
        rb.velocity = Vector3.zero;
        rb.AddForce(to.normalized * power, ForceMode.VelocityChange);
    }
}
