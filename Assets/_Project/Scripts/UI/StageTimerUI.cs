using UnityEngine;
using UnityEngine.UI;

// StageTimer의 남은 시간을 "분:초" 형식(예: 05:00)으로 화면에 표시한다.
[RequireComponent(typeof(Text))]
public class StageTimerUI : MonoBehaviour
{
    public StageTimer stageTimer;

    private Text timerText;

    void Awake()
    {
        timerText = GetComponent<Text>();
    }

    void OnEnable()
    {
        if (stageTimer != null) stageTimer.OnTimeChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (stageTimer != null) stageTimer.OnTimeChanged -= Refresh;
    }

    private void Refresh()
    {
        if (stageTimer == null || timerText == null) return;

        float t = Mathf.Max(0f, stageTimer.RemainingTime);
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
