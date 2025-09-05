using UnityEngine;

public class BallDebugKick : MonoBehaviour
{
    public BallController ball;
    public Vector3 force = new Vector3(0, 0, 6f);

    void Reset()
    {
        if (!ball) ball = GetComponent<BallController>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && ball)
        {
            ball.Kick(force);
        }
    }
}
