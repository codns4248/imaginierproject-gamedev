using UnityEngine;

// 스테이지가 진행될수록(5스테이지 클리어마다 한 단계씩, StageProgress.ExtractionInterval과 동일 주기)
// 몬스터 스탯을 키우는 배율을 계산한다. 1~5층은 기본, 6~10층은 1단계, 11~15층은 2단계... 식으로 무한히 오른다.
//
// 배율 누적 방식: tier끼리 곱하지 않고(그러면 지수적으로 폭주한다) "기본값 대비 tier * 증가율"만큼만
// 매번 더하는 선형 배율을 쓴다 - 예: tier당 +20%면 10단계 후에도 기본의 3배(1 + 10*0.2)에 그친다.
//
// 지금은 체력에만 실제로 적용된다(EnemySpawner.Update() 참고). 데미지/이동속도도 같은 방식으로 올릴 수
// 있도록 배율 계산식은 미리 만들어뒀지만, 증가율을 0으로 둬서 아직은 아무 효과가 없다 - 나중에
// 밸런싱하면서 DamageRatePerTier/SpeedRatePerTier 값만 채우고 실제 적용부를 추가하면 된다.
public static class MonsterDifficulty
{
    private const int StagesPerTier = 5; // 몇 스테이지 클리어마다 한 단계 강해지는지

    private const float HealthRatePerTier = 0.2f; // tier당 체력 +20% (실제로 쓰이는 값 - 대충 잡은 기본치, 밸런싱 필요)
    private const float DamageRatePerTier = 0f;   // 아직 미사용
    private const float SpeedRatePerTier = 0f;    // 아직 미사용

    // 현재 스테이지 번호 기준 난이도 단계. 1~5층=0, 6~10층=1, 11~15층=2 ...
    public static int CurrentTier => Mathf.Max(0, (StageProgress.CurrentStageNo - 1) / StagesPerTier);

    public static float HealthMultiplier => 1f + CurrentTier * HealthRatePerTier;
    public static float DamageMultiplier => 1f + CurrentTier * DamageRatePerTier;
    public static float SpeedMultiplier => 1f + CurrentTier * SpeedRatePerTier;
}
