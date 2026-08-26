using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 무기별 강화 레벨을 게임 전체에서 하나로 유지하는 정적 저장소 (ResourceBank와 같은 스타일).
// 강화가 거점(로비)에서 이뤄지고 실제 사용은 스테이지(MainScene)에서 이뤄지는데, 씬이 바뀌면
// Player/무기 GameObject가 통째로 새로 생성되므로, 강화 레벨 자체는 인스턴스가 아니라 여기
// (무기 이름 기준)에 저장해두고, 각 무기는 Awake()에서 이 값을 읽어와 스탯에 재적용한다.
// ResourceBank와 마찬가지로 로컬 JSON 파일에 저장해서 게임을 껐다 켜도 유지된다.
public static class WeaponEnhanceStore
{
    private static readonly Dictionary<string, int[]> levels = new Dictionary<string, int[]>();

    private static string SavePath => Path.Combine(Application.persistentDataPath, "weapon_enhance.json");

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
        Save();
        return true;
    }

    [Serializable]
    private class WeaponEntry
    {
        public string weaponName;
        public int[] levels;
    }

    [Serializable]
    private class SaveData
    {
        public List<WeaponEntry> entries = new List<WeaponEntry>();
    }

    public static void Save()
    {
        SaveData data = new SaveData();
        foreach (KeyValuePair<string, int[]> kv in levels)
            data.entries.Add(new WeaponEntry { weaponName = kv.Key, levels = (int[])kv.Value.Clone() });

        File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }

    // 게임(플레이 모드) 시작 시 자동으로 한 번 불려서 이전 저장분을 되살린다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        levels.Clear();
        if (!File.Exists(SavePath)) return;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data?.entries == null) return;

            foreach (WeaponEntry entry in data.entries)
            {
                if (string.IsNullOrEmpty(entry.weaponName) || entry.levels == null || entry.levels.Length != 5) continue;
                levels[entry.weaponName] = entry.levels;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"무기 강화 세이브 파일을 불러오지 못했습니다: {e.Message}");
        }
    }
}
