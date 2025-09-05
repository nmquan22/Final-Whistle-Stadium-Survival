using UnityEngine;
public class Billboard : MonoBehaviour
{
    void LateUpdate(){ if (Camera.main) transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward); }
}