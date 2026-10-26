using System.Collections.Generic;
using UnityEngine;

// 레벨업 창에 보여줄 카드 한 장.
public class LevelUpCard
{
    public bool isStat;
    public RunStatType stat;
    public LevelUpConfig.FillerCard filler;
    public int amount; // 자원/회복약 개수
    public string title;
    public string levelText;   // 스탯 카드: "Lv 2 → 3", 보충 카드: 비움
    public string description;
    public Sprite icon;
}

// 레벨업 카드를 뽑고 적용한다.
// 1) 아직 최대가 아닌 스탯 카드 중에서 최대 3장을 고르고, 2) 3장이 안 되면 남는 칸을 보충 카드(자원, 회복약, 자석,
// 폭탄, 분노, 부활)로 가중치 랜덤 채운다. 한 번에 같은 카드가 두 장 나오지 않는다.
public static class LevelUpCardPool
{
    public static List<LevelUpCard> Roll(int count = 3)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        List<LevelUpCard> result = new List<LevelUpCard>(count);

        // 1) 스탯 카드
        List<LevelUpConfig.StatCard> stats = new List<LevelUpConfig.StatCard>();
        if (cfg.statCards != null)
            foreach (LevelUpConfig.StatCard s in cfg.statCards)
                if (s != null && !PlayerRunStats.IsMaxed(s.stat)) stats.Add(s);

        while (result.Count < count && stats.Count > 0)
        {
            int i = Random.Range(0, stats.Count);
            result.Add(MakeStatCard(stats[i]));
            stats.RemoveAt(i);
        }

        // 2) 보충 카드 (가중치 랜덤, 중복 없이)
        List<LevelUpConfig.FillerCard> fillers = new List<LevelUpConfig.FillerCard>();
        if (cfg.fillerCards != null)
            foreach (LevelUpConfig.FillerCard f in cfg.fillerCards)
                if (f != null && f.weight > 0f) fillers.Add(f);

        while (result.Count < count && fillers.Count > 0)
        {
            float total = 0f;
            foreach (LevelUpConfig.FillerCard f in fillers) total += f.weight;

            float roll = Random.value * total;
            int pick = fillers.Count - 1;
            for (int i = 0; i < fillers.Count; i++)
            {
                roll -= fillers[i].weight;
                if (roll <= 0f) { pick = i; break; }
            }

            result.Add(MakeFillerCard(fillers[pick]));
            fillers.RemoveAt(pick);
        }

