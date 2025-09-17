using System;
using System.Collections.Generic;
using UnityEngine;
using TensorFlowLite;

#region (Optional) Shared types – xóa nếu bạn đã có ở file khác
[Serializable] public struct Keypoint2D { public int id; public float x, y, score; }
public interface IPoseSource2D { bool TryGet2D(out List<Keypoint2D> kps); }
#endregion

public class TFLitePoseSource : MonoBehaviour, IPoseSource2D
{
    public enum FloatInputRange
    {
        ZeroTo255,     // Mặc định cho MoveNet Thunder (.tflite từ Kaggle)
        ZeroToOne,     // 0..1
        MinusOneToOne  // [-1, 1]
    }

    [Header("Model (.tflite)")]
    [Tooltip("Kéo file .tflite hoặc .tflite.bytes (TextAsset) vào đây")]
    public TextAsset modelFile;
    [Tooltip("192: Lightning, 256: Thunder")]
    public int inputSize = 256;

    [Header("Preprocess")]
    public bool letterbox = true;   // giữ tỉ lệ như tf.image.resize_with_pad
    public bool mirror = true;      // lật gương
    public FloatInputRange floatRange = FloatInputRange.ZeroTo255; // Thunder = ZeroTo255

    [Header("Runtime")]
    [Range(1, 8)] public int threads = 2;

    [Header("Overlay")]
    public bool debugPreview = true;
    public bool debugOverlay = true;
    public bool showScores = true;
    public bool showSkeleton = true;
    [Range(0f, 1f)] public float minScoreToDraw = 0.25f;
    public float pointSize = 6f;
    public float lineThickness = 3f;
    public int fontSize = 12;

    // --- runtime ---
    Interpreter _interpreter;
    WebCamTexture _cam;
    RenderTexture _rt;
    Texture2D _read;
    Texture2D _white1;
    GUIStyle _labelStyle;

    bool _inputIsFloat;     // true nếu input tensor là float (Thunder), false nếu quantized u8
    float[] _inputF;
    byte[] _inputU8;

    float[] _outKey;        // flat output
    int[] _outShape;        // vd: [1,1,17,3]

    readonly List<Keypoint2D> _latest = new(17);
    float _emaFps; const float _emaA = 0.1f;

    // COCO bones
    static readonly (int a, int b)[] BONES = new (int, int)[]
    {
        (5,6),(5,7),(7,9),(6,8),(8,10),(11,12),
        (11,13),(13,15),(12,14),(14,16),
        (5,11),(6,12),(0,1),(1,3),(0,2),(2,4)
    };

    void Start()
    {
        if (!modelFile)
        {
            Debug.LogError("[Pose] Chưa gán Model (.tflite) cho TFLitePoseSource");
            enabled = false; return;
        }

        // Webcam
        _cam = new WebCamTexture(640, 480, 30);
        _cam.Play();

        // Input textures
        _rt = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32);
        _read = new Texture2D(inputSize, inputSize, TextureFormat.RGBA32, false);
        _white1 = new Texture2D(1, 1); _white1.SetPixel(0, 0, Color.white); _white1.Apply();

        // Interpreter
        var opt = new InterpreterOptions() { threads = threads };
        // Trên mobile có thể thử GPU delegate:
        // opt.AddGpuDelegate();
        _interpreter = new Interpreter(modelFile.bytes, opt);

        // Input/output tensors
        var inInfo = _interpreter.GetInputTensorInfo(0);
        _interpreter.ResizeInputTensor(0, new int[] { 1, inputSize, inputSize, 3 });
        _interpreter.AllocateTensors();

        // Một số bản plugin không expose enum TensorType ⇒ check bằng string
        _inputIsFloat = inInfo.type.ToString().ToLower().Contains("float");

        int inLen = inputSize * inputSize * 3;
        if (_inputIsFloat) _inputF = new float[inLen];
        else _inputU8 = new byte[inLen];

        _outShape = _interpreter.GetOutputTensorInfo(0).shape;
        int outLen = 1; for (int i = 0; i < _outShape.Length; i++) outLen *= _outShape[i];
        _outKey = new float[outLen];

