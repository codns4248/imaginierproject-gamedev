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
    // 스폰 속도 곡선 (밸런스 4차 2026-10-29): 1층은 완만하게(0.9) 시작해서 3~4층부터 눈에 띄게, 7층 이후는 크게 늘어난다.
    //   배율 = SpawnStart + SpawnLinear * i + SpawnQuad * i^2 (i = 층 - 1), 상한 MaxSpawnRate. 1층 0.9, 3층 1.3, 5층 1.9, 7층 2.7, 10층 4.0, 상한 4.5
    private const float SpawnStart = 0.9f;
    private const float SpawnLinear = 0.12f;
    private const float SpawnQuad = 0.03f;
    private const float MaxSpawnRate = 7f;         // 20층(PeakStage) 이후 고정되는 스폰 배율

    // 밸런스 5차(2026-10-29): 스폰량/동시 마리 수/특수 몬스터 비율/자원·경험치 증가는 20층에서 정점을 찍고 이후 고정.
    // 몬스터 체력/속도/공격력은 정점 없이 계속 오른다 (플레이어 최대 강화 상태에서 따로 테스트 예정).
    public const int PeakStage = 20;

    private const int BaseMaxEnemies = 55;         // 1층 동시 최대 마리 수 (밸런스 4차: 초반은 적게)
    private const int MaxEnemiesPerStage = 10;     // 층마다 +10마리 (1층 55, 5층 95, 10층 145, 20층에 상한 240)
    private const int HardMaxEnemies = 240;        // 동시 최대 마리 수의 절대 상한 (성능 보호)
    private const float MixedZoneSpawnRate = 0.4f; // 생각의 방(모든 스포너가 동시에 켜지는 구역)은 스폰 속도를 낮춘다

    private static int StageIndex => Mathf.Max(0, StageProgress.CurrentStageNo - 1); // 1층 = 0

    // 현재 스테이지 번호 기준 난이도 단계. 1~5층=0, 6~10층=1, 11~15층=2 ...
    public static int CurrentTier => StageIndex / StagesPerTier;

    public static float HealthMultiplier => 1f + StageIndex * HealthLinear + StageIndex * StageIndex * HealthQuadratic;
    public static float SpeedMultiplier => Mathf.Min(MaxSpeedMultiplier, 1f + StageIndex * SpeedPerStage);
    public static int DamageBonus => StageIndex / StagesPerDamageStep;

    // 스폰 간격을 이 값으로 나눈다 (클수록 자주 나온다).
    public static float SpawnRateMultiplier =>
        SpawnRateAt(StageIndex) * (StageManager.IsMixedMonsterZone ? MixedZoneSpawnRate : 1f);

    // 10층(i=9)까지는 2차 곡선, 그 뒤로는 직선으로 20층(i=19)의 MaxSpawnRate까지 오르고 이후 고정.
    private static float SpawnRateAt(int i)
    {
        const int curveEnd = 9;
        float atCurveEnd = SpawnStart + SpawnLinear * curveEnd + SpawnQuad * curveEnd * curveEnd;
        if (i <= curveEnd) return SpawnStart + SpawnLinear * i + SpawnQuad * i * i;
        float t = Mathf.Clamp01((i - curveEnd) / (float)(PeakStage - 1 - curveEnd));
        return Mathf.Lerp(atCurveEnd, MaxSpawnRate, t);
    }

    // 일반 몬스터가 자원을 떨어뜨릴 확률: 모든 층 75% 고정 (밸런스 5차 2026-10-29).
    // 층이 오를수록 확률을 깎으면 "잡은 것에 비해 안 나온다"는 느낌이 들어서, 확률은 그대로 두고
    // 늘어나는 수급은 무기 강화 비용(WeaponEnhanceUtil.CostForWeapon)과 상인 가격(MerchantPriceScale)이 가져가게 했다.
    // 예상 층당 수급(맵 평균): 1층 약 21, 5층 44, 10층 120, 15층 175, 20층 236 (20층까지 누적 약 2,400).
    public const float CommonResourceDropChance = 0.75f;

    // 자원 한 개(바닥에 떨어진 아이템 하나)의 가치 배율: 1층 x1, 층마다 +0.1, 20층 x2.9 이후 고정 (층 표시 옆에 보여 준다, StageNumberUI).
    // "더 깊이 갈수록 같은 몬스터를 잡아도 더 많이 받는다"가 눈에 보이도록, 확률이 아니라 아이템 하나의 개수(1~3)로 준다
    // (여러 개짜리는 바닥에서 더 크게 보인다, ResourcePickup 참고). 20층까지 무기 2자루를 15번씩 다 강화할 수 있게 맞춘 값:
    // 예상 층당 수급(맵 평균) 1층 21, 5층 62, 10층 228, 15층 420, 20층 684 / 20층까지 누적 약 5,500.
    public static float ResourceAmountMultiplier => AmountMultiplierAt(StageProgress.CurrentStageNo);
    private static float AmountMultiplierAt(int stage) => 1f + 0.1f * (Mathf.Clamp(stage, 1, PeakStage) - 1);

    // 자원 아이템 하나의 개수를 굴린다: 배율의 정수 부분 + 소수 부분 확률로 1개 더 (평균이 배율과 같다).
    public static int RollResourceAmount()
    {
        float m = ResourceAmountMultiplier;
        int whole = Mathf.FloorToInt(m);
        return whole + (Random.value < m - whole ? 1 : 0);
    }

    // 상인 상품 가격 배율: 층당 자원 수급(스폰 배율 x 자원 개수 배율)을 그대로 따라가서, 어느 층에서든 같은 상품이
    // "그 층 수급의 비슷한 몫"이 되게 한다. 5층을 x1.8로 두고 비례: 10층 x5.8, 15층 x9.5, 20층 x14 이후 고정.
    // 상인은 어느 층에서나 확률로 나오므로, 1~2층은 원래 가격(x1) 밑으로 내려가지 않게 막는다.
    public static float MerchantPriceScale => Mathf.Max(1f, 1.8f * IncomeScaleAt(StageProgress.CurrentStageNo) / IncomeScaleAt(5));
    private static float IncomeScaleAt(int stage) => SpawnRateAt(Mathf.Clamp(stage, 1, PeakStage) - 1) * AmountMultiplierAt(stage);

    // 정예 스포너는 일반 몬스터만큼 늘리면 경험치/드랍이 폭주하므로 증가분의 30%만 적용한다.
    // 특수 패턴 몬스터(돌진/원거리/매복/벌떼)는 초반에는 안 나오다가 unlockStage층부터 rampFloors층에 걸쳐 점점 많이 나온다 (0 -> 1).
    // (공장 맵만 예외 규칙: 위 주석 참고)
    // rampFloors는 더 이상 쓰지 않는다: 어느 몬스터든 unlockStage층에 첫 등장해서 PeakStage(20층)에 100%가 되도록 직선으로 오른다.
    public static float SpecialSpawnChance(int unlockStage, int rampFloors)
    {
        int stage = StageProgress.CurrentStageNo;
        // 공장은 원거리 몬스터만 나오는 맵이라 등장 층 규칙 대신 1층 40%에서 시작해 20층에 100%가 된다 (초반 원거리 부담 완화).
        if (StageManager.CurrentTheme == "공장") return Mathf.Clamp01(0.4f + 0.6f * (stage - 1f) / (PeakStage - 1f));
        return Mathf.Clamp01((stage - unlockStage + 1f) / Mathf.Max(1, PeakStage - unlockStage + 1));
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
