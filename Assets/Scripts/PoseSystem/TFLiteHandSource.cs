using UnityEngine;
using TensorFlowLite;
using System.Collections.Generic;
using PoseSystem;


public class TFLiteHandSource : MonoBehaviour, IHandSource
{
    [Header("Model (.tflite or .tflite.bytes)")]
    public TextAsset modelFile;                 // hand_landmark_full.tflite(.bytes) hoặc bản tương thích
    [Tooltip("224 theo MediaPipe; 192/256 cũng được miễn đúng với model")]
    public int inputSize = 224;

    [Header("Preprocess")]
    public bool mirrorPreview = true;           // ảnh debug
    public bool mirrorLogic = false;             // đảo trục X cho toạ độ landmark
    public bool normalize01 = true;             // RGB -> [0..1]
    public bool forceSigmoid = false;            // đa số bản landmark trả logits → nên bật

    [Header("ROI Tracking (tắt nếu dùng palm)")]
    public bool useRoiTracking = false;
    [Range(0.3f, 1.2f)] public float bootstrapSize = 0.8f;
    [Range(1.2f, 2.2f)] public float roiScale = 1.6f;
    [Range(0.0f, 1.0f)] public float roiSmooth = 0.20f;
    public int lostHoldFrames = 6;

    [Header("Runtime")]
    [Range(1, 8)] public int threads = 2;

    public enum PreviewMode { ROI, FullCamera }
    [Header("Debug Overlay")]
    public bool debugPreview = true;
    public bool debugSkeleton = true;
    public PreviewMode previewMode = PreviewMode.FullCamera;
    [Range(1, 10)] public float lineThickness = 2f;
    [Range(0, 20)] public float pointSize = 4f;
    public bool showRoiLabel = true;

    // ==== runtime ====
    WebCamTexture cam;
    RenderTexture rt;           // buffer để crop ROI -> inputSize
    Texture2D readTex;          // readback ROI (debug + build input)
    Texture2D white1;           // 1x1 cho vẽ GUI
    Interpreter interpreter;

    float[] inputF;             // NHWC float input
    float[] outRaw;             // 63 floats
    int landmarkTensorIndex = -1;
    int presenceTensorIndex = -1;    // nếu model có output 1x1 presence
    int handednessTensorIndex = -1;  // nếu model có output 1x1 handedness (không dùng)
    int[] landmarkShape;

    readonly List<HandKeypoint> latest = new(21);
    float overallScore = 1f;    // sẽ lấy từ palm score hoặc presence nếu có
    bool haveFrame;

    // ROI normalized theo camera (vuông)
    Rect roi;
    bool hasRoi;
    int lostFrames;

    // score lấy từ PalmDetector (đưa qua PalmRoiDriver)
    float lastPalmScore = -1f;

    public Texture GetCameraTexture() => cam;

    static readonly (int a, int b)[] BONES = new (int, int)[]
    {
        (0,1),(1,2),(2,3),(3,4),
        (0,5),(5,6),(6,7),(7,8),
        (0,9),(9,10),(10,11),(11,12),
        (0,13),(13,14),(14,15),(15,16),
        (0,17),(17,18),(18,19),(19,20)
    };

    const int LOG_EVERY = 45; // log ~1.5s/lần ở 30fps

    static void DumpRange(string tag, float[] a, int n = -1)
    {
        if (a == null) { Debug.Log($"{tag}: null"); return; }
        if (n < 0 || n > a.Length) n = a.Length;
        float mn = 1e9f, mx = -1e9f;
        for (int i = 0; i < n; i++) { float v = a[i]; if (v < mn) mn = v; if (v > mx) mx = v; }
        string head = "";
        for (int i = 0; i < Mathf.Min(12, n); i++) head += a[i].ToString("F2") + " ";
        Debug.Log($"{tag}: range {mn:F2}..{mx:F2} | first: {head}");
    }
    void Start()
    {
        if (!modelFile) { Debug.LogError("[Hand] Chưa gán modelFile"); enabled = false; return; }

        // Webcam
        cam = new WebCamTexture(640, 480, 30);
        cam.Play();

        // RT & readback
        rt = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32);
        readTex = new Texture2D(inputSize, inputSize, TextureFormat.RGBA32, false);
        white1 = new Texture2D(1, 1); white1.SetPixel(0, 0, Color.white); white1.Apply();

