using UnityEngine;
using UnityEngine.InputSystem;

// Pistol 전용 공격 스크립트.
// 들고 있을 때(isHeld)는 좌클릭을 누르고 있으면 attackInterval마다 자동으로 마우스 방향으로 발사한다.
// 들고 있지 않을 때는 자동공격 모드로 전환되어, autoAttackRange 안의 가장 가까운 적을 향해
// 같은 attackInterval로 끊임없이 계속 발사한다 (무기 자체는 보이지 않고 투사체만 나간다).
[RequireComponent(typeof(WeaponAim))]
public class PistolAttack : MonoBehaviour, IEnhanceableWeapon
{
    // 연속 발사 사이의 최소 간격(초). 이 시간이 지나기 전에는 발사되지 않는다.
    public float attackInterval = 0.3f;

    // 발사 순간 무기가 튀어 오르는 반동 각도(도 단위).
    public float recoilKickAngle = 25f;

    [Header("공격력 / 투사체")]
    // Pistol의 공격력. 나중에 무기 강화나 공격력을 올려주는 아이템 등으로 이 값을 바꾸는 식으로
    // 확장할 예정이라, 데미지 계산은 전부 이 값 하나만 참조하게 해뒀다.
    public float damage = 1f;
    public GameObject projectilePrefab;

    [Header("치명타")]
    // 공격마다 이 확률로 치명타가 터져서 데미지가 critMultiplier배 들어간다.
    // 나중에 강화나 스탯 아이템으로 두 값 다 바뀔 수 있어서 인스턴스 필드로 뒀다.
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    [Header("자동공격 (들고 있지 않을 때)")]
    // 들고 있지 않을 때, 플레이어를 중심으로 이 반경 안의 가장 가까운 적에게 자동으로 발사한다.
    public float autoAttackRange = 10f;

    [Header("강화 1레벨당 변화량 (전체 축소 0.5배에 맞춘 값)")]
    public float intervalReductionPerWood = 0.015f; // 목재: 발사 간격 (15강에서 0.3 -> 약 0.075, 하한 0.05)
    public float bulletSizePerCopper = 0.05f;       // 구리(공격 범위): 총알 크기 = 맞는 판정 크기 (10강에서 1.5배)
    public float speedPerOil = 0.2f;                // 기름: 투사체 속도 (기본 속도 4 -> 10강에서 +2, 15강에서 +3)

    // === 강화 단계별 특수 효과 ===
    // 각 강화 종류(자원)가 5단계/10단계에 도달하면 효과가 켜진다. 같은 종류 안에서는 도달한 가장 높은 단계 효과만
    // 적용된다(10단계가 되면 5단계 효과는 꺼지고 10단계 효과가 그 상위호환으로 대신한다). 다른 종류끼리는 같이 켜진다.
    // 효과는 쏜 탄(ProjectileModifiers)에 실려 나가며 직접 조작과 자동공격이 똑같이 동작한다. 단계는 이 권총 "한 자루"의 강화 레벨이다.
    [Header("특수효과: 목재(공속) - 필살탄")]
    // 일정 발수마다 쏘는 탄이 필살탄이 된다: 크고 금빛에 데미지가 크다. 10단계는 치명타 확정 + 맞을 때마다 충격 링.
    public int ultimateEveryTier5 = 6;
    public int ultimateEveryTier10 = 4;
    public float ultimateDamageTier5 = 2.5f;
    public float ultimateDamageTier10 = 3.5f;
    public float ultimateSize = 1.8f;
    public Color ultimateColor = new Color(1f, 0.85f, 0.25f);
    public float ultimateImpactRadius = 0.7f;

    [Header("특수효과: 철(공격력) - 눈덩이탄")]
    // 적을 맞힐 때마다(도탄으로 꺾일 때도) 탄이 커지고 데미지가 오른다.
    public float snowballPerHitTier5 = 0.15f;
    public int snowballMaxStacksTier5 = 3;
    public float snowballPerHitTier10 = 0.25f;
    public int snowballMaxStacksTier10 = 6;

    [Header("특수효과: 구리(범위) - 도탄")]
    // 적을 맞히면 반경 안의 아직 안 맞힌 가장 가까운 적에게 꺾인다. 꺾이는 건 관통 횟수를 쓰지 않는다.
    public int bouncesTier5 = 2;
    public float bounceRadiusTier5 = 2.5f;
    public int bouncesTier10 = 4;
    public float bounceRadiusTier10 = 5f;

    [Header("특수효과: 화학물질(치명타) - 표식")]
    // 치명타로 맞힌 적에게 표식: 지속시간 동안 모든 무기에게 받는 피해가 늘어난다. 10단계는 표식 적이 죽으면 근처 적에게 옮겨 간다.
    public float markDurationTier5 = 5f;
    public float markDamageTier5 = 1.2f;
    public float markDurationTier10 = 8f;
    public float markDamageTier10 = 1.4f;
    public float markSpreadRadiusTier10 = 3f;

