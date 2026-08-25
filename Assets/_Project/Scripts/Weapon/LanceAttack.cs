using System.Collections;
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
[RequireComponent(typeof(WeaponAim))]
public class LanceAttack : MonoBehaviour, IAutoMeleeWeapon, IEnhanceableWeapon
{
    // 연속 공격 사이의 최소 간격(초).
    public float attackInterval = 0.4f;

    [Header("찌르기 모션")]
    public float thrustDistance = 1.2f;   // 평소 궤도 반지름보다 얼마나 더 멀리 찔러 나갈지
    public float thrustOutDuration = 0.1f; // 앞으로 찔러 나가는 데 걸리는 시간
    public float thrustBackDuration = 0.15f; // 다시 원래 자리로 돌아오는 데 걸리는 시간

    [Header("공격력 / 판정 범위")]
    public float damage = 1f;
    public float hitRadius = 0.8f; // 목표 방향(찌르는 방향) 기준 판정 범위 (세로 방향)
    public float hitWidthScale = 0.8f; // 찌르는 방향에 수직인 폭은 hitRadius의 이 배율만큼만 사용 (가로를 좁혀서 타원형 판정)

    [Header("치명타")]
    // 이번 찌르기가 치명타인지는 시작 시점에 한 번만 판정해서, 찌르는 동안 맞는 모든 적에게 동일하게 적용한다.
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    private WeaponAim weaponAim;
    private float attackCooldown;

    private bool isThrusting;
    private float restRadius;   // 찌르기 시작 시점의 평소 궤도 반지름 (WeaponAim.orbitRadius)
    private float thrustAngle;  // 찌르기 시작 시점의 목표 방향 각도 (찌르는 동안 고정)
    private float thrustDamage; // 이번 찌르기의 최종 데미지 (치명타면 이미 배율이 적용된 값)
    private bool thrustIsCrit;  // 이번 찌르기가 치명타인지

    private readonly HashSet<Enemy> hitThisThrust = new HashSet<Enemy>();

    // === IAutoMeleeWeapon ===
    // 평소 궤도 반지름 + 찌르는 거리 + 판정 반지름 = 창끝이 플레이어로부터 닿을 수 있는 최대 거리.
    // 관련 스탯이 강화로 바뀌면 이 값도 자동으로 같이 늘어난다.
    public float MaxReach => weaponAim.orbitRadius + thrustDistance + hitRadius;
    public bool IsHeld => weaponAim.isHeld;
    public bool IsAttacking => isThrusting;
    public bool IsOnCooldown => attackCooldown > 0f;

    // === IEnhanceableWeapon ===
    private readonly int[] enhanceLevels = new int[4];
    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => enhanceLevels[WeaponEnhanceUtil.IndexOf(type)];

    public void ApplyEnhance(ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0 || enhanceLevels[idx] >= MaxEnhanceLevel) return;
        enhanceLevels[idx]++;

        switch (type)
        {
            case ResourceType.Wood: attackInterval = Mathf.Max(0.1f, attackInterval - 0.02f); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: hitRadius += 0.1f; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
        }
    }

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
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
        if (weaponAim.isHeld && !isThrusting && Mouse.current.leftButton.isPressed && attackCooldown <= 0f)
        {
            StartThrust(weaponAim.AimDirection);
        }
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
        StartCoroutine(ThrustRoutine(dir));
    }

    private IEnumerator ThrustRoutine(Vector2 dir)
    {
        isThrusting = true;
        hitThisThrust.Clear();
        thrustDamage = CriticalHit.Roll(damage, critChance, critMultiplier, out thrustIsCrit);
        weaponAim.externalControl = true; // 찌르는 동안은 WeaponAim의 자동 궤도 추적을 잠깐 끈다
        weaponAim.SetForceVisible(true);  // 들고 있지 않아도 공격하는 동안은 보이게 한다

        restRadius = weaponAim.orbitRadius;
        thrustAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        float outTarget = restRadius + thrustDistance;

        // 1) 앞으로 찔러 나간다.
        float elapsed = 0f;
        while (elapsed < thrustOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / thrustOutDuration);
            ApplyLanceTransform(Mathf.Lerp(restRadius, outTarget, t));
            CheckHit();
            yield return null;
        }

        // 2) 다시 원래 거리로 돌아온다.
        elapsed = 0f;
        while (elapsed < thrustBackDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / thrustBackDuration);
            ApplyLanceTransform(Mathf.Lerp(outTarget, restRadius, t));
            yield return null;
        }

        ApplyLanceTransform(restRadius);
        weaponAim.externalControl = false; // 다시 WeaponAim에게 조준을 맡긴다
        weaponAim.SetForceVisible(false);  // 공격이 끝나면 다시 숨긴다 (들고 있는 중이면 WeaponAim이 계속 보이게 유지)
        isThrusting = false;
    }

    // 찌르기 시작 각도(thrustAngle)를 고정한 채로 반지름만 바꿔서 위치/회전을 계산해 적용한다.
    private void ApplyLanceTransform(float radius)
    {
        float rad = thrustAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        transform.position = weaponAim.Pivot.position + offset;
        transform.rotation = Quaternion.Euler(0f, 0f, thrustAngle + weaponAim.visualRotationOffset);
    }

    // 찌르는 방향으로 긴 타원형 판정. 원형으로 넉넉하게 후보를 추린 뒤, 찌르는 방향(along)과
    // 그 수직 방향(across) 성분으로 나눠서 타원 범위(along/hitRadius)^2 + (across/가로반지름)^2 <= 1 안에
    // 있는 경우만 실제로 맞은 것으로 처리한다.
    private void CheckHit()
    {
        float acrossRadius = hitRadius * hitWidthScale;
        float boundingRadius = Mathf.Max(hitRadius, acrossRadius);

        float rad = thrustAngle * Mathf.Deg2Rad;
        Vector2 alongDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 acrossDir = new Vector2(-alongDir.y, alongDir.x);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, boundingRadius);
        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null || hitThisThrust.Contains(enemy)) continue;

            Vector2 offset = (Vector2)hit.transform.position - (Vector2)transform.position;
            float along = Vector2.Dot(offset, alongDir);
            float across = Vector2.Dot(offset, acrossDir);

            float normalized = (along * along) / (hitRadius * hitRadius) + (across * across) / (acrossRadius * acrossRadius);
            if (normalized > 1f) continue; // 타원 범위 밖

            hitThisThrust.Add(enemy);

            Vector2 knockDir = offset.sqrMagnitude < 0.0001f ? Vector2.up : offset.normalized;
            enemy.Hit(knockDir, thrustDamage, thrustIsCrit);
        }
    }
}
