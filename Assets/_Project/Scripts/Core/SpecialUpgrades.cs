using System;
using System.IO;
using UnityEngine;

// 거점 "특수 강화"에서 고르는 강화 종류. 순서가 저장 파일의 배열 순서이므로 새 종류는 맨 뒤에 추가한다.
public enum SpecialUpgradeType
{
    Backpack,          // 배낭: 거점 창고 자원을 일부 들고 탐험을 시작한다
    Storage,           // 창고 크기: 거점에 보관할 수 있는 자원 종류별 최대치
    Revive,            // 부활 횟수 (기본 0)
    StartWeapon,       // 시작 무기 선택 (거치대 5개에 무기가 전시된다)
    Insurance,         // 사망 보험: 죽어도 이번 탐험 자원 일부를 지킨다
    Reroll,            // 레벨업 카드 새로고침 (탐험마다)
    WeaponSlots,       // 무기 슬롯 수 (기본 3)
    MerchantDiscount,  // 상인 할인
    RareDrop,          // 희귀자원 드랍률
    StartPotion,       // 시작 회복약
}

// 거점 "특수 강화": 희귀자원(거점 창고 stash)으로 하는 영구 유틸 강화. 단계는 저장 파일(special_upgrades.json)에 남아
// 게임을 껐다 켜도 유지된다 (PermanentUpgradeManager와 같은 방식). 각 효과는 아래 "효과 값"을 소비처가 읽어서 쓴다.
// 이름/설명/최대 단계/비용과 효과 수치는 이 파일 한 곳에서 바꾼다 (UI는 SpecialUpgradeUI).
public static class SpecialUpgrades
{
    public class Def
    {
        public SpecialUpgradeType type;
        public string title;
        public int[] costs;                    // 단계별 희귀자원 비용 (길이 = 최대 단계)
        public Func<int, string> describe;     // 그 단계의 효과 설명
        public bool implemented = true;        // false면 창에 "준비 중"으로 보이고 강화할 수 없다
        public int MaxLevel => costs.Length;
    }

    // 효과 수치 (단계 = 배열 번호, 0 = 강화 안 함)
    private static readonly int[] BackpackCapacityByLevel = { 0, 30, 60, 100, 150 };
    private static readonly int[] StorageCapByLevel = { 300, 500, 800, 1200, 1800, 2500 };

    public static readonly Def[] All =
    {
        // 설명은 창의 한 줄(약 250px, 14px 글꼴 기준 한글 15자 안팎)에 들어가게 짧게 쓴다 - 길면 SpecialUpgradeUI가 글자를 줄인다.
        new Def { type = SpecialUpgradeType.Backpack, title = "배낭", costs = new[] { 1, 2, 3, 4 },
            describe = l => l == 0 ? "들고 나갈 수 없음" : $"자원 {BackpackCapacityByLevel[l]}개 들고 출발" },
        new Def { type = SpecialUpgradeType.Storage, title = "창고 크기", costs = new[] { 1, 1, 2, 2, 3 },
            describe = l => $"자원별 최대 {StorageCapByLevel[l]}개 보관" },
        new Def { type = SpecialUpgradeType.Revive, title = "부활", costs = new[] { 2, 4, 6 },
            describe = l => l == 0 ? "부활 없음" : $"탐험마다 부활 {l}회" },
        new Def { type = SpecialUpgradeType.StartWeapon, title = "시작 무기 선택", costs = new[] { 2 },
            describe = l => l == 0 ? "시작 무기 고정" : "거치대에서 선택 가능" },
        new Def { type = SpecialUpgradeType.Insurance, title = "사망 보험", costs = new[] { 2, 3, 4 },
            describe = l => l == 0 ? "죽으면 자원 전부 잃음" : $"죽어도 자원 {l * 10}% 보존" },
        new Def { type = SpecialUpgradeType.Reroll, title = "카드 새로고침", costs = new[] { 1, 2, 3 },
            describe = l => l == 0 ? "다시 뽑기 없음" : $"탐험마다 {l}회 다시 뽑기" },
        new Def { type = SpecialUpgradeType.WeaponSlots, title = "무기 슬롯", costs = new[] { 2, 4 },
            describe = l => $"무기 {3 + l}개 장착" },
        new Def { type = SpecialUpgradeType.MerchantDiscount, title = "상인 할인", costs = new[] { 1, 2, 3 },
            describe = l => l == 0 ? "할인 없음" : $"상인 가격 {l * 10}% 할인" },
        new Def { type = SpecialUpgradeType.RareDrop, title = "희귀자원 드랍률", costs = new[] { 1, 2, 3, 4, 5 },
            describe = l => l == 0 ? "기본 드랍률" : $"희귀자원 드랍률 +{l * 20}%" },
        new Def { type = SpecialUpgradeType.StartPotion, title = "시작 회복약", costs = new[] { 1, 2, 3 },
            describe = l => $"회복약 {HealthPotion.BasePotionCount + l}개로 시작" },
    };