        Debug.Log($"[Pose] input type={inInfo.type} shape=[1,{inputSize},{inputSize},3]");
        Debug.Log($"[Pose] output0 shape=({string.Join(",", _outShape)}) len={outLen}");
    }

    void OnDestroy()
    {
        try { _interpreter?.Dispose(); } catch { }
        try { _cam?.Stop(); } catch { }
        try { _rt?.Release(); } catch { }
    }

    void Update()
    {
        if (_cam == null || !_cam.didUpdateThisFrame) return;

        // 1) webcam → RT (resize/pad)
        if (letterbox) BlitLetterbox(_cam, _rt, mirror);
        else Graphics.Blit(_cam, _rt);

        // đọc về Texture2D
        var bak = RenderTexture.active; RenderTexture.active = _rt;
        _read.ReadPixels(new Rect(0, 0, inputSize, inputSize), 0, 0, false);
        _read.Apply(false);
        RenderTexture.active = bak;

        // 2) điền input
        var px = _read.GetPixels32();
        if (_inputIsFloat)
        {
            // Thunder yêu cầu float32 **trong khoảng 0..255** (KHÔNG chia 255)
            int t = 0;
            switch (floatRange)
            {
                case FloatInputRange.ZeroTo255:
                    for (int i = 0; i < px.Length; i++) { _inputF[t++] = px[i].r; _inputF[t++] = px[i].g; _inputF[t++] = px[i].b; }
                    break;
                case FloatInputRange.ZeroToOne:
                    for (int i = 0; i < px.Length; i++) { _inputF[t++] = px[i].r / 255f; _inputF[t++] = px[i].g / 255f; _inputF[t++] = px[i].b / 255f; }
                    break;
                case FloatInputRange.MinusOneToOne:
                    for (int i = 0; i < px.Length; i++)
                    {
                        _inputF[t++] = (px[i].r - 127.5f) / 127.5f;
                        _inputF[t++] = (px[i].g - 127.5f) / 127.5f;
                        _inputF[t++] = (px[i].b - 127.5f) / 127.5f;
                    }
                    break;
            }
            _interpreter.SetInputTensorData(0, _inputF);
        }
        else
        {
            // Quantized u8
            int t = 0;
            for (int i = 0; i < px.Length; i++) { _inputU8[t++] = px[i].r; _inputU8[t++] = px[i].g; _inputU8[t++] = px[i].b; }
            _interpreter.SetInputTensorData(0, _inputU8);
        }

        // 3) infer
        _interpreter.Invoke();

        // 4) decode output (17x3: y, x, score)
        _interpreter.GetOutputTensorData(0, _outKey);
        Decode17x3(_outKey, _outShape, _latest);

#if UNITY_EDITOR
        if (Time.frameCount % 15 == 0)
        {
            float mean = 0f, mx = 0f; int n = 0;
            foreach (var p in _latest) { mean += p.score; if (p.score > mx) mx = p.score; n++; }
            mean = (n > 0) ? mean / n : 0f;
            Debug.Log($"[Pose] meanScore={mean:0.00} max={mx:0.00}");
        }
#endif

        // fps smoothed
        float fps = 1f / Mathf.Max(Time.deltaTime, 1e-6f);
        _emaFps = (_emaFps == 0f) ? fps : Mathf.Lerp(_emaFps, fps, _emaA);
    }

    // ==== decode MoveNet (… , 17, 3) ====
    static void Decode17x3(float[] flat, int[] shape, List<Keypoint2D> dst)
    {
        int dims = shape.Length;
        if (dims < 2 || shape[dims - 2] != 17 || shape[dims - 1] != 3)
        {
            Debug.LogWarning("[Pose] Output không phải dạng (...,17,3). Hãy dùng MoveNet singlepose.");
            return;
        }

        dst.Clear();
        int idx = 0;
        for (int k = 0; k < 17; k++)
        {
            float y = flat[idx + 0];
            float x = flat[idx + 1];
            float s = flat[idx + 2];
            idx += 3;
            dst.Add(new Keypoint2D { id = k, x = Mathf.Clamp01(x), y = Mathf.Clamp01(y), score = Mathf.Clamp01(s) });
        }
    }

    // ==== letterbox blit (giữ tỉ lệ) ====
    static void BlitLetterbox(Texture src, RenderTexture dst, bool mirror)
    {
        var bak = RenderTexture.active;
        RenderTexture.active = dst;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, dst.width, dst.height, 0);
        GL.Clear(true, true, Color.black);

        float sa = (float)src.width / src.height;
        float da = (float)dst.width / dst.height;

        Rect r;
        if (sa > da)
        {
            float newH = dst.width / sa;
            float y = (dst.height - newH) * 0.5f;
            r = new Rect(0, y, dst.width, newH);
        }
        else
        {
            float newW = dst.height * sa;
            float x = (dst.width - newW) * 0.5f;
            r = new Rect(x, 0, newW, dst.height);
        }

        var uv = mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
        Graphics.DrawTexture(r, src, uv, 0, 0, 0, 0);
        GL.PopMatrix();

        RenderTexture.active = bak;
    }

    // ==== overlay/debug ====
    void OnGUI()
    {
        if (!debugPreview) return;
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
            var st = _labelStyle.normal; st.textColor = Color.white; _labelStyle.normal = st;
        }
        if (_white1 == null) { _white1 = new Texture2D(1, 1); _white1.SetPixel(0, 0, Color.white); _white1.Apply(); }
        if (_cam == null || _cam.width < 16) return;

        var rect = new Rect(10, 10, 256, 256);
        var uv = mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
        GUI.DrawTextureWithTexCoords(rect, _read ? (Texture)_read : (Texture)_cam, uv);

        if (!debugOverlay || _latest.Count != 17) return;

        Vector2 ToScreen(Keypoint2D p) =>
            new Vector2(rect.x + p.x * rect.width, rect.y + (1f - p.y) * rect.height);

        // skeleton
        if (showSkeleton)
        {
            foreach (var (a, b) in BONES)
            {
                var pa = _latest[a]; var pb = _latest[b];
                if (pa.score < minScoreToDraw || pb.score < minScoreToDraw) continue;
                DrawLine(ToScreen(pa), ToScreen(pb), lineThickness, Color.red);
            }
        }

        // points + scores
        float avg = 0; int cnt = 0;
        for (int i = 0; i < 17; i++)
        {
            var p = _latest[i];
            if (p.score < minScoreToDraw) continue;
            avg += p.score; cnt++;

            var P = ToScreen(p);
            DrawDot(P, pointSize, Color.Lerp(Color.red, Color.green, p.score));
            if (showScores) DrawLabel(P + new Vector2(6, -2), $"{i}:{p.score:0.00}");
        }
        if (cnt > 0) avg /= cnt;

        var hud = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(rect.x, rect.y - 18, rect.width, 18), $"FPS:{_emaFps:0}  mean:{avg:0.00}", hud);
    }

    void DrawDot(Vector2 p, float size, Color color)
    {
        var r = new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size);
        var bak = GUI.color; GUI.color = color;
        GUI.DrawTexture(r, _white1); GUI.color = bak;
    }
    void DrawLine(Vector2 a, Vector2 b, float thickness, Color color)
    {
        var bakCol = GUI.color; var bakMat = GUI.matrix;
        Vector2 d = b - a; float len = d.magnitude; if (len < 1e-3f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(ang, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), _white1);
        GUI.matrix = bakMat; GUI.color = bakCol;
    }
    void DrawLabel(Vector2 pos, string text)
    {
        var size = _labelStyle.CalcSize(new GUIContent(text));
        var back = new Rect(pos.x - 2, pos.y - size.y + 2, size.x + 4, size.y);
        var bak = GUI.color; GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(back, _white1); GUI.color = bak;
        GUI.Label(new Rect(pos.x, pos.y - size.y, size.x, size.y), text, _labelStyle);
    }

    // === IPoseSource2D ===
    public bool TryGet2D(out List<Keypoint2D> kps)
    {
        if (_latest.Count == 17) { kps = new List<Keypoint2D>(_latest); return true; }
        kps = null; return false;
    }
}
