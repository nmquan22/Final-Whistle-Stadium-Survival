using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class AITeamManager : NetworkBehaviour
{
    public Transform homeGoal;     // khung thành của HOME
    public Transform awayGoal;     // khung thành của AWAY
    public BallNetwork ball;       // bóng
    public float supportRadius = 4.5f;
    public float reassignInterval = 0.2f;

    readonly List<AIPlayerController> bots = new();
    float timer;

    public Vector3 BallPos => ball ? ball.transform.position : Vector3.zero;

    public void Register(AIPlayerController b) { if (!bots.Contains(b)) bots.Add(b); }
    public void Unregister(AIPlayerController b) { bots.Remove(b); }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }
        if (!ball) ball = FindObjectOfType<BallNetwork>();
    }

    void Update()
    {
        if (!IsServer) return;
        timer += Time.deltaTime;
        if (timer < reassignInterval) return;
        timer = 0f;

        if (bots.Count == 0 || !ball) return;

        // 1) chọn chaser: bot xa bóng nhỏ nhất
        int chaserIdx = 0; float best = float.MaxValue;
        for (int i = 0; i < bots.Count; i++)
        {
            float d = (bots[i].transform.position - BallPos).sqrMagnitude;
            if (d < best) { best = d; chaserIdx = i; }
        }

        // 2) gán vai & điểm support
        for (int i = 0; i < bots.Count; i++)
        {
            var b = bots[i];
            if (!b) continue;

            b.isChaser = (i == chaserIdx);

            if (!b.isChaser)
            {
                // điểm hỗ trợ: một vòng tròn quanh bóng, hướng về goal đối phương (HOME goal)
                Vector3 toHome = (homeGoal ? (homeGoal.position - BallPos) : Vector3.back);
                toHome.y = 0; if (toHome.sqrMagnitude < 0.001f) toHome = Vector3.back;
                toHome.Normalize();

                // rải các bot hỗ trợ lệch góc hai bên
                float angle = (i % 2 == 0) ? 30f : -30f;
                Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 dir = rot * toHome;
                b.supportPoint = BallPos + dir * supportRadius;
                // giữ trong sân (đơn giản: kẹp theo XZ)
                b.supportPoint.y = BallPos.y;
            }
        }
    }
}
