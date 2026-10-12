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
}

// 각 무기 스크립트가 enhanceLevels 배열 인덱스를 통일해서 쓰기 위한 공용 유틸.
public static class WeaponEnhanceUtil
{
    public const int MaxLevel = 15;

    // 5강마다 특수효과가 켜지는 단계.
    public static readonly int[] MilestoneTiers = { 5, 10, 15 };

    // 다음 1단계 강화 비용 (무기 강화/거점 영구 강화 공용). 단계가 오를수록 1.2배씩 비싸진다.
    // ponytail: 밸런스 임시값 (0강 5개 -> 14강 64개, 15강까지 총 약 360개). 팀 밸런스 확정 시 두 숫자만 조정.
    private const int BaseCost = 5;
    private const float CostGrowth = 1.2f;
    public static int CostForLevel(int currentLevel) =>
        UnityEngine.Mathf.RoundToInt(BaseCost * UnityEngine.Mathf.Pow(CostGrowth, currentLevel));

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
