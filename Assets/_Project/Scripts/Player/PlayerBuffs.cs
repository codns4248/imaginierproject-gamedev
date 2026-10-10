using UnityEngine;

// 무기 특수 효과 등이 플레이어에게 임시로 거는 버프. 지금은 검의 기름 강화 특수효과 "질주"
// (적을 처치하면 잠깐 이동속도 증가, 10단계는 근접 무기 쿨다운도 단축)만 있다.
//
// 같은 종류의 버프는 여러 무기(여러 자루)에서 동시에 걸어도 합산되지 않고 가장 센 하나만 남는다
// (자원 5종 효과를 여러 자루가 겹쳐서 이동속도가 폭주하는 것을 막기 위함).
public static class PlayerBuffs
{
    private static float moveMultiplier = 1f;
    private static float cooldownReduction;
    private static float expiresAt;

    private static bool IsActive => Time.time < expiresAt;

    // Player_Movement가 이동속도에 곱한다. 버프가 없으면 1.
    public static float MoveSpeedMultiplier => IsActive ? moveMultiplier : 1f;

    // 근접 무기(SwordAttack)가 쿨다운 감소 속도에 곱한다. 쿨다운 시간이 reduction만큼 줄어드는 것과 같다
    // (예: 20% 단축이면 쿨다운이 1/0.8 = 1.25배 빨리 돈다). 버프가 없으면 1.
    public static float MeleeCooldownSpeedMultiplier => IsActive && cooldownReduction > 0f ? 1f / (1f - cooldownReduction) : 1f;

    // 질주 버프를 건다. 이미 더 센 이동속도 버프가 켜져 있으면 무시하고, 같거나 더 세면 덮어쓰면서 지속시간을 새로 시작한다.
    public static void ApplySprint(float moveBonus, float duration, float meleeCooldownReduction)
    {
        float newMultiplier = 1f + moveBonus;
        if (IsActive && newMultiplier < moveMultiplier) return;

        moveMultiplier = newMultiplier;
        cooldownReduction = Mathf.Clamp(meleeCooldownReduction, 0f, 0.9f);
        expiresAt = Time.time + duration;
    }

    // 스테이지를 벗어나거나 죽었을 때 남아있는 버프를 없앤다.
    public static void Clear()
    {
        expiresAt = 0f;
        moveMultiplier = 1f;
        cooldownReduction = 0f;
    }

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 expiresAt이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Clear();
}
