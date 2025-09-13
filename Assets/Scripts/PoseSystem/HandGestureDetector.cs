using UnityEngine;
using System.Collections.Generic;

public class HandGestureDetector : MonoBehaviour
{
    public PoseSmoother smoother;
    public int framesNeeded = 5;
    public float margin = 0.06f;   // khoảng cách tối thiểu (0..1)
    int lCnt, rCnt; bool lUp, rUp;

    void Update()
    {
        if (smoother == null || !smoother.TryGetSmoothed(out var k)) return;

        bool LUpNow = k[9].y < (k[5].y - margin);   // 9: left wrist, 5: left shoulder
        bool RUpNow = k[10].y < (k[6].y - margin);  // 10: right wrist, 6: right shoulder

        lCnt = LUpNow ? Mathf.Min(framesNeeded, lCnt + 1) : Mathf.Max(0, lCnt - 1);
        rCnt = RUpNow ? Mathf.Min(framesNeeded, rCnt + 1) : Mathf.Max(0, rCnt - 1);

        bool newL = lCnt >= framesNeeded;
        bool newR = rCnt >= framesNeeded;

        if (newL != lUp) { lUp = newL; Debug.Log($"[Pose] Left hand {(lUp ? "UP" : "DOWN")}"); }
        if (newR != rUp) { rUp = newR; Debug.Log($"[Pose] Right hand {(rUp ? "UP" : "DOWN")}"); }
    }
}
