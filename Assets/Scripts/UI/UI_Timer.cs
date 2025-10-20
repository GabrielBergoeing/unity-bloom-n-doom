using UnityEngine;
using TMPro;

public class UI_Timer : MonoBehaviour
{
    private MatchManager matchManager;
    private TextMeshProUGUI textMeshPro;

    private float updateInterval = 1f;
    private float nextUpdateTime;

    private Color startColor = new Color(1f, 1f, 1f, 0.8f);
    private Color endColor = new Color(1f, 0.2f, 0.2f, 1f);

    private void Awake()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();
    }

    private void Start()
    {
        matchManager = MatchManager.instance;
    }

    private void Update()
    {
        if (matchManager == null)
        {
            if (MatchManager.instance != null)
            {
                matchManager = MatchManager.instance;
                Debug.Log("[UI_Timer] MatchManager reference obtained in Update().");
            }
            else
            {
                Debug.LogWarning("[UI_Timer] MatchManager.instance still NULL. Skipping frame.");
                return;
            }
        }

        if (Time.time >= nextUpdateTime && matchManager != null)
        {
            UpdateTimerDisplay();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    private void UpdateTimerDisplay()
    {
        float remainingTime = Mathf.Max(matchManager.timer, 0f);
        int minutes = Mathf.FloorToInt(remainingTime / 60);
        int seconds = Mathf.FloorToInt(remainingTime % 60);

        textMeshPro.text = $"{minutes:00}:{seconds:00}";

        float t = Mathf.InverseLerp(0f, 900f, remainingTime);
        textMeshPro.color = Color.Lerp(endColor, startColor, t);
    }
}
