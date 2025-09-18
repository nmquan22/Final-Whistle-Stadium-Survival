using UnityEngine;

public class WeatherApply : MonoBehaviour
{
    public Light sun;                 // Kéo Directional Light vào đây
    public ParticleSystem rain;       // (tuỳ chọn) hạt mưa
    public ParticleSystem snow;       // (tuỳ chọn) hạt tuyết
    public Material skyboxMat;        // (tuỳ chọn) Skybox có _Exposure

    void Start()
    {
        var gf = GameFlow.Instance; if (!gf) return;

        // reset nhanh
        if (rain) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (snow) snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        RenderSettings.fog = false;

        // áp thời tiết
        switch (gf.SelectedWeather)
        {
            case GameFlow.WeatherKind.Rain:
                if (rain) rain.Play();
                RenderSettings.fog = true; RenderSettings.fogDensity = 0.015f; break;
            case GameFlow.WeatherKind.Snow:
                if (snow) snow.Play();
                RenderSettings.fog = true; RenderSettings.fogDensity = 0.010f; break;
            case GameFlow.WeatherKind.Foggy:
                RenderSettings.fog = true; RenderSettings.fogDensity = 0.020f; break;
            case GameFlow.WeatherKind.Night:
                if (sun) sun.intensity = 0.25f;
                if (skyboxMat && skyboxMat.HasProperty("_Exposure")) skyboxMat.SetFloat("_Exposure", 0.6f);
                break;
            default: // Clear
                if (sun) sun.intensity = 1.1f;
                if (skyboxMat && skyboxMat.HasProperty("_Exposure")) skyboxMat.SetFloat("_Exposure", 1.2f);
                break;
        }

        // thời gian trong ngày → xoay mặt trời (0=đêm, 0.5=trưa)
        if (sun)
        {
            float angle = Mathf.Lerp(-90f, 270f, gf.TimeOfDay01);
            sun.transform.rotation = Quaternion.Euler(angle, 30f, 0f);
        }
    }
}
