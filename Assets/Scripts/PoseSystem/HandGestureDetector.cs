using UnityEngine;
using System.Collections.Generic;
using PoseSystem;

public enum HandGesture { None, Open, Fist, Pinch, TwoPinch, Point }

public class HandGestureDetector : MonoBehaviour
{
    [Header("Source (drag HandSource here)")]
    public MonoBehaviour sourceBehaviour;
    IHandSource src;

    [Header("Thresholds")]
    [Range(0, 1)] public float minOverallScore = 0.2f;
    public float pinchThresh = 0.06f;
    public float fistCurlThresh = 0.035f;
    public float pointSpread = 0.02f;

    [Header("Debug")]
    public bool logState = true;              // bật để thấy g=Pinch/Point...
    public bool logFailReasons = true;        // bật để biết vì sao TryGet() fail
    [Range(0.05f, 2f)] public float logInterval = 0.3f;
    float lastLogTime = -999f;
    HandGesture lastLogged = HandGesture.None;

    void Awake() { src = sourceBehaviour as IHandSource; }
    public void SetSource(MonoBehaviour m) { sourceBehaviour = m; src = m as IHandSource; }

    public bool TryGet(out HandGesture g, out Vector2 palm, out Vector2 indexDir)
    {
        g = HandGesture.None; palm = Vector2.zero; indexDir = Vector2.right;

        if (src == null)
        {
            if (logFailReasons && Time.time - lastLogTime > 0.5f)
            { Debug.LogWarning("[HandGesture] FAIL: SourceBehaviour is null (drag PythonHandSource/TFLiteHandSource vào)."); lastLogTime = Time.time; }
            return false;
        }

        if (!src.TryGetHand(out var kps, out var s))
        {
            if (logFailReasons && Time.time - lastLogTime > 0.5f)
            { Debug.LogWarning("[HandGesture] FAIL: Source has no frame (chưa nhận landmark từ server/camera)."); lastLogTime = Time.time; }
            return false;
        }

        if (s < minOverallScore)
        {
            if (logFailReasons && Time.time - lastLogTime > 0.5f)
            { Debug.LogWarning($"[HandGesture] FAIL: score {s:F2} < min {minOverallScore:F2}"); lastLogTime = Time.time; }
            return false;
        }

        if (kps == null || kps.Count < 21)
        {
            if (logFailReasons && Time.time - lastLogTime > 0.5f)
            { Debug.LogWarning($"[HandGesture] FAIL: keypoints count = {(kps == null ? 0 : kps.Count)}"); lastLogTime = Time.time; }
            return false;
        }

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

        if (logState && (g != lastLogged || Time.time - lastLogTime >= logInterval))
        {
            Debug.Log($"[HandGesture] g={g} palm=({palm.x:F2},{palm.y:F2}) idx=({indexDir.x:F2},{indexDir.y:F2}) " +
                      $"dTI={dTI:F3} dTM={dTM:F3} avgTipW={avgTipW:F3} score={s:F2}");
            lastLogged = g; lastLogTime = Time.time;
        }

        return true;
    }
}
