using System.Collections.Generic;

// 무기별 강화 레벨을 게임 전체에서 하나로 유지하는 정적 저장소 (ResourceBank와 같은 스타일).
// 강화가 거점(로비)에서 이뤄지고 실제 사용은 스테이지(MainScene)에서 이뤄지는데, 씬이 바뀌면
// Player/무기 GameObject가 통째로 새로 생성되므로, 강화 레벨 자체는 인스턴스가 아니라 여기
// (무기 이름 기준)에 저장해두고, 각 무기는 Awake()에서 이 값을 읽어와 스탯에 재적용한다.
public static class WeaponEnhanceStore
{
    private static readonly Dictionary<string, int[]> levels = new Dictionary<string, int[]>();

    public static int GetLevel(string weaponName, ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0) return 0;
        return levels.TryGetValue(weaponName, out int[] arr) ? arr[idx] : 0;
    }

    /// <summary>레벨이 최대치 미만이면 1 올리고 true, 이미 최대면 아무 일도 하지 않고 false.</summary>
    public static bool TryEnhance(string weaponName, ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0) return false;

        if (!levels.TryGetValue(weaponName, out int[] arr))
        {
            arr = new int[5];
            levels[weaponName] = arr;
        }

        if (arr[idx] >= WeaponEnhanceUtil.MaxLevel) return false;
        arr[idx]++;
        return true;
    }
}
