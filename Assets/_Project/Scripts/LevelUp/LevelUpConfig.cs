using System;
using UnityEngine;

// 레벨업으로 올릴 수 있는 플레이어 능력 (PlayerRunStats 참고).
public enum RunStatType { Damage, AttackSpeed, Range, Crit, ProjectileSpeed, MoveSpeed, MaxHealth, XpGain }

// 레벨업 시스템의 모든 수치/문구/그림을 모아 둔 설정 에셋 (Resources/LevelUpConfig).
// 밸런스는 이 에셋의 인스펙터에서 바꾸면 되고, 에셋이 없으면 코드 기본값(ResetToDefaults)으로 동작한다.
[CreateAssetMenu(menuName = "Endure/Level Up Config", fileName = "LevelUpConfig")]
public class LevelUpConfig : ScriptableObject
{
    [Serializable]
    public class StatCard
    {
        public RunStatType stat;
        public string title;
        [Tooltip("{v} 자리에 한 단계 증가량(+10% 등)이 들어간다")]
        public string description;
        public float perLevel;
        public int maxLevel = 5;
        public Sprite icon;
    }

    public enum FillerKind { Resource, Potion, Magnet, Bomb, Rage, Revive }

    // 올릴 스탯 카드가 3장이 안 될 때 빈자리를 채우는 보충 카드. weight가 클수록 잘 나온다.
    [Serializable]
    public class FillerCard
    {
        public FillerKind kind;
        public ResourceType resource; // kind가 Resource일 때만
        public string title;
        [Tooltip("{n} 자리에 개수, {s} 자리에 초, {p} 자리에 퍼센트가 들어간다")]
        public string description;
        public float weight;
        public Sprite icon;
    }

    [Header("경험치")]
    public float orbBaseXp = 1f;            // 1스테이지 일반 몬스터 경험치 구슬 하나의 양
    public float orbXpGrowthPerStage = 0.1f; // 스테이지가 하나 오를 때마다 구슬 경험치 +10%
    public float eliteXpMultiplier = 10f;   // 정예/보스는 이만큼 더 준다
    public float requiredXpBase = 30f;      // 1 -> 2레벨에 필요한 경험치 (1층 45초 기준 1층에서 레벨업 약 3번)
    public float requiredXpGrowth = 1.15f;  // 레벨이 하나 오를 때마다 필요 경험치 x1.15 (스테이지 경험치 증가보다 가파르게)

    [Header("경험치 구슬 (자원 아이템과 같은 흡입 규칙)")]
    public float orbAttractRadius = 0.75f;
    public float orbMoveSpeed = 3f;
    public float orbPickupRadius = 0.1f;
    public float orbSize = 0.14f;
    public float eliteOrbSizeMultiplier = 1.6f;

    [Header("스탯 카드")]
    public StatCard[] statCards;

    [Header("보충 카드")]
    public FillerCard[] fillerCards;
    public int resourceCardBaseAmount = 15;
    public float resourceCardAmountPerStage = 3f;
    public int rareCardBaseAmount = 1;
    public float rareCardAmountPerStage = 0.2f;
    public int potionCardAmount = 1;
    [Range(0f, 1f)] public float bombDamageRatio = 0.6f;      // 화면 안 일반 몬스터: 최대 체력의 이 비율만큼 피해
    [Range(0f, 1f)] public float bombEliteDamageRatio = 0.15f; // 정예/보스
    public float rageDamageBonus = 0.5f;
    public float rageDuration = 10f;

    [Header("부활 (기본 부활과 부활 카드 공통)")]
    [Range(0.1f, 1f)] public float reviveHealthRatio = 0.5f;
    public float reviveInvulnerability = 2f;
    public float reviveShockwaveRadius = 1.5f;
    public float reviveKnockback = 4f;

    [Header("UI 그림 (PixelArtGUI)")]
    public Font font;
    public Sprite panel;
    public Sprite panelBottomBevel;
    public Sprite panelTopBevel;
    public Sprite panelOutline;
    public Sprite corners;
    public Sprite titleBanner;
    public Sprite barFill;
    public Sprite reviveIcon;
    public Sprite rageIcon;
    public Sprite orbIcon; // 바닥에 떨어지는 경험치 구슬 그림 (비어 있으면 코드로 그린 임시 구체)
    [Tooltip("UI에서 그림 1픽셀을 몇 칸으로 키워 보일지 (픽셀 아트 배율)")]
    public float pixelScale = 3f;

    private static LevelUpConfig instance;

