using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Barracuda;

[Serializable] public struct Keypoint2D { public int id; public float x, y, score; }

public class BarracudaPoseSource : MonoBehaviour
{
    [Header("Model")]
    public NNModel modelAsset;
    public string inputName = "";                 // để trống sẽ tự lấy inputs[0]
    public string heatmapName = "";               // để trống sẽ tự lấy outputs[0]
    public string offsetsName = "";               // để trống sẽ tự lấy outputs[1]
    public int inputSize = 192;                   // 192: lightning, 256: thunder

    [Header("Preprocess")]
    public bool normalize01 = true;               // MoveNet khuyến nghị 0..1
    public bool mirror = true;                    // lật hình cho giống gương

    [Header("Webcam")]
    public int requestedWidth = 640, requestedHeight = 480, requestedFps = 30;

    [Header("Runtime")]
    public WorkerFactory.Type workerType = WorkerFactory.Type.Auto;

    // --- runtime ---
    Model _model; IWorker _worker; WebCamTexture _cam;
    RenderTexture _rt; Texture2D _read;
    readonly List<Keypoint2D> _latest = new(17);
    readonly object _lock = new();
    bool _loggedOnce;

    // --- debug ---
    public bool debugPreview = true, debugOverlay = true, showModelInput = false;
    Texture2D _dot;
    public WebCamTexture CameraTex => _cam;
    public RenderTexture InputRT => _rt;

    void Start()
    {
        if (!modelAsset) { Debug.LogError("[Pose] Assign NNModel"); enabled = false; return; }

        _model = ModelLoader.Load(modelAsset);

        if (string.IsNullOrEmpty(inputName))
            inputName = (_model.inputs.Count > 0) ? _model.inputs[0].name : "serving_default_input";

        if (string.IsNullOrEmpty(heatmapName) || string.IsNullOrEmpty(offsetsName))
        {
            if (_model.outputs.Count >= 2) { heatmapName = _model.outputs[0]; offsetsName = _model.outputs[1]; }
            else { Debug.LogError("[Pose] Model RAW phải có >=2 outputs (heatmap, offsets)"); enabled = false; return; }
        }

        _worker = WorkerFactory.CreateWorker(workerType, _model);

        _cam = new WebCamTexture(requestedWidth, requestedHeight, requestedFps);
        _cam.Play();

        _rt = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32);
        _read = new Texture2D(inputSize, inputSize, TextureFormat.RGBA32, false);