    [Header("특수효과: 기름(속도) - 가속탄")]
    // 날아가면서 점점 빨라지고(최고 속도까지), 멀리 날아갈수록 데미지가 늘어난다 (상한까지).
    public float accelerationTier5 = 12f;
    public float maxSpeedTier5 = 12f;
    public float distanceDamagePerUnitTier5 = 0.125f;
    public float distanceDamageCapTier5 = 0.5f;
    public float accelerationTier10 = 30f;
    public float maxSpeedTier10 = 20f;
    public float distanceDamagePerUnitTier10 = 0.25f;
    public float distanceDamageCapTier10 = 1f;

    // 10단계 효과는 10강에서 켜진다 (종류별 최대 강화 WeaponEnhanceUtil.MaxLevel과 따로 고정값으로 둔다).
    private const int TierLow = 5;
    private const int TierHigh = 10;

    private WeaponAim weaponAim;
    private SpriteRenderer spriteRenderer;
    private int shotCount; // 지금까지 쏜 발수 (필살탄 주기 계산용)

    // 다음 발사까지 남은 쿨다운 시간(초). 0 이하가 되어야 다시 발사할 수 있다.
    private float attackCooldown;

    // === IEnhanceableWeapon ===
    // 강화 레벨 자체는 이 무기 한 자루의 WeaponIdentity에 저장된다 (읽고 쓰는 건 WeaponEnhanceStore를 거침).
    // 이 인스턴스는 그 레벨을 실제 스탯(damage 등)에 반영하는 역할만 한다.
    // 같은 종류를 여러 자루 들어도 자루마다 따로 강화된다.
    private WeaponIdentity identity;
    private float projectileSpeedBonus; // 기름(발사속도) 강화 누적분. Fire()에서 Projectile.speed에 더해준다.
    private float bulletSize = 1f;      // 구리(공격 범위) 강화 누적분. Fire()에서 탄 크기에 곱한다.

    // 강화로 바뀌는 스탯의 원본값. Awake에서 저장해두고, 런 종료(사망/추출)로 강화가 리셋되면
    // 여기로 되돌린 뒤 현재 레벨만큼 다시 적용한다 (포탈 이동은 무기 인스턴스가 유지되므로 직접 되돌려야 함).
    private float baseAttackInterval, baseDamage, baseCritChance, baseAutoAttackRange;

    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => WeaponEnhanceStore.GetLevel(identity, type);

    // 거점 강화 UI에서 자원을 소모하고 호출한다. 스토어에 기록 + 스탯 한 단계 적용.
    public void ApplyEnhance(ResourceType type)
    {
        if (!WeaponEnhanceStore.TryEnhance(identity, type)) return;
        ApplyStatDelta(type);
    }

