using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Lance 전용 공격 스크립트. Sword처럼 플레이어 주위 궤도에 떠서 목표 방향을 향하고 있지만,
// 공격 방식은 휘두르기가 아니라 "찌르기": 목표 방향을 향해 궤도 반지름이 순간적으로 늘어나며
// (찔러 나감) 다시 원래 거리로 돌아온다. 각도는 찌르는 동안 고정된다.
//
// 들고 있을 때는 마우스 방향으로 좌클릭 시 찌르고(수동), 들고 있지 않을 때는
// MeleeAutoAttackQueue가 차례가 되면 TriggerAutoAttack()을 호출해서 가장 가까운 적 방향으로
// 같은 찌르기 모션을 재생한다(자동). IAutoMeleeWeapon으로 큐에 자신을 등록한다.
//
// 판정은 검과 같은 접촉 방식이다: 창 모양 상자가 적의 몸통 원(Enemy.TryGetBodyCircle)에 실제로 닿은 적만 맞는다.
[RequireComponent(typeof(WeaponAim))]
public class LanceAttack : MonoBehaviour, IAutoMeleeWeapon, IEnhanceableWeapon
{
    // 연속 공격 사이의 최소 간격(초). 찌르기를 시작한 순간부터 센다 (찌르는 시간도 이 안에 포함).
    public float attackInterval = 0.8f;

    [Header("찌르기 모션")]
    public float thrustDistance = 1.2f;   // 평소 궤도 반지름보다 얼마나 더 멀리 찔러 나갈지
    public float thrustOutDuration = 0.13f; // 앞으로 찔러 나가는 데 걸리는 시간
    public float thrustBackDuration = 0.2f; // 다시 원래 자리로 돌아오는 데 걸리는 시간

    [Header("속도 강화 (목재=공격 간격, 기름=찌르는/돌아오는 시간)")]
    // 최대 강화(10레벨)에서 간격 0.8 -> 0.4, 찌르기 0.13 -> 0.10, 돌아오기 0.2 -> 0.14가 되도록 잡았다 (검과 같은 비율).
    public float intervalReductionPerWood = 0.04f;
    public float minAttackInterval = 0.2f;
    public float thrustOutReductionPerOil = 0.003f;
    public float thrustBackReductionPerOil = 0.006f;
    public float minThrustOutDuration = 0.06f;
    public float minThrustBackDuration = 0.1f;

    [Header("공격력 / 판정 범위")]
    public float damage = 1f;

    // 공격 범위 강화(구리)의 기준값. 접촉 판정에는 직접 안 쓰이고, 창 그림 크기를 얼마나 키울지와 자동공격 감지 거리(MaxReach)에 쓰인다.
    public float hitRadius = 0.8f;

    // 구리(공격 범위) 강화 1레벨당 hitRadius가 늘어나는 양 = 원본 hitRadius x 이 비율. 0.1이면 최대 강화(10레벨)에서 2배.
    public float rangeGrowthPerLevel = 0.1f;

    // 판정이 커진 만큼 창 그림을 얼마나 따라 키울지. 1이면 같은 배율(최대 강화에서 2배), 0이면 그림은 그대로.
    public float rangeVisualSizeFollow = 1f;

    [Header("판정 방식 (접촉 판정)")]
    // 창 그림은 정사각형 안에 대각선으로 그려져 있어서 그 대각선 길이를 창 길이로 쓴다. 접촉 상자의 폭은 길이에 이 비율을 곱한 값.
    public float contactWidthRatio = 0.3f;

    // 찔러 나가는 동안 한 프레임에 움직인 거리를 이 간격(월드 거리)으로 쪼개서 중간 위치마다 검사한다 (빠르게 찔러도 적을 건너뛰지 않게).
    public float contactStepDistance = 0.1f;

