using UnityEngine;
public static class TeamAI
{
    public static Transform ClosestToBall(Transform[] players)
    {
        if (players == null || players.Length == 0) return null;
        float best = float.MaxValue; Transform bestT = null;
        Vector3 bp = WorldContext.BallPos;
        foreach (var t in players){
            if (!t) continue;
            float d = (t.position - bp).sqrMagnitude;
            if (d < best){ best = d; bestT = t; }
        }
        return bestT;
    }
}