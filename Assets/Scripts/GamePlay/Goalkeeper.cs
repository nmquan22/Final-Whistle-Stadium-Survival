using UnityEngine;
public class Goalkeeper : MonoBehaviour
{
    public float patrolWidth = 10f;
    public float goalZ = 32f;
    public float moveSpeed = 3.5f;

    void Update(){
        var pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -patrolWidth, patrolWidth);
        pos.z = Mathf.MoveTowards(pos.z, Mathf.Sign(goalZ)*Mathf.Abs(goalZ), Time.deltaTime * moveSpeed);
        transform.position = pos;
        var look = new Vector3(0, pos.y, 0);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look - transform.position), 360 * Time.deltaTime);
    }
}