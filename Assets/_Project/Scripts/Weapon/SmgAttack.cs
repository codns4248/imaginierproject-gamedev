using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Smg 전용 공격 스크립트. Pistol과 거의 동일하지만(같은 Projectile 프리팹, 조준 방식) 두 가지가 다르다:
// 1) 한 번 발사되면 0.2초 간격으로 3발이 나가는 3점사이고, 3점사가 끝나면 잠깐 쉬었다가 다시 쏠 수 있다.
// 2) 여기서 쓰는 투사체는 적을 관통하지 않는다(한 마리만 맞고 사라짐).
// 들고 있지 않을 때는 자동공격 모드로 전환되어, autoAttackRange 안의 가장 가까운 적을 향해
// 같은 3점사 사이클을 끊임없이 반복한다 (무기 자체는 보이지 않고 투사체만 나간다).
//
// 강화 특수효과는 "총알을 쏟아붓는" 방향이다: 휴식 없는 난사(목재), 큰 탄 옆에 나란히 나가는 작은 탄(철),
// 맞으면 갈라지는 파편(구리), 치명타 폭발(화학물질), 밀어내는 충격(기름). 아래 EffectTier/FireShot 참고.
// (철: 조준 방향으로 곧게 나가는 큰 탄 양옆으로 작은 탄이 부채꼴로 같이 나간다)
[RequireComponent(typeof(WeaponAim))]
public class SmgAttack : MonoBehaviour, IEnhanceableWeapon
{
    [Header("3점사 타이밍")]
    public int burstCount = 3;        // 한 번에 발사되는 탄 수
    public float burstInterval = 0.2f; // 점사 내 발사 간격
    public float restDuration = 0.4f;  // 3점사가 끝난 뒤 다음 3점사까지 쉬는 시간
    public float recoilKickAngle = 15f; // 한 발 쏠 때마다 살짝 튀는 반동 각도

    [Header("공격력 / 투사체")]
    // Smg 투사체 하나당 데미지. 나중에 강화 요소로 바뀔 수 있어 이 값 하나만 참조하게 해뒀다.
    public float damage = 1f;
    public GameObject projectilePrefab; // Pistol과 같은 Projectile 프리팹을 그대로 쓰되, 발사 시 maxHits를 1로 덮어써서 관통을 없앤다.

    [Header("치명타")]
    // 3점사의 발 하나하나가 각자 독립적으로 치명타 판정을 받는다.
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    [Header("자동공격 (들고 있지 않을 때)")]
    public float autoAttackRange = 10f;

    [Header("강화 1레벨당 변화량 (전체 축소 0.5배에 맞춘 값)")]
    public float gapReductionPerWood = 0.01f; // 목재: 점사 내 발사 간격 (15강에서 0.2 -> 0.05)
    public float autoRangePerCopper = 0.25f;  // 구리: 자동공격 사거리
    public float speedPerOil = 0.2f;          // 기름: 투사체 속도 (기본 속도 4 -> 10강에서 +2, 15강에서 +3)

    // === 강화 단계별 특수 효과 ===
    // 각 강화 종류(자원)가 5단계/10단계에 도달하면 효과가 켜진다. 같은 종류 안에서는 도달한 가장 높은 단계 효과만
    // 적용된다(10단계가 되면 5단계 효과는 꺼지고 10단계 효과가 그 상위호환으로 대신한다). 다른 종류끼리는 같이 켜진다.
    // 효과는 쏜 탄(ProjectileModifiers)에 실려 나가며 직접 조작과 자동공격이 똑같이 동작한다. 단계는 이 SMG "한 자루"의 강화 레벨이다.
    [Header("특수효과: 목재(공속) - 무한 난사")]
    // 5단계부터 점사 사이 휴식이 사라져 끊김 없이 쏘고, 한 번 쏜 묶음이 조준 방향에서 이 각도 안에서 흩어진다.
    // 10단계는 발사 간격이 더 짧아지고(배율) 더 넓게 흩어지며 탄에 궤적이 남는다.
    public float scatterTier5 = 6f;
    public float scatterTier10 = 10f;
    public float gapScaleTier10 = 0.7f;
    public float tracerLength = 0.35f;

