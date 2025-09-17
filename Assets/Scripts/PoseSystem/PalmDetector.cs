using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TensorFlowLite;

public class PalmDetector : MonoBehaviour, IPalmDetector
{
    [Header("Model (.tflite/.tflite.bytes)")]
    public TextAsset palmModel;                 // palm_detection_full_192.tflite(.bytes)
    [Range(1, 4)] public int threads = 1;
    public int detectEveryNFrames = 2;

    [Header("Source")]
    public TFLiteHandSource handSource;         // dùng chung webcam
    public bool mirrorPreview = true;           // để TRÙNG với HandSource

    [Header("Thresholds")]
    [Range(0, 1)] public float scoreThreshold = 0.6f;
    [Range(0, 1)] public float iouThreshold = 0.3f;

    // ==== runtime ====
    WebCamTexture cam;
    RenderTexture rt;
    Texture2D readTex;
    Interpreter interpreter;
    float[] inputF;

    int inW = 192, inH = 192;
    int scoresTensorIndex = -1, boxesTensorIndex = -1;

    float[] scores;                             // [N]
    float[] boxes;                              // [N*18]
    List<Anchor> anchors = new();
    readonly List<Result> results = new();

    struct Anchor { public float x, y; }        // normalized center
    struct Result { public float score; public Rect rect; }

    public bool TryGetBestPalm(out Rect roi01, out float score)
    {
        if (results.Count > 0) { roi01 = results[0].rect; score = results[0].score; return true; }
        roi01 = default; score = 0; return false;
    }

    IEnumerator Start()
    {
        if (!palmModel) { Debug.LogError("[Palm] Chưa gán palmModel"); enabled = false; yield break; }
        if (handSource == null) { Debug.LogError("[Palm] handSource = null"); enabled = false; yield break; }

        // đợi HandSource tạo xong webcam
        while (handSource.GetCameraTexture() == null) yield return null;
        cam = handSource.GetCameraTexture() as WebCamTexture;
        while (cam != null && cam.width < 16) yield return null;

        // đảm bảo mirror giống HandSource để ROI khỏi lệch ngang
        mirrorPreview = (handSource != null) ? handSource.mirrorPreview : mirrorPreview;

        // TFLite
        var opt = new InterpreterOptions() { threads = threads }; // KHÔNG GPU
        interpreter = new Interpreter(palmModel.bytes, opt);
        interpreter.ResizeInputTensor(0, new int[] { 1, 192, 192, 3 });
        interpreter.AllocateTensors();

        var inInfo = interpreter.GetInputTensorInfo(0);
        if (inInfo.shape.Length >= 3) { inH = inInfo.shape[1]; inW = inInfo.shape[2]; }
        Debug.Log($"[Palm] Model input = {inW}x{inH}");

        rt = new RenderTexture(inW, inH, 0, RenderTextureFormat.ARGB32);
        readTex = new Texture2D(inW, inH, TextureFormat.RGBA32, false);
        inputF = new float[inW * inH * 3];

        // outputs
        int oCount = interpreter.GetOutputTensorCount();
        for (int i = 0; i < oCount; i++)
        {
            var info = interpreter.GetOutputTensorInfo(i);
            int len = 1; foreach (var s in info.shape) len *= s;
            Debug.Log($"[Palm] Out#{i} type={info.type} shape=({string.Join(",", info.shape)}) len={len}");
            if (len == 2016) scoresTensorIndex = i;
            else if (len == 2016 * 18) boxesTensorIndex = i;
        }
        if (scoresTensorIndex < 0 || boxesTensorIndex < 0)
        {
            Debug.LogError("[Palm] Không tìm thấy output 2016 & 2016*18 (đúng biến thể 192x192?).");
            enabled = false; yield break;
        }

        scores = new float[2016];
        boxes = new float[2016 * 18];

        // anchors cho 192x192 (24x24, 12x12, 6x6) * 2 anchors/cell = 2016
        anchors = GenerateAnchors192x2016(inW, inH);
        Debug.Log($"[Palm] anchors={anchors.Count}");
    }

    void OnDestroy()
    {
        try { interpreter?.Dispose(); } catch { }
        try { rt?.Release(); } catch { }
    }

