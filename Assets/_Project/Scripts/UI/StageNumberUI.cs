using UnityEngine;
using UnityEngine.UI;

// 현재 몇 번째 스테이지인지(StageProgress.CurrentStageNo)를 타이머 위에 "N층"으로 표시한다.
[RequireComponent(typeof(Text))]
public class StageNumberUI : MonoBehaviour
{
    public StageTimer stageTimer;

    private Text numberText;

    void Awake()
    {
        numberText = GetComponent<Text>();
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
        if (numberText == null) return;
        numberText.text = StageProgress.CurrentStageNo + "층";
    }
}
