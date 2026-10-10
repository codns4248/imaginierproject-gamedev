using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Sword 전용 공격 스크립트. Pistol의 반동(살짝 튀었다 돌아오는 것)과 달리, 목표 방향을 중심으로
// 위에서 아래로(시계 방향) 크게 한 번 휘두르는 모션을 직접 계산해서 재생한다.
// 휘두르는 동안에는 WeaponAim의 자동 조준/회전을 잠깐 꺼두고(Pivot, orbitRadius 등
// WeaponAim의 값은 그대로 읽어 쓰면서) 이 스크립트가 위치/회전을 직접 제어한다.
//
// 들고 있을 때는 마우스 방향으로 좌클릭 시 휘두르고(수동), 들고 있지 않을 때는
// MeleeAutoAttackQueue가 차례가 되면 TriggerAutoAttack()을 호출해서 가장 가까운 적 방향으로
// 같은 휘두르기 모션을 재생한다(자동). IAutoMeleeWeapon으로 큐에 자신을 등록한다.
[RequireComponent(typeof(WeaponAim))]
public class SwordAttack : MonoBehaviour, IAutoMeleeWeapon, IEnhanceableWeapon
{
    // 연속 공격 사이의 최소 간격(초).
    public float attackInterval = 0.45f;

    [Header("휘두르기 모션")]
    // 휘두르는 전체 각도. 공격을 시작하는 순간의 목표 방향이 이 범위의 정중앙이 된다.
    public float swingAngle = 120f;

    // 위(swingAngle/2 만큼 위)에서 아래(swingAngle/2 만큼 아래)까지 휘두르는 데 걸리는 시간(초).
    public float swingDuration = 0.225f;

    [Header("공격력 / 판정 범위")]
    // Sword의 공격력. Pistol과 마찬가지로 나중에 강화 요소가 생기면 이 값만 바꾸면 되게 해뒀다.
    public float damage = 1f;

    // 칼의 현재 위치를 중심으로 한 판정 반지름 (칼날 크기에 맞춰 설정).
    public float hitRadius = 1f;

    // 구리(공격 범위) 강화로 판정이 커진 만큼 칼 그림을 얼마나 따라 키울지. 1이면 판정과 같은 배율(10단계에서 3배),
    // 0.5면 증가분의 절반만(10단계에서 2배), 0이면 그림은 그대로.
    public float rangeVisualSizeFollow = 1f;

    [Header("치명타")]
    // 이번 스윙이 치명타인지는 스윙 시작 시점에 한 번만 판정해서, 스윙 도중 맞는 모든 적에게 동일하게 적용한다.
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    // === 강화 단계별 특수 효과 ===
    // 각 강화 종류(자원)가 5단계/10단계에 도달하면 효과가 켜진다. 같은 종류 안에서는 도달한 가장 높은 단계 효과만
    // 적용된다(10단계가 되면 5단계 효과는 꺼지고 10단계 효과가 그 상위호환으로 대신한다). 다른 종류끼리는 같이 켜진다.
    // 스윙 단위 이벤트(몇 번째 스윙 / 치명타 / 처치)에만 걸어서 직접 조작과 자동공격이 똑같이 동작한다.
    [Header("특수효과: 철(공격력) - 강타")]
    public int heavySwingEvery = 3;                 // 5단계: 이 횟수마다 한 번씩 강타
    public float heavySwingDamageMultiplier = 2f;   // 강타 데미지 배율
    public float heavySwingKnockbackMultiplier = 2f; // 강타 넉백 배율
    public float ironTier10DamageBonus = 0.4f;      // 10단계: 모든 스윙 데미지 +40% (강타는 그대로 유지)
    public Color heavySwingColor = new Color(1f, 0.4f, 0.35f); // 강타 스윙 동안 검에 입히는 색

    [Header("특수효과: 화학물질(치명타) - 충격(기절)")]
    public float stunDurationTier5 = 0.5f;          // 5단계: 치명타 스윙에 맞은 적 기절 시간
    public float stunDurationTier10 = 1f;           // 10단계: 기절 시간
    public float stunDamageTakenTier10 = 1.5f;      // 10단계: 기절한 적이 받는 피해 배율

