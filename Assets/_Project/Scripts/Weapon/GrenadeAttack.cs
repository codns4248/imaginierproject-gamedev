using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Grenade 전용 공격 스크립트. 들고 있는 방식은 Pistol과 같지만(WeaponAim이 제자리에서 회전만 시킴),
// 공격은 투사체를 쏘는 대신 목표 지점까지 무기와 같은 모양의 투사체를 "던지는" 방식이다.
// 들고 있을 때는 마우스 위치로, 들고 있지 않을 때(자동공격)는 가장 가까운 적 위치로 던진다.
// 어느 쪽이든 플레이어로부터 maxRange를 넘어서는 위치는 그 방향으로 maxRange 거리까지만 날아간다.
//
// 강화(IEnhanceableWeapon): 목재=공격 간격, 철=공격력, 구리=폭발 범위, 화학물질=치명타 확률,
// 기름=발사속도 (투사체가 날아가는 속도 + 던질 수 있는 사거리 + 착탄 후 터질 때까지의 딜레이).
// 기본 상태는 느리게 날아가고 한참 뒤에 터지며, 기름을 올릴수록 빠르고 멀리 나가고 빨리 터진다.
//
// 강화 특수효과: 바운스탄(목재), 연쇄폭발(철), 중력탄(구리), 독구름(화학물질), 화염(기름).
// 한 발의 정보는 GrenadeShot에 실려 투사체 -> 경고 범위로 넘어가고, 터지는 순간 Detonate로 돌아온다.
// 연쇄폭발로 튀어나온 작은 수류탄도 중력/독/화상 효과를 전부 받고, 튕기는 것(바운스)만 원본 전용이다.
[RequireComponent(typeof(WeaponAim))]
public class GrenadeAttack : MonoBehaviour, IEnhanceableWeapon
{
    public float attackInterval = 1f; // 연속 투척 사이의 최소 간격(초)
    public float maxRange = 3f;       // 플레이어 기준 최대 투척 거리. 자동공격 탐지 반경도 이 값을 그대로 쓴다.

    [Header("공격력 / 투사체")]
    public float damage = 1f;
    public GameObject grenadeProjectilePrefab;

    [Header("치명타")]
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    [Header("투척 / 폭발 기본값 (프리팹 값 대신 여기서 정해서 투사체에 넘긴다)")]
    public float throwSpeed = 3f;       // 투사체가 날아가는 속도(유닛/초). 거리 / 이 값 = 비행 시간
    public float explosionDelay = 1f;   // 목표 지점에 도착한 뒤 터지기까지의 시간(초). 이 동안 붉은 경고 범위가 보인다.
    public float explosionRadius = 0.75f; // 폭발 반지름

    [Header("강화 1레벨당 변화량 (전체 축소 0.5배에 맞춘 값)")]
    public float intervalReductionPerWood = 0.05f; // 목재: 투척 간격 (10강에서 1 -> 0.5, 하한 0.3)
    public float radiusPerCopper = 0.075f;         // 구리: 폭발 반지름 (10강에서 0.75 -> 1.5, 기본의 2배)
    public float speedPerOil = 0.6f;               // 기름: 투사체 속도 (10강에서 3 -> 9)
    public float delayReductionPerOil = 0.07f;     // 기름: 착탄 후 터지기까지의 딜레이 (10강에서 1 -> 0.3, 하한 0.2)
    public float rangePerOil = 0.15f;              // 기름: 최대 투척 거리 (10강에서 3 -> 4.5)

    // === 강화 단계별 특수 효과 ===
    // 각 강화 종류가 5단계/10단계에 도달하면 효과가 켜진다. 같은 종류 안에서는 도달한 가장 높은 단계 효과만 적용되고,
    // 다른 종류끼리는 같이 켜진다. 직접 조작과 자동공격이 똑같이 동작한다. 단계는 이 수류탄 "한 자루"의 강화 레벨이다.
    [Header("특수효과 공통")]
    // 바운스/작은 수류탄은 원본 딜레이 대신 이 시간 뒤에 터진다 (기름 강화로 원본 딜레이가 더 짧아지면 그쪽을 따른다).
    public float followUpDelay = 0.3f;
    // 바운스/작은 수류탄이 포물선으로 통통 튀어 오르는 높이 (더 작은 수류탄은 clusterSpriteScale만큼 더 낮게 뜬다).
    public float bounceArcHeight = 0.5f;
    public float clusterArcHeight = 0.4f;

