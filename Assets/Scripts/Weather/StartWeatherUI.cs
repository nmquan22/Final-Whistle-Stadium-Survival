using UnityEngine;
using UnityEngine.UI;   // Slider
using TMPro;            // TMP_Dropdown, TMP_Text
using System.Collections.Generic;

public class StartWeatherUI : MonoBehaviour
{
    public TMP_Dropdown weatherDropdown;  // <-- dropdown TMP
    public Slider timeOfDaySlider;        // 0..1
    public TMP_Text sliderLabel;          // nhãn hiển thị giờ

    static readonly List<string> kOptions = new() { "Clear", "Rain", "Snow", "Foggy", "Night" };

    void Start()
    {
        // Ép danh sách dropdown đúng 5 mục
        if (weatherDropdown)
        {
            weatherDropdown.ClearOptions();
            weatherDropdown.AddOptions(kOptions);
            var gf = GameFlow.Instance;
            weatherDropdown.value = gf ? (int)gf.SelectedWeather : 0;
            weatherDropdown.RefreshShownValue();
        }

        if (timeOfDaySlider)
        {
            timeOfDaySlider.minValue = 0f;
            timeOfDaySlider.maxValue = 1f;
            timeOfDaySlider.wholeNumbers = false;
            if (GameFlow.Instance) timeOfDaySlider.value = GameFlow.Instance.TimeOfDay01;

            UpdateSliderLabel(timeOfDaySlider.value);
            timeOfDaySlider.onValueChanged.AddListener(UpdateSliderLabel);
        }
    }

    void UpdateSliderLabel(float v)
    {
        if (!sliderLabel) return;
        var (hh, mm) = ToClock(v);
        sliderLabel.text = $"{hh:00}:{mm:00}";
    }

    (int hh, int mm) ToClock(float t01)
    {
        float h = Mathf.Repeat(t01, 1f) * 24f;
        int hh = Mathf.FloorToInt(h);
        int mm = Mathf.FloorToInt((h - hh) * 60f);
        return (hh, mm);
    }

    public void CommitToGameFlow()
    {
        var gf = GameFlow.Instance; if (!gf) return;
        if (weatherDropdown) gf.SelectedWeather = (GameFlow.WeatherKind)weatherDropdown.value;
        if (timeOfDaySlider) gf.TimeOfDay01 = timeOfDaySlider.value;
    }
}
