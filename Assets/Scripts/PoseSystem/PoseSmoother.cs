using UnityEngine;
using System.Collections.Generic;

[System.Serializable] public struct KP { public int id; public float x, y, score; }

public class PoseSmoother : MonoBehaviour
{
    public BarracudaPoseSource source;
    public float minScore = 0.2f;
    public int holdFrames = 6;

    // One Euro params
    public float minCutoff = 2.0f;
    public float beta = 0.5f;
    public float dCutoff = 1.0f;

    class OneEuro
    {
        float minc, beta, dcut, dxPrev, xPrev; bool has;
        float Alpha(float cutoff) { float te = Time.deltaTime; float tau = 1f / (2 * Mathf.PI * cutoff); return 1f / (1f + tau / Mathf.Max(1e-4f, te)); }
        public OneEuro(float m, float b, float d) { minc = m; beta = b; dcut = d; }
        public float Filter(float x)
        {
            if (!has) { has = true; xPrev = x; dxPrev = 0; return x; }
            float dx = (x - xPrev) / Mathf.Max(Time.deltaTime, 1e-4f);
            float adx = Alpha(dcut); dxPrev = dxPrev + (adx * (dx - dxPrev));
            float cutoff = minc + beta * Mathf.Abs(dxPrev);
            float a = Alpha(cutoff); xPrev = xPrev + (a * (x - xPrev)); return xPrev;
        }
    }

    OneEuro[] fx = new OneEuro[17];
    OneEuro[] fy = new OneEuro[17];
    Vector2[] last = new Vector2[17];
    int[] hold = new int[17];

    void Awake() { for (int i = 0; i < 17; i++) { fx[i] = new OneEuro(minCutoff, beta, dCutoff); fy[i] = new OneEuro(minCutoff, beta, dCutoff); } }

    public bool TryGetSmoothed(out List<KP> kpsOut)
    {
        kpsOut = null;
        if (source == null || !source.TryGet2D(out var raw)) return false;

        var list = new List<KP>(17);
        for (int i = 0; i < 17; i++)
        {
            var p = raw[i];
            Vector2 v = new Vector2(p.x, p.y);
            if (p.score >= minScore) { last[i] = v; hold[i] = holdFrames; }
            else if (hold[i] > 0) { hold[i]--; v = last[i]; }    // giữ vị trí cũ vài frame

            float sx = fx[i].Filter(v.x);
            float sy = fy[i].Filter(v.y);
            list.Add(new KP { id = i, x = sx, y = sy, score = p.score });
        }
        kpsOut = list; return true;
    }
}
