using System;
using System.IO;
using UnityEngine;

// 거점(로비)에서 자원(ResourceBank.stash)을 영구히 소모해 플레이어 전체 스탯을 강화한다.
// WeaponEnhanceStore(런 한정, 무기 종류별)와 달리 세이브 파일에 저장되어 게임을 껐다 켜도 유지된다
// (CLAUDE.md의 player_permanent_upgrade를 로컬 세이브로 반영. Express API 붙으면 Save/Load 내부만 교체할 예정).
//
// 자원 종류를 그대로 강화 대상 스탯의 키로 재사용한다 (무기 강화의 WeaponEnhanceUtil.AllTypes와 동일 5종류):
//   목재->공격속도, 철->공격력, 구리->체력, 화학물질->치명타 확률, 기름->이동속도
// (무기 강화는 구리를 "공격범위"에 쓰지만, 영구 강화엔 범위 스탯이 없어 구리를 체력에 배정했다.
//  기름은 원래 docs/schema.sql이 적어둔 대로 이동속도에 배정 - 무기 강화 쪽에서 발사속도로 겹쳐 쓴 것과는 별개).
public static class PermanentUpgradeManager
{
    public const int MaxLevel = WeaponEnhanceUtil.MaxLevel;
    private const int CostPerLevel = 5;

    private static readonly int[] levels = new int[WeaponEnhanceUtil.AllTypes.Length];

    public static event Action OnChanged;

    private static string SavePath => Path.Combine(Application.persistentDataPath, "permanent_upgrades.json");

    public static int GetLevel(ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        return idx < 0 ? 0 : levels[idx];
    }

    /// <summary>거점 영구 강화 팝업에서 호출. stash를 소모하고 레벨을 1 올린다.
    /// 이미 최대 레벨이거나 자원이 부족하면 아무 일도 하지 않고 false.</summary>
    public static bool TryUpgrade(ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0 || levels[idx] >= MaxLevel) return false;
        if (!ResourceBank.TrySpendStash(type, CostPerLevel)) return false;

        levels[idx]++;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    [Serializable]
    private class SaveData
    {
        public int[] levels = new int[WeaponEnhanceUtil.AllTypes.Length];
    }

    public static void Save()
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(new SaveData { levels = (int[])levels.Clone() }));
    }

    // 게임(플레이 모드) 시작 시 자동으로 한 번 불려서 이전 저장분을 되살린다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        if (!File.Exists(SavePath)) return;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data?.levels == null) return;

            Array.Copy(data.levels, levels, Mathf.Min(data.levels.Length, levels.Length));
            OnChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"영구 강화 세이브 파일을 불러오지 못했습니다: {e.Message}");
        }
    }
}
