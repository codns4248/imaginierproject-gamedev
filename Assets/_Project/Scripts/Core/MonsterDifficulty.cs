using UnityEngine;

// 스테이지가 진행될수록 몬스터를 강하게 만드는 배율을 계산한다 (1층 = 기본값).
// 스포너(EnemySpawner, BeeSwarmSpawner)가 몬스터를 만들 때 ApplyTo로 체력/이동속도/공격력을 올리고,
// 스폰 간격과 동시 최대 마리 수도 여기 배율을 따른다.
//
// 체력은 1차 + 2차 항으로 오른다 (지수식처럼 폭주하지 않으면서, 플레이어가 강화/레벨업으로 강해지는 속도를 따라가도록):
//   1층 1배, 5층 약 1.8배, 10층 약 3.6배, 15층 약 6배, 20층 약 9.3배.
// 이동속도/스폰 속도는 천천히 오르다 상한에서 멈추고, 공격력(접촉/원거리)은 10층마다 1씩 오른다.
// 밸런스 1차 조정(2026-10-25) 값이라 플레이해 보면서 아래 상수만 바꾸면 된다.
public static class MonsterDifficulty
{
    private const int StagesPerTier = 5; // 난이도 "단계" 표시용 (5층마다 한 단계, 추출 주기와 같음)

    private const float HealthLinear = 0.15f;      // 층마다 체력 +15%
    private const float HealthQuadratic = 0.015f;  // + (층-1)^2 x 1.5%
    private const float SpeedPerStage = 0.025f;    // 층마다 이동속도 +2.5%
    private const float MaxSpeedMultiplier = 1.4f; // 이동속도는 1.4배(17층)에서 멈춘다
    private const int StagesPerDamageStep = 10;    // 10층마다 접촉/원거리 공격력 +1 (11층부터 2, 21층부터 3)
    private const float BaseSpawnRate = 1.8f;      // 모든 스포너의 기본 스폰 속도 배율 (핵앤슬래시: 1.8배). 밸런스 2차 조정(2026-10-26)
    private const float SpawnRatePerStage = 0.05f; // 층마다 스폰 속도 +5%
    private const float MaxSpawnRateMultiplier = 2f; // BaseSpawnRate에 곱해지기 전의 층별 증가 상한
    private const int BaseMaxEnemies = 110;        // 1층 동시 최대 마리 수 (예전 50)
    private const int MaxEnemiesPerStage = 6;      // 층마다 +6마리
    private const int HardMaxEnemies = 220;        // 동시 최대 마리 수의 절대 상한 (성능 보호)
    private const float MixedZoneSpawnRate = 0.4f; // 생각의 방(모든 스포너가 동시에 켜지는 구역)은 스폰 속도를 낮춘다

    private static int StageIndex => Mathf.Max(0, StageProgress.CurrentStageNo - 1); // 1층 = 0

    // 현재 스테이지 번호 기준 난이도 단계. 1~5층=0, 6~10층=1, 11~15층=2 ...
    public static int CurrentTier => StageIndex / StagesPerTier;

    public static float HealthMultiplier => 1f + StageIndex * HealthLinear + StageIndex * StageIndex * HealthQuadratic;
    public static float SpeedMultiplier => Mathf.Min(MaxSpeedMultiplier, 1f + StageIndex * SpeedPerStage);
    public static int DamageBonus => StageIndex / StagesPerDamageStep;

    // 스폰 간격을 이 값으로 나눈다 (클수록 자주 나온다).
    public static float SpawnRateMultiplier =>
        BaseSpawnRate * Mathf.Min(MaxSpawnRateMultiplier, 1f + StageIndex * SpawnRatePerStage) * (StageManager.IsMixedMonsterZone ? MixedZoneSpawnRate : 1f);

    // 정예 스포너는 일반 몬스터만큼 늘리면 경험치/드랍이 폭주하므로 증가분의 30%만 적용한다.
    // 특수 패턴 몬스터(돌진/원거리/매복/벌떼)는 초반에는 안 나오다가 unlockStage층부터 rampFloors층에 걸쳐 점점 많이 나온다 (0 -> 1).
    // 공장 맵은 원거리 몬스터만 나오는 맵 특성이라 처음부터 100%로 둔다.
    public static float SpecialSpawnChance(int unlockStage, int rampFloors)
    {
        if (StageManager.CurrentTheme == "공장") return 1f;
        return Mathf.Clamp01((StageProgress.CurrentStageNo - unlockStage + 1f) / Mathf.Max(1, rampFloors));
    }

    public static float SpawnRateFor(bool elite) => elite ? 1f + (SpawnRateMultiplier - 1f) * 0.3f : SpawnRateMultiplier;

    // 지금 층의 동시 최대 마리 수 (각 스포너의 maxEnemies와 비교해서 작은 쪽을 쓴다).
    public static int MaxConcurrentEnemies => Mathf.Min(HardMaxEnemies, BaseMaxEnemies + StageIndex * MaxEnemiesPerStage);

    // 스포너가 몬스터를 만든 직후 부른다: 체력, 이동속도, 공격력을 지금 층에 맞게 올린다.
    public static void ApplyTo(Enemy enemy)
    {
        if (enemy == null) return;

        enemy.ApplyHealthMultiplier(HealthMultiplier);
        enemy.moveSpeed *= SpeedMultiplier;

        int bonus = DamageBonus;
        if (bonus <= 0) return;
        enemy.contactDamage += bonus;
        RangedEnemyAI ranged = enemy.GetComponent<RangedEnemyAI>();
        if (ranged != null) ranged.projectileDamage += bonus;
        AmbushCasterAI ambush = enemy.GetComponent<AmbushCasterAI>();
        if (ambush != null) ambush.aoeDamage += bonus;
    }
}
