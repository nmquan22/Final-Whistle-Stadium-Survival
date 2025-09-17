using UnityEngine;
using System.Collections.Generic;

[System.Serializable] public struct KP { public int id; public float x, y, score; }

// Interface & struct Keypoint2D đã có trong TFLitePoseSource:
// public interface IPoseSource2D { bool TryGet2D(out List<Keypoint2D> kps); }
// public struct Keypoint2D { public int id; public float x, y, score; }

public class PoseSmoother : MonoBehaviour, IPoseSource2D
{
    [Header("Raw Pose Source (component implements IPoseSource2D)")]
    public MonoBehaviour rawSourceBehaviour;   // Kéo TFLitePoseSource vào đây
    IPoseSource2D raw;

    [Header("Filter gates")]
    public float minScore = 0.2f;             // ngưỡng tin cậy để chấp nhận điểm mới
    public int holdFrames = 6;                // giữ vị trí cũ thêm vài frame khi score thấp

    [Header("One Euro Filter")]
    public float minCutoff = 2.0f;
    public float beta = 0.5f;
    public float dCutoff = 1.0f;

    const int K = 17;
    OneEuro[] fx = new OneEuro[K];
    OneEuro[] fy = new OneEuro[K];
    Vector2[] last = new Vector2[K];
    int[] hold = new int[K];

    class OneEuro
    {
        readonly float minc, beta, dcut;
        float dxPrev, xPrev;
        bool has;

        public OneEuro(float m, float b, float d) { minc = m; beta = b; dcut = d; }

        float Alpha(float cutoff, float dt)
        {
            dt = Mathf.Max(1e-4f, dt);
            float tau = 1f / (2f * Mathf.PI * Mathf.Max(1e-4f, cutoff));
            return 1f / (1f + tau / dt);
        }

        public float Filter(float x, float dt)
        {
            if (!has) { has = true; xPrev = x; dxPrev = 0; return x; }
            float dx = (x - xPrev) / Mathf.Max(1e-4f, dt);
            float adx = Alpha(dcut, dt);
            dxPrev = dxPrev + adx * (dx - dxPrev);

            float cutoff = minc + beta * Mathf.Abs(dxPrev);
            float a = Alpha(cutoff, dt);
            xPrev = xPrev + a * (x - xPrev);
            return xPrev;
        }

        public void Reset() { has = false; dxPrev = 0f; }
    }

    void Awake()
    {
        raw = rawSourceBehaviour as IPoseSource2D;
        if (raw == null)
        {
            Debug.LogError("PoseSmoother: rawSourceBehaviour không implement IPoseSource2D (kéo TFLitePoseSource vào).");
            enabled = false; return;
        }
        ResetFilters();
    }

    public void ResetFilters()
    {
        for (int i = 0; i < K; i++)
        {
            fx[i] = new OneEuro(minCutoff, beta, dCutoff);
            fy[i] = new OneEuro(minCutoff, beta, dCutoff);
            last[i] = Vector2.zero;
            hold[i] = 0;
        }
    }

    // ===== API cũ của bạn (trả về KP) =====
    public bool TryGetSmoothed(out List<KP> kpsOut)
    {
        kpsOut = null;
        if (raw == null || !raw.TryGet2D(out var rawKps)) return false;

        float dt = Time.deltaTime;
        var list = new List<KP>(K);

        for (int i = 0; i < K; i++)
        {
            var p = rawKps[i];
            Vector2 v = new Vector2(p.x, p.y);

            if (p.score >= minScore) { last[i] = v; hold[i] = holdFrames; }
            else if (hold[i] > 0) { hold[i]--; v = last[i]; } // giữ vị trí cũ thêm vài frame

            float sx = fx[i].Filter(v.x, dt);
            float sy = fy[i].Filter(v.y, dt);

            list.Add(new KP { id = i, x = sx, y = sy, score = p.score });
        }

        kpsOut = list;
        return true;
    }

    // ===== IPoseSource2D (trả về Keypoint2D, để chain sang PoseTargetsDriver2D) =====
    public bool TryGet2D(out List<Keypoint2D> kps)
    {
        kps = null;
        if (!TryGetSmoothed(out var smoothedKP)) return false;

        var list = new List<Keypoint2D>(K);
        for (int i = 0; i < K; i++)
        {
            var p = smoothedKP[i];
            list.Add(new Keypoint2D { id = p.id, x = p.x, y = p.y, score = p.score });
        }
        kps = list;
        return true;
    }
}
