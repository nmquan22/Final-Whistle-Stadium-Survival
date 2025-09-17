using UnityEngine;
using System;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using PoseSystem;

public class PythonHandSource : MonoBehaviour, IHandSource
{
    [Header("WebSocket")]
    public string url = "ws://127.0.0.1:8765";
    public bool mirrorLogic = false;

    ClientWebSocket ws;
    CancellationTokenSource cts;
    readonly List<HandKeypoint> latest = new(21);
    volatile bool hasFrame = false;
    volatile float overall = 0f;

    public Texture2D PreviewTex { get; private set; }

    [Serializable] class HandPoint { public int id; public float x, y, z, score; }
    [Serializable] class Packet { public int w, h, iw, ih; public float ts, score; public string img; public HandPoint[] hand; }

    async void Start()
    {
        ws = new ClientWebSocket();
        cts = new CancellationTokenSource();
        try
        {
            await ws.ConnectAsync(new Uri(url), cts.Token);
            _ = ReceiveLoop();
            Debug.Log("[PythonHandSource] Connected " + url);
        }
        catch (Exception e)
        {
            Debug.LogError("[PythonHandSource] Connect fail: " + e.Message);
            enabled = false;
        }
    }

    async Task ReceiveLoop()
    {
        var buf = new ArraySegment<byte>(new byte[1 << 16]); // 64KB
        while (ws.State == WebSocketState.Open)
        {
            var sb = new StringBuilder();
            WebSocketReceiveResult r;
            do
            {
                r = await ws.ReceiveAsync(buf, cts.Token);
                sb.Append(Encoding.UTF8.GetString(buf.Array, 0, r.Count));
            } while (!r.EndOfMessage && !cts.IsCancellationRequested);

            try
            {
                var pkt = JsonUtility.FromJson<Packet>(sb.ToString());
                // landmarks
                if (pkt != null && pkt.hand != null && pkt.hand.Length == 21)
                {
                    latest.Clear();
                    for (int i = 0; i < 21; i++)
                    {
                        var p = pkt.hand[i];
                        float x = mirrorLogic ? 1f - p.x : p.x;
                        latest.Add(new HandKeypoint { id = i, x = x, y = p.y, z = p.z, score = p.score });
                    }
                    overall = Mathf.Clamp01(pkt.score);
                    hasFrame = true;
                }
                else hasFrame = false;

                // preview image
                if (!string.IsNullOrEmpty(pkt.img))
                {
                    var bytes = Convert.FromBase64String(pkt.img);
                    if (PreviewTex == null) PreviewTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    PreviewTex.LoadImage(bytes); // cập nhật texture
                }
            }
            catch (Exception e) { Debug.LogWarning("[PythonHandSource] Parse fail: " + e.Message); }
        }
    }

    public bool TryGetHand(out List<HandKeypoint> kps, out float score)
    {
        kps = null; score = 0;
        if (hasFrame) { kps = new List<HandKeypoint>(latest); score = overall; return true; }
        return false;
    }

    void OnDestroy() { try { cts?.Cancel(); ws?.Dispose(); } catch { } }
}
