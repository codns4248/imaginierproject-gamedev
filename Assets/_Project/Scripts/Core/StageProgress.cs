// 런 중 현재 층수(스테이지 번호)를 추적하는 정적 유틸 (ResourceBank와 같은 스타일).
// docs/schema.sql의 stage.stage_no / return_available_yn(stage_no % 5 = 0) 규칙을 그대로 반영한다.
// 실제 구역별 씬(숲/공장/오염호수 등, 김성철 브랜치 머지 예정)은 아직 없어서,
// 지금은 로직 뼈대만 두고 StageExtraction이 같은 MainScene을 재시작하는 식으로 "다음 층 이동"을 흉내낸다.
public static class StageProgress
{
    private const int ExtractionInterval = 5;

    public static int CurrentStageNo { get; private set; } = 1;

    public static bool IsExtractionFloor => CurrentStageNo % ExtractionInterval == 0;

    public static void AdvanceToNextStage()
    {
        CurrentStageNo++;
    }

    // 로비 도착(클리어/추출) 또는 사망으로 런이 끝나면 다음 런은 다시 1층부터 시작한다.
    public static void ResetToFirstStage()
    {
        CurrentStageNo = 1;
    }
}