    [Header("치명타")]
    // 이번 찌르기가 치명타인지는 찌르기를 시작할 때 한 번만 판정해서, 그 찌르기 동안 맞는 모든 적에게 동일하게 적용한다
    // (연속 찌르기는 찌르기마다 따로 판정).
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    // === 강화 단계별 특수 효과 ===
    // 각 강화 종류(자원)가 5단계/10단계에 도달하면 효과가 켜진다. 같은 종류 안에서는 도달한 가장 높은 단계 효과만
    // 적용된다(10단계가 되면 5단계 효과는 꺼지고 10단계 효과가 그 상위호환으로 대신한다). 다른 종류끼리는 같이 켜진다.
    // 찌르기 단위 이벤트(맞힌 순서 / 치명타 / 맞힌 횟수 / 끝까지 뻗는 순간)에만 걸어서 직접 조작과 자동공격이 똑같이 동작한다.
    // 단계는 이 창 "한 자루"의 강화 레벨로 정해진다.
    [Header("특수효과: 철(공격력) - 달아오르는 창")]
    // 찌르기가 적을 맞히는 데 성공하면 데미지 버프 중첩이 하나 쌓인다 (찌르기 한 번에 중첩은 최대 1개 - 연속 찌르기는 찌르기마다 따로 쌓는다). 중첩 하나당 데미지 +10%.
    // 적을 맞힐 때마다 지속시간이 갱신되고, 지속시간 동안 못 맞히면 한꺼번에 사라지지 않고 일정 간격마다 한 단계씩 내려간다.
    // 쌓일수록 창이 붉어진다. 중첩은 이 창 한 자루에 쌓인다.
    public float heatDamagePerStack = 0.1f;
    public int heatMaxStacksTier5 = 3;
    public int heatMaxStacksTier10 = 5;
    public float heatDuration = 5f;
    public float heatDecayInterval = 0.4f;
    public Color heatColor = new Color(1f, 0.3f, 0.25f); // 최대 중첩일 때 창 색 (검 강타 색과 같은 계열)

    [Header("특수효과: 기름(속도) - 연타 환급")]
    // 적을 맞힐 때마다 남은 공격 쿨다운이 줄어든다. 같은 적은 창에 맞든 충격파에 맞든 찌르기 한 번에 한 번만 환급되고, 환급 총량의 상한도 찌르기 한 번 기준이다 (연속 찌르기는 찌르기마다 따로 계산).
    public float refundPerHitTier5 = 0.05f;
    public float refundCapTier5 = 0.3f;
    public float refundPerHitTier10 = 0.1f;
    public float refundCapTier10 = 0.6f;

    [Header("특수효과: 화학물질(치명타) - 급소 처형")]
    // 치명타 찌르기에 맞은 뒤 체력 비율이 이 값 이하로 남은 적은 즉사한다. 정예/보스(Enemy.IsEliteOrBoss)에게는 통하지 않는다.
    public float executeThresholdTier5 = 0.2f;
    public float executeThresholdTier10 = 0.3f;

    [Header("특수효과: 구리(범위) - 창끝 충격")]
    // 창이 끝까지 뻗는 순간 창끝 주변 반경 안의 적에게 (그 찌르기 데미지 x 배율)의 충격 피해. 충격 피해도 창에 맞은 것과 똑같이 취급돼서
    // 달아오름 중첩/연타 환급/급소 처형이 발동한다 (창은 빗나가고 충격만 맞혀도 공격 성공으로 친다). 달아오름 중첩에 의한 데미지 증가도
    // 찌르기 데미지에 이미 반영돼 있어 충격 피해도 같이 커진다.
    public float burstRadiusTier5 = 0.5f;
    public float burstDamageTier5 = 0.6f;
    public float burstKnockbackTier5 = 0.5f;
    public float burstRadiusTier10 = 0.9f;
    public float burstDamageTier10 = 0.8f;
    public float burstKnockbackTier10 = 1.5f;
    public Color burstColor = new Color(1f, 0.95f, 0.6f, 0.55f);

