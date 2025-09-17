using UnityEngine;
using System.Collections.Generic;
using PoseSystem;   

public enum HandGesture { None, Open, Fist, Pinch, TwoPinch, Point }

public class HandGestureDetector : MonoBehaviour
{
    [Header("Source (drag TFLiteHandSource here)")]
    public MonoBehaviour sourceBehaviour;
    IHandSource src;

    [Header("Thresholds")]
    [Range(0, 1)] public float minOverallScore = 0.2f;
    [Tooltip("Khoảng cách thumb-index (chuẩn hoá) để coi là pinch")]
    public float pinchThresh = 0.06f;
    [Tooltip("Cỡ nắm tay: tips gần wrist")]
    public float fistCurlThresh = 0.035f;
    [Tooltip("Index dài hơn middle để coi là point")]
    public float pointSpread = 0.02f;

    void Awake() { src = sourceBehaviour as IHandSource; }

    // API chính: trả gesture + palm (wrist) + hướng index
    public bool TryGet(out HandGesture g, out Vector2 palm, out Vector2 indexDir)
    {
        g = HandGesture.None; palm = Vector2.zero; indexDir = Vector2.right;
        if (src == null || !src.TryGetHand(out var kps, out var s) || s < minOverallScore || kps.Count < 21) return false;

        Vector2 L(int i) => new(kps[i].x, kps[i].y);

        var wrist = L(0);
        var thumb4 = L(4);
        var index8 = L(8);
        var mid12 = L(12);
        var ring16 = L(16);
        var pink20 = L(20);

        palm = wrist;
        indexDir = (index8 - wrist).sqrMagnitude > 1e-6f ? (index8 - wrist).normalized : Vector2.right;

        float dTI = Vector2.Distance(thumb4, index8);
        float dTM = Vector2.Distance(thumb4, mid12);
        float avgTipW = (Vector2.Distance(index8, wrist) + Vector2.Distance(mid12, wrist) +
                         Vector2.Distance(ring16, wrist) + Vector2.Distance(pink20, wrist)) * 0.25f;

        bool pinch = dTI < pinchThresh;
        bool pinch2 = dTM < pinchThresh;
        bool fist = avgTipW < fistCurlThresh;
        bool point = !pinch && !fist && (Vector2.Distance(index8, wrist) > Vector2.Distance(mid12, wrist) + pointSpread);

        if (pinch && !pinch2) g = HandGesture.Pinch;
        else if (pinch2) g = HandGesture.TwoPinch;
        else if (fist) g = HandGesture.Fist;
        else if (point) g = HandGesture.Point;
        else g = HandGesture.Open;

        return true;
    }
}
