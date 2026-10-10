using UnityEngine;

// 무기별 강화 레벨을 "런(원정) 동안만" 다루는 창구 (ResourceBank와 같은 스타일의 static 클래스).
// 강화는 스테이지(런) 안에서 이번 런에 파밍한 자원(runHeld)을 소모해서 이뤄지고,
// 사망/추출로 런이 끝나면 ResetForNewRun()으로 전부 버려진다.
// (schema.sql의 expedition_item = 플레이어가 아니라 원정에 종속되는 무기별 강화 횟수).
//
// 레벨은 무기 "종류"가 아니라 무기 "한 자루"(WeaponIdentity)에 저장된다 - 같은 종류를 여러 자루 들어도 각자
// 따로 강화하고, 새로 얻은 무기는 0에서 시작한다. 버린 무기를 다시 줍거나(WeaponPickup이 레벨을 실어 나른다)
// 상인에게 산 새 무기를 지급받는 경로는 WeaponSwitcher.TryGiveWeapon을 참고.
//
// 포탈로 다음 스테이지에 넘어가는 것은 씬 전환이 아니라 같은 MainScene 안의 좌표 이동이라
// 무기 GameObject가 그대로 유지되므로, 레벨도 자연히 유지된다 (= "포탈 이동 시 강화 유지").
//
// 예전엔 로컬 JSON에 영구 저장했지만, 강화가 런 종속으로 바뀌면서 영구 저장을 제거했다
// (런 자체가 게임 재시작을 넘겨 유지되지 않으므로 저장할 이유가 없다).
public static class WeaponEnhanceStore
{
    public static int GetLevel(WeaponIdentity weapon, ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0 || weapon == null) return 0;
        return weapon.GetLevel(idx);
    }

    /// <summary>레벨이 최대치 미만이면 1 올리고 true, 이미 최대면 아무 일도 하지 않고 false.</summary>
    public static bool TryEnhance(WeaponIdentity weapon, ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0 || weapon == null) return false;

        int level = weapon.GetLevel(idx);
        if (level >= WeaponEnhanceUtil.MaxLevel) return false;

        weapon.SetLevel(idx, level + 1);
        return true;
    }

    /// <summary>런 종료(사망/추출) 시 호출. 지금 씬에 있는 모든 무기 자루의 강화를 버리고,
    /// 스탯도 원본값으로 되돌린다 (포탈 이동으로 유지돼 온 인스턴스 대비).</summary>
    public static void ResetForNewRun()
    {
        foreach (WeaponIdentity id in Object.FindObjectsByType<WeaponIdentity>(FindObjectsSortMode.None))
            id.ResetLevels();

        foreach (WeaponAim aim in Object.FindObjectsByType<WeaponAim>(FindObjectsSortMode.None))
        {
            IEnhanceableWeapon weapon = aim.GetComponent<IEnhanceableWeapon>();
            if (weapon != null) weapon.ReapplyEnhancements();
        }
    }
}