    // 스탯 한 단계분을 실제 필드에 반영한다. ApplyEnhance(구매 시)와 Awake의 재적용(로드 시) 둘 다에서 쓰인다.
    private void ApplyStatDelta(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: attackInterval = Mathf.Max(0.05f, attackInterval - intervalReductionPerWood); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: bulletSize += bulletSizePerCopper; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
            case ResourceType.Oil: projectileSpeedBonus += speedPerOil; break;
        }
    }

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        identity = GetComponent<WeaponIdentity>();

        baseAttackInterval = attackInterval;
        baseDamage = damage;
        baseCritChance = critChance;
        baseAutoAttackRange = autoAttackRange;

        ReapplyEnhancements();
    }

    // WeaponEnhanceStore의 현재 강화 레벨을 스탯에 반영한다. 먼저 원본값으로 되돌린 뒤
    // 레벨 수만큼 ApplyStatDelta를 다시 적용하므로, 강화가 리셋된 뒤 호출해도 정확히 맞는다.
    public void ReapplyEnhancements()
    {
        attackInterval = baseAttackInterval;
        damage = baseDamage;
        critChance = baseCritChance;
        autoAttackRange = baseAutoAttackRange;
        projectileSpeedBonus = 0f;
        bulletSize = 1f;
        shotCount = 0; // 강화가 리셋/재계산될 때(사망, 탈출 등) 필살탄 주기도 처음부터

        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes)
        {
            int level = WeaponEnhanceStore.GetLevel(identity, type);
            for (int i = 0; i < level; i++) ApplyStatDelta(type);
        }

        // 거점 영구 강화(공격력/공속/치명타)도 같은 델타 공식으로 적용한다. 런 종료로 리셋되지 않는다.
        for (int i = 0; i < PermanentUpgradeManager.GetLevel(ResourceType.Iron); i++) ApplyStatDelta(ResourceType.Iron);
        for (int i = 0; i < PermanentUpgradeManager.GetLevel(ResourceType.Wood); i++) ApplyStatDelta(ResourceType.Wood);
        for (int i = 0; i < PermanentUpgradeManager.GetLevel(ResourceType.Chemical); i++) ApplyStatDelta(ResourceType.Chemical);
    }

    void OnEnable()
    {
        PermanentUpgradeManager.OnChanged += ReapplyEnhancements;
    }

    void OnDisable()
    {
        PermanentUpgradeManager.OnChanged -= ReapplyEnhancements;
    }

    void Update()
    {
        if (EnemyManager.PlayerDead) return; // 플레이어가 죽으면 자동/수동 상관없이 더 이상 발사하지 않는다
        if (PauseManager.IsPaused) return;   // 멈춘 동안(레벨업 카드 클릭 등) 클릭으로 발사되지 않게

        // 기믹 "한파" 중에는 WeaponTimeScale이 1보다 작아져 쿨다운 회복이 느려진다(재장전 체감 저하).
        // 레벨업 공격속도는 반대로 1보다 크게 만들어 쿨다운이 더 빨리 돈다 (PlayerRunStats.WeaponTimeScale).
        attackCooldown -= Time.deltaTime * PlayerRunStats.WeaponTimeScale;
        if (attackCooldown > 0f) return;

        if (weaponAim.isHeld)
        {
            // leftButton.isPressed는 눌려있는 동안 계속 true라서, 꾹 누르고 있으면 쿨다운이 풀리는
            // 즉시 자동으로 재발사되고(연사), attackInterval보다 빠르게는 발사되지 않는다.
            if (Mouse.current.leftButton.isPressed)
            {
                attackCooldown = attackInterval;
                weaponAim.Kick(recoilKickAngle);
                Fire(weaponAim.AimDirection);
            }
        }
        else
        {
            Enemy target = EnemyManager.FindNearest(transform.position, autoAttackRange);
            if (target != null)
            {
                attackCooldown = attackInterval;
                Fire((Vector2)target.transform.position - (Vector2)transform.position);
            }
        }
    }

    // 강화 UI에 보여줄 특수효과 설명. 강화창(너비 534px, 글자 16px)은 "현재 효과 → 다음 단계 효과"를 한 줄에 같이 보여주므로
    // 한글 기준 효과 하나당 12~14자 안쪽으로, 읽자마자 뭔지 알 수 있는 말로 쓴다.
    // 수치는 인스펙터 값을 그대로 읽어서 밸런스를 바꿔도 설명이 따라간다.
    public string GetMilestoneEffect(ResourceType type, int tier)
    {
        bool high = tier == TierHigh;
        if (tier != TierLow && !high) return null; // 15단계는 아직 미정
        switch (type)
        {
            case ResourceType.Wood:
                return $"{(high ? ultimateEveryTier10 : ultimateEveryTier5)}발마다 필살탄 ×{(high ? ultimateDamageTier10 : ultimateDamageTier5):0.#}";
            case ResourceType.Iron:
                return $"적중마다 크기·피해 +{(high ? snowballPerHitTier10 : snowballPerHitTier5) * 100f:0}%";
            case ResourceType.Copper:
                return $"다른 적에게 튕김 {(high ? bouncesTier10 : bouncesTier5)}회";
            case ResourceType.Chemical:
                return $"표식: 받는 피해 +{((high ? markDamageTier10 : markDamageTier5) - 1f) * 100f:0}%" + (high ? "·전이" : "");
            case ResourceType.Oil:
                return $"가속, 멀수록 피해 +{(high ? distanceDamageCapTier10 : distanceDamageCapTier5) * 100f:0}%";
        }
        return null;
    }

    // 특수효과가 켜지는 단계를 구한다: 0(없음) / 5 / 10. 이 권총 한 자루의 강화 레벨 기준.
    private int EffectTier(ResourceType type)
    {
        int level = GetEnhanceLevel(type);
        if (level >= TierHigh) return TierHigh;
        if (level >= TierLow) return TierLow;
        return 0;
    }

    // 이번 발이 필살탄인지: 목재 5단계부터 N발마다 한 번 (5단계 6발, 10단계 4발).
    private bool IsUltimateShot()
    {
        int tier = EffectTier(ResourceType.Wood);
        if (tier < TierLow) return false;

        int every = tier >= TierHigh ? ultimateEveryTier10 : ultimateEveryTier5;
        return every > 0 && shotCount % every == 0;
    }

    // Pistol 위치에서 주어진 방향으로 투사체를 하나 생성해서 날려보낸다.
    private void Fire(Vector2 direction)
    {
        FireShot(direction);
    }

    /// <summary>탄을 한 발 쏘고 그 탄을 돌려준다 (프리팹이 없으면 null). 에디터 테스트에서 직접 불러도 된다.</summary>
    public Projectile FireShot(Vector2 direction)
    {
        if (projectilePrefab == null) return null;

        shotCount++;
        bool ultimate = IsUltimateShot();
        bool ultimateHigh = ultimate && EffectTier(ResourceType.Wood) >= TierHigh;

        float shotBaseDamage = damage;
        if (ultimate) shotBaseDamage *= ultimateHigh ? ultimateDamageTier10 : ultimateDamageTier5;

        // 10단계 필살탄은 치명타 확정 (Random.value가 정확히 1일 수 있어 확률을 1보다 크게 준다).
        float finalDamage = CriticalHit.Roll(shotBaseDamage, ultimateHigh ? 1.01f : critChance, critMultiplier, out bool isCrit);

        GameObject projGO = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        // 탄 크기(=판정 크기): 구리 강화 x 레벨업 공격 범위. 특수효과 크기(필살탄/눈덩이)는 이 크기를 기준으로 곱해진다.
        projGO.transform.localScale *= bulletSize * PlayerRunStats.RangeMultiplier;
        Projectile projectile = projGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.speed = (projectile.speed + projectileSpeedBonus) * PlayerRunStats.ProjectileSpeedMultiplier;
            projectile.ApplyModifiers(BuildModifiers(ultimate, ultimateHigh));
            projectile.Fire(direction, finalDamage, isCrit);
        }

        // 필살탄은 총구에서도 번쩍인다.
        if (ultimate)
            CircleFlash.Spawn(transform.position, 0.35f, new Color(ultimateColor.r, ultimateColor.g, ultimateColor.b, 0.6f), 0.2f, spriteRenderer);

        return projectile;
    }

    // 지금 켜져 있는 강화 효과들을 이번 탄에 실을 값으로 정리한다. 켜진 효과가 하나도 없으면 null (예전과 똑같은 평범한 탄).
    private ProjectileModifiers BuildModifiers(bool ultimate, bool ultimateHigh)
    {
        ProjectileModifiers m = new ProjectileModifiers();

        if (ultimate)
        {
            m.sizeMultiplier = ultimateSize;
            m.tint = ultimateColor;
            if (ultimateHigh)
            {
                m.impactRing = true;
                m.impactRadius = ultimateImpactRadius;
                m.impactColor = new Color(ultimateColor.r, ultimateColor.g, ultimateColor.b, 0.6f);
            }
        }

        int iron = EffectTier(ResourceType.Iron);
        if (iron >= TierLow)
        {
            bool high = iron >= TierHigh;
            m.snowballDamagePerHit = high ? snowballPerHitTier10 : snowballPerHitTier5;
            m.snowballSizePerHit = m.snowballDamagePerHit;
            m.snowballMaxStacks = high ? snowballMaxStacksTier10 : snowballMaxStacksTier5;
            m.snowballAura = high;
        }

        int copper = EffectTier(ResourceType.Copper);
        if (copper >= TierLow)
        {
            bool high = copper >= TierHigh;
            m.maxBounces = high ? bouncesTier10 : bouncesTier5;
            m.bounceRadius = high ? bounceRadiusTier10 : bounceRadiusTier5;
            m.beamColor = high ? new Color(1f, 1f, 1f, 1f) : new Color(0.5f, 0.9f, 1f, 0.8f); // 10단계는 밝고 굵은 번개선
            m.beamWidth = high ? 0.12f : 0.05f;
        }

        int chemical = EffectTier(ResourceType.Chemical);
        if (chemical >= TierLow)
        {
            bool high = chemical >= TierHigh;
            m.markDuration = high ? markDurationTier10 : markDurationTier5;
            m.markDamageTaken = high ? markDamageTier10 : markDamageTier5;
            m.markSpreadRadius = high ? markSpreadRadiusTier10 : 0f;
        }

        int oil = EffectTier(ResourceType.Oil);
        if (oil >= TierLow)
        {
            bool high = oil >= TierHigh;
            m.acceleration = high ? accelerationTier10 : accelerationTier5;
            m.maxSpeed = high ? maxSpeedTier10 : maxSpeedTier5;
            m.distanceDamagePerUnit = high ? distanceDamagePerUnitTier10 : distanceDamagePerUnitTier5;
            m.distanceDamageCap = high ? distanceDamageCapTier10 : distanceDamageCapTier5;
            m.tailColor = new Color(0.6f, 0.85f, 1f, high ? 0.8f : 0.6f);
        }

        return m.HasAnyEffect ? m : null;
    }
}