    [Header("특수효과: 철(공격력) - 다중 탄")]
    // 조준한 방향으로 곧게 나가는 가운데(큰) 탄 양옆으로 작은 탄들이 부채꼴로 같이 나간다 (가운데 탄은 그대로라 조준은 틀어지지 않는다).
    // 작은 탄은 피해가 낮다. 작은 탄도 다른 효과(파편, 폭발, 밀어내기)를 전부 받고 치명타도 각자 굴린다.
    public int smallBulletsTier5 = 2;
    public int smallBulletsTier10 = 4;
    public float smallDamageTier5 = 0.4f;
    public float smallDamageTier10 = 0.6f;
    public float smallBulletSize = 0.6f;
    public float smallBulletAngleStep = 8f; // 가운데 탄에서 양옆으로 이 각도씩 벌어진다 (2발: ±8°, 4발: ±8°, ±16°)

    [Header("특수효과: 구리(범위) - 파편탄")]
    // 적을 맞히면 그 자리에서 앞쪽으로 부채꼴 파편이 갈라져 나간다. 파편은 다시 갈라지지 않고 짧은 거리만 난다.
    public int fragmentsTier5 = 2;
    public int fragmentsTier10 = 3;
    public float fragmentDamageTier5 = 0.5f;
    public float fragmentDamageTier10 = 0.6f;
    public float fragmentAngleTier5 = 20f;
    public float fragmentAngleTier10 = 25f;
    public float fragmentRange = 3f;
    public float fragmentSize = 0.5f;

    [Header("특수효과: 화학물질(치명타) - 폭발탄")]
    // 치명타로 맞힌 곳에서 폭발해 주변 적에게 (맞힌 탄 피해 x 배율)의 피해. 10단계는 치명타가 더 크게 터지고,
    // 치명타가 아닌 일반 적중도 작게 터진다. 폭발 피해로는 다시 폭발/파편이 생기지 않는다.
    public float blastRadiusTier5 = 0.7f;
    public float blastDamageTier5 = 1f;
    public float blastRadiusTier10 = 1.1f;
    public float blastDamageTier10 = 1.5f;
    public float smallBlastRadius = 0.5f;
    public float smallBlastDamage = 0.4f;
    public Color blastColor = new Color(1f, 0.55f, 0.15f, 0.55f);

    [Header("특수효과: 기름(속도) - 충격탄")]
    // 맞은 적이 평소보다 이만큼 더 세게 밀려난다.
    public float knockbackTier5 = 2.5f;
    public float knockbackTier10 = 4f;

    // 10단계 효과는 10강에서 켜진다 (종류별 최대 강화 WeaponEnhanceUtil.MaxLevel과 따로 고정값으로 둔다).
    private const int TierLow = 5;
    private const int TierHigh = 10;

    // 탄이 많은 무기라 연출(폭발 원/선)이 너무 많이 떠 있으면 새 연출은 건너뛴다.
    private const int FxBudget = 16;

    private WeaponAim weaponAim;
    private SpriteRenderer weaponSprite;
    private bool isFiring; // 3점사 + 휴식 사이클이 진행 중이면 true (이 동안은 새 사이클을 시작하지 않는다)

    // === IEnhanceableWeapon ===
    private WeaponIdentity identity;
    private float projectileSpeedBonus; // 기름(발사속도) 강화 누적분. Fire()에서 Projectile.speed에 더해준다.

    // 강화로 바뀌는 스탯의 원본값. Awake에서 저장해두고, 런 종료(사망/추출)로 강화가 리셋되면
    // 여기로 되돌린 뒤 현재 레벨만큼 다시 적용한다 (포탈 이동은 무기 인스턴스가 유지되므로 직접 되돌려야 함).
    private float baseBurstInterval, baseDamage, baseCritChance, baseAutoAttackRange;

