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

    private WeaponAim weaponAim;

    // 다음 발사까지 남은 쿨다운 시간(초). 0 이하가 되어야 다시 발사할 수 있다.
    private float attackCooldown;

    // === IEnhanceableWeapon ===
    // 강화 레벨 자체는 WeaponEnhanceStore(무기 이름 기준, 씬이 바뀌어도 유지)에 저장된다.
    // 이 인스턴스는 그 레벨을 실제 스탯(damage 등)에 반영하는 역할만 한다.
    private float projectileSpeedBonus; // 기름(발사속도) 강화 누적분. Fire()에서 Projectile.speed에 더해준다.
    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => WeaponEnhanceStore.GetLevel(gameObject.name, type);

    // 거점 강화 UI에서 자원을 소모하고 호출한다. 스토어에 기록 + 스탯 한 단계 적용.
    public void ApplyEnhance(ResourceType type)
    {
        if (!WeaponEnhanceStore.TryEnhance(gameObject.name, type)) return;
        ApplyStatDelta(type);
    }

    // 스탯 한 단계분을 실제 필드에 반영한다. ApplyEnhance(구매 시)와 Awake의 재적용(로드 시) 둘 다에서 쓰인다.
    private void ApplyStatDelta(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: attackInterval = Mathf.Max(0.05f, attackInterval - 0.02f); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: autoAttackRange += 0.5f; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
            case ResourceType.Oil: projectileSpeedBonus += 2f; break;
        }
    }

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();

        // 다른 씬에서 저장된 강화 레벨만큼, 이 새 인스턴스의 스탯에 다시 적용한다(재생).
        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes)
        {
            int level = WeaponEnhanceStore.GetLevel(gameObject.name, type);
            for (int i = 0; i < level; i++) ApplyStatDelta(type);
        }
    }

    void Update()
    {
        if (EnemyManager.PlayerDead) return; // 플레이어가 죽으면 자동/수동 상관없이 더 이상 발사하지 않는다

        attackCooldown -= Time.deltaTime;
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

    // Pistol 위치에서 주어진 방향으로 투사체를 하나 생성해서 날려보낸다.
    private void Fire(Vector2 direction)
    {
        if (projectilePrefab == null) return;

        float finalDamage = CriticalHit.Roll(damage, critChance, critMultiplier, out bool isCrit);

        GameObject projGO = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Projectile projectile = projGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.speed += projectileSpeedBonus;
            projectile.Fire(direction, finalDamage, isCrit);
        }
    }
}