    void Update()
    {
        if (cam == null || cam.width < 16 || !cam.didUpdateThisFrame) return;
        if (detectEveryNFrames > 1 && (Time.frameCount % detectEveryNFrames) != 0) return;

        // 1) Blit full frame -> inW×inH (flip Y WebCamTexture)
        BlitFull(cam, rt, mirrorPreview);
        var bak = RenderTexture.active; RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, inW, inH), 0, 0, false); readTex.Apply(false);
        RenderTexture.active = bak;

        // 2) NHWC float
        var px = readTex.GetPixels32(); int t = 0;
        for (int i = 0; i < px.Length; i++) { inputF[t++] = px[i].r / 255f; inputF[t++] = px[i].g / 255f; inputF[t++] = px[i].b / 255f; }

        // 3) run
        interpreter.SetInputTensorData(0, inputF);
        interpreter.Invoke();
        interpreter.GetOutputTensorData(scoresTensorIndex, scores);
        interpreter.GetOutputTensorData(boxesTensorIndex, boxes);

        // 4) decode + NMS
        DecodeAndNms();
    }

    void DecodeAndNms()
    {
        results.Clear();
        int N = anchors.Count;

        // Thông số decode gần với TensorsToDetectionsCalculator (MediaPipe)
        const float X_SCALE = 192f, Y_SCALE = 192f, W_SCALE = 192f, H_SCALE = 192f;

        for (int i = 0; i < N; i++)
        {
            float sc = Sigmoid(scores[i]);
            if (sc < scoreThreshold) continue;

            int b = i * 18;
            // boxes: [cx, cy, w, h, k0x, k0y, ...] – model palm full 192 thường trả kiểu này
            float dx = boxes[b + 0], dy = boxes[b + 1];
            float dw = boxes[b + 2], dh = boxes[b + 3];

            // decode (fixed_anchor_size)
            float cx = dx / X_SCALE + anchors[i].x;
            float cy = dy / Y_SCALE + anchors[i].y;
            float w = Mathf.Exp(dw / W_SCALE);
            float h = Mathf.Exp(dh / H_SCALE);

            var r = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);

            // clamp nhẹ
            r.x = Mathf.Clamp01(r.x); r.y = Mathf.Clamp01(r.y);
            r.width = Mathf.Clamp01(r.width); r.height = Mathf.Clamp01(r.height);

            results.Add(new Result { score = sc, rect = r });
        }

        // NMS đơn giản
        results.Sort((a, b) => b.score.CompareTo(a.score));
        var kept = new List<Result>();
        foreach (var det in results)
        {
            bool drop = false;
            foreach (var k in kept)
                if (IoU(det.rect, k.rect) >= iouThreshold) { drop = true; break; }
            if (!drop) { kept.Add(det); if (kept.Count >= 4) break; }
        }
        results.Clear(); results.AddRange(kept);
    }

    // anchors: 24x24, 12x12, 6x6; 2 anchors / cell => 2016
    List<Anchor> GenerateAnchors192x2016(int inputW, int inputH)
    {
        int fm8 = Mathf.CeilToInt(inputW / 8f);   // 24
        int fm16 = Mathf.CeilToInt(inputW / 16f); // 12
        int fm32 = Mathf.CeilToInt(inputW / 32f); // 6

        var list = new List<Anchor>(2016);
        void AddGrid(int fm, int perCell)
        {
            for (int y = 0; y < fm; y++)
                for (int x = 0; x < fm; x++)
                {
                    float ax = (x + 0.5f) / fm;
                    float ay = (y + 0.5f) / fm;
                    for (int k = 0; k < perCell; k++) list.Add(new Anchor { x = ax, y = ay });
                }
        }
        AddGrid(fm8, 2);
        AddGrid(fm16, 2);
        AddGrid(fm32, 2);

        // đảm bảo đúng N
        if (list.Count != 2016)
        {
            Debug.LogWarning($"[Palm] anchors count {list.Count} != 2016 → sẽ trim/pad");
            if (list.Count > 2016) list.RemoveRange(2016, list.Count - 2016);
            while (list.Count < 2016) list.Add(list[list.Count - 1]);
        }
        return list;
    }

    // ===== utils =====
    static void BlitFull(Texture src, RenderTexture dst, bool mirror)
    {
        var bak = RenderTexture.active; RenderTexture.active = dst;
        GL.PushMatrix(); GL.LoadPixelMatrix(0, dst.width, dst.height, 0);
        GL.Clear(true, true, Color.black);

        Rect uv = new Rect(0, 0, 1, 1);
        // WebCamTexture gốc ở dưới → flip Y
        uv = new Rect(uv.x, 1f - uv.y - uv.height, uv.width, uv.height);
        if (mirror) uv = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);

        Graphics.DrawTexture(new Rect(0, 0, dst.width, dst.height), src, uv, 0, 0, 0, 0);
        GL.PopMatrix(); RenderTexture.active = bak;
    }

    static float Sigmoid(float x) => 1f / (1f + Mathf.Exp(-x));
    static float IoU(Rect a, Rect b)
    {
        float x1 = Mathf.Max(a.xMin, b.xMin), y1 = Mathf.Max(a.yMin, b.yMin);
        float x2 = Mathf.Min(a.xMax, b.xMax), y2 = Mathf.Min(a.yMax, b.yMax);
        float inter = Mathf.Max(0, x2 - x1) * Mathf.Max(0, y2 - y1);
        float uni = a.width * a.height + b.width * b.height - inter + 1e-6f;
        return inter / uni;
    }
}
