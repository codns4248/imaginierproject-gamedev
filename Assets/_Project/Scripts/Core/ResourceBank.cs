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
    private const int TypeCount = 6;

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

    // 거점 특수 강화 "창고 크기": 자원 종류마다 창고에 둘 수 있는 최대치 (희귀자원은 제한 없음).
    // 이미 그보다 많이 쌓여 있던 창고분은 줄이지 않고, 새로 들어오는 것만 막는다.
    public static bool IsCapped(ResourceType type) => type != ResourceType.Rare;
    public static int StashCap => SpecialUpgrades.StorageCapPerType;

    /// <summary>창고가 가득 차서 확정하지 못하고 사라진 자원이 있을 때 (자원 종류 순서대로 사라진 양).</summary>
    public static event Action<int[]> OnStashOverflow;

    /// <summary>스테이지 클리어(로비 도착) 시 호출. runHeld를 stash로 합치고 저장한다.
    /// 창고 최대치(StashCap)를 넘는 만큼은 사라진다 (OnStashOverflow).</summary>
    public static void CommitRunToStash()
    {
        int[] overflow = null;
        for (int i = 0; i < TypeCount; i++)
        {
            int add = runHeld[i];
            if (IsCapped((ResourceType)i))
            {
                int room = Mathf.Max(0, StashCap - stash[i]);
                if (add > room)
                {
                    overflow ??= new int[TypeCount];
                    overflow[i] = add - room;
                    add = room;
                }
            }
            stash[i] += add;
            runHeld[i] = 0;
        }
        Save();
        OnChanged?.Invoke();
        if (overflow != null) OnStashOverflow?.Invoke(overflow);
    }

    /// <summary>플레이어 사망 시 호출. 확정되지 않은 파밍분을 잃는다.
    /// keepRatio(거점 특수 강화 "사망 보험", 0~1)만큼은 종류마다 내림해서 창고(stash)로 확정한다.</summary>
    public static void DiscardRun(float keepRatio = 0f)
    {
        keepRatio = Mathf.Clamp01(keepRatio);
        bool anyKept = false;
        for (int i = 0; i < TypeCount; i++)
        {
            runHeld[i] = Mathf.FloorToInt(runHeld[i] * keepRatio);
            anyKept |= runHeld[i] > 0;
        }

        if (anyKept) { CommitRunToStash(); return; } // 남긴 만큼 창고로 (저장 + OnChanged)
        OnChanged?.Invoke();
    }

    /// <summary>거점(로비) 강화 등으로 영구 보관분(stash)을 소모한다. 부족하면 아무 일도 하지 않고 false.</summary>
    public static bool TrySpendStash(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        if (stash[(int)type] < amount) return false;

        stash[(int)type] -= amount;
        Save();
        OnChanged?.Invoke();
        return true;
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
            if (data?.stash == null) return;

            // 자원 종류가 늘어난(Rare 추가) 이전 세이브 파일도 겹치는 만큼은 그대로 살린다.
            Array.Copy(data.stash, stash, Mathf.Min(data.stash.Length, TypeCount));
            OnChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"자원 세이브 파일을 불러오지 못했습니다: {e.Message}");
        }
    }
}
