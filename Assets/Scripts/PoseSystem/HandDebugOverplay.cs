using UnityEngine;
using System.Collections.Generic;
using PoseSystem;

public class HandDebugOverlay : MonoBehaviour
{
    public MonoBehaviour sourceBehaviour;    // PythonHandSource hoặc TFLiteHandSource (IHandSource)
    public PythonHandSource pythonPreview;   // tham chiếu để lấy PreviewTex
    public TFLiteHandSource tflitePreview;   // nếu dùng TFLite: lấy WebCamTexture của nó
    public Rect rect = new Rect(10, 10, 320, 240);
    public bool invertY = false;       // GUI y-down → nên bật

    IHandSource source;
    Texture2D white1;

    static readonly (int a, int b)[] BONES = new (int, int)[]{
        (0,1),(1,2),(2,3),(3,4),(0,5),(5,6),(6,7),(7,8),
        (0,9),(9,10),(10,11),(11,12),(0,13),(13,14),(14,15),(15,16),
        (0,17),(17,18),(18,19),(19,20)
    };

    void Awake()
    {
        source = sourceBehaviour as IHandSource;
        white1 = new Texture2D(1, 1); white1.SetPixel(0, 0, Color.white); white1.Apply();
    }

    void OnGUI()
    {
        // 1) Vẽ nền video nếu có
        Texture preview = null;
        if (pythonPreview && pythonPreview.PreviewTex) preview = pythonPreview.PreviewTex;
        else if (tflitePreview) preview = tflitePreview.GetCameraTexture();
        if (preview) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, false);

        if (source == null) return;
        if (!source.TryGetHand(out var kps, out var s)) return;

        // 2) Vẽ skeleton
        Vector2 ToScreen(HandKeypoint p)
        {
            float x = Mathf.Clamp01(p.x);
            float y = Mathf.Clamp01(p.y);
            if (invertY) y = 1f - y;
            return new Vector2(rect.x + x * rect.width, rect.y + y * rect.height);
        }

        GUI.color = Color.green;
        foreach (var (a, b) in BONES)
        {
            var A = ToScreen(kps[a]); var B = ToScreen(kps[b]);
            DrawLine(A, B, 2f);
        }
        GUI.color = Color.yellow;
        for (int i = 0; i < kps.Count; i++)
        {
            var p = ToScreen(kps[i]);
            GUI.DrawTexture(new Rect(p.x - 2, p.y - 2, 4, 4), white1);
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x, rect.y - 18, 240, 18), $"score≈{s:0.00}");
    }

    void DrawLine(Vector2 a, Vector2 b, float w)
    {
        var d = b - a; float len = d.magnitude; if (len < 1e-3f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var bak = GUI.matrix; GUIUtility.RotateAroundPivot(ang, a);
        GUI.DrawTexture(new Rect(a.x, a.y - w * 0.5f, len, w), white1);
        GUI.matrix = bak;
    }
}
