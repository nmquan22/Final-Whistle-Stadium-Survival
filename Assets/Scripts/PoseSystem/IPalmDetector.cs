public interface IPalmDetector
{
    // ROI chuẩn hoá [0..1] (gốc trái-trên) và score
    bool TryGetBestPalm(out UnityEngine.Rect roi01, out float score);
}