        // TFLite
        var opt = new InterpreterOptions() { threads = threads };
        interpreter = new Interpreter(modelFile.bytes, opt);
        interpreter.ResizeInputTensor(0, new int[] { 1, inputSize, inputSize, 3 });
        interpreter.AllocateTensors();


        // Pick outputs: 63 floats (landmarks) + optional presence(1) + handedness(1)
        int outCount = interpreter.GetOutputTensorCount();
        for (int i = 0; i < outCount; i++)
        {
            var info = interpreter.GetOutputTensorInfo(i);
            int len = 1; foreach (var s in info.shape) len *= s;
            Debug.Log($"[Hand] Out#{i} type={info.type} shape=({string.Join(",", info.shape)}) len={len}");

            if (len == 63 && landmarkTensorIndex < 0) landmarkTensorIndex = i;
            else if (len == 1 && presenceTensorIndex < 0) presenceTensorIndex = i;
            else if (len == 1 && handednessTensorIndex < 0) handednessTensorIndex = i;
        }
        if (landmarkTensorIndex < 0)
        {
            Debug.LogError("[Hand] Không tìm thấy output 63-float (landmarks)"); enabled = false; return;
        }

        landmarkShape = interpreter.GetOutputTensorInfo(landmarkTensorIndex).shape;
        outRaw = new float[63];
        inputF = new float[inputSize * inputSize * 3];

        // bootstrap roi giữa khung
        roi = CenterSquare(bootstrapSize);
        hasRoi = false;