    [Header("특수효과: 기름(속도) - 질주")]
    public float sprintMoveBonusTier5 = 0.25f;      // 5단계: 적 처치 시 이동속도 +25%
    public float sprintDurationTier5 = 2f;
    public float sprintMoveBonusTier10 = 0.4f;      // 10단계: +40%
    public float sprintDurationTier10 = 3f;
    public float sprintCooldownReductionTier10 = 0.2f; // 10단계: 질주 중 스윙 쿨다운 -20%

    [Header("특수효과: 나무(공속) - 회전 베기 / 상시 회전")]
    public float spinSwingAngle = 360f;       // 5단계: 스윙이 부채꼴(120도) 대신 한 바퀴(360도)로 바뀐다
    public float spinDurationRatio = 0.9f;    // 한 바퀴 도는 시간 = 공격 간격 x 이 비율 (나무 강화가 계속 의미를 갖도록 간격에 묶는다)
    public float spinStartOffset = 60f;       // 360도 스윙은 목표 방향에서 이만큼 위쪽에서 출발해 시계방향으로 한 바퀴 돈다
    public float autoOrbitDamageMultiplier = 0.5f;    // 10단계 상시 회전: 안 들고 있을 때(자동)의 데미지 배율
    public float autoOrbitKnockbackMultiplier = 0.5f; // 같은 조건의 넉백 배율
    public float orbitSpreadEaseSpeed = 360f; // 여러 자루가 동시에 돌 때 서로 간격을 벌리는 속도(도/초)

    // 구리(범위) 검기: 칼날이 정해진 방향을 지나가는 순간 그 방향으로 검기를 날린다. 방향 규칙:
    //   부채꼴 스윙 (직접/자동 공통)  -> 그 공격의 방향 (직접=마우스 방향, 자동=가까운 적 방향)
    //   360도 회전/상시 회전, 직접 조작 -> 마우스 방향
    //   360도 회전/상시 회전, 자동      -> 무작위 방향
    [Header("특수효과: 구리(범위) - 검기")]
    public int waveEvery = 4;                   // 5단계: 이 횟수째 스윙마다 검기 한 번 (10단계는 매 스윙, 이 횟수째는 3갈래)
    public float waveDamageMultiplier = 0.6f;   // 검기 데미지 = 그 스윙 데미지(치명타/강타/자동 회전 감소 반영)의 이 배율
    public float waveSpeed = 7.5f;              // 플레이어 투사체(Projectile.prefab speed 5)의 1.5배
    public float waveRadius = 0.2f;             // 임시 파란 원 반지름 (크기는 눈으로 보고 조절)
    public float waveRange = 4f;                // 최대 사거리
    public float waveKnockbackMultiplier = 0.5f;
    public float waveFanAngle = 20f;            // 10단계 3갈래: 가운데 검기에서 양옆으로 벌어지는 각도
    public Color waveColor = new Color(0.3f, 0.6f, 1f, 0.85f);

    private const int TierLow = 5;
    private int TierHigh => WeaponEnhanceUtil.MaxLevel;

    // 스윙 방식: 기본 부채꼴 / 나무 5단계 360도 회전 베기 / 나무 10단계 상시 회전.
    private enum SwingMode { Arc, Spin360, Orbit }
    private SwingMode mode;
    private float activeSwingAngle;     // 이번 스윙이 훑는 총 각도 (부채꼴/360도 스윙)
    private float activeSwingDuration;  // 이번 스윙에 걸리는 시간
    private float swingStartOffset;     // 목표 방향에서 얼마나 위쪽에서 출발하는지

    private WeaponAim weaponAim;
    private SpriteRenderer spriteRenderer;

    private int swingCount;                  // 이 검이 지금까지 휘두른 횟수 (강타 주기 계산용)
    private float swingKnockbackMultiplier = 1f;
    private bool isHeavySwing;