    [Header("특수효과: 목재(공속) - 연속 찌르기")]
    // 한 번 공격에 같은 목표를 향해 여러 번 연달아 찌른다. 첫 찌르기는 정면에서 약간 비스듬히 벗어난 각도이고, 이어서 반대쪽으로 비스듬히 찌른다.
    // 연속 찌르기 동안은 모션이 훨씬 빠르다. 2번째부터는 데미지가 줄어든다. 쿨다운은 공격 전체에 한 번만 적용된다.
    public int flurryThrustsTier5 = 2;
    public int flurryThrustsTier10 = 3;
    public float flurryFollowUpDamage = 0.7f;
    public float flurryAngleSpread = 10f;   // 목표 방향에서 좌/우로 비스듬히 벗어나는 각도(도)
    public float flurryTimeScale = 0.2f;    // 연속 찌르기 중 찌르기/돌아오기 시간 배율 (작을수록 빠름). 0.2면 기본 찌르기보다 5배 빠르다.

    // 최대 강화가 15로 늘어도 10단계 효과는 10강에서 켜져야 하므로 MaxLevel이 아니라 고정값으로 둔다 (SwordAttack과 동일).
    // 15단계 효과는 아직 기획 전이라, 15강에서도 10단계 효과가 그대로 유지된다.
    private const int TierLow = 5;
    private const int TierHigh = 10;

    // 강화 UI에 보여줄 특수효과 설명. 수치는 인스펙터 값을 그대로 읽어서 밸런스를 바꿔도 설명이 따라간다.
    public string GetMilestoneEffect(ResourceType type, int tier)
    {
        bool high = tier == TierHigh;
        if (tier != TierLow && !high) return null; // 15단계는 아직 미정
        switch (type)
        {
            case ResourceType.Wood:
                return $"연속 찌르기 {(high ? flurryThrustsTier10 : flurryThrustsTier5)}연타";
            case ResourceType.Iron:
                return $"달아오름: 적중마다 피해 +{heatDamagePerStack * 100f:0}% (최대 {(high ? heatMaxStacksTier10 : heatMaxStacksTier5)}중첩)";
            case ResourceType.Copper:
                return $"창끝 충격: 피해 x{(high ? burstDamageTier10 : burstDamageTier5):0.#}, 반경 {(high ? burstRadiusTier10 : burstRadiusTier5):0.#}";
            case ResourceType.Chemical:
                return $"급소 처형: 치명타 후 체력 {(high ? executeThresholdTier10 : executeThresholdTier5) * 100f:0}% 이하 즉사";
            case ResourceType.Oil:
                return $"연타 환급: 적중마다 쿨다운 -{(high ? refundPerHitTier10 : refundPerHitTier5):0.##}초 (최대 {(high ? refundCapTier10 : refundCapTier5):0.#}초)";
        }
        return null;
    }

    private WeaponAim weaponAim;
    private SpriteRenderer spriteRenderer;
    private float attackCooldown;

    private enum ThrustPhase { None, Out, Back }
    private ThrustPhase phase;
    private float phaseElapsed;
    private float outTarget;     // 찔러 나갔을 때의 궤도 반지름
    private float lastRadius;    // 직전 프레임의 궤도 반지름 (접촉 판정이 이번 프레임에 훑은 구간을 구하는 데 쓴다)

    private float restRadius;   // 찌르기 시작 시점의 평소 궤도 반지름 (WeaponAim.orbitRadius)
    private float thrustAngle;  // 지금 찌르는 방향 각도 (한 번 찌르는 동안 고정)
    private float thrustDamage; // 이번 찌르기의 데미지 (치명타면 이미 배율이 적용된 값)
    private bool thrustIsCrit;  // 이번 찌르기가 치명타인지

    // 한 번의 공격은 찌르기 1회(기본) 또는 연속 찌르기(목재 5단계 이상)로 이루어진다.
    private float attackBaseAngle;      // 이번 공격의 목표 방향
    private int thrustsInAttack;        // 이번 공격의 총 찌르기 횟수
    private int thrustIndex;            // 지금 몇 번째 찌르기인지 (0부터)
    private float segOutDuration;       // 지금 찌르기의 찔러 나가는 시간 / 돌아오는 시간
    private float segBackDuration;
    private float refundedThisThrust;   // 이번 찌르기에서 지금까지 환급한 쿨다운 (연타 환급 상한)

