using System;
using UnityEngine;

// 레벨업으로 얻은 플레이어 능력 (이번 탐험 동안만 유지).
// 무기 강화(무기 한 자루의 수치에 더하기)와 달리 플레이어 쪽에만 쌓이는 퍼센트 보너스라서, 무기 수치 자체는 건드리지 않고
// 각 무기/플레이어가 쓰는 순간에 여기 배율을 읽어 곱한다. 그래서 무기를 버렸다 주워도 중복으로 쌓이지 않고,
// 새로 주운 무기도 바로 같은 보너스를 받는다. 같은 능력을 여러 번 고르면 더하기로 쌓인다 (+10% 두 번 = +20%).
// 탐험이 끝나면(사망/탈출/추출) LevelUpSystem.ResetForNewRun이 초기화한다.
public static class PlayerRunStats
{
    private static readonly int[] levels = new int[Enum.GetValues(typeof(RunStatType)).Length];

    // 보충 카드 "분노": 잠깐 공격력이 크게 오른다. Time.time 기준이라 일시정지 중에는 시간이 흐르지 않는다.
    private static float rageBonus;
    private static float rageExpiresAt;

    // 능력 단계가 바뀔 때마다 (초기화 포함) 호출된다. 범위처럼 무기 쪽에서 다시 계산해야 하는 값, 최대 체력 등이 구독한다.
    public static event Action Changed;

    public static int GetLevel(RunStatType stat) => levels[(int)stat];

    public static int GetMaxLevel(RunStatType stat)
    {
        LevelUpConfig.StatCard card = LevelUpConfig.Instance.GetStat(stat);
        return card != null ? card.maxLevel : 0;
    }

    public static bool IsMaxed(RunStatType stat) => GetLevel(stat) >= GetMaxLevel(stat);

    public static void AddLevel(RunStatType stat)
    {
        if (IsMaxed(stat)) return;
        levels[(int)stat]++;
        Changed?.Invoke();
    }

    // 한 단계당 증가량 x 지금 단계.
    private static float Bonus(RunStatType stat)
    {
        LevelUpConfig.StatCard card = LevelUpConfig.Instance.GetStat(stat);
        return card != null ? card.perLevel * levels[(int)stat] : 0f;
    }

    // 모든 무기의 피해에 곱해진다 (Enemy.Hit/TakeDotDamage에서 한 번에 적용). 분노 버프도 여기에 같이 곱해진다.
    public static float DamageMultiplier => (1f + Bonus(RunStatType.Damage)) * (1f + RageBonus);

    // 공격 속도: 무기들이 쓰는 시간 배율(쿨다운/모션)에 곱해진다. 기믹 "한파"의 감속과 같이 쓰려고 WeaponTimeScale로 묶어 둔다.
    public static float AttackSpeedMultiplier => 1f + Bonus(RunStatType.AttackSpeed);
    public static float WeaponTimeScale => StageGimmickManager.WeaponTimeScale * AttackSpeedMultiplier;

    // 공격 범위: 검/창 크기와 판정, 수류탄 폭발 반경, 총알 크기.
    public static float RangeMultiplier => 1f + Bonus(RunStatType.Range);

    // 치명타 확률에 더해진다 (+5%p씩). CriticalHit.Roll에서 한 번에 적용.
    public static float CritBonus => Bonus(RunStatType.Crit);

    public static float ProjectileSpeedMultiplier => 1f + Bonus(RunStatType.ProjectileSpeed);
    public static float MoveSpeedMultiplier => 1f + Bonus(RunStatType.MoveSpeed);
    public static int MaxHealthBonus => Mathf.RoundToInt(Bonus(RunStatType.MaxHealth));
    public static float XpGainMultiplier => 1f + Bonus(RunStatType.XpGain);

    public static float RageBonus => Time.time < rageExpiresAt ? rageBonus : 0f;
    public static float RageRemaining => Mathf.Max(0f, rageExpiresAt - Time.time);

    public static void ApplyRage(float bonus, float duration)
    {
        rageBonus = Mathf.Max(RageBonus, bonus);
        rageExpiresAt = Time.time + duration;
    }

    public static void ResetForNewRun()
    {
        Array.Clear(levels, 0, levels.Length);
        rageBonus = 0f;
        rageExpiresAt = 0f;
        Changed?.Invoke();
    }

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 단계가 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Array.Clear(levels, 0, levels.Length);
        rageBonus = 0f;
        rageExpiresAt = 0f;
        Changed = null;
    }
}
