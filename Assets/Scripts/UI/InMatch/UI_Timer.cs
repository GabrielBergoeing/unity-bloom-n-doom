using UnityEngine;
using TMPro;

public class UI_Timer : MonoBehaviour
{
    private TextMeshProUGUI textMeshPro;

    private float updateInterval = 1f;
    private float nextUpdateTime;
    private float totalTime;

    private Color startColor = new Color(1f, 1f, 1f, 0.8f);
    private Color endColor = new Color(1f, 0.2f, 0.2f, 1f);

    private void Awake()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();
        totalTime = GameManager.instance.currentLevel.matchDuration;
    }

    private void Update()
    {
        float t = GetTimer();
        if (t < 0f) return;

        if (Time.time >= nextUpdateTime)
        {
            UpdateTimerDisplay(t);
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    private float GetTimer()
    {
        if (OnlineMatchManager.instance != null) return OnlineMatchManager.instance.timer;
        if (MatchManager.instance != null)       return MatchManager.instance.timer;
        return -1f;
    }

    private void UpdateTimerDisplay(float remainingTime)
    {
        remainingTime = Mathf.Max(remainingTime, 0f);
        int minutes = Mathf.FloorToInt(remainingTime / 60);
        int seconds = Mathf.FloorToInt(remainingTime % 60);

        textMeshPro.text = $"{minutes:00}:{seconds:00}";

        float t = Mathf.InverseLerp(0f, totalTime, remainingTime);
        textMeshPro.color = Color.Lerp(endColor, startColor, t);
    }
}