        return result;
    }

    private static LevelUpCard MakeStatCard(LevelUpConfig.StatCard s)
    {
        int level = PlayerRunStats.GetLevel(s.stat);
        bool toMax = level + 1 >= s.maxLevel;
        return new LevelUpCard
        {
            isStat = true,
            stat = s.stat,
            title = s.title,
            levelText = $"Lv {level} → {level + 1}{(toMax ? " (MAX)" : "")}",
            description = (s.description ?? "").Replace("{v}", ValueText(s)),
            icon = s.icon,
        };
    }

    // 한 단계 증가량을 사람이 읽는 글자로: +10%, +5%p, +1칸
    private static string ValueText(LevelUpConfig.StatCard s)
    {
        switch (s.stat)
        {
            case RunStatType.MaxHealth: return $"+{s.perLevel:0}칸";
            case RunStatType.Crit: return $"+{s.perLevel * 100f:0}%p";
            default: return $"+{s.perLevel * 100f:0}%";
        }
    }

    private static LevelUpCard MakeFillerCard(LevelUpConfig.FillerCard f)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        int stage = Mathf.Max(1, StageProgress.CurrentStageNo);

        int amount = 0;
        if (f.kind == LevelUpConfig.FillerKind.Resource)
        {
            amount = f.resource == ResourceType.Rare
                ? cfg.rareCardBaseAmount + Mathf.FloorToInt(cfg.rareCardAmountPerStage * (stage - 1))
                : cfg.resourceCardBaseAmount + Mathf.FloorToInt(cfg.resourceCardAmountPerStage * (stage - 1));
        }
        else if (f.kind == LevelUpConfig.FillerKind.Potion)
        {
            amount = cfg.potionCardAmount;
        }

        string desc = (f.description ?? "")
            .Replace("{n}", amount.ToString())
            .Replace("{s}", cfg.rageDuration.ToString("0"))
            .Replace("{p}", (cfg.rageDamageBonus * 100f).ToString("0"));

        Sprite icon = f.icon;
        if (icon == null && f.kind == LevelUpConfig.FillerKind.Resource) icon = ResourcePickup.GetIconSprite(f.resource);

        return new LevelUpCard
        {
            isStat = false,
            filler = f,
            amount = amount,
            title = f.title,
            levelText = "",
            description = desc,
            icon = icon,
        };
    }

    public static void Apply(LevelUpCard card)
    {
        if (card == null) return;

        if (card.isStat)
        {
            PlayerRunStats.AddLevel(card.stat);

            // 최대 체력은 늘어난 만큼 바로 채워준다 (PlayerHealth가 Changed를 받아 최대치를 먼저 늘린 뒤).
            if (card.stat == RunStatType.MaxHealth)
            {
                PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
                LevelUpConfig.StatCard s = LevelUpConfig.Instance.GetStat(card.stat);
                if (health != null && s != null) health.Heal(Mathf.RoundToInt(s.perLevel));
            }
            return;
        }

        LevelUpConfig cfg = LevelUpConfig.Instance;
        switch (card.filler.kind)
        {
            case LevelUpConfig.FillerKind.Resource:
                ResourceBank.AddRunResource(card.filler.resource, card.amount);
                break;

            case LevelUpConfig.FillerKind.Potion:
                HealthPotion potion = Object.FindFirstObjectByType<HealthPotion>();
                if (potion != null) potion.AddPotions(card.amount);
                break;

            case LevelUpConfig.FillerKind.Magnet:
                XpOrb.ForceAttractAll();
                foreach (ResourcePickup pickup in Object.FindObjectsByType<ResourcePickup>(FindObjectsSortMode.None))
                    pickup.ForceAttract();
                break;

            case LevelUpConfig.FillerKind.Bomb:
                Bomb(cfg);
                break;

            case LevelUpConfig.FillerKind.Rage:
                PlayerRunStats.ApplyRage(cfg.rageDamageBonus, cfg.rageDuration);
                break;

            case LevelUpConfig.FillerKind.Revive:
                PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
                if (health != null) health.AddRevive(1);
                break;
        }
    }

    // 화면 안에 보이는 모든 적에게 최대 체력 비율만큼 피해 (정예/보스는 작은 비율). 플레이어 위치에서 큰 섬광.
    private static void Bomb(LevelUpConfig cfg)
    {
        Camera cam = Camera.main;
        GameObject playerObj = GameObject.Find("Player");
        Vector2 center = playerObj != null ? (Vector2)playerObj.transform.position : Vector2.zero;
        SpriteRenderer sorting = playerObj != null ? playerObj.GetComponent<SpriteRenderer>() : null; // 섬광을 캐릭터와 같은 층에 그린다

        List<Enemy> targets = new List<Enemy>(EnemyManager.ActiveEnemies);
        foreach (Enemy e in targets)
        {
            if (e == null || e.IsDying) continue;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(e.transform.position);
                if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
            }

            float ratio = e.IsEliteOrBoss ? cfg.bombEliteDamageRatio : cfg.bombDamageRatio;
            Vector2 dir = (Vector2)e.transform.position - center;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            e.Hit(dir.normalized, e.maxHealth * ratio, false, 1.5f);
            PixelFx.Play("explosion", e.transform.position, 0.7f); // 폭탄: 화면 안 적마다 작은 폭발
            CircleFlash.Spawn(e.transform.position, 0.35f, new Color(1f, 0.85f, 0.4f, 0.8f), 0.35f, sorting);
        }

        float screenRadius = cam != null ? cam.orthographicSize * cam.aspect * 1.2f : 5f;
        CircleFlash.Spawn(center, screenRadius, new Color(1f, 0.95f, 0.7f, 0.45f), 0.5f, sorting);
    }
}