    public static LevelUpConfig Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = Resources.Load<LevelUpConfig>("LevelUpConfig");
            if (instance == null)
            {
                instance = CreateInstance<LevelUpConfig>();
                instance.ResetToDefaults();
            }
            return instance;
        }
    }

    // n레벨에서 다음 레벨로 가는 데 필요한 경험치.
    public float RequiredXp(int level) => requiredXpBase * Mathf.Pow(requiredXpGrowth, Mathf.Max(0, level - 1));

    public StatCard GetStat(RunStatType stat)
    {
        if (statCards != null)
            foreach (StatCard card in statCards)
                if (card != null && card.stat == stat) return card;
        return null;
    }

    // 수치/문구 기본값. 그림(Sprite/Font)은 건드리지 않는다 (에셋을 만들 때 에디터에서 따로 채운다).
    public void ResetToDefaults()
    {
        Sprite Icon(RunStatType t) => GetStat(t)?.icon;
        statCards = new[]
        {
            new StatCard { stat = RunStatType.Damage,          title = "공격력",      description = "모든 무기의 공격력 {v}",     perLevel = 0.10f, maxLevel = 5, icon = Icon(RunStatType.Damage) },
            new StatCard { stat = RunStatType.AttackSpeed,     title = "공격속도",    description = "모든 무기의 공격 속도 {v}",  perLevel = 0.10f, maxLevel = 5, icon = Icon(RunStatType.AttackSpeed) },
            new StatCard { stat = RunStatType.Range,           title = "공격 범위",   description = "무기·폭발·탄 크기 {v}",      perLevel = 0.10f, maxLevel = 5, icon = Icon(RunStatType.Range) },
            new StatCard { stat = RunStatType.Crit,            title = "치명타",      description = "치명타 확률 {v}",            perLevel = 0.05f, maxLevel = 5, icon = Icon(RunStatType.Crit) },
            new StatCard { stat = RunStatType.ProjectileSpeed, title = "투사체 속도", description = "탄·검기·수류탄 속도 {v}",    perLevel = 0.10f, maxLevel = 5, icon = Icon(RunStatType.ProjectileSpeed) },
            new StatCard { stat = RunStatType.MoveSpeed,       title = "이동속도",    description = "이동 속도 {v}",              perLevel = 0.08f, maxLevel = 5, icon = Icon(RunStatType.MoveSpeed) },
            new StatCard { stat = RunStatType.MaxHealth,       title = "최대 체력",   description = "최대 체력 {v}, 바로 회복",   perLevel = 1f,    maxLevel = 5, icon = Icon(RunStatType.MaxHealth) },
            new StatCard { stat = RunStatType.XpGain,          title = "경험치",      description = "경험치 획득량 {v}",          perLevel = 0.10f, maxLevel = 5, icon = Icon(RunStatType.XpGain) },
        };

        FillerCard Old(FillerKind k, ResourceType r)
        {
            if (fillerCards != null)
                foreach (FillerCard c in fillerCards)
                    if (c != null && c.kind == k && (k != FillerKind.Resource || c.resource == r)) return c;
            return null;
        }
        FillerCard Make(FillerKind k, ResourceType r, string title, string desc, float weight) =>
            new FillerCard { kind = k, resource = r, title = title, description = desc, weight = weight, icon = Old(k, r)?.icon };

        fillerCards = new[]
        {
            Make(FillerKind.Resource, ResourceType.Wood,     "나무 보급",     "나무 {n}개 획득",        10f),
            Make(FillerKind.Resource, ResourceType.Iron,     "철 보급",       "철 {n}개 획득",          10f),
            Make(FillerKind.Resource, ResourceType.Copper,   "구리 보급",     "구리 {n}개 획득",        10f),
            Make(FillerKind.Resource, ResourceType.Chemical, "화학물질 보급", "화학물질 {n}개 획득",    10f),
            Make(FillerKind.Resource, ResourceType.Oil,      "기름 보급",     "기름 {n}개 획득",        10f),
            Make(FillerKind.Resource, ResourceType.Rare,     "희귀자원",      "희귀자원 {n}개 획득",    5f),
            Make(FillerKind.Potion,   ResourceType.Wood,     "회복약",        "회복약 {n}개 획득",      12f),
            Make(FillerKind.Magnet,   ResourceType.Wood,     "자석",          "맵의 경험치·자원을 전부 끌어옴", 10f),
            Make(FillerKind.Bomb,     ResourceType.Wood,     "폭탄",          "화면 안 모든 적에게 큰 피해",    10f),
            Make(FillerKind.Rage,     ResourceType.Wood,     "분노",          "{s}초 동안 공격력 +{p}%",        10f),
            Make(FillerKind.Revive,   ResourceType.Wood,     "부활",          "부활 횟수 +1",                   3f),
        };
    }
}