    [Header("특수효과: 목재(공속) - 바운스탄")]
    // 원본이 터진 뒤 던진 방향으로 튕겨 나가 한 번 더 터진다 (5단계 1번, 10단계 2번). 튕긴 폭발은 피해가 조금 약하다.
    public float bounceDamageTier5 = 0.7f;
    public float bounceDamageTier10 = 0.8f;
    public float bounceHopScale = 1.6f; // 튕기는 거리 = 폭발 반지름 x 이 값

    [Header("특수효과: 철(공격력) - 연쇄폭발")]
    // 원본(튕긴 것 포함)이 터지면 상하좌우로 작은 수류탄 4개가 튀어나와 또 터진다. 10단계는 작은 수류탄이 터질 때
    // 같은 방향으로 더 작은 수류탄이 1개 더 나가서, 십자 모양이 바깥으로 점점 퍼진다. 단계가 내려갈수록 피해/범위가 줄어든다.
    public float clusterDamageScale = 0.5f; // 작은 수류탄 피해 = 부모 폭발 피해 x 이 값
    public float clusterRadiusScale = 0.6f; // 작은 수류탄 반지름 = 부모 반지름 x 이 값
    public float clusterHopScale = 1.5f;    // 튀어나가는 거리 = 부모 반지름 x 이 값
    public float clusterSpeed = 4f;
    public float clusterSpriteScale = 0.6f; // 한 단계 작아질 때마다 투사체 그림 크기 배율

    [Header("특수효과: 구리(범위) - 중력탄")]
    // 착탄 후 터질 때까지 (폭발 반지름 + 여유 거리) 안의 적을 가운데로 끌어당긴다. 정예/보스는 끌려오지 않는다.
    // 구리 강화가 폭발 반지름 자체도 키우므로 배율이 아니라 더하기로 넓힌다 (작은 수류탄은 크기 비율만큼 여유도 줄어든다).
    // 10단계는 더 넓고 세게 끌어당기고, 폭발 피해도 커진다.
    public float pullExtraRangeTier5 = 0.35f;
    public float pullExtraRangeTier10 = 0.6f;
    public float pullSpeedTier5 = 2.5f;
    public float pullSpeedTier10 = 3.5f;
    public float gravityBlastDamageTier10 = 1.5f;

    [Header("특수효과: 화학물질(치명타) - 독구름")]
    // 폭발 자리에 폭발 범위만 한 독구름이 남아 초당 (그 폭발 피해 x 비율)의 피해를 준다. 겹친 구름은 각자 피해를 준다.
    // 10단계는 더 오래 남고, 구름 안에서 죽은 적 자리에 작은 독구름이 생긴다 (작은 구름은 다시 퍼지지 않는다).
    public float poisonDamagePerSecond = 0.4f;
    public float poisonDurationTier5 = 3f;
    public float poisonDurationTier10 = 6f;
    public float deathCloudRadiusScale = 0.6f;
    public float deathCloudDuration = 3f;

    [Header("특수효과: 기름(속도) - 화염")]
    // 폭발에 맞은 적이 불탄다. 맞을 때마다 1중첩(최대 burnMaxStacks), 중첩당 초당 (공격력 x 비율)의 피해.
    // 10단계는 더 오래, 2배로 타고, 불탄 적 바로 옆 적에게 불이 옮겨 붙는다 (옮겨 붙은 불은 다시 옮기지 못함).
    public float burnDamagePerStack = 0.15f;
    public float burnDamageScaleTier10 = 2f;
    public float burnDurationTier5 = 3f;
    public float burnDurationTier10 = 5f;
    public int burnMaxStacks = 5;
    public float burnSpreadRadius = 0.35f;

    // 10단계 효과는 10강에서 켜진다 (종류별 최대 강화 WeaponEnhanceUtil.MaxLevel과 따로 고정값으로 둔다).
    private const int TierLow = 5;
    private const int TierHigh = 10;