        Debug.Log($"[Pose] Inputs: {string.Join(", ", _model.inputs.Select(i => i.name))}");
        Debug.Log($"[Pose] Outputs: {string.Join(", ", _model.outputs)}");
    }

    void OnDestroy()
    {
        try { _worker?.Dispose(); } catch { }
        try { _cam?.Stop(); } catch { }
        try { _rt?.Release(); } catch { }
    }

    void Update()
    {
        if (_cam == null || !_cam.didUpdateThisFrame) return;

        // 1) webcam -> RT vuông -> Texture2D
        Graphics.Blit(_cam, _rt);
        var bak = RenderTexture.active; RenderTexture.active = _rt;
        _read.ReadPixels(new Rect(0, 0, inputSize, inputSize), 0, 0, false);
        _read.Apply(false, false);
        RenderTexture.active = bak;

        // 2) Texture2D -> Tensor NHWC (đặt tên tensor = inputName)
        var px = _read.GetPixels32();
        using var input = new Tensor(1, inputSize, inputSize, 3, name: inputName);

        for (int y = 0; y < inputSize; y++)
        {
            int row = y * inputSize;
            for (int x = 0; x < inputSize; x++)
            {
                var c = px[row + x];
                int xx = mirror ? (inputSize - 1 - x) : x;
                if (normalize01)
                {
                    input[0, y, xx, 0] = c.r / 255f; input[0, y, xx, 1] = c.g / 255f; input[0, y, xx, 2] = c.b / 255f;
                }
                else
                {
                    input[0, y, xx, 0] = c.r; input[0, y, xx, 1] = c.g; input[0, y, xx, 2] = c.b;
                }
            }
        }

        // 3) chạy mạng & lấy 2 output
        _worker.Execute(input);
        using var heat = _worker.PeekOutput(heatmapName);
        using var offs = _worker.PeekOutput(offsetsName);

        if (!_loggedOnce) { _loggedOnce = true; Debug.Log($"[Pose] heat {heat.shape} | offs {offs.shape}"); }

        // 4) decode -> 17 điểm
        var kps = DecodeRawHeads(heat, offs, inputSize);
        lock (_lock) { _latest.Clear(); _latest.AddRange(kps); }
    }

    // ====== DECODE: xử lý cả 2 layout (N,H,W,C) và (N,H,K,W) + soft-argmax 3x3 ======
    List<Keypoint2D> DecodeRawHeads(Tensor heat, Tensor offs, int inSize)
    {
        // phát hiện layout
        // layout A (chuẩn): heat C=17, offs C=34
        bool layoutA = (heat.shape.channels == 17 && offs.shape.channels == 34);
        // layout B (đảo trục W<->C): heat W=17, offs W=34
        bool layoutB = (heat.shape.width == 17 && offs.shape.width == 34);

        if (!layoutA && !layoutB)
            Debug.LogWarning($"[Pose] Lạ layout: heat {heat.shape} | offs {offs.shape} (vẫn thử giải mã)");

        int Hm = heat.shape.height;
        int Wm = layoutB ? heat.shape.channels : heat.shape.width; // nếu B: width thật nằm ở channels
        int K = 17;

        float strideY = (float)inSize / Hm;
        float strideX = (float)inSize / Wm;

        float ReadHeat(int y, int x, int k)
            => layoutB ? heat[0, y, k, x] : heat[0, y, x, k];
        float ReadOffY(int y, int x, int k)
            => layoutB ? offs[0, y, 2 * k + 0, x] : offs[0, y, x, 2 * k + 0];
        float ReadOffX(int y, int x, int k)
            => layoutB ? offs[0, y, 2 * k + 1, x] : offs[0, y, x, 2 * k + 1];

        const float TEMP = 2.0f; // softmax nhiệt độ

        var list = new List<Keypoint2D>(K);
        for (int k = 0; k < K; k++)
        {
            // 1) argmax thô
            float best = float.NegativeInfinity; int by = 0, bx = 0;
            for (int y = 0; y < Hm; y++)
                for (int x = 0; x < Wm; x++)
                {
                    float v = ReadHeat(y, x, k);
                    if (v > best) { best = v; by = y; bx = x; }
                }

            // 2) soft-argmax 3x3 quanh đỉnh
            float sumW = 0, offCellY = 0, offCellX = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                int yy = Mathf.Clamp(by + dy, 0, Hm - 1);
                for (int dx = -1; dx <= 1; dx++)
                {
                    int xx = Mathf.Clamp(bx + dx, 0, Wm - 1);
                    float v = ReadHeat(yy, xx, k);
                    float w = Mathf.Exp(v * TEMP);
                    sumW += w;
                    offCellY += w * (yy - by);
                    offCellX += w * (xx - bx);
                }
            }
            float fy = by + (sumW > 1e-6f ? offCellY / sumW : 0f);
            float fx = bx + (sumW > 1e-6f ? offCellX / sumW : 0f);

            // 3) offsets tại ô đỉnh
            float offY = ReadOffY(by, bx, k);
            float offX = ReadOffX(by, bx, k);

            // 4) map về pixel & chuẩn hoá
            float yPix = fy * strideY + offY;
            float xPix = fx * strideX + offX;

            float y01 = Mathf.Clamp01(yPix / inSize);
            float x01 = Mathf.Clamp01(xPix / inSize);
            float sc = Mathf.Clamp01(1f / (1f + Mathf.Exp(-best)));

            list.Add(new Keypoint2D { id = k, x = x01, y = y01, score = sc });
        }
        return list;
    }

    public bool TryGet2D(out List<Keypoint2D> kps)
    {
        lock (_lock) { if (_latest.Count == 17) { kps = new List<Keypoint2D>(_latest); return true; } }
        kps = null; return false;
    }

    void OnGUI()
    {
        if (!debugPreview) return;
        Texture tex = showModelInput ? (Texture)_rt : (Texture)_cam;
        if (!tex) return;

        var rect = new Rect(10, 10, 256, 256);
        var uv = mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
        GUI.DrawTextureWithTexCoords(rect, tex, uv);

        if (debugOverlay && TryGet2D(out var kps))
        {
            if (_dot == null) { _dot = new Texture2D(1, 1); _dot.SetPixel(0, 0, Color.magenta); _dot.Apply(); }
            foreach (var p in kps)
            {
                if (p.score < 0.3f) continue;
                float x = rect.x + p.x * rect.width;
                float y = rect.y + (1f - p.y) * rect.height;
                GUI.DrawTexture(new Rect(x - 3, y - 3, 6, 6), _dot);
            }
        }
    }
}
