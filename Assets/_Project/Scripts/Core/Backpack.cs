using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 거점 특수 강화 "배낭": 거점 창고(stash)의 일반 자원을 일부 들고 탐험을 시작한다.
// 창고 건물(Building_Storehouse -> BackpackUI)에서 자원 종류마다 담을 양을 정해 두면, 거점 포탈로 탐험을 시작할 때(StagePortal)
// 그만큼 창고에서 꺼내 이번 탐험 자원(runHeld)으로 옮긴다. 무기 강화/상인에 바로 쓸 수 있고, 귀환하면 남은 만큼 창고로 돌아가고,
// 죽으면 탐험 자원과 함께 잃는다 (사망 보험만큼은 지킨다). 희귀자원은 담을 수 없다.
// 담을 양은 저장(backpack.json)되어 다음 탐험에도 그대로 쓴다 (창고에 모자라면 있는 만큼만 꺼낸다).
public static class Backpack
{
    public static readonly ResourceType[] Carriable =
        { ResourceType.Wood, ResourceType.Iron, ResourceType.Copper, ResourceType.Chemical, ResourceType.Oil };

    private static readonly int[] packed = new int[Enum.GetValues(typeof(ResourceType)).Length];

    public static event Action OnChanged;

    public static int Capacity => SpecialUpgrades.BackpackCapacity;
    public static int Get(ResourceType type) => packed[(int)type];

    public static int Total
    {
        get
        {
            int total = 0;
            foreach (ResourceType type in Carriable) total += packed[(int)type];
            return total;
        }
    }

    /// <summary>이 종류를 지금 최대 얼마까지 담을 수 있는지 (창고에 있는 양과 배낭 남은 칸 중 작은 쪽).</summary>
    public static int MaxFor(ResourceType type) =>
        type == ResourceType.Rare ? 0 : Mathf.Min(ResourceBank.GetStash(type), packed[(int)type] + Mathf.Max(0, Capacity - Total));

    public static void Set(ResourceType type, int amount)
    {
        int clamped = Mathf.Clamp(amount, 0, MaxFor(type));
        if (clamped == packed[(int)type]) return;
        packed[(int)type] = clamped;
        Save();
        OnChanged?.Invoke();
    }

    public static void Clear()
    {
        Array.Clear(packed, 0, packed.Length);
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>담을 양을 지금 창고에 있는 양과 배낭 용량에 맞게 줄인다 (창고 창을 열 때).</summary>
    public static void FitToStash()
    {
        bool changed = false;
        int budget = Capacity;
        foreach (ResourceType type in Carriable)
        {
            int fit = Mathf.Max(0, Mathf.Min(packed[(int)type], ResourceBank.GetStash(type), budget));
            budget -= fit;
            if (fit == packed[(int)type]) continue;
            packed[(int)type] = fit;
            changed = true;
        }
        if (!changed) return;
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>거점 포탈로 탐험을 시작할 때 부른다: 담아 둔 만큼 창고에서 꺼내 이번 탐험 자원으로 옮기고 화면에 알려 준다.</summary>
    public static void PackForRun()
    {
        if (StageManager.IsInStage) return;
        int budget = Capacity;
        if (budget <= 0) return;

        var parts = new List<string>();
        foreach (ResourceType type in Carriable)
        {
            int take = Mathf.Min(packed[(int)type], ResourceBank.GetStash(type), budget);
            if (take <= 0 || !ResourceBank.TrySpendStash(type, take)) continue;
            ResourceBank.AddRunResource(type, take);
            budget -= take;
            parts.Add($"{ResourceNames.Korean(type)} {take}");
        }
        if (parts.Count > 0)
            ScreenToast.Show($"배낭에 담은 자원을 들고 출발합니다\n{string.Join("   ", parts)}", new Color(0.75f, 0.95f, 0.75f));
    }

    // ===================== 저장 =====================

    private static string SavePath => Path.Combine(Application.persistentDataPath, "backpack.json");

    [Serializable]
    private class SaveData
    {
        public int[] packed;
    }

    private static void Save()
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(new SaveData { packed = (int[])packed.Clone() }));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Array.Clear(packed, 0, packed.Length);
        if (!File.Exists(SavePath)) return;
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data?.packed == null) return;
            for (int i = 0; i < packed.Length && i < data.packed.Length; i++)
                packed[i] = (ResourceType)i == ResourceType.Rare ? 0 : Mathf.Max(0, data.packed[i]);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"배낭 세이브 파일을 불러오지 못했습니다: {e.Message}");
        }
    }
}