    private static readonly Vector2[] CrossDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
    private static readonly List<Collider2D> hitBuffer = new List<Collider2D>();
    private static ContactFilter2D triggerFilter = new ContactFilter2D { useTriggers = true };

    private WeaponAim weaponAim;
    private Camera mainCamera;
    private float attackCooldown;

    // === IEnhanceableWeapon ===
    private WeaponIdentity identity;

    // 강화로 바뀌는 스탯의 원본값. Awake에서 저장해두고, 런 종료(사망/추출)로 강화가 리셋되면
    // 여기로 되돌린 뒤 현재 레벨만큼 다시 적용한다 (포탈 이동은 무기 인스턴스가 유지되므로 직접 되돌려야 함).
    private float baseAttackInterval, baseDamage, baseCritChance, baseExplosionRadius;
    private float baseThrowSpeed, baseExplosionDelay, baseMaxRange;

    // Awake 전에도(예: 에디터에서) 강화 레벨을 읽고 올릴 수 있게 필요할 때 찾아온다.
    private WeaponIdentity Identity => identity != null ? identity : (identity = GetComponent<WeaponIdentity>());

    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => WeaponEnhanceStore.GetLevel(Identity, type);

    public void ApplyEnhance(ResourceType type)
    {
        if (!WeaponEnhanceStore.TryEnhance(Identity, type)) return;
        ApplyStatDelta(type);
    }

