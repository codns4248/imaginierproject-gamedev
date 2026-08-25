// 자원 소모로 강화 가능한 무기가 구현하는 인터페이스.
// 지금 EnhancementPanel UI에 이미 구현된 5개 스탯(공격속도/공격력/공격범위/치명타 확률/발사속도) 그대로 따른다.
// docs/schema.sql은 기름->이동속도로 적어뒀지만, 이동속도는 permanent_upgrade_type에도 별도로 있어서
// 두 시스템이 겹치는 문제가 있음 - 스키마 정리 전까지는 UI에 이미 있는 "발사속도"(무기 스탯)로 둔다.
// 마일스톤 패시브(item_enhance_milestone)는 아직 범위 밖, 여기선 단순 스탯 증가만 다룬다.
public interface IEnhanceableWeapon
{
    int MaxEnhanceLevel { get; }
    int GetEnhanceLevel(ResourceType type);
    void ApplyEnhance(ResourceType type);
}

// 각 무기 스크립트가 enhanceLevels 배열 인덱스를 통일해서 쓰기 위한 공용 유틸.
public static class WeaponEnhanceUtil
{
    public const int MaxLevel = 10;

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
