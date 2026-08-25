// 자원 소모로 강화 가능한 무기가 구현하는 인터페이스 (docs/schema.sql weapon_enhance_stat 기준).
// 목재/철/구리/화학물질 4종만 다룬다 - 기름(이동속도)은 무기가 아니라 플레이어 자체 스탯이라 별도 처리.
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
            default: return -1; // Oil은 무기 강화 대상이 아님
        }
    }
}
