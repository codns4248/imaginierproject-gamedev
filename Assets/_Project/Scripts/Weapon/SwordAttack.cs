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

    [Header("치명타")]
    // 이번 스윙이 치명타인지는 스윙 시작 시점에 한 번만 판정해서, 스윙 도중 맞는 모든 적에게 동일하게 적용한다.
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    private WeaponAim weaponAim;

    // 다음 공격까지 남은 쿨다운 시간(초).
    private float attackCooldown;

    private bool isSwinging;
    private float swingElapsed;
    private float swingCenterAngle; // 스윙을 시작한 순간의 목표 방향 각도 (스윙 내내 고정)
    private float swingDamage;      // 이번 스윙의 최종 데미지 (치명타면 이미 배율이 적용된 값)
    private bool swingIsCrit;       // 이번 스윙이 치명타인지

    // 이번 스윙 동안 이미 맞힌 적 목록 (같은 스윙에서 같은 적이 여러 프레임에 걸쳐 중복으로 맞지 않도록).
    private readonly HashSet<Enemy> hitThisSwing = new HashSet<Enemy>();

    // === IAutoMeleeWeapon ===
    // 궤도 반지름 + 판정 반지름 = 칼끝이 플레이어로부터 닿을 수 있는 최대 거리.
    // hitRadius나 orbitRadius가 강화로 바뀌면 이 값도 자동으로 같이 늘어난다.
    public float MaxReach => weaponAim.orbitRadius + hitRadius;
    public bool IsHeld => weaponAim.isHeld;
    public bool IsAttacking => isSwinging;
    public bool IsOnCooldown => attackCooldown > 0f;

    // === IEnhanceableWeapon ===
    private WeaponIdentity identity;
    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => WeaponEnhanceStore.GetLevel(identity.type, type);

    public void ApplyEnhance(ResourceType type)
    {
        if (!WeaponEnhanceStore.TryEnhance(identity.type, type)) return;
        ApplyStatDelta(type);
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

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
        identity = GetComponent<WeaponIdentity>();

        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes)
        {
            int level = WeaponEnhanceStore.GetLevel(identity.type, type);
            for (int i = 0; i < level; i++) ApplyStatDelta(type);
        }
    }

    void OnEnable()
    {
        MeleeAutoAttackQueue.Register(this);
    }

    void OnDisable()
    {
        MeleeAutoAttackQueue.Unregister(this);
    }

    void Update()
    {
        attackCooldown -= Time.deltaTime;

        // 들고 있을 때만 마우스 좌클릭으로 수동 발동한다. 자동 발동은 MeleeAutoAttackQueue가
        // TriggerAutoAttack()을 직접 호출해서 처리하므로 여기서는 신경 쓰지 않는다.
        if (weaponAim.isHeld && !isSwinging && Mouse.current.leftButton.isPressed && attackCooldown <= 0f)
        {
            StartSwing(weaponAim.AimDirection);
        }

        if (isSwinging)
        {
            UpdateSwing();
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
        hitThisSwing.Clear();
        swingDamage = CriticalHit.Roll(damage, critChance, critMultiplier, out swingIsCrit);

        // 이 순간부터 WeaponAim의 자동 회전/위치 갱신을 멈추고 이 스크립트가 직접 제어한다.
        weaponAim.externalControl = true;
        weaponAim.SetForceVisible(true); // 들고 있지 않아도 공격하는 동안은 보이게 한다

        swingCenterAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    private void UpdateSwing()
    {
        swingElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(swingElapsed / swingDuration);

        // 목표 방향(중앙각)을 기준으로 +half(위) ~ -half(아래) 사이를 시간에 따라 훑는다.
        float half = swingAngle * 0.5f;
        float currentAngle = Mathf.Lerp(swingCenterAngle + half, swingCenterAngle - half, t);

        ApplySwordTransform(currentAngle);
        CheckHit();

        if (t >= 1f)
        {
            isSwinging = false;
            weaponAim.externalControl = false;
            weaponAim.SetForceVisible(false); // 공격이 끝나면 다시 숨긴다 (들고 있는 중이면 WeaponAim이 계속 보이게 유지)
        }
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
            enemy.Hit(knockDir.normalized, swingDamage, swingIsCrit);
        }
    }
}