    // 쏠 때마다 새로 만들면 쓰레기가 많이 생기므로, 강화가 바뀔 때만 다시 만들어 두고 재사용한다.
    private ProjectileModifiers modsMain, modsSmall, modsFragment;
    private bool modsDirty = true;
    private System.Action<Projectile, Enemy, float, bool> hitHandler;

    private static readonly List<Collider2D> blastBuffer = new List<Collider2D>();

    // Awake 전에도(예: 에디터에서) 강화 레벨을 읽고 올릴 수 있게 필요할 때 찾아온다.
    private WeaponIdentity Identity => identity != null ? identity : (identity = GetComponent<WeaponIdentity>());

    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => WeaponEnhanceStore.GetLevel(Identity, type);

    public void ApplyEnhance(ResourceType type)
    {
        if (!WeaponEnhanceStore.TryEnhance(Identity, type)) return;
        ApplyStatDelta(type);
        modsDirty = true;
    }

    private void ApplyStatDelta(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: burstInterval = Mathf.Max(0.05f, burstInterval - gapReductionPerWood); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: autoAttackRange += autoRangePerCopper; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
            case ResourceType.Oil: projectileSpeedBonus += speedPerOil; break;
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
                return high ? $"더 빠르고 넓은 난사 (±{scatterTier10:0}°)" : "휴식 없이 계속 난사";
            case ResourceType.Iron:
                return $"작은 탄 {(high ? smallBulletsTier10 : smallBulletsTier5)}발 부채꼴 ({(high ? smallDamageTier10 : smallDamageTier5) * 100f:0}%)";
            case ResourceType.Copper:
                return $"맞히면 파편 {(high ? fragmentsTier10 : fragmentsTier5)}갈래";
            case ResourceType.Chemical:
                return high ? "치명타 대폭발, 평타도 폭발" : $"치명타 시 폭발 (반경 {blastRadiusTier5:0.#})";
            case ResourceType.Oil:
                return $"맞은 적 {(high ? knockbackTier10 : knockbackTier5):0.#}배 밀려남";
        }
        return null;
    }

    // 특수효과가 켜지는 단계를 구한다: 0(없음) / 5 / 10. 이 SMG 한 자루의 강화 레벨 기준.
    private int EffectTier(ResourceType type)
    {
        int level = GetEnhanceLevel(type);
        if (level >= TierHigh) return TierHigh;
        if (level >= TierLow) return TierLow;
        return 0;
    }

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
        weaponSprite = GetComponent<SpriteRenderer>();
        identity = GetComponent<WeaponIdentity>();

        baseBurstInterval = burstInterval;
        baseDamage = damage;
        baseCritChance = critChance;
        baseAutoAttackRange = autoAttackRange;

        ReapplyEnhancements();
    }

    // WeaponEnhanceStore의 현재 강화 레벨을 스탯에 반영한다. 먼저 원본값으로 되돌린 뒤
    // 레벨 수만큼 ApplyStatDelta를 다시 적용하므로, 강화가 리셋된 뒤 호출해도 정확히 맞는다.
    public void ReapplyEnhancements()
    {
        burstInterval = baseBurstInterval;
        damage = baseDamage;
        critChance = baseCritChance;
        autoAttackRange = baseAutoAttackRange;
        projectileSpeedBonus = 0f;
        modsDirty = true;

        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes)
        {
            int level = WeaponEnhanceStore.GetLevel(Identity, type);
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
        if (EnemyManager.PlayerDead || isFiring) return;

        if (weaponAim.isHeld)
        {
            // isPressed를 매 프레임 확인하기 때문에, 꾹 누르고 있으면 사이클이 끝나자마자 바로 다음 사이클이 시작된다.
            if (Mouse.current.leftButton.isPressed)
            {
                StartCoroutine(BurstRoutine(auto: false));
            }
        }
        else
        {
            // 자동 모드: 사거리 안에 적이 있을 때만 3점사 사이클을 시작한다.
            if (FindAutoDirection().HasValue)
            {
                StartCoroutine(BurstRoutine(auto: true));
            }
        }
    }

    // 목재 5단계 이상이면 점사 사이 휴식이 없다: 3발을 쏜 뒤에도 같은 간격만 기다리고 바로 다음 3발로 이어간다.
    private bool IsContinuousFire => EffectTier(ResourceType.Wood) >= TierLow;

    // 점사 안에서 한 발과 다음 발 사이 간격. 목재 10단계는 더 짧다.
    private float ShotGap
    {
        get
        {
            float gap = burstInterval;
            if (EffectTier(ResourceType.Wood) >= TierHigh) gap *= gapScaleTier10;
            return Mathf.Max(0.03f, gap);
        }
    }

    private IEnumerator BurstRoutine(bool auto)
    {
        isFiring = true;
        bool continuous = IsContinuousFire;

        do
        {
            for (int i = 0; i < burstCount; i++)
            {
                Vector2? direction = auto ? FindAutoDirection() : weaponAim.AimDirection;
                if (direction.HasValue)
                {
                    weaponAim.Kick(recoilKickAngle);
                    FireShot(direction.Value);
                }

                // 일반 점사는 마지막 발 이후에 간격 대신 아래의 휴식 시간을 기다린다. 난사 모드는 마지막 발 뒤에도 같은 간격만 기다린다.
                // 기믹 "한파" 중에는 WeaponTimeScale(<1)로 나눠서 대기 시간을 늘린다 = 점사/휴식이 느려진다.
                if (continuous || i < burstCount - 1)
                    yield return new WaitForSeconds(ShotGap / StageGimmickManager.WeaponTimeScale);
            }

            if (!continuous)
                yield return new WaitForSeconds(restDuration / StageGimmickManager.WeaponTimeScale);
        }
        while (continuous && KeepFiring(auto));

        isFiring = false;
    }

    // 난사 모드에서 다음 3발을 이어서 쏠지: 직접 조작이면 마우스를 계속 누르고 있어야 하고, 자동이면 사거리 안에 적이 있어야 한다.
    // 그 사이 무기를 들었다 내려놓아 모드가 바뀌었으면 여기서 끝내고, Update가 알맞은 모드로 다시 시작한다.
    private bool KeepFiring(bool auto)
    {
        if (EnemyManager.PlayerDead) return false;
        if (auto == weaponAim.isHeld) return false;
        if (auto) return FindAutoDirection().HasValue;
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    // 자동 모드에서 현재 가장 가까운 적을 향한 방향을 구한다. 사거리 안에 적이 없으면 null.
    private Vector2? FindAutoDirection()
    {
        Enemy target = EnemyManager.FindNearest(transform.position, autoAttackRange);
        if (target == null) return null;
        return (Vector2)target.transform.position - (Vector2)transform.position;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // 한 번 쏠 때 나가는 탄: 가운데(큰) 탄 하나 + 철 단계에 따른 작은 탄들. 흩어짐(목재)은 묶음 전체에 한 번만 적용돼서
    // 가운데 탄과 작은 탄들이 나란한 채로 같이 흩어진다. 가운데 탄을 돌려준다.
    public Projectile FireShot(Vector2 aimDirection)
    {
        if (projectilePrefab == null) return null;
        if (modsDirty) RebuildModifiers();

        Vector2 dir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;

        int woodTier = EffectTier(ResourceType.Wood);
        float scatter = woodTier >= TierHigh ? scatterTier10 : woodTier >= TierLow ? scatterTier5 : 0f;
        if (scatter > 0f) dir = Rotate(dir, Random.Range(-scatter, scatter));

        Vector2 origin = transform.position;
        Projectile main = SpawnBullet(origin, dir, damage, modsMain);

        // 작은 탄은 가운데 탄 양옆으로 부채꼴로 퍼져 나간다 (가운데 탄은 조준 방향 그대로라서 조준은 틀어지지 않는다).
        int small = SmallBulletCount(out float smallScale);
        for (int i = 0; i < small; i++)
        {
            float sign = i % 2 == 0 ? -1f : 1f;
            float angle = sign * smallBulletAngleStep * (i / 2 + 1);
            SpawnBullet(origin, Rotate(dir, angle), damage * smallScale, modsSmall);
        }

        // 10단계 난사는 총구에서도 번쩍인다.
        if (woodTier >= TierHigh && FxAvailable())
            CircleFlash.Spawn(origin + dir * 0.2f, 0.14f, new Color(1f, 0.9f, 0.5f, 0.6f), 0.08f, weaponSprite);

        return main;
    }

    private int SmallBulletCount(out float damageScale)
    {
        int tier = EffectTier(ResourceType.Iron);
        if (tier >= TierHigh) { damageScale = smallDamageTier10; return smallBulletsTier10; }
        if (tier >= TierLow) { damageScale = smallDamageTier5; return smallBulletsTier5; }
        damageScale = 0f;
        return 0;
    }

    // 탄 하나를 만들어 날린다. baseDamage에서 치명타를 따로 굴린다 (탄마다 독립).
    private Projectile SpawnBullet(Vector2 position, Vector2 dir, float baseDamage, ProjectileModifiers mods)
    {
        float finalDamage = CriticalHit.Roll(baseDamage, critChance, critMultiplier, out bool isCrit);

        GameObject projGO = Instantiate(projectilePrefab, position, Quaternion.identity);
        Projectile projectile = projGO.GetComponent<Projectile>();
        if (projectile == null) return null;

        projectile.maxHits = 1; // Smg 투사체는 관통하지 않고 첫 번째로 맞은 적에게서 사라진다.
        projectile.speed += projectileSpeedBonus;
        projectile.ApplyModifiers(mods);
        projectile.Fire(dir, finalDamage, isCrit);
        return projectile;
    }

    // 지금 강화 단계에 맞는 탄 효과 묶음을 (가운데 탄 / 작은 탄 / 파편) 세 종류로 만들어 둔다. 켜진 효과가 없으면 null (평범한 탄).
    private void RebuildModifiers()
    {
        modsDirty = false;

        bool needHandler = EffectTier(ResourceType.Copper) >= TierLow || EffectTier(ResourceType.Chemical) >= TierLow;
        if (needHandler && hitHandler == null) hitHandler = OnBulletHit;

        modsMain = MakeModifiers(1f, false, needHandler);
        modsSmall = MakeModifiers(smallBulletSize, false, needHandler);
        modsFragment = MakeModifiers(fragmentSize, true, needHandler);
    }

    private ProjectileModifiers MakeModifiers(float size, bool fragment, bool needHandler)
    {
        ProjectileModifiers m = new ProjectileModifiers();
        m.isFragment = fragment;
        m.sizeMultiplier = size;
        if (needHandler) m.onHit = hitHandler;

        int oil = EffectTier(ResourceType.Oil);
        if (oil >= TierLow) m.knockbackMultiplier = oil >= TierHigh ? knockbackTier10 : knockbackTier5;

        if (fragment)
        {
            m.maxTravel = fragmentRange; // 파편은 짧은 거리만 난다
        }
        else if (EffectTier(ResourceType.Wood) >= TierHigh)
        {
            m.tracerLength = tracerLength; // 10단계 난사: 궤적
            m.tailColor = new Color(1f, 0.9f, 0.5f, 0.5f);
        }

        return m.HasAnyEffect ? m : null;
    }

    private static bool FxAvailable()
    {
        return CircleFlash.ActiveCount + BeamFlash.ActiveCount < FxBudget;
    }

    // 탄이 적을 맞힌 직후 Projectile이 부른다: 구리(파편탄) + 화학물질(폭발탄).
    private void OnBulletHit(Projectile bullet, Enemy victim, float dealt, bool crit)
    {
        Vector2 pos = bullet.transform.position;
        bool fragment = bullet.Modifiers != null && bullet.Modifiers.isFragment;

        if (!fragment) SpawnFragments(bullet, victim, pos, dealt, crit); // 파편은 다시 갈라지지 않는다
        Blast(pos, dealt, crit);
    }

    // 구리: 맞힌 자리에서 앞쪽 부채꼴로 파편이 갈라져 나간다. 파편은 맞힌 적을 다시 때리지 않고, 각자 치명타를 굴린다.
    private void SpawnFragments(Projectile bullet, Enemy victim, Vector2 pos, float dealt, bool crit)
    {
        int tier = EffectTier(ResourceType.Copper);
        if (tier < TierLow) return;

        bool high = tier >= TierHigh;
        int count = high ? fragmentsTier10 : fragmentsTier5;
        float ratio = high ? fragmentDamageTier10 : fragmentDamageTier5;
        float angle = high ? fragmentAngleTier10 : fragmentAngleTier5;
        if (count <= 0) return;

        float baseDamage = crit ? dealt / critMultiplier : dealt; // 치명타 배율을 걷어낸 원래 피해
        Vector2 dir = bullet.Direction;

        for (int i = 0; i < count; i++)
        {
            float offset = count == 1 ? 0f : Mathf.Lerp(-angle, angle, i / (count - 1f)); // 2갈래: ±각도, 3갈래: -각도/0/+각도
            Vector2 fragmentDir = Rotate(dir, offset);

            Projectile fragment = SpawnBullet(pos, fragmentDir, baseDamage * ratio, modsFragment);
            if (fragment != null) fragment.IgnoreEnemy(victim);

            if (FxAvailable())
                BeamFlash.Spawn(pos, pos + fragmentDir * 0.4f, new Color(1f, 0.95f, 0.7f, 0.8f), high ? 0.06f : 0.04f, 0.15f, weaponSprite);
        }
    }

    // 화학물질: 치명타 탄은 맞힌 곳에서 폭발한다 (10단계는 더 크게). 10단계는 치명타가 아닌 적중도 작게 터진다.
    // 폭발은 반경 안 모든 적(맞은 적 포함)에게 피해를 주고, 폭발 피해로는 파편/폭발이 다시 생기지 않는다.
    private void Blast(Vector2 center, float dealt, bool crit)
    {
        int tier = EffectTier(ResourceType.Chemical);
        if (tier < TierLow) return;

        bool high = tier >= TierHigh;
        float radius, multiplier;
        bool big;
        if (crit)
        {
            radius = high ? blastRadiusTier10 : blastRadiusTier5;
            multiplier = high ? blastDamageTier10 : blastDamageTier5;
            big = true;
        }
        else if (high)
        {
            radius = smallBlastRadius;
            multiplier = smallBlastDamage;
            big = false;
        }
        else
        {
            return;
        }

        float blastDamage = dealt * multiplier;

        blastBuffer.Clear();
        Physics2D.OverlapCircle(center, radius + 0.5f, ContactFilter2D.noFilter, blastBuffer); // 적 충돌 원이 크므로 후보는 넉넉하게
        foreach (Collider2D c in blastBuffer)
        {
            Enemy e = c.GetComponent<Enemy>();
            if (e == null || e.IsDying) continue;

            // 몸통 원이 폭발 원에 닿은 적만 맞는다 (눈에 보이는 폭발 범위와 맞추려고).
            if (e.TryGetBodyCircle(out Vector2 bodyCenter, out float bodyRadius))
            {
                if (Vector2.Distance(center, bodyCenter) > radius + bodyRadius) continue;
            }
            else if (Vector2.Distance(center, c.ClosestPoint(center)) > radius) continue;

            Vector2 away = (Vector2)e.transform.position - center;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
            e.Hit(away.normalized, blastDamage, crit && big, 0.5f);
        }

        if (FxAvailable())
            CircleFlash.Spawn(center, radius, blastColor, big ? 0.22f : 0.12f, weaponSprite);
    }
}
