using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;

[Serializable] public struct Keypoint2D { public int id; public float x, y, score; }

public class BarracudaPoseSource : MonoBehaviour
{
    [Header("Model")]
    public NNModel modelAsset;
    public string inputName = "input";
    public string outputName = "output";
    public int inputSize = 192;
    [Header("Webcam")]
    public int requestedWidth = 640, requestedHeight = 480, requestedFps = 30;
    public bool mirror = true;
    [Header("Runtime")]
    public WorkerFactory.Type workerType = WorkerFactory.Type.Auto;

    Model _model; IWorker _worker; WebCamTexture _cam;
    RenderTexture _rt; Texture2D _read; List<Keypoint2D> _latest = new(17);
    readonly object _lock = new();

    void Start()
    {
        if (!modelAsset) { Debug.LogError("Assign NNModel"); enabled = false; return; }
        _model = ModelLoader.Load(modelAsset);
        if (string.IsNullOrEmpty(inputName) && _model.inputs.Count > 0) inputName = _model.inputs[0].name;
        if (string.IsNullOrEmpty(outputName) && _model.outputs.Count > 0) outputName = _model.outputs[0];
        _worker = WorkerFactory.CreateWorker(workerType, _model);
        _cam = new WebCamTexture(requestedWidth, requestedHeight, requestedFps); _cam.Play();
        _rt = new RenderTexture(inputSize, inputSize, 0, RenderTextureFormat.ARGB32);
        _read = new Texture2D(inputSize, inputSize, TextureFormat.RGBA32, false);
    }
    void OnDestroy() { try { _worker?.Dispose(); } catch { } try { _cam?.Stop(); } catch { } try { _rt?.Release(); } catch { } }

    void Update()
    {
        if (_cam == null || !_cam.didUpdateThisFrame) return;
        Graphics.Blit(_cam, _rt);
        var bak = RenderTexture.active; RenderTexture.active = _rt;
        _read.ReadPixels(new Rect(0, 0, inputSize, inputSize), 0, 0, false); _read.Apply(false, false);
        RenderTexture.active = bak;

        var px = _read.GetPixels32();
        using var input = new Tensor(1, inputSize, inputSize, 3);
        for (int y = 0; y < inputSize; y++)
        {
            int row = y * inputSize;
            for (int x = 0; x < inputSize; x++)
            {
                var c = px[row + x]; float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                int xx = mirror ? (inputSize - 1 - x) : x;
                input[0, y, xx, 0] = r; input[0, y, xx, 1] = g; input[0, y, xx, 2] = b;
            }
        }
        _worker.Execute(new Dictionary<string, Tensor> { { inputName, input } });
        using var o = _worker.PeekOutput(outputName);
        var kps = ParseMoveNet(o);
        lock (_lock) { _latest.Clear(); _latest.AddRange(kps); }
    }

    List<Keypoint2D> ParseMoveNet(Tensor o)
    {
        var s = o.shape; var list = new List<Keypoint2D>(17);
        if (s.rank == 4 && s.batch == 1 && s.height == 1 && s.width == 17 && s.channels >= 3)
        {
            for (int i = 0; i < 17; i++)
            {
                float y = o[0, 0, i, 0], x = o[0, 0, i, 1], sc = o[0, 0, i, 2];
                list.Add(new Keypoint2D { id = i, x = Mathf.Clamp01(x), y = Mathf.Clamp01(y), score = Mathf.Clamp01(sc) });
            }
        }
        else if (s.rank == 3 && s.batch == 1 && s.height == 17 && s.width >= 3)
        {
            for (int i = 0; i < 17; i++)
            {
                float y = o[0, i, 0], x = o[0, i, 1], sc = o[0, i, 2];
                list.Add(new Keypoint2D { id = i, x = Mathf.Clamp01(x), y = Mathf.Clamp01(y), score = Mathf.Clamp01(sc) });
            }
        }
        else Debug.LogWarning($"Unexpected output shape: {s}");
        return list;
    }

    public bool TryGet2D(out List<Keypoint2D> kps)
    {
        lock (_lock) { if (_latest.Count == 17) { kps = new List<Keypoint2D>(_latest); return true; } }
        kps = null; return false;
    }
}
