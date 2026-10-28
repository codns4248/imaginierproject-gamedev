// 자원 소모로 강화 가능한 무기가 구현하는 인터페이스.
// 지금 WeaponUpgradePanel UI에 이미 구현된 5개 스탯(공격속도/공격력/공격범위/치명타 확률/발사속도) 그대로 따른다.
// docs/schema.sql은 기름->이동속도로 적어뒀지만, 이동속도는 permanent_upgrade_type에도 별도로 있어서
// 두 시스템이 겹치는 문제가 있음 - 스키마 정리 전까지는 UI에 이미 있는 "발사속도"(무기 스탯)로 둔다.
// 마일스톤 패시브(item_enhance_milestone)는 아직 범위 밖, 여기선 단순 스탯 증가만 다룬다.
public interface IEnhanceableWeapon
{
    int MaxEnhanceLevel { get; }
    int GetEnhanceLevel(ResourceType type);
    void ApplyEnhance(ResourceType type);

    // WeaponEnhanceStore의 현재 강화 레벨을 스탯에 다시 반영한다. 원본값으로 되돌린 뒤
    // 레벨 수만큼 재적용하므로, 런 종료로 강화가 0으로 리셋된 뒤 호출해도 정확히 맞는다.
    void ReapplyEnhancements();

    // 강화 UI에 보여줄 특수효과 설명 (tier = WeaponEnhanceUtil.MilestoneTiers 중 하나).
    // 특수효과가 없는 무기/단계는 null을 돌려주면 UI에 "미정"으로 표시된다.
    string GetMilestoneEffect(ResourceType type, int tier) => null;

    // 이 무기 한 자루에 지금까지 올린 강화 횟수의 합 (5개 종류 전부). 무기 전체 강화 횟수 상한(TotalMaxLevel)과 비교하는 데 쓴다.
    int GetTotalEnhanceLevel()
    {
        int total = 0;
        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes) total += GetEnhanceLevel(type);
        return total;
    }

    // 지금 그 종류를 한 단계 더 올릴 수 있는지: 그 종류가 종류별 최대(MaxEnhanceLevel) 미만이고,
    // 무기 전체 강화 횟수도 상한(TotalMaxLevel) 미만일 때만 true. 자원을 쓰기 전에 이걸로 먼저 확인한다.
    bool CanEnhance(ResourceType type) =>
        GetEnhanceLevel(type) < MaxEnhanceLevel && GetTotalEnhanceLevel() < WeaponEnhanceUtil.TotalMaxLevel;
}

// 각 무기 스크립트가 enhanceLevels 배열 인덱스를 통일해서 쓰기 위한 공용 유틸.
public static class WeaponEnhanceUtil
{
    // 강화 종류(목재/철/구리/화학물질/기름) 하나당 최대 단계.
    public const int MaxLevel = 10;

    // 무기 한 자루가 5개 종류를 통틀어 올릴 수 있는 총 강화 횟수. 그래서 종류별 최대(10)를 다 채울 수는 없고,
    // 예를 들어 10+5 / 5+5+5 / 7+4+4 처럼 15번 안에서 나눠서 올려야 한다.
    public const int TotalMaxLevel = 15;

    // 5강마다 특수효과가 켜지는 단계 (종류별 최대가 10이라 5강과 10강).
    public static readonly int[] MilestoneTiers = { 5, 10 };

    // 다음 1단계 강화 비용 (무기 강화/거점 영구 강화 공용). 단계가 오를수록 1.2배씩 비싸진다.
    // 밸런스 1차 조정(2026-10-25): 0강 15개 -> 9강 77개. 한 종류 5강까지 총 112개, 10강까지 총 389개.
    // (일반 몬스터 자원 드랍 50%, 1층 45초 기준으로 5강 특수효과가 대략 3층 전후에 켜지도록 맞춘 값). 팀 밸런스 확정 시 두 숫자만 조정.
    private const int BaseCost = 15;
    private const float CostGrowth = 1.2f;
    public static int CostForLevel(int currentLevel) =>
        UnityEngine.Mathf.RoundToInt(BaseCost * UnityEngine.Mathf.Pow(CostGrowth, currentLevel));

    // 탐험 중 무기 강화 비용 (밸런스 5차 2026-10-29): 종류별 단계가 아니라 그 무기에 지금까지 올린 강화 횟수의 합(0~14)으로 정한다.
    // 그래서 공격력을 5번 올렸다면, 아직 한 번도 안 올린 공격속도도 6번째 강화 가격(15 x 1.25^5 = 46)을 낸다.
    // 0번째 15개 ... 5번째 46 ... 10번째 140 ... 14번째 341. 5번까지 총 123, 10번까지 499, 15번을 다 채우는 데 1,646개
    // (한 번에 쓰는 자원은 올리는 스탯의 종류라서, 실제로는 고른 2~3종류로 나뉘어 든다).
    private const float WeaponCostGrowth = 1.25f;
    public static int CostForWeapon(int totalEnhanceLevel) =>
        UnityEngine.Mathf.RoundToInt(BaseCost * UnityEngine.Mathf.Pow(WeaponCostGrowth, totalEnhanceLevel));

    // 무기가 Awake()에서 저장된 레벨만큼 스탯을 다시 적용(재생)할 때 순회할 목록.
    public static readonly ResourceType[] AllTypes =
    {
        ResourceType.Wood, ResourceType.Iron, ResourceType.Copper, ResourceType.Chemical, ResourceType.Oil
    };

    public static int IndexOf(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return 0;
            case ResourceType.Iron: return 1;
            case ResourceType.Copper: return 2;
            case ResourceType.Chemical: return 3;
            case ResourceType.Oil: return 4;
            default: return -1;
        }
    }
}
