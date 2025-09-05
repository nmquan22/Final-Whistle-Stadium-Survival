using UnityEngine;
public static class WorldContext
{
    public static Transform BallTransform;
    public static Rigidbody BallBody;
    public static Transform GoalHome;
    public static Transform GoalAway;
    public static Transform LastPossessor;

    public static Vector3 BallPos => BallTransform ? BallTransform.position : Vector3.zero;

    public static void Kick(Vector3 impulse)
    {
        if (BallBody) BallBody.AddForce(impulse, ForceMode.Impulse);
        else if (BallTransform && BallTransform.TryGetComponent<Rigidbody>(out var rb))
        { BallBody = rb; BallBody.AddForce(impulse, ForceMode.Impulse); }
    }
}