    // 달아오르는 창(철) 상태
    private int heatStacks;             // 지금 쌓인 중첩 수
    private float heatTimer;            // 마지막으로 맞힌 뒤 남은 유지 시간
    private float heatDecayTimer;       // 유지 시간이 끝난 뒤 다음 단계 하락까지 남은 시간
    private bool heatGainedThisThrust;  // 이번 찌르기에서 이미 중첩을 쌓았는지
    private Color lastTint = Color.white;

    private readonly HashSet<Enemy> hitThisThrust = new HashSet<Enemy>();
    private readonly HashSet<Enemy> refundedEnemies = new HashSet<Enemy>(); // 이번 찌르기에서 이미 쿨다운을 환급해 준 적

    // === IAutoMeleeWeapon ===
    // 평소 궤도 반지름 + 찌르는 거리 + 판정 반지름 = 창끝이 플레이어로부터 닿을 수 있는 최대 거리.
    // 관련 스탯이 강화로 바뀌면 이 값도 자동으로 같이 늘어난다.
    public float MaxReach => weaponAim.orbitRadius + thrustDistance + hitRadius;
    public bool IsHeld => weaponAim.isHeld;
    public bool IsAttacking => phase != ThrustPhase.None;
    public bool IsOnCooldown => attackCooldown > 0f;
    public bool ExcludeFromQueue => false; // 창은 항상 큐를 거쳐서 공격한다
    public float CooldownRemaining => attackCooldown;
    public int HeatStacks => heatStacks;

    // === IEnhanceableWeapon ===
    private WeaponIdentity identity;

    // 강화로 바뀌는 스탯의 원본값. Awake에서 저장해두고, 런 종료(사망/추출)로 강화가 리셋되면
    // 여기로 되돌린 뒤 현재 레벨만큼 다시 적용한다 (포탈 이동은 무기 인스턴스가 유지되므로 직접 되돌려야 함).
    private float baseAttackInterval, baseDamage, baseHitRadius, baseCritChance, baseThrustOutDuration, baseThrustBackDuration;

    // 공격 범위 강화로 창 그림을 키우기 위한 원본값 (UpdateVisualSize 참고).
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