    private void ApplyStatDelta(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: attackInterval = Mathf.Max(0.3f, attackInterval - intervalReductionPerWood); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: explosionRadius += radiusPerCopper; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
            case ResourceType.Oil:
                throwSpeed += speedPerOil;
                explosionDelay = Mathf.Max(0.2f, explosionDelay - delayReductionPerOil);
                maxRange += rangePerOil;
                break;
        }
    }

    // 강화 UI에 보여줄 특수효과 설명. 강화창(너비 534px, 글자 16px)은 "현재 효과 → 다음 단계 효과"를 한 줄에 같이 보여주므로
    // 한글 기준 효과 하나당 12~14자 안쪽으로, 읽자마자 뭔지 알 수 있는 말로 쓴다.
    public string GetMilestoneEffect(ResourceType type, int tier)
    {
        bool high = tier == TierHigh;
        if (tier != TierLow && !high) return null;
        switch (type)
        {
            case ResourceType.Wood:
                return $"튕기며 {(high ? 2 : 1)}번 더 폭발";
            case ResourceType.Iron:
                return high ? "더 작은 수류탄 연쇄 폭발" : "십자로 작은 수류탄 4개";
            case ResourceType.Copper:
                return high ? "넓게 끌어모아 대폭발" : "폭발 전 적을 끌어모음";
            case ResourceType.Chemical:
                return high ? $"독구름 {poisonDurationTier10:0}초, 죽으면 확산" : $"독구름 {poisonDurationTier5:0}초 남김";
            case ResourceType.Oil:
                return high ? "화상 2배, 옆 적에 옮겨붙음" : $"맞은 적 {burnDurationTier5:0}초 화상 (중첩)";
        }
        return null;
    }

    // 특수효과가 켜지는 단계를 구한다: 0(없음) / 5 / 10. 이 수류탄 한 자루의 강화 레벨 기준.
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
        identity = GetComponent<WeaponIdentity>();

        baseAttackInterval = attackInterval;
        baseDamage = damage;
        baseCritChance = critChance;
        baseExplosionRadius = explosionRadius;
        baseThrowSpeed = throwSpeed;
        baseExplosionDelay = explosionDelay;
        baseMaxRange = maxRange;

        ReapplyEnhancements();
    }

    // WeaponEnhanceStore의 현재 강화 레벨을 스탯에 반영한다. 먼저 원본값으로 되돌린 뒤
    // 레벨 수만큼 ApplyStatDelta를 다시 적용하므로, 강화가 리셋된 뒤 호출해도 정확히 맞는다.
    public void ReapplyEnhancements()
    {
        attackInterval = baseAttackInterval;
        damage = baseDamage;
        critChance = baseCritChance;
        explosionRadius = baseExplosionRadius;
        throwSpeed = baseThrowSpeed;
        explosionDelay = baseExplosionDelay;
        maxRange = baseMaxRange;

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

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (EnemyManager.PlayerDead) return;
        if (PauseManager.IsPaused) return; // 멈춘 동안(레벨업 카드 클릭 등) 클릭으로 던지지 않게

        attackCooldown -= Time.deltaTime * PlayerRunStats.WeaponTimeScale;
        if (attackCooldown > 0f) return;

        if (weaponAim.isHeld)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                attackCooldown = attackInterval;
                ThrowAtMouse();
            }
        }
        else
        {
            Enemy target = EnemyManager.FindNearest(transform.position, maxRange);
            if (target != null)
            {
                attackCooldown = attackInterval;
                ThrowAt(target.transform.position);
            }
        }
    }

    private void ThrowAtMouse()
    {
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(screenPos);
        ThrowAt(mouseWorldPos);
    }

    // rawTarget 방향으로 던지되, 플레이어 기준 maxRange를 넘으면 그 방향으로 maxRange까지만 날아가게 클램프한다.
    private void ThrowAt(Vector2 rawTarget)
    {
        if (grenadeProjectilePrefab == null || weaponAim.Pivot == null) return;

        Vector2 origin = weaponAim.Pivot.position;
        Vector2 toTarget = rawTarget - origin;

        Vector2 targetPos = toTarget.magnitude > maxRange
            ? origin + toTarget.normalized * maxRange
            : rawTarget;

        int woodTier = EffectTier(ResourceType.Wood);
        GrenadeShot shot = new GrenadeShot
        {
            owner = this,
            damage = damage,
            radius = explosionRadius * PlayerRunStats.RangeMultiplier, // 레벨업 공격 범위 (작은 수류탄/중력/독구름도 이 반경 기준)
            delay = explosionDelay,
            generation = 0,
            bouncesLeft = woodTier == TierHigh ? 2 : woodTier == TierLow ? 1 : 0,
            direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right,
        };
        LaunchShot(transform.position, targetPos, shot, throwSpeed);
    }

    // arcHeight > 0이면 포물선으로 통통 튀어 날아간다 (바운스/연쇄폭발). 원본 투척은 0.
    private void LaunchShot(Vector2 from, Vector2 to, GrenadeShot shot, float speed, float arcHeight = 0f)
    {
        if (grenadeProjectilePrefab == null) return;

        GameObject projGO = Instantiate(grenadeProjectilePrefab, from, Quaternion.identity);
        if (shot.generation > 0) projGO.transform.localScale *= Mathf.Pow(clusterSpriteScale, shot.generation);

        GrenadeProjectile projectile = projGO.GetComponent<GrenadeProjectile>();
        if (projectile != null) projectile.Launch(from, to, shot, speed * PlayerRunStats.ProjectileSpeedMultiplier, arcHeight);
        else Destroy(projGO);
    }

    // 바운스/작은 수류탄이 착탄 후 터지기까지의 시간.
    private float FollowUpDelay => Mathf.Min(followUpDelay, explosionDelay);

    // === 중력탄 (ExplosionZone이 경고 시간 동안 부른다) ===

    // 이 발이 끌어당기는 반경. 중력탄이 꺼져 있으면 0.
    public float PullRadiusFor(GrenadeShot shot)
    {
        int tier = EffectTier(ResourceType.Copper);
        if (tier == 0) return 0f;
        float extra = tier == TierHigh ? pullExtraRangeTier10 : pullExtraRangeTier5;
        return shot.radius + extra * Mathf.Pow(clusterRadiusScale, shot.generation);
    }

    // center 쪽으로 반경 안의 적을 끌어당긴다. 가운데에 가까울수록 느려져서 한가운데에서 떨지 않게 한다.
    public void PullEnemies(Vector2 center, float pullRadius)
    {
        float speed = EffectTier(ResourceType.Copper) == TierHigh ? pullSpeedTier10 : pullSpeedTier5;

        int count = Physics2D.OverlapCircle(center, pullRadius, triggerFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Enemy enemy = hitBuffer[i] != null ? hitBuffer[i].GetComponent<Enemy>() : null;
            if (enemy == null || enemy.IsDying || enemy.IsEliteOrBoss) continue;

            Vector2 toCenter = center - (Vector2)enemy.transform.position;
            float dist = toCenter.magnitude;
            if (dist < 0.05f) continue;
            enemy.Pull(toCenter / dist * Mathf.Min(speed, dist * 8f));
        }
    }

    // === 폭발 (ExplosionZone이 터지는 순간 부른다) ===
    public void Detonate(Vector2 position, GrenadeShot shot)
    {
        int ironTier = EffectTier(ResourceType.Iron);
        int copperTier = EffectTier(ResourceType.Copper);
        int chemTier = EffectTier(ResourceType.Chemical);
        int oilTier = EffectTier(ResourceType.Oil);

        float blastDamage = shot.damage * (copperTier == TierHigh ? gravityBlastDamageTier10 : 1f);
        float dealt = CriticalHit.Roll(blastDamage, critChance, critMultiplier, out bool isCrit);

        // 1) 폭발 피해 (+ 화상)
        int count = Physics2D.OverlapCircle(position, shot.radius, triggerFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Enemy enemy = hitBuffer[i] != null ? hitBuffer[i].GetComponent<Enemy>() : null;
            if (enemy == null || enemy.IsDying) continue;

            Vector2 knockDir = (Vector2)enemy.transform.position - position;
            if (knockDir.sqrMagnitude < 0.0001f) knockDir = Vector2.up;
            enemy.Hit(knockDir.normalized, dealt, isCrit);

            if (oilTier > 0)
            {
                bool high = oilTier == TierHigh;
                GrenadeBurn.Ignite(enemy,
                    damage * burnDamagePerStack * (high ? burnDamageScaleTier10 : 1f),
                    high ? burnDurationTier10 : burnDurationTier5,
                    burnMaxStacks, high, burnSpreadRadius);
            }
        }

        // 2) 독구름 (폭발 피해를 준 다음에 만들어서, 이 폭발로 죽은 적은 작은 독구름을 만들지 않는다)
        if (chemTier > 0)
        {
            bool high = chemTier == TierHigh;
            PoisonCloud.Spawn(position, shot.radius, shot.damage * poisonDamagePerSecond,
                high ? poisonDurationTier10 : poisonDurationTier5,
                high, deathCloudRadiusScale, deathCloudDuration);
        }

        // 3) 연쇄폭발: 원본은 상하좌우로 작은 수류탄 4개, 10단계면 작은 수류탄이 같은 방향으로 더 작은 수류탄 1개
        if (ironTier > 0)
        {
            if (shot.generation == 0)
            {
                foreach (Vector2 dir in CrossDirections) LaunchChild(position, shot, dir);
            }
            else if (shot.generation == 1 && ironTier == TierHigh)
            {
                LaunchChild(position, shot, shot.direction);
            }
        }

        // 4) 바운스: 원본만 던진 방향으로 튕겨서 한 번 더 터진다
        if (shot.generation == 0 && shot.bouncesLeft > 0)
        {
            float ratio = EffectTier(ResourceType.Wood) == TierHigh ? bounceDamageTier10 : bounceDamageTier5;
            GrenadeShot next = new GrenadeShot
            {
                owner = this,
                damage = shot.isBounce ? shot.damage : shot.damage * ratio,
                radius = shot.radius,
                delay = FollowUpDelay,
                generation = 0,
                bouncesLeft = shot.bouncesLeft - 1,
                isBounce = true,
                direction = shot.direction,
            };
            LaunchShot(position, position + shot.direction * (shot.radius * bounceHopScale), next, throwSpeed, bounceArcHeight);
        }
    }

    private void LaunchChild(Vector2 position, GrenadeShot parent, Vector2 dir)
    {
        GrenadeShot child = new GrenadeShot
        {
            owner = this,
            damage = parent.damage * clusterDamageScale,
            radius = parent.radius * clusterRadiusScale,
            delay = FollowUpDelay,
            generation = parent.generation + 1,
            direction = dir,
        };
        float arc = clusterArcHeight * Mathf.Pow(clusterSpriteScale, child.generation - 1);
        LaunchShot(position, position + dir * (parent.radius * clusterHopScale), child, clusterSpeed, arc);
    }
}
