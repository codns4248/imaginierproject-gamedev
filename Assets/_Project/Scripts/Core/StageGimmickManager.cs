using UnityEngine;

// 스테이지 입장 시 확률적으로 기믹(GimmickType)을 하나 뽑아 스테이지 동안 유지시키는 트리거 프레임워크.
// 실제 연출/효과(안개 UI, 비 파티클, 몬스터 가속, 벼락 장판, 공격속도 저하 등)는 이 클래스가 몰라도 된다 -
// 각 효과 구현은 OnGimmickStart/OnGimmickEnd를 구독하거나, 매 프레임 IsActive()로 직접 물어보면 된다
// (예: PlayerMovement가 IsActive(GimmickType.HeavyRain)로 자기 속도에 배율을 곱할지 판단).
public static class StageGimmickManager
{
    // 스테이지 입장마다 기믹이 뜰 확률. 여기 숫자 하나만 바꾸면 된다 (0~1).
    private const float TriggerChance = 0.2f;

    private static readonly GimmickType[] AllGimmicks =
    {
        GimmickType.Fog,
        GimmickType.HeavyRain,
        GimmickType.Typhoon,
        GimmickType.Lightning,
        GimmickType.ColdWave,
    };

    public static GimmickType? CurrentGimmick { get; private set; }

    public static event System.Action<GimmickType> OnGimmickStart;
    public static event System.Action<GimmickType> OnGimmickEnd;

    // 스테이지에 새로 들어올 때마다 호출한다 (StageManager.EnterZone). 이전 기믹을 먼저 정리하고
    // TriggerChance 확률로 당첨되면 5종 중 하나를 무작위로 골라 시작시킨다.
    public static void RollForStage()
    {
        ClearGimmick();

        if (Random.value >= TriggerChance) return;

        GimmickType picked = AllGimmicks[Random.Range(0, AllGimmicks.Length)];
        CurrentGimmick = picked;
        Debug.Log($"[StageGimmick] 기믹 발생: {picked}");
        OnGimmickStart?.Invoke(picked);
    }

    // 스테이지를 벗어날 때(거점 복귀, 다음 스테이지 입장 직전) 호출해 현재 기믹을 종료시킨다.
    public static void ClearGimmick()
    {
        if (CurrentGimmick == null) return;

        GimmickType ended = CurrentGimmick.Value;
        CurrentGimmick = null;
        Debug.Log($"[StageGimmick] 기믹 종료: {ended}");
        OnGimmickEnd?.Invoke(ended);
    }

    public static bool IsActive(GimmickType type) => CurrentGimmick == type;

    // === 효과 배율 (각 소비 스크립트가 매 프레임 직접 읽어서 쓴다. 이 클래스는 실제 연출/이동/공격
    // 코드를 몰라도 되고, 소비 스크립트들도 서로/StageGimmickManager 내부 상태를 몰라도 된다) ===

    // 호우: 플레이어 이동속도 20% 감소.
    public static float PlayerMoveSpeedMultiplier => IsActive(GimmickType.HeavyRain) ? 0.8f : 1f;

    // 태풍: 몬스터 이동속도 + 애니메이션 재생속도 20% 증가 (둘 다 같은 배율 사용).
    public static float EnemySpeedMultiplier => IsActive(GimmickType.Typhoon) ? 1.2f : 1f;

    // 한파: 무기 공격/재장전 관련 타이머가 흐르는 속도. 1보다 작을수록 쿨다운 회복과 휘두르기/찌르기
    // 모션 진행이 느려진다 (각 무기 스크립트가 Time.deltaTime에 이 값을 곱해서 쓴다).
    public static float WeaponTimeScale => IsActive(GimmickType.ColdWave) ? 0.65f : 1f;
}
