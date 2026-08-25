using System;
using System.IO;
using UnityEngine;

// 자원 보관을 담당하는 정적 유틸 (EnemyManager와 같은 스타일).
// 두 종류로 나뉜다:
//  - stash: 거점(로비)에 영구 보관되는 자원. 세이브 파일에 저장되고 씬을 넘나들어도 유지된다.
//  - runHeld: 현재 스테이지에서 파밍했지만 아직 확정되지 않은 자원("파밍 후의 자원 공간").
//             스테이지 클리어로 로비에 도착하면 stash로 합쳐지고, 사망하면 소실된다
//             (CLAUDE.md의 player_resource / expedition_resource 이원 구조를 그대로 반영).
public static class ResourceBank
{
    private const int TypeCount = 5;

    private static readonly int[] stash = new int[TypeCount];
    private static readonly int[] runHeld = new int[TypeCount];

    // ponytail: 로컬 JSON 파일 저장. Express API 붙으면 Save/Load 내부만 API 호출로 교체.
    private static string SavePath => Path.Combine(Application.persistentDataPath, "resources.json");

    public static event Action OnChanged;

    public static int GetStash(ResourceType type) => stash[(int)type];
    public static int GetRunHeld(ResourceType type) => runHeld[(int)type];

    /// <summary>파밍(적 처치 등)으로 자원을 획득한다. 아직 확정되지 않은 runHeld에 쌓인다.</summary>
    public static void AddRunResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        runHeld[(int)type] += amount;
        OnChanged?.Invoke();
    }

    /// <summary>무기/플레이어 강화 등으로 이번 런 파밍분을 소모한다. 부족하면 아무 일도 하지 않고 false.</summary>
    public static bool TrySpendRunResource(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        if (runHeld[(int)type] < amount) return false;

        runHeld[(int)type] -= amount;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>스테이지 클리어(로비 도착) 시 호출. runHeld를 stash로 합치고 저장한다.</summary>
    public static void CommitRunToStash()
    {
        for (int i = 0; i < TypeCount; i++)
        {
            stash[i] += runHeld[i];
            runHeld[i] = 0;
        }
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>플레이어 사망 시 호출. 확정되지 않은 파밍분을 전부 잃는다.</summary>
    public static void DiscardRun()
    {
        for (int i = 0; i < TypeCount; i++)
            runHeld[i] = 0;
        OnChanged?.Invoke();
    }

    [Serializable]
    private class SaveData
    {
        public int[] stash = new int[TypeCount];
    }

    public static void Save()
    {
        SaveData data = new SaveData { stash = (int[])stash.Clone() };
        File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }

    // 게임(플레이 모드) 시작 시 자동으로 한 번 불려서 이전 저장분을 stash에 되살린다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        if (!File.Exists(SavePath)) return;

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data?.stash == null || data.stash.Length != TypeCount) return;

            Array.Copy(data.stash, stash, TypeCount);
            OnChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"자원 세이브 파일을 불러오지 못했습니다: {e.Message}");
        }
    }
}