    // 다음 공격까지 남은 쿨다운 시간(초).
    private float attackCooldown;

    private bool isSwinging;
    private float swingElapsed;
    private float swingCenterAngle; // 스윙을 시작한 순간의 목표 방향 각도 (스윙 내내 고정)
    private float swingDamage;      // 이번 스윙의 최종 데미지 (치명타면 이미 배율이 적용된 값)
    private bool swingIsCrit;       // 이번 스윙이 치명타인지

    // 이번 스윙 동안 이미 맞힌 적 목록 (같은 스윙에서 같은 적이 여러 프레임에 걸쳐 중복으로 맞지 않도록).
    private readonly HashSet<Enemy> hitThisSwing = new HashSet<Enemy>();

    // 이번 스윙에서 아직 안 날린 검기 (구리 특수효과). 스윙을 시작할 때 발사 방향과 "칼날이 시작점에서 몇 도 돌았을 때
    // 그 방향을 지나는지"를 정해두고, 칼날이 그만큼 돌면 날린다.
    private bool wavePending;
    private bool waveFan;
    private float waveFireAngle;
    private float waveFireTravel;
    private float waveDamage;
    private bool waveCrit;

    // === IAutoMeleeWeapon ===
    // 궤도 반지름 + 판정 반지름 = 칼끝이 플레이어로부터 닿을 수 있는 최대 거리.
    // hitRadius나 orbitRadius가 강화로 바뀌면 이 값도 자동으로 같이 늘어난다.
    public float MaxReach => weaponAim.orbitRadius + hitRadius;
    public bool IsHeld => weaponAim.isHeld;
    public bool IsAttacking => isSwinging;
    public bool IsOnCooldown => attackCooldown > 0f;

    // 나무 10단계 상시 회전은 큐와 상관없이 스스로 계속 돌기 때문에 자동공격 큐에서 뺀다.
    // (안 빼면 이 검이 계속 "공격 중"이라 큐가 영원히 막혀서 창 같은 다른 근접 무기가 못 나간다)
    public bool ExcludeFromQueue => EffectTier(ResourceType.Wood) >= TierHigh;

    // === IEnhanceableWeapon ===
    private WeaponIdentity identity;

    // 강화로 바뀌는 스탯의 원본값. Awake에서 저장해두고, 런 종료(사망/추출)로 강화가 리셋되면
    // 여기로 되돌린 뒤 현재 레벨만큼 다시 적용한다 (포탈 이동은 무기 인스턴스가 유지되므로 직접 되돌려야 함).
    private float baseAttackInterval, baseDamage, baseHitRadius, baseCritChance, baseSwingDuration;

    // 공격 범위(구리) 강화로 판정이 커지면 칼 그림도 같은 배율로 키우기 위한 원본값 (UpdateVisualSize 참고).
    private Vector3 baseLocalScale;
    private float baseOrbitRadius;

    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => WeaponEnhanceStore.GetLevel(identity, type);

    public void ApplyEnhance(ResourceType type)
    {
        if (!WeaponEnhanceStore.TryEnhance(identity, type)) return;
        ApplyStatDelta(type);
        UpdateVisualSize();
    }

    // 판정 반지름(hitRadius)이 원본 대비 몇 배가 됐는지만큼 칼 그림을 키운다 (구리 강화 = 공격 범위).
    // 칼은 중심이 위치/판정의 기준점이라 그냥 키우면 칼자루 쪽이 플레이어 몸을 뚫고 반대편으로 나가 버린다.
    // 그래서 커진 길이의 절반만큼 궤도 반지름도 바깥으로 밀어서 칼자루(플레이어 쪽 끝)는 원래 자리에 있게 한다
    // (그 결과 칼끝과 판정 중심이 같이 바깥으로 나가서 실제 닿는 거리는 판정 반지름 증가분보다 조금 더 늘어난다).
    // 칼 스프라이트가 위쪽(+Y)을 향해 서 있고 visualRotationOffset이 이를 바깥 방향으로 돌려준다는 전제다.
    private void UpdateVisualSize()
    {
        float scale = baseHitRadius > 0f ? hitRadius / baseHitRadius : 1f;
        scale = 1f + (scale - 1f) * rangeVisualSizeFollow;

        transform.localScale = baseLocalScale;
        float baseHalfLength = spriteRenderer != null && spriteRenderer.sprite != null
            ? spriteRenderer.sprite.bounds.size.y * transform.lossyScale.y * 0.5f
            : 0f;

        transform.localScale = new Vector3(baseLocalScale.x * scale, baseLocalScale.y * scale, baseLocalScale.z);
        weaponAim.orbitRadius = baseOrbitRadius + (scale - 1f) * baseHalfLength;
    }

