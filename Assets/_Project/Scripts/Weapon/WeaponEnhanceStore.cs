using System.Collections.Generic;
using UnityEngine;

// 무기별 강화 레벨을 "런(원정) 동안만" 하나로 유지하는 정적 저장소 (ResourceBank와 같은 스타일).
// 강화는 스테이지(런) 안에서 이번 런에 파밍한 자원(runHeld)을 소모해서 이뤄지고,
// 사망/추출로 런이 끝나면 ResetForNewRun()으로 전부 버려진다.
// (schema.sql의 expedition_item = 플레이어가 아니라 원정에 종속되는 무기별 강화 횟수).
//
// 포탈로 다음 스테이지에 넘어가는 것은 씬 전환이 아니라 같은 MainScene 안의 좌표 이동이라
// 무기 GameObject가 그대로 유지되므로, 이 값도 자연히 유지된다 (= "포탈 이동 시 강화 유지").
//
// 키는 GameObject.name이 아니라 WeaponIdentity.type(WeaponType)을 쓴다 - 시작부터 들고 있는
// 무기는 이름이 "Pistol" 그대로지만, 필드에서 F키로 주운 무기는 Instantiate라 이름 뒤에
// "(Clone)"이 붙어서 이름 기준으로는 강화가 이어지지 않는 문제가 있었다.
//
// 예전엔 로컬 JSON에 영구 저장했지만, 강화가 런 종속으로 바뀌면서 영구 저장을 제거했다
// (런 자체가 게임 재시작을 넘겨 유지되지 않으므로 저장할 이유가 없다).
public static class WeaponEnhanceStore
{
    private static readonly Dictionary<WeaponType, int[]> levels = new Dictionary<WeaponType, int[]>();

    public static int GetLevel(WeaponType weaponType, ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0) return 0;
        return levels.TryGetValue(weaponType, out int[] arr) ? arr[idx] : 0;
    }

    /// <summary>레벨이 최대치 미만이면 1 올리고 true, 이미 최대면 아무 일도 하지 않고 false.</summary>
    public static bool TryEnhance(WeaponType weaponType, ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0) return false;

        if (!levels.TryGetValue(weaponType, out int[] arr))
        {
            arr = new int[5];
            levels[weaponType] = arr;
        }

        if (arr[idx] >= WeaponEnhanceUtil.MaxLevel) return false;
        arr[idx]++;
        return true;
    }

    /// <summary>런 종료(사망/추출) 시 호출. 이번 런의 강화를 전부 버리고,
    /// 씬에 존재하는 무기들의 스탯도 원본값으로 되돌린다 (포탈 이동으로 유지돼 온 인스턴스 대비).</summary>
    public static void ResetForNewRun()
    {
        levels.Clear();

        foreach (WeaponAim aim in Object.FindObjectsByType<WeaponAim>(FindObjectsSortMode.None))
        {
            IEnhanceableWeapon weapon = aim.GetComponent<IEnhanceableWeapon>();
            if (weapon != null) weapon.ReapplyEnhancements();
        }
    }
}
