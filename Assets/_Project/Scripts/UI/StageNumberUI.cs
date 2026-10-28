using UnityEngine;
using UnityEngine.UI;

// 현재 몇 번째 스테이지인지(StageProgress.CurrentStageNo)를 타이머 위에 "N층"으로 표시한다.
// 2층부터는 옆에 그 층의 자원 획득 배율(MonsterDifficulty.ResourceAmountMultiplier)도 작게 붙여서, 깊이 갈수록 더 많이 받는다는 걸 보여 준다.
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
        float resourceScale = MonsterDifficulty.ResourceAmountMultiplier;
        numberText.text = resourceScale > 1.001f
            ? $"{StageProgress.CurrentStageNo}층  <size=16><color=#FFCA4A>자원 x{resourceScale:0.0}</color></size>"
            : StageProgress.CurrentStageNo + "층";
    }
}