    private void ApplyStatDelta(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: attackInterval = Mathf.Max(0.1f, attackInterval - 0.02f); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: hitRadius += 0.1f; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
            // 발사체가 없는 근접무기라 "발사속도"는 휘두르는 모션 자체를 빠르게 하는 것으로 대체.
            case ResourceType.Oil: swingDuration = Mathf.Max(0.05f, swingDuration - 0.015f); break;
        }
    }

    // 특수효과가 켜지는 단계를 구한다: 0(없음) / 5 / 10. 강화 레벨은 이 검 "한 자루"(identity)의 값이라서,
    // 같은 검을 여러 자루 들어도 자루마다 켜진 효과가 다르다.
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
        spriteRenderer = GetComponent<SpriteRenderer>();
        identity = GetComponent<WeaponIdentity>();

        baseAttackInterval = attackInterval;
        baseDamage = damage;
        baseHitRadius = hitRadius;
        baseCritChance = critChance;
        baseSwingDuration = swingDuration;
        baseLocalScale = transform.localScale;
        baseOrbitRadius = weaponAim.orbitRadius;

        ReapplyEnhancements();
    }

    // WeaponEnhanceStore의 현재 강화 레벨을 스탯에 반영한다. 먼저 원본값으로 되돌린 뒤
    // 레벨 수만큼 ApplyStatDelta를 다시 적용하므로, 강화가 리셋된 뒤 호출해도 정확히 맞는다.
    public void ReapplyEnhancements()
    {
        attackInterval = baseAttackInterval;
        damage = baseDamage;
        hitRadius = baseHitRadius;
        critChance = baseCritChance;
        swingDuration = baseSwingDuration;

        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes)
        {
            int level = WeaponEnhanceStore.GetLevel(identity, type);
            for (int i = 0; i < level; i++) ApplyStatDelta(type);
        }

        // 거점 영구 강화(공격력/공속/치명타)도 같은 델타 공식으로 적용한다. 런 종료로 리셋되지 않는다.
        for (int i = 0; i < PermanentUpgradeManager.GetLevel(ResourceType.Iron); i++) ApplyStatDelta(ResourceType.Iron);
        for (int i = 0; i < PermanentUpgradeManager.GetLevel(ResourceType.Wood); i++) ApplyStatDelta(ResourceType.Wood);
        for (int i = 0; i < PermanentUpgradeManager.GetLevel(ResourceType.Chemical); i++) ApplyStatDelta(ResourceType.Chemical);

        UpdateVisualSize();
    }

    void OnEnable()
    {
        MeleeAutoAttackQueue.Register(this);
        PermanentUpgradeManager.OnChanged += ReapplyEnhancements;
    }

    void OnDisable()
    {
        if (spriteRenderer != null) spriteRenderer.color = Color.white; // 강타 도중 비활성화돼도 색이 남지 않게
        orbiters.Remove(this);
        MeleeAutoAttackQueue.Unregister(this);
        PermanentUpgradeManager.OnChanged -= ReapplyEnhancements;
    }

    void Update()
    {
        // 질주(기름 10단계) 버프 중에는 쿨다운이 더 빨리 돈다.
        attackCooldown -= Time.deltaTime * StageGimmickManager.WeaponTimeScale * PlayerBuffs.MeleeCooldownSpeedMultiplier;

        if (!isSwinging)
        {
            if (EffectTier(ResourceType.Wood) >= TierHigh)
            {
                // 나무 10단계 상시 회전: 들고 있으면 마우스를 누르고 있는 동안만, 안 들고 있으면(자동) 계속 돈다.
                bool spin = weaponAim.isHeld
                    ? Mouse.current != null && Mouse.current.leftButton.isPressed
                    : !EnemyManager.PlayerDead;
                if (spin) StartOrbit();
            }
            else if (weaponAim.isHeld && Mouse.current.leftButton.isPressed && attackCooldown <= 0f)
            {
                // 들고 있을 때만 마우스 좌클릭으로 수동 발동한다. 자동 발동은 MeleeAutoAttackQueue가
                // TriggerAutoAttack()을 직접 호출해서 처리하므로 여기서는 신경 쓰지 않는다.
                StartSwing(weaponAim.AimDirection);
            }
        }

        if (isSwinging)
        {
            // 기믹 "한파" 중에는 휘두르는 모션 자체가 느리게 재생된다.
            float dt = Time.deltaTime * StageGimmickManager.WeaponTimeScale;
            if (mode == SwingMode.Orbit) UpdateOrbit(dt);
            else UpdateSwing(dt);
        }
    }

    // MeleeAutoAttackQueue가 자기 차례가 되면 호출한다.
    public void TriggerAutoAttack(Vector2 targetPosition)
    {
        Vector2 dir = targetPosition - (Vector2)weaponAim.Pivot.position;
        StartSwing(dir);
    }

    private void StartSwing(Vector2 dir)
    {
        isSwinging = true;
        swingElapsed = 0f;
        attackCooldown = attackInterval;

        // 나무(공속) 특수효과: 5단계부터 스윙이 부채꼴(120도) 대신 한 바퀴(360도) 회전 베기로 바뀐다.
        // 한 바퀴 시간은 공격 간격에 묶어서(간격 x 0.9), 나무 강화로 간격이 줄면 회전도 그만큼 빨라지게 한다.
        if (EffectTier(ResourceType.Wood) >= TierLow)
        {
            mode = SwingMode.Spin360;
            activeSwingAngle = spinSwingAngle;
            activeSwingDuration = Mathf.Max(0.05f, attackInterval * spinDurationRatio);
            swingStartOffset = spinStartOffset;
        }
        else
        {
            mode = SwingMode.Arc;
            activeSwingAngle = swingAngle;
            activeSwingDuration = swingDuration;
            swingStartOffset = swingAngle * 0.5f; // 목표 방향 중심으로 +half(위)에서 -half(아래)까지
        }

        PrepareSwing(1f, 1f);

        // 이 순간부터 WeaponAim의 자동 회전/위치 갱신을 멈추고 이 스크립트가 직접 제어한다.
        weaponAim.externalControl = true;
        weaponAim.SetForceVisible(true); // 들고 있지 않아도 공격하는 동안은 보이게 한다

        swingCenterAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        PrepareWave(swingCenterAngle + swingStartOffset); // 칼날은 이 각도에서 출발해 시계방향으로 돈다
    }

    // 스윙(상시 회전은 한 바퀴)이 시작될 때마다 한 번: 철(강타)/치명타 판정과 "이번 스윙에 이미 맞힌 적" 초기화.
    // 상시 회전은 "한 바퀴 = 스윙 1회"로 취급하므로 카운터/치명타/적중 중복 방지가 전부 바퀴 단위로 돈다.
    private void PrepareSwing(float extraDamageMultiplier, float extraKnockbackMultiplier)
    {
        hitThisSwing.Clear();

        // 철(공격력) 특수효과: 5단계는 N번째 스윙마다 강타, 10단계는 모든 스윙 +40%에 강타 유지.
        swingCount++;
        int ironTier = EffectTier(ResourceType.Iron);
        float damageMultiplier = extraDamageMultiplier;
        if (ironTier >= TierHigh) damageMultiplier *= 1f + ironTier10DamageBonus;
        isHeavySwing = ironTier >= TierLow && heavySwingEvery > 0 && swingCount % heavySwingEvery == 0;
        swingKnockbackMultiplier = extraKnockbackMultiplier;
        if (isHeavySwing)
        {
            damageMultiplier *= heavySwingDamageMultiplier;
            swingKnockbackMultiplier *= heavySwingKnockbackMultiplier;
        }
        if (spriteRenderer != null) spriteRenderer.color = isHeavySwing ? heavySwingColor : Color.white; // 강타라는 걸 눈으로 알 수 있게

        swingDamage = CriticalHit.Roll(damage * damageMultiplier, critChance, critMultiplier, out swingIsCrit);
    }

    private void UpdateSwing(float dt)
    {
        swingElapsed += dt;
        float t = Mathf.Clamp01(swingElapsed / activeSwingDuration);

        // 목표 방향(중앙각)에서 swingStartOffset만큼 위쪽에서 출발해 activeSwingAngle만큼 시계방향으로 훑는다.
        float currentAngle = swingCenterAngle + swingStartOffset - activeSwingAngle * t;

        ApplySwordTransform(currentAngle);
        CheckHit();
        UpdateWave(activeSwingAngle * t);

        if (t >= 1f) EndSwing();
    }

    private void EndSwing()
    {
        isSwinging = false;
        wavePending = false;
        if (mode == SwingMode.Orbit) orbiters.Remove(this);
        if (spriteRenderer != null) spriteRenderer.color = Color.white; // 강타 색 원복
        weaponAim.externalControl = false;
        weaponAim.SetForceVisible(false); // 공격이 끝나면 다시 숨긴다 (들고 있는 중이면 WeaponAim이 계속 보이게 유지)
    }

    // === 나무 10단계: 상시 회전 ===
    // 칼이 플레이어 주변을 계속 돈다. 한 바퀴 시간은 공격 간격이고, 한 바퀴가 스윙 1회다(적은 바퀴마다 1번만 맞는다).
    // 직접 조작: 마우스를 누르고 있는 동안만 돌고, 놓으면 현재 바퀴를 끝까지 돈 뒤 멈춘다.
    // 자동(안 들고 있을 때): 계속 돌되 데미지/넉백이 줄어든다. 자동공격 큐와 상관없이 스스로 돈다(ExcludeFromQueue).
    // 같은 종류의 칼이 여러 자루 동시에 돌면 모두 하나의 공유 시계를 따르면서 360/N도 간격으로 벌어진다.
    private static readonly List<SwordAttack> orbiters = new List<SwordAttack>();
    private static float orbitClockAngle;

    private float orbitProgress;     // 현재 바퀴를 얼마나 돌았는지(도)
    private float orbitPhaseOffset;  // 공유 시계 각도 기준으로 이 칼이 서 있는 오프셋(도)

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이에서 남은 값이 섞이지 않게 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOrbitStatics()
    {
        orbiters.Clear();
    }

    // 한 바퀴 시간 = 공격 간격 (질주 버프의 쿨다운 단축이 켜져 있으면 그만큼 빨라진다).
    private float OrbitDegreesPerSecond => 360f / Mathf.Max(0.05f, attackInterval / PlayerBuffs.MeleeCooldownSpeedMultiplier);

    // 한 바퀴가 끝날 때마다 다음 바퀴를 이어서 돌지 판단한다.
    private bool ShouldKeepOrbiting()
    {
        if (EffectTier(ResourceType.Wood) < TierHigh || EnemyManager.PlayerDead) return false;
        if (weaponAim.isHeld) return Mouse.current != null && Mouse.current.leftButton.isPressed;
        return true;
    }

    private void StartOrbit()
    {
        mode = SwingMode.Orbit;
        isSwinging = true;
        orbitProgress = 0f;

        // 지금 칼날이 있는 각도에서 이어서 돈다 (갑자기 튀지 않게).
        float startAngle = CurrentBladeAngle();
        orbiters.RemoveAll(o => o == null);
        if (orbiters.Count == 0) orbitClockAngle = startAngle;
        if (!orbiters.Contains(this)) orbiters.Add(this);
        orbitPhaseOffset = Mathf.DeltaAngle(orbitClockAngle, startAngle);

        weaponAim.externalControl = true;
        weaponAim.SetForceVisible(true);
        BeginOrbitRevolution();
    }

    // 한 바퀴를 시작할 때마다: 안 들고 있으면(자동) 이번 바퀴의 데미지/넉백을 줄인다.
    private void BeginOrbitRevolution()
    {
        bool auto = !weaponAim.isHeld;
        PrepareSwing(auto ? autoOrbitDamageMultiplier : 1f, auto ? autoOrbitKnockbackMultiplier : 1f);
        PrepareWave(CurrentBladeAngle());
    }

    private void UpdateOrbit(float dt)
    {
        float speed = OrbitDegreesPerSecond;

        // 공유 시계는 회전 중인 검들 중 맨 앞의 한 자루만 돌린다 (여러 자루가 각자 돌리면 그만큼 빨라지므로).
        // 앞의 검이 멈추면 다음 검이 자연스럽게 시계를 이어받는다.
        if (orbiters.Count > 0 && orbiters[0] == this)
            orbitClockAngle = Mathf.Repeat(orbitClockAngle - speed * dt, 360f);

        // 여러 자루가 돌고 있으면 360/N 간격이 되도록 오프셋을 부드럽게 맞춘다.
        int index = Mathf.Max(0, orbiters.IndexOf(this));
        float desiredOffset = 360f * index / Mathf.Max(1, orbiters.Count);
        orbitPhaseOffset = Mathf.MoveTowardsAngle(orbitPhaseOffset, desiredOffset, orbitSpreadEaseSpeed * dt);

        ApplySwordTransform(orbitClockAngle + orbitPhaseOffset);
        CheckHit();

        orbitProgress += speed * dt;
        UpdateWave(orbitProgress);
        if (orbitProgress >= 360f)
        {
            if (ShouldKeepOrbiting())
            {
                orbitProgress -= 360f;
                BeginOrbitRevolution();
            }
            else
            {
                EndSwing();
            }
        }
    }

    // 지금 칼날이 플레이어 기준 몇 도 위치에 있는지. 칼이 플레이어와 겹쳐 있으면 조준 방향을 쓴다.
    private float CurrentBladeAngle()
    {
        Vector2 offset = (Vector2)transform.position - (Vector2)weaponAim.Pivot.position;
        if (offset.sqrMagnitude < 0.0001f) offset = weaponAim.AimDirection;
        if (offset.sqrMagnitude < 0.0001f) offset = Vector2.right;
        return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }

    // 주어진 각도로 궤도 위 위치와 회전을 계산해서 그대로 적용한다. WeaponAim의 궤도 계산과 동일한 방식.
    private void ApplySwordTransform(float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector3 orbitOffset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * weaponAim.orbitRadius;
        transform.position = weaponAim.Pivot.position + orbitOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + weaponAim.visualRotationOffset);
    }

    // 칼의 현재 위치를 검사해서 아직 이번 스윙에서 맞히지 않은 적에게만 데미지를 준다.
    private void CheckHit()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null || hitThisSwing.Contains(enemy)) continue;

            hitThisSwing.Add(enemy);

            Vector2 knockDir = (Vector2)hit.transform.position - (Vector2)transform.position;
            if (knockDir.sqrMagnitude < 0.0001f) knockDir = Vector2.up;
            bool wasDying = enemy.IsDying;
            enemy.Hit(knockDir.normalized, swingDamage, swingIsCrit, swingKnockbackMultiplier);
            if (wasDying) continue; // 이미 죽는 중이던 적은 효과 대상이 아니다

            // 화학물질(치명타) 특수효과: 치명타 스윙에 맞은 적 기절. 죽은 적에게는 걸 필요 없다.
            if (swingIsCrit && !enemy.IsDying) ApplyStun(enemy);

            // 기름(속도) 특수효과: 적을 처치하면 질주 버프.
            if (enemy.IsDying) OnKill();
        }
    }

    // === 구리(범위) 특수효과: 검기 ===
    // 스윙(상시 회전은 한 바퀴)이 시작될 때마다 한 번: 이번 스윙에 검기가 나가는지, 어느 방향으로, 칼날이 시작 각도에서
    // 몇 도 돌았을 때 나가는지를 정한다. 5단계는 waveEvery번째 스윙마다, 10단계는 매 스윙 (waveEvery번째는 3갈래).
    // 카운터는 철 특수효과와 같은 swingCount를 쓴다. 반드시 PrepareSwing 뒤에 호출해야 한다(swingDamage가 필요).
    private void PrepareWave(float bladeStartAngle)
    {
        wavePending = false;

        int tier = EffectTier(ResourceType.Copper);
        if (tier < TierLow || waveEvery <= 0) return;

        bool everyNth = swingCount % waveEvery == 0;
        if (tier < TierHigh && !everyNth) return;

        waveFan = tier >= TierHigh && everyNth;
        waveFireAngle = ChooseWaveAngle();
        waveFireTravel = Mathf.Repeat(bladeStartAngle - waveFireAngle, 360f); // 칼날이 시계방향으로 이만큼 돌면 그 방향을 지난다
        waveDamage = swingDamage * waveDamageMultiplier;
        waveCrit = swingIsCrit;
        wavePending = true;
    }

    private float ChooseWaveAngle()
    {
        // 부채꼴 스윙은 직접/자동 모두 "그 공격의 방향"(= swingCenterAngle)으로 나간다.
        if (mode == SwingMode.Arc) return swingCenterAngle;

        // 360도 회전/상시 회전: 직접 조작이면 마우스 방향, 자동이면 무작위.
        if (weaponAim.isHeld)
        {
            Vector2 aim = weaponAim.AimDirection;
            if (aim.sqrMagnitude > 0.0001f) return Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            return CurrentBladeAngle();
        }
        return Random.value * 360f;
    }

    // 칼날이 이번 스윙에서 travelled도 돌았을 때 호출한다. 정해둔 방향을 지났으면 검기를 날린다.
    private void UpdateWave(float travelled)
    {
        if (wavePending && travelled >= waveFireTravel) FireWave();
    }

    private void FireWave()
    {
        wavePending = false;

        Vector2 origin = transform.position; // 칼날 위치에서 출발
        SpawnWave(origin, waveFireAngle);
        if (waveFan)
        {
            SpawnWave(origin, waveFireAngle + waveFanAngle);
            SpawnWave(origin, waveFireAngle - waveFanAngle);
        }
    }

    private void SpawnWave(Vector2 origin, float angle)
    {
        SwordWave.Spawn(this, origin, angle, waveSpeed, waveRadius, waveRange, waveDamage, waveCrit,
            waveKnockbackMultiplier, waveColor, spriteRenderer);
    }

    // 검기가 적을 맞혔을 때 SwordWave가 돌려주는 콜백: 검 본체와 같은 화학물질(기절)/기름(질주) 효과를 적용한다.
    public void OnWaveHit(Enemy enemy, bool crit)
    {
        if (crit && !enemy.IsDying) ApplyStun(enemy);
        if (enemy.IsDying) OnKill();
    }

    private void ApplyStun(Enemy enemy)
    {
        int tier = EffectTier(ResourceType.Chemical);
        if (tier >= TierHigh) enemy.Stun(stunDurationTier10, stunDamageTakenTier10);
        else if (tier >= TierLow) enemy.Stun(stunDurationTier5);
    }

    private void OnKill()
    {
        int tier = EffectTier(ResourceType.Oil);
        if (tier >= TierHigh) PlayerBuffs.ApplySprint(sprintMoveBonusTier10, sprintDurationTier10, sprintCooldownReductionTier10);
        else if (tier >= TierLow) PlayerBuffs.ApplySprint(sprintMoveBonusTier5, sprintDurationTier5, 0f);
    }
}