    private static readonly int[] levels = new int[Enum.GetValues(typeof(SpecialUpgradeType)).Length];

    // "시작 무기 선택"을 해금한 뒤 거치대에서 고른 무기 (WeaponType 이름, 고른 적 없으면 null). StartWeaponStand가 바꾼다.
    private static string startWeaponName;

    public static event Action OnChanged;

    // ===================== 효과 값 (소비처가 읽는다) =====================

    public static int BackpackCapacity => BackpackCapacityByLevel[GetLevel(SpecialUpgradeType.Backpack)];
    public static int StorageCapPerType => StorageCapByLevel[GetLevel(SpecialUpgradeType.Storage)];
    public static int ReviveCount => GetLevel(SpecialUpgradeType.Revive);
    public static bool StartWeaponUnlocked => GetLevel(SpecialUpgradeType.StartWeapon) > 0;
    public static float DeathKeepRatio => GetLevel(SpecialUpgradeType.Insurance) * 0.1f;
    public static int RerollsPerRun => GetLevel(SpecialUpgradeType.Reroll);
    public static int WeaponSlotCount => 3 + GetLevel(SpecialUpgradeType.WeaponSlots);
    public static float MerchantPriceMultiplier => 1f - GetLevel(SpecialUpgradeType.MerchantDiscount) * 0.1f;
    public static float RareDropMultiplier => 1f + GetLevel(SpecialUpgradeType.RareDrop) * 0.2f;
    public static int StartPotionBonus => GetLevel(SpecialUpgradeType.StartPotion);

    /// <summary>해금했고 거치대에서 무기를 고른 적이 있으면 그 무기. 아니면 false (씬에 처음 들려 있던 무기를 쓴다).</summary>
    public static bool TryGetStartWeapon(out WeaponType type)
    {
        type = default;
        return StartWeaponUnlocked && !string.IsNullOrEmpty(startWeaponName) && Enum.TryParse(startWeaponName, out type);
    }

    /// <summary>거치대에서 시작 무기를 고른다 (저장 + OnChanged - WeaponSwitcher가 거점에서 0번 슬롯을 바로 바꾼다).</summary>
    public static void SetStartWeapon(WeaponType type)
    {
        startWeaponName = type.ToString();
        Save();
        OnChanged?.Invoke();
    }

    // ===================== 단계 =====================

    public static Def Get(SpecialUpgradeType type) => Array.Find(All, d => d.type == type);
    public static int GetLevel(SpecialUpgradeType type) => levels[(int)type];
    public static bool IsMaxed(SpecialUpgradeType type) => GetLevel(type) >= Get(type).MaxLevel;
    public static int NextCost(SpecialUpgradeType type) => IsMaxed(type) ? 0 : Get(type).costs[GetLevel(type)];

    public static bool CanUpgrade(SpecialUpgradeType type) =>
        Get(type).implemented && !IsMaxed(type) && ResourceBank.GetStash(ResourceType.Rare) >= NextCost(type);

    /// <summary>거점 창고의 희귀자원을 써서 한 단계 올린다. 최대 단계이거나 부족하면 아무 일도 하지 않고 false.</summary>
    public static bool TryUpgrade(SpecialUpgradeType type)
    {
        if (!CanUpgrade(type)) return false;
        if (!ResourceBank.TrySpendStash(ResourceType.Rare, NextCost(type))) return false;

        levels[(int)type]++;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    // ===================== 저장 =====================

    private static string SavePath => Path.Combine(Application.persistentDataPath, "special_upgrades.json");

    [Serializable]
    private class SaveData
    {
        public int[] levels;
        public string startWeapon; // 시작 무기 선택 결과 (WeaponType 이름, 없으면 빈 값)
    }

    public static void Save()
    {
        File.WriteAllText(SavePath, JsonUtility.ToJson(new SaveData { levels = (int[])levels.Clone(), startWeapon = startWeaponName }));
    }

    // 게임(플레이 모드) 시작 시 자동으로 한 번 불려서 이전 저장분을 되살린다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Load()
    {
        Array.Clear(levels, 0, levels.Length);
        startWeaponName = null;
        if (!File.Exists(SavePath)) return;
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null) return;
            startWeaponName = data.startWeapon;
            if (data.levels == null) return;
            for (int i = 0; i < levels.Length && i < data.levels.Length; i++)
                levels[i] = Mathf.Clamp(data.levels[i], 0, All[i].MaxLevel);
            OnChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"특수 강화 세이브 파일을 불러오지 못했습니다: {e.Message}");
        }
    }
}
