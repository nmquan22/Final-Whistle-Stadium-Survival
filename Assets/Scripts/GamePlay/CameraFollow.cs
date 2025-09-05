using UnityEngine;
public class CameraFollow : MonoBehaviour
{
    public Transform target; public Vector3 offset = new Vector3(0,12,-12); public float smooth = 8f;
    void LateUpdate(){
        if(!target) return;
        var desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * smooth);
        transform.rotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
    }
}