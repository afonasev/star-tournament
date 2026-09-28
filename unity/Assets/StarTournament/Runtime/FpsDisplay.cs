using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    // Display-only wall-clock average: never samples fixed ticks or smoothed/scaled delta time.
    public sealed class FpsAverage
    {
        // Technical telemetry cadence, not a gameplay or readability balance parameter.
        const double IntervalSeconds = 0.5;
        double elapsed;
        int frames;
        public double Value { get; private set; }
        public bool Add(double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return false;
            elapsed += seconds;
            frames++;
            if (elapsed < IntervalSeconds) return false;
            Value = frames / elapsed;
            elapsed = 0;
            frames = 0;
            return true;
        }
    }

    public sealed class FpsDisplay : MonoBehaviour
    {
        public const string PreferenceKey = "StarTournament.Settings.ShowFps";
        readonly FpsAverage average = new FpsAverage();
        Text counter, setting;
        bool matchActive;
        public bool Visible { get; private set; }
        public string CurrentText => counter ? counter.text : "FPS —";
        public void Initialize(Font font, int fontSize, Text settingLabel)
        {
            setting = settingLabel;
            var panel = new GameObject("fps-overlay", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            // Layout dimensions derive from the existing profile font, keeping HUD's left corner free.
            rect.anchoredPosition = new Vector2(-fontSize, -fontSize);
            rect.sizeDelta = new Vector2(fontSize * 7, fontSize * 1.6f);
            panel.GetComponent<Image>().color = new Color32(10, 18, 30, 230);
            panel.GetComponent<Image>().raycastTarget = false;
            var label = new GameObject("fps-value", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            counter = label.GetComponent<Text>();
            counter.rectTransform.anchorMin = Vector2.zero;
            counter.rectTransform.anchorMax = Vector2.one;
            counter.rectTransform.offsetMin = counter.rectTransform.offsetMax = Vector2.zero;
            counter.font = font; counter.fontSize = fontSize;
            counter.alignment = TextAnchor.MiddleCenter;
            counter.color = Color.white; counter.raycastTarget = false;
            counter.text = "FPS —";
            Visible = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
            Refresh();
        }
        public void Toggle()
        {
            Visible = !Visible;
            PlayerPrefs.SetInt(PreferenceKey, Visible ? 1 : 0);
            PlayerPrefs.Save(); // Write only on explicit settings changes, never per frame.
            Refresh();
        }
        public void SetSessionVisibility(bool visible)
        {
            Visible = visible;
            Refresh();
        }
        public void RestoreGeneralPreference() => SetSessionVisibility(PlayerPrefs.GetInt(PreferenceKey, 1) != 0);
        public void SetMatchActive(bool active)
        {
            matchActive = active;
            Refresh();
        }
        void Refresh()
        {
            counter.transform.parent.gameObject.SetActive(Visible && matchActive);
            setting.text = "Настройки · Показывать FPS: " + (Visible ? "вкл" : "выкл");
        }
        void Update()
        {
            if (counter && average.Add(Time.unscaledDeltaTime))
                counter.text = "FPS " + Math.Round(average.Value).ToString("0");
        }
    }
}