    private void ApplyStatDelta(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: attackInterval = Mathf.Max(minAttackInterval, attackInterval - intervalReductionPerWood); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: hitRadius += baseHitRadius * rangeGrowthPerLevel; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
            // 발사체가 없는 근접무기라 "발사속도"는 찌르고 돌아오는 모션 자체를 빠르게 하는 것으로 대체.
            case ResourceType.Oil:
                thrustOutDuration = Mathf.Max(minThrustOutDuration, thrustOutDuration - thrustOutReductionPerOil);
                thrustBackDuration = Mathf.Max(minThrustBackDuration, thrustBackDuration - thrustBackReductionPerOil);
                break;
        }
    }

    // 특수효과가 켜지는 단계를 구한다: 0(없음) / 5 / 10. 이 창 한 자루의 강화 레벨 기준.
    private int EffectTier(ResourceType type)
    {
        int level = GetEnhanceLevel(type);
        if (level >= TierHigh) return TierHigh;
        if (level >= TierLow) return TierLow;
        return 0;
    }

    // 창 그림의 월드 길이: 정사각형 스프라이트의 대각선 (창이 그 안에 대각선으로 그려져 있다). 현재 크기 기준.
    private float WorldSpearLength()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return 0f;
        Vector2 size = spriteRenderer.sprite.bounds.size;
        Vector3 scale = transform.lossyScale;
        return new Vector2(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y)).magnitude;
    }

    // 판정 기준값(hitRadius)이 원본 대비 몇 배가 됐는지만큼 창 그림을 키운다 (구리 강화 = 공격 범위).
    // 창은 중심이 위치/판정의 기준점이라 그냥 키우면 창 뒤쪽이 플레이어 몸을 뚫고 나가 버려서,
    // 커진 길이의 절반만큼 궤도 반지름도 바깥으로 밀어 창 뒤쪽 끝(손잡이)이 원래 자리에 있게 한다 (SwordAttack.UpdateVisualSize와 같은 방식).
    private void UpdateVisualSize()
    {
        float scale = baseHitRadius > 0f ? hitRadius / baseHitRadius : 1f;
        scale = 1f + (scale - 1f) * rangeVisualSizeFollow;

        transform.localScale = baseLocalScale;
        float baseHalfLength = WorldSpearLength() * 0.5f;

        transform.localScale = new Vector3(baseLocalScale.x * scale, baseLocalScale.y * scale, baseLocalScale.z);
        weaponAim.orbitRadius = baseOrbitRadius + (scale - 1f) * baseHalfLength;
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
        baseThrustOutDuration = thrustOutDuration;
        baseThrustBackDuration = thrustBackDuration;
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
        thrustOutDuration = baseThrustOutDuration;
        thrustBackDuration = baseThrustBackDuration;

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

        // 강화가 리셋/재계산될 때(사망, 탈출 등) 달아오른 상태도 같이 식는다.
        heatStacks = 0;
        heatTimer = 0f;
        heatDecayTimer = 0f;
        ApplyHeatTint(MaxHeatStacks());
    }

    void OnEnable()
    {
        MeleeAutoAttackQueue.Register(this);
        PermanentUpgradeManager.OnChanged += ReapplyEnhancements;
    }

    void OnDisable()
    {
        if (spriteRenderer != null) spriteRenderer.color = Color.white; // 달아오른 색이 남지 않게
        lastTint = Color.white;
        MeleeAutoAttackQueue.Unregister(this);
        PermanentUpgradeManager.OnChanged -= ReapplyEnhancements;
    }

    void Update()
    {
        attackCooldown -= Time.deltaTime * StageGimmickManager.WeaponTimeScale;

        // 들고 있을 때만 마우스 좌클릭으로 수동 발동한다. 자동 발동은 MeleeAutoAttackQueue가
        // TriggerAutoAttack()을 직접 호출해서 처리하므로 여기서는 신경 쓰지 않는다.
        if (weaponAim.isHeld && !IsAttacking && Mouse.current.leftButton.isPressed && attackCooldown <= 0f)
        {
            StartThrust(weaponAim.AimDirection);
        }

        if (IsAttacking) UpdateThrust(Time.deltaTime * StageGimmickManager.WeaponTimeScale);

        UpdateHeat(Time.deltaTime);
    }

    // MeleeAutoAttackQueue가 자기 차례가 되면 호출한다.
    public void TriggerAutoAttack(Vector2 targetPosition)
    {
        Vector2 dir = targetPosition - (Vector2)weaponAim.Pivot.position;
        StartThrust(dir);
    }

    private void StartThrust(Vector2 dir)
    {
        attackCooldown = attackInterval;

        weaponAim.externalControl = true; // 찌르는 동안은 WeaponAim의 자동 궤도 추적을 잠깐 끈다
        weaponAim.SetForceVisible(true);  // 들고 있지 않아도 공격하는 동안은 보이게 한다

        restRadius = weaponAim.orbitRadius;
        outTarget = restRadius + thrustDistance;
        attackBaseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        thrustsInAttack = FlurryThrusts();
        thrustIndex = 0;

        BeginThrust();
    }

    // 목재(공속) 특수효과: 5단계 이상이면 한 번 공격이 연속 찌르기가 된다 (찌르기 횟수 2 -> 3). 아니면 1회.
    private int FlurryThrusts()
    {
        int tier = EffectTier(ResourceType.Wood);
        if (tier >= TierHigh) return Mathf.Max(1, flurryThrustsTier10);
        if (tier >= TierLow) return Mathf.Max(1, flurryThrustsTier5);
        return 1;
    }

    // 연속 찌르기의 각 찌르기 각도 오프셋: 첫 번째는 한쪽으로, 두 번째는 반대쪽으로 비스듬히, (3연속이면) 세 번째는 정면.
    private float FlurryAngleOffset(int index)
    {
        if (thrustsInAttack <= 1) return 0f;
        switch (index % 3)
        {
            case 0: return flurryAngleSpread;
            case 1: return -flurryAngleSpread;
            default: return 0f;
        }
    }

    // 찌르기 한 번을 시작한다: 각도/데미지(치명타는 찌르기마다 따로 굴린다)/모션 시간을 정하고 맞은 적 기록을 비운다.
    private void BeginThrust()
    {
        hitThisThrust.Clear();

        // 달아오름 중첩과 연타 환급은 찌르기 한 번마다 따로 계산한다 (연속 찌르기의 각 찌르기가 각자 중첩을 쌓고 환급한다).
        heatGainedThisThrust = false;
        refundedThisThrust = 0f;
        refundedEnemies.Clear();

        thrustAngle = attackBaseAngle + FlurryAngleOffset(thrustIndex);

        float damageScale = thrustIndex == 0 ? 1f : flurryFollowUpDamage; // 연속 찌르기의 2번째부터는 데미지가 줄어든다
        thrustDamage = CriticalHit.Roll(damage * damageScale * HeatDamageMultiplier(), critChance, critMultiplier, out thrustIsCrit);

        float motionScale = thrustsInAttack > 1 ? flurryTimeScale : 1f; // 연속 찌르기는 모션이 훨씬 빠르다
        segOutDuration = Mathf.Max(0.015f, thrustOutDuration * motionScale);
        segBackDuration = Mathf.Max(0.025f, thrustBackDuration * motionScale);

        lastRadius = restRadius;
        phase = ThrustPhase.Out;
        phaseElapsed = 0f;
    }

    // 1) 앞으로 찔러 나가고(이때만 판정) 2) 다시 원래 거리로 돌아온다. 연속 찌르기면 돌아온 뒤 바로 다음 찌르기를 시작한다.
    private void UpdateThrust(float dt)
    {
        phaseElapsed += dt;

        if (phase == ThrustPhase.Out)
        {
            float t = Mathf.Clamp01(phaseElapsed / segOutDuration);
            float radius = Mathf.Lerp(restRadius, outTarget, t);
            ApplyLanceTransform(radius);
            SweepContact(lastRadius, radius);
            lastRadius = radius;

            if (t >= 1f)
            {
                FireTipBurst(); // 끝까지 뻗은 순간 (구리 특수효과)
                phase = ThrustPhase.Back;
                phaseElapsed = 0f;
            }
        }
        else if (phase == ThrustPhase.Back)
        {
            float t = Mathf.Clamp01(phaseElapsed / segBackDuration);
            ApplyLanceTransform(Mathf.Lerp(outTarget, restRadius, t));

            if (t >= 1f)
            {
                thrustIndex++;
                if (thrustIndex < thrustsInAttack) BeginThrust();
                else EndThrust();
            }
        }
    }

    private void EndThrust()
    {
        ApplyLanceTransform(restRadius);
        weaponAim.externalControl = false; // 다시 WeaponAim에게 조준을 맡긴다
        weaponAim.SetForceVisible(false);  // 공격이 끝나면 다시 숨긴다 (들고 있는 중이면 WeaponAim이 계속 보이게 유지)
        phase = ThrustPhase.None;
    }

    // 찌르기 시작 각도(thrustAngle)를 고정한 채로 반지름만 바꿔서 위치/회전을 계산해 적용한다.
    private void ApplyLanceTransform(float radius)
    {
        float rad = thrustAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        transform.position = weaponAim.Pivot.position + offset;
        transform.rotation = Quaternion.Euler(0f, 0f, thrustAngle + weaponAim.visualRotationOffset);
    }

    // 창이 궤도 반지름 fromRadius에서 toRadius까지 움직인 동안 지나간 자리를 일정 간격으로 쪼개서 검사한다.
    private void SweepContact(float fromRadius, float toRadius)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(toRadius - fromRadius) / Mathf.Max(0.01f, contactStepDistance)));
        for (int i = 1; i <= steps; i++)
            CheckContactAt(Mathf.Lerp(fromRadius, toRadius, (float)i / steps));
    }

    // 창이 궤도 반지름 radius에 있을 때의 창 모양 상자(찌르는 방향으로 긴 직사각형)와 닿은 적을 때린다.
    // 상자 길이는 창 그림 길이 그대로라서 구리 강화로 창이 커지면 같이 커진다.
    private void CheckContactAt(float radius)
    {
        float rad = thrustAngle * Mathf.Deg2Rad;
        Vector2 center = (Vector2)weaponAim.Pivot.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;

        float length = WorldSpearLength();
        Vector2 boxSize = new Vector2(length, length * contactWidthRatio); // 상자 로컬 x축이 찌르는 방향

        foreach (var hit in MeleeContact.Touched(center, boxSize, thrustAngle))
            HitEnemy(hit, center);
    }

    // 에디터에서 접촉 판정 상자가 어떻게 생겼는지 보기 위한 표시 (Scene 뷰, 또는 Game 뷰의 Gizmos 켜기). 찌르는 동안만 그린다.
    void OnDrawGizmos()
    {
        if (!IsAttacking) return;

        float length = WorldSpearLength();
        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, 0f, thrustAngle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, length * contactWidthRatio, 0f));
    }

    private void HitEnemy(Collider2D hit, Vector2 spearCenter)
    {
        Enemy enemy = hit.GetComponent<Enemy>();
        if (enemy == null || hitThisThrust.Contains(enemy)) return;

        hitThisThrust.Add(enemy);

        Vector2 offset = (Vector2)hit.transform.position - spearCenter;
        Vector2 knockDir = offset.sqrMagnitude < 0.0001f ? Vector2.up : offset.normalized;

        bool wasDying = enemy.IsDying;
        enemy.Hit(knockDir, thrustDamage, thrustIsCrit);
        if (wasDying) return; // 이미 죽는 중이던 적은 효과 대상이 아니다

        RegisterHeatHit();                    // 철(공격력) 특수효과
        RefundCooldown(enemy);                // 기름(속도) 특수효과
        if (thrustIsCrit) TryExecute(enemy, knockDir); // 화학물질(치명타) 특수효과
    }

    // === 철(공격력) 특수효과: 달아오르는 창 ===
    private int MaxHeatStacks()
    {
        int tier = EffectTier(ResourceType.Iron);
        if (tier >= TierHigh) return Mathf.Max(0, heatMaxStacksTier10);
        if (tier >= TierLow) return Mathf.Max(0, heatMaxStacksTier5);
        return 0;
    }

    // 지금 쌓인 중첩에 따른 데미지 배율 (찌르기를 시작할 때 계산된다).
    private float HeatDamageMultiplier()
    {
        return 1f + heatDamagePerStack * Mathf.Min(heatStacks, MaxHeatStacks());
    }

    // 적을 맞혔을 때: 유지 시간을 갱신하고, 이번 찌르기에서 처음 맞힌 거라면 중첩을 하나 쌓는다 (하락 중이었다면 하락도 멈춘다).
    private void RegisterHeatHit()
    {
        int max = MaxHeatStacks();
        if (max <= 0) return;

        if (!heatGainedThisThrust)
        {
            heatGainedThisThrust = true;
            heatStacks = Mathf.Min(max, heatStacks + 1);
        }
        heatTimer = heatDuration;
        heatDecayTimer = 0f;
    }

    // 매 프레임: 유지 시간이 다 지나면 heatDecayInterval마다 중첩이 한 단계씩 내려가고, 중첩 수에 맞춰 창 색을 바꾼다.
    private void UpdateHeat(float dt)
    {
        int max = MaxHeatStacks();
        if (heatStacks > max) heatStacks = max;

        if (heatStacks > 0)
        {
            if (heatTimer > 0f)
            {
                heatTimer -= dt;
            }
            else
            {
                heatDecayTimer -= dt;
                if (heatDecayTimer <= 0f)
                {
                    heatStacks--;
                    heatDecayTimer += Mathf.Max(0.05f, heatDecayInterval); // 유지 시간이 끝난 순간 한 단계, 이후 간격마다 한 단계씩
                }
            }
        }

        ApplyHeatTint(max);
    }

    private void ApplyHeatTint(int max)
    {
        if (spriteRenderer == null) return;

        Color tint = max > 0 ? Color.Lerp(Color.white, heatColor, (float)heatStacks / max) : Color.white;
        if (tint == lastTint) return;
        lastTint = tint;
        spriteRenderer.color = tint;
    }

    // 찌르기 한 번에 같은 적은 한 번만 환급한다 (창에 맞든 충격파에 맞든 둘 다 맞든 1번).
    private void RefundCooldown(Enemy enemy)
    {
        int tier = EffectTier(ResourceType.Oil);
        if (tier < TierLow) return;
        if (!refundedEnemies.Add(enemy)) return;

        float perHit = tier >= TierHigh ? refundPerHitTier10 : refundPerHitTier5;
        float cap = tier >= TierHigh ? refundCapTier10 : refundCapTier5;

        float amount = Mathf.Min(perHit, cap - refundedThisThrust);
        if (amount <= 0f) return;

        refundedThisThrust += amount;
        attackCooldown -= amount;
    }

    // 치명타 찌르기에 맞고도 체력 비율이 기준 이하로 남은 적을 즉사시킨다. 정예/보스는 제외.
    private void TryExecute(Enemy enemy, Vector2 knockDir)
    {
        int tier = EffectTier(ResourceType.Chemical);
        if (tier < TierLow || enemy.IsDying || enemy.IsEliteOrBoss) return;

        float threshold = tier >= TierHigh ? executeThresholdTier10 : executeThresholdTier5;
        if (enemy.HealthRatio <= threshold) enemy.Execute(knockDir);
    }

    // 창이 끝까지 뻗은 순간 창끝 주변 원 안에 몸이 닿은 적에게 충격 피해 (구리 특수효과). 창이 이미 맞힌 적도 다시 맞을 수 있다.
    private void FireTipBurst()
    {
        int tier = EffectTier(ResourceType.Copper);
        if (tier < TierLow) return;

        bool high = tier >= TierHigh;
        float radius = high ? burstRadiusTier10 : burstRadiusTier5;
        float burstDamage = thrustDamage * (high ? burstDamageTier10 : burstDamageTier5);
        float knockback = high ? burstKnockbackTier10 : burstKnockbackTier5;

        float rad = thrustAngle * Mathf.Deg2Rad;
        Vector2 axis = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 tip = (Vector2)transform.position + axis * (WorldSpearLength() * 0.5f);

        CircleFlash.Spawn(tip, radius, burstColor, 0.2f, spriteRenderer);

        // 후보는 충돌체로 넉넉하게 뽑고(적 충돌 원이 크므로 +0.6), 실제로는 몸통 원이 충격 원에 닿은 적만 맞힌다.
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(tip, radius + 0.6f))
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null || enemy.IsDying) continue;

            if (enemy.TryGetBodyCircle(out Vector2 bodyCenter, out float bodyRadius))
            {
                if (Vector2.Distance(tip, bodyCenter) > radius + bodyRadius) continue;
            }
            else if (Vector2.Distance(tip, hit.ClosestPoint(tip)) > radius) continue;

            Vector2 dir = (Vector2)hit.transform.position - tip;
            if (dir.sqrMagnitude < 0.0001f) dir = axis;
            dir = dir.normalized;

            // 충격 피해도 창에 맞은 것과 똑같이 취급한다: 달아오름 중첩/연타 환급/급소 처형이 그대로 발동한다.
            enemy.Hit(dir, burstDamage, thrustIsCrit, knockback);
            RegisterHeatHit();
            RefundCooldown(enemy);
            if (thrustIsCrit) TryExecute(enemy, dir);
        }
    }
}
