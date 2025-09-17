using System.Collections.Generic;
using UnityEngine;
using PoseSystem;   

public class PalmRoiDriver : MonoBehaviour
{
    [Header("References")]
    public MonoBehaviour palmBehaviour;   // PalmDetector
    public TFLiteHandSource handSource;   // TFLiteHandSource

    [Header("Use Palm if available")]
    public float minPalmScore = 0.5f;

    [Header("Fallback khi chưa có Palm/landmarks")]
    public bool enableBootstrapScan = true;
    [Range(0.4f, 1.0f)] public float bootstrapSize = 0.9f;
    public int scanEveryFrames = 10;

    [Header("Bám ROI theo landmarks khi đã có tay")]
    [Range(1.2f, 2.2f)] public float roiScale = 1.8f;
    [Range(0, 1)] public float smooth = 0.35f;

    [Header("Nếu ROI bị ngược dọc, bật cái này")]
    public bool compensateYFlip = false;

    IPalmDetector palm; Rect last; bool hasLast;
    int tick, scanIndex;

    static readonly Vector2[] SCAN_POS = new Vector2[] {
        new Vector2(0.50f, 0.50f),
        new Vector2(0.20f, 0.50f),
        new Vector2(0.80f, 0.50f),
        new Vector2(0.50f, 0.20f),
        new Vector2(0.50f, 0.80f),
    };

    void Awake() { palm = palmBehaviour as IPalmDetector; }

    void Update()
    {
        if (handSource == null) return;

        // 1) ƯU TIÊN: bám theo landmarks nếu có (ổn định nhất)
        if (handSource.TryGetHand(out List<HandKeypoint> kps, out float _score) &&
            kps != null && kps.Count >= 21)
        {
            float minx = 1f, miny = 1f, maxx = 0f, maxy = 0f;
            for (int i = 0; i < 21; i++)
            {
                minx = Mathf.Min(minx, Mathf.Clamp01(kps[i].x));
                maxx = Mathf.Max(maxx, Mathf.Clamp01(kps[i].x));
                miny = Mathf.Min(miny, Mathf.Clamp01(kps[i].y));
                maxy = Mathf.Max(maxy, Mathf.Clamp01(kps[i].y));
            }

            float side = Mathf.Max(maxx - minx, maxy - miny) * roiScale;
            float cx = (minx + maxx) * 0.5f;
            float cy = (miny + maxy) * 0.5f;
            Rect r = MakeRect(cx, cy, side);

            if (hasLast)
            {
                float a = Mathf.Clamp01(smooth);
                r = new Rect(
                    Mathf.Lerp(last.x, r.x, a),
                    Mathf.Lerp(last.y, r.y, a),
                    Mathf.Lerp(last.width, r.width, a),
                    Mathf.Lerp(last.height, r.height, a)
                );
            }

            if (compensateYFlip) r.y = 1f - r.y - r.height;

            handSource.SetExternalRoi(r);
            last = r; hasLast = true;
            return;
        }

        // 2) Nếu chưa có landmarks → dùng PALM (nếu có)
        if (palm != null && palm.TryGetBestPalm(out var rPalm, out var sPalm) && sPalm >= minPalmScore)
        {
            // đồng bộ mirror đã xử lý trong PalmDetector; chỉ bù Y nếu cần
            if (hasLast)
            {
                float a = Mathf.Clamp01(smooth);
                rPalm = new Rect(
                    Mathf.Lerp(last.x, rPalm.x, a),
                    Mathf.Lerp(last.y, rPalm.y, a),
                    Mathf.Lerp(last.width, rPalm.width, a),
                    Mathf.Lerp(last.height, rPalm.height, a)
                );
            }
            if (compensateYFlip) rPalm.y = 1f - rPalm.y - rPalm.height;

            handSource.SetExternalRoi(rPalm, sPalm);
            last = rPalm; hasLast = true;
            return;
        }

        // 3) Fallback: quét ROI 5 vị trí để “bắt tay” lần đầu
        if (enableBootstrapScan)
        {
            tick++;
            if (tick % Mathf.Max(1, scanEveryFrames) == 0)
            {
                Vector2 c = SCAN_POS[scanIndex++ % SCAN_POS.Length];
                Rect r = MakeRect(c.x, c.y, bootstrapSize);
                if (compensateYFlip) r.y = 1f - r.y - r.height;
                handSource.SetExternalRoi(r);
                hasLast = false;
            }
        }
    }

    static Rect MakeRect(float cx, float cy, float side01)
    {
        side01 = Mathf.Clamp(side01, 0.1f, 1f);
        Rect r = new Rect(cx - side01 * 0.5f, cy - side01 * 0.5f, side01, side01);
        r.x = Mathf.Clamp01(r.x); r.y = Mathf.Clamp01(r.y);
        if (r.x + r.width > 1f) r.x = 1f - r.width;
        if (r.y + r.height > 1f) r.y = 1f - r.height;
        return r;
    }
}