        var inInfo = interpreter.GetInputTensorInfo(0);
        Debug.Log($"[Hand] Input {string.Join("x", inInfo.shape)} | pickOut idx={landmarkTensorIndex}, shape=({string.Join(",", landmarkShape)})");
        Debug.Log($"[Hand] presenceIdx={presenceTensorIndex}, handednessIdx={handednessTensorIndex}");
        Debug.Log($"[LM] InputTensor shape={string.Join("x", inInfo.shape)} type={inInfo.type}");
        var outInfo = interpreter.GetOutputTensorInfo(landmarkTensorIndex);
        Debug.Log($"[LM] OutTensor idx={landmarkTensorIndex} shape=({string.Join(",", outInfo.shape)}) type={outInfo.type}");
        Debug.Log($"[LM] presenceIdx={presenceTensorIndex} handedIdx={handednessTensorIndex} | mirrorPreview={mirrorPreview} mirrorLogic={mirrorLogic} inputSize={inputSize}");

    }

    void OnDestroy()
    {
        try { interpreter?.Dispose(); } catch { }
        try { cam?.Stop(); } catch { }
        try { rt?.Release(); } catch { }
    }

    void Update()
    {
        if (cam == null || cam.width < 16 || !cam.didUpdateThisFrame) return;

        // 1) Crop ROI -> RT
        BlitROI(cam, rt, roi, mirrorPreview);

        // 2) Readback ROI
        var bak = RenderTexture.active; RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, inputSize, inputSize), 0, 0, false);
        readTex.Apply(false);
        RenderTexture.active = bak;

        // 3) Build input
        var px = readTex.GetPixels32();
        int t = 0;
        if (normalize01)
        {
            for (int i = 0; i < px.Length; i++)
            { inputF[t++] = px[i].r / 255f; inputF[t++] = px[i].g / 255f; inputF[t++] = px[i].b / 255f; }
        }
        else
        {
            for (int i = 0; i < px.Length; i++)
            { inputF[t++] = px[i].r; inputF[t++] = px[i].g; inputF[t++] = px[i].b; }
        }

        if (Time.frameCount % LOG_EVERY == 0)
        {
            // Thống kê ROI & ảnh
            Debug.Log($"[LM] ROI={roi.x:F2},{roi.y:F2},{roi.width:F2} | cam={cam.width}x{cam.height} | preview={(previewMode == PreviewMode.FullCamera ? "Full" : "ROI")}");
            // inputF là NHWC [0..1] nếu normalize01=true
            DumpRange("[LM] inputF RGB", inputF, 3 * 64); // lấy 64 pixel đầu * 3 kênh
        }

        // 4) Inference
        interpreter.SetInputTensorData(0, inputF);
        interpreter.Invoke();

        // landmarks
        interpreter.GetOutputTensorData(landmarkTensorIndex, outRaw);

        // presence (nếu có)
        float presenceScore = -1f;
        if (presenceTensorIndex >= 0)
        {
            float[] tmp = new float[1];
            interpreter.GetOutputTensorData(presenceTensorIndex, tmp);
            presenceScore = Sigmoid(tmp[0]);
        }

        if (Time.frameCount % LOG_EVERY == 0)
        {
            DumpRange("[LM] outRaw(xy) THO", outRaw, 42); // 21*2 xy đầu
        }


        // 5) Decode landmark trong hệ quy chiếu ROI (0..1)
        DecodeLandmarks(outRaw, latest, mirrorLogic, inputSize);

        if (Time.frameCount % LOG_EVERY == 0 && latest.Count == 21)
        {
            float minx = 1, maxx = 0, miny = 1, maxy = 0;
            for (int i = 0; i < 21; i++)
            {
                minx = Mathf.Min(minx, latest[i].x); maxx = Mathf.Max(maxx, latest[i].x);
                miny = Mathf.Min(miny, latest[i].y); maxy = Mathf.Max(maxy, latest[i].y);
            }
            Debug.Log($"[LM] decoded x in {minx:F2}..{maxx:F2} | y in {miny:F2}..{maxy:F2}");
        }

        // 6) Update ROI tracking (nếu không dùng palm)
        if (useRoiTracking) UpdateROIFromLandmarks(latest);

        // 7) overall score ưu tiên từ palm, rồi tới presence, cuối cùng =1
        overallScore = (lastPalmScore >= 0f) ? lastPalmScore :
                       (presenceScore >= 0f ? presenceScore : 1f);

        haveFrame = latest.Count == 21;

        // Log đúng format C#
        if (haveFrame && Time.frameCount % 30 == 0)
        {
            string one = "";
            for (int i = 0; i < 5; i++) one += $"[{i}:{latest[i].x:F2},{latest[i].y:F2}] ";
            Debug.Log($"[Hand] sample {one} roi=({roi.x:F2},{roi.y:F2},{roi.width:F2}) score={overallScore:F2}");
        }
        else if (!haveFrame && Time.frameCount % 60 == 0)
        {
            bool allZero = true; for (int i = 0; i < 63; i++) if (Mathf.Abs(outRaw[i]) > 1e-6f) { allZero = false; break; }
            if (allZero) Debug.LogWarning("[Hand] landmark output all zeros (sai ROI/sigmoid/output-index?)");
        }
    }

    // ===== ROI tracking =====
    Rect CenterSquare(float s) { s = Mathf.Clamp01(s); return new Rect(0.5f - s * 0.5f, 0.5f - s * 0.5f, s, s); }

    void UpdateROIFromLandmarks(List<HandKeypoint> pts)
    {
        if (pts == null || pts.Count < 21) { LostStep(); return; }

        float minx = 1f, miny = 1f, maxx = 0f, maxy = 0f;
        for (int i = 0; i < 21; i++)
        {
            float x = Mathf.Clamp01(pts[i].x), y = Mathf.Clamp01(pts[i].y);
            minx = Mathf.Min(minx, x); maxx = Mathf.Max(maxx, x);
            miny = Mathf.Min(miny, y); maxy = Mathf.Max(maxy, y);
        }
        float bw = maxx - minx, bh = maxy - miny;
        bool valid = bw > 0.08f && bh > 0.08f;
        if (!valid) { LostStep(); return; }

        lostFrames = 0; hasRoi = true;

        float cx_old = roi.x + (minx + maxx) * 0.5f * roi.width;
        float cy_old = roi.y + (miny + maxy) * 0.5f * roi.height;
        float side = Mathf.Max(bw, bh) * roi.width * roiScale;

        float a = Mathf.Clamp01(roiSmooth);
        float cx = Mathf.Lerp(roi.center.x, cx_old, a);
        float cy = Mathf.Lerp(roi.center.y, cy_old, a);
        float s = Mathf.Lerp(roi.width, side, a);

        s = Mathf.Clamp(s, 0.15f, 1.0f);
        var r = new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s);
        r.x = Mathf.Clamp01(r.x); r.y = Mathf.Clamp01(r.y);
        if (r.x + r.width > 1f) r.x = 1f - r.width;
        if (r.y + r.height > 1f) r.y = 1f - r.height;

        roi = r;
    }

    void LostStep()
    {
        lostFrames++;
        if (lostFrames >= lostHoldFrames) { hasRoi = false; roi = CenterSquare(bootstrapSize); }
    }

    // ===== Decode landmark (x,y trong [0..1] theo ROI) =====
    static float Sigmoid(float x) => 1f / (1f + Mathf.Exp(-x));

    static void DecodeLandmarks(float[] flat, List<HandKeypoint> dst, bool mirrorLogic, int inputSize)
    {
        dst.Clear();
        if (flat == null || flat.Length < 63) return;

        // đo biên độ x,y
        float mn = 1e9f, mx = -1e9f;
        for (int i = 0; i < 42; i++) { float v = flat[i]; if (v < mn) mn = v; if (v > mx) mx = v; }

        bool looksUnit = (mn >= -0.05f && mx <= 1.05f);
        bool looksSigned = (!looksUnit && mn >= -1.2f && mx <= 1.2f);
        bool looksPixel = (!looksUnit && !looksSigned && mx <= inputSize * 1.3f && mn >= -inputSize * 0.2f);
        bool looksLogits = (!looksUnit && !looksSigned && !looksPixel); // còn lại

        int idx = 0;
        for (int k = 0; k < 21; k++)
        {
            float x = flat[idx++], y = flat[idx++], z = flat[idx++];

            if (looksPixel) { x /= inputSize; y /= inputSize; }
            else if (looksSigned) { x = 0.5f * (x + 1f); y = 0.5f * (y + 1f); }
            else if (looksLogits) { x = Sigmoid(x); y = Sigmoid(y); }
            // looksUnit: giữ nguyên

            if (mirrorLogic) x = 1f - x;

            dst.Add(new HandKeypoint
            {
                id = k,
                x = Mathf.Clamp01(x),
                y = Mathf.Clamp01(y),
                z = z,
                score = 1f
            });
        }
    }


    // ===== Blit ROI (uv normalized) → square RT (flip Y của WebCamTexture) =====
    static void BlitROI(Texture src, RenderTexture dst, Rect roi01, bool mirror)
    {
        var bak = RenderTexture.active; RenderTexture.active = dst;
        GL.PushMatrix(); GL.LoadPixelMatrix(0, dst.width, dst.height, 0);
        GL.Clear(true, true, Color.black);

        Rect dstRect = new Rect(0, 0, dst.width, dst.height);
        // WebCamTexture gốc Y ở bottom → flip Y
        Rect uv = new Rect(roi01.x, 1f - roi01.y - roi01.height, roi01.width, roi01.height);
        if (mirror) uv = new Rect(uv.x + uv.width, uv.y, -uv.width, uv.height);

        Graphics.DrawTexture(dstRect, src, uv, 0, 0, 0, 0);
        GL.PopMatrix(); RenderTexture.active = bak;
    }

    // ==== IHandSource ====
    public bool TryGetHand(out List<HandKeypoint> kps, out float score)
    {
        kps = null; score = 0f;
        if (haveFrame) { kps = new List<HandKeypoint>(latest); score = overallScore; return true; }
        return false;
    }

    // Nhận ROI từ PalmDetector (qua PalmRoiDriver): có cả score của palm
    public void SetExternalRoi(Rect roi01, float palmScore = -1f)
    {
        roi = roi01; hasRoi = true; lostFrames = 0;
        if (palmScore >= 0f) lastPalmScore = Mathf.Clamp01(palmScore);
    }

    // ==== Debug overlay ====
    void OnGUI()
    {
        if (!debugPreview) return;

        var rect = new Rect(10, 40, 256, 256);

        // 1) Vẽ nền: full camera hay ROI
        if (previewMode == PreviewMode.FullCamera && cam != null)
        {
            GUI.DrawTexture(rect, cam, ScaleMode.ScaleToFit, false);
        }
        else
        {
            GUI.DrawTexture(rect, readTex, ScaleMode.StretchToFill, false);
        }

        if (showRoiLabel)
        {
            string lbl = (hasRoi ? "ROI: tracking" : "ROI: center bootstrap") + $" | score={overallScore:F2}";
            GUI.Label(new Rect(rect.x, rect.y - 18, 260, 18), lbl);
        }

        if (!debugSkeleton || !haveFrame) return;

        // 2) Map keypoint ra toạ độ hiển thị
        Vector2 ToScreen(HandKeypoint p)
        {
            float X = Mathf.Clamp01(p.x), Y = Mathf.Clamp01(p.y);
            if (previewMode == PreviewMode.FullCamera)
            {
                float gx = roi.x + X * roi.width;
                float gy = roi.y + Y * roi.height;
                float sx = rect.x + gx * rect.width;
                float sy = rect.y + gy * rect.height;
                return new Vector2(sx, sy);
            }
            else
            {
                return new Vector2(rect.x + X * rect.width,
                                   rect.y + Y * rect.height);
            }
        }

        if (white1 == null) { white1 = new Texture2D(1, 1); white1.SetPixel(0, 0, Color.white); white1.Apply(); }

        var bakCol = GUI.color; GUI.color = Color.green;
        foreach (var (a, b) in BONES)
        {
            var pa = ToScreen(latest[a]); var pb = ToScreen(latest[b]);
            DrawLine(pa, pb, lineThickness);
        }
        GUI.color = Color.yellow;
        for (int i = 0; i < 21; i++)
        {
            var p = ToScreen(latest[i]);
            GUI.DrawTexture(new Rect(p.x - pointSize * 0.5f, p.y - pointSize * 0.5f, pointSize, pointSize), white1);
        }
        GUI.color = bakCol;

        // Ép vẽ chấm cổ tay để test nhanh
        var wrist = ToScreen(latest[0]);
        GUI.DrawTexture(new Rect(wrist.x - 3, wrist.y - 3, 6, 6), white1);
    }

    void DrawLine(Vector2 a, Vector2 b, float w)
    {
        var d = b - a; float len = d.magnitude; if (len < 1e-3f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var bak = GUI.matrix; GUIUtility.RotateAroundPivot(ang, a);
        if (white1 == null) { white1 = new Texture2D(1, 1); white1.SetPixel(0, 0, Color.white); white1.Apply(); }
        GUI.DrawTexture(new Rect(a.x, a.y - w * 0.5f, len, w), white1);
        GUI.matrix = bak;
    }

}
