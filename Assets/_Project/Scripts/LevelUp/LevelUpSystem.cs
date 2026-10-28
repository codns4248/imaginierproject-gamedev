using System;
using UnityEngine;

// 경험치와 레벨 (이번 탐험 동안만 유지). 몬스터가 떨어뜨린 경험치 구슬(XpOrb)을 주우면 AddXp가 불리고,
// 경험치가 필요량을 넘으면 레벨이 오르면서 "고르지 않은 레벨업"(PendingLevelUps)이 하나 쌓인다.
// 레벨업 창(LevelUpUI)이 그걸 보고 게임을 멈춘 뒤 카드 3장을 보여주고, 하나 고르면 ConsumePending으로 하나 줄인다.
// 넘친 경험치는 다음 레벨로 이어지고, 큰 구슬 하나로 두 레벨이 한꺼번에 차면 카드를 고르자마자 다음 카드가 이어서 뜬다.
public static class LevelUpSystem
{
    public static int Level { get; private set; } = 1;
    public static float Xp { get; private set; }
    public static int PendingLevelUps { get; private set; }

    // 지금 레벨에서 다음 레벨까지 필요한 경험치.
    public static float RequiredXp => LevelUpConfig.Instance.RequiredXp(Level);
    public static float Progress => Mathf.Clamp01(Xp / Mathf.Max(0.01f, RequiredXp));

    // 경험치/레벨이 바뀔 때마다 (초기화 포함) 호출된다. 경험치 바 갱신용.
    public static event Action Changed;

    // 고르지 않은 레벨업이 새로 생겼을 때 호출된다. 레벨업 창이 열리는 신호.
    public static event Action LevelUpReady;

    public static void AddXp(float amount)
    {
        if (amount <= 0f) return;

        Xp += amount * PlayerRunStats.XpGainMultiplier;
        bool leveled = false;
        while (Xp >= RequiredXp)
        {
            Xp -= RequiredXp;
            Level++;
            PendingLevelUps++;
            leveled = true;
        }

        Changed?.Invoke();
        if (leveled) LevelUpReady?.Invoke();
    }

    // 레벨업 보상 하나를 골랐을 때.
    public static void ConsumePending()
    {
        if (PendingLevelUps > 0) PendingLevelUps--;
        Changed?.Invoke();
    }

    // 몬스터 한 마리가 떨어뜨리는 경험치: 스테이지가 오를수록 많고, 정예/보스는 훨씬 많다.
    public static float OrbXpFor(bool elite)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        int stage = Mathf.Clamp(StageProgress.CurrentStageNo, 1, MonsterDifficulty.PeakStage); // 20층 이후 구슬당 경험치 고정
        float xp = cfg.orbBaseXp * (1f + cfg.orbXpGrowthPerStage * (stage - 1));
        return elite ? xp * cfg.eliteXpMultiplier : xp;
    }

    // 탐험이 끝날 때(사망/탈출/추출) 부른다: 레벨, 경험치, 레벨업 능력, 바닥의 경험치 구슬을 버리고
    // 부활 횟수를 기본값으로 되돌린다 (플레이어가 거점에 계속 남아 있는 구조라 직접 되돌려야 한다).
    public static void ResetForNewRun()
    {
        Level = 1;
        Xp = 0f;
        PendingLevelUps = 0;
        PlayerRunStats.ResetForNewRun();
        XpOrb.DestroyAll();

        PlayerHealth health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null) health.ResetRunState();

        Changed?.Invoke();
    }

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 레벨이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Level = 1;
        Xp = 0f;
        PendingLevelUps = 0;
        Changed = null;
        LevelUpReady = null;
    }
}
