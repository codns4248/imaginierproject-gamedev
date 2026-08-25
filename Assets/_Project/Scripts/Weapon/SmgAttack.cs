using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Smg 전용 공격 스크립트. Pistol과 거의 동일하지만(같은 Projectile 프리팹, 조준 방식) 두 가지가 다르다:
// 1) 한 번 발사되면 0.2초 간격으로 3발이 나가는 3점사이고, 3점사가 끝나면 잠깐 쉬었다가 다시 쏠 수 있다.
// 2) 여기서 쓰는 투사체는 적을 관통하지 않는다(한 마리만 맞고 사라짐).
// 들고 있지 않을 때는 자동공격 모드로 전환되어, autoAttackRange 안의 가장 가까운 적을 향해
// 같은 3점사 사이클을 끊임없이 반복한다 (무기 자체는 보이지 않고 투사체만 나간다).
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

    private WeaponAim weaponAim;
    private bool isFiring; // 3점사 + 휴식 사이클이 진행 중이면 true (이 동안은 새 사이클을 시작하지 않는다)

    // === IEnhanceableWeapon ===
    private readonly int[] enhanceLevels = new int[5];
    private float projectileSpeedBonus; // 기름(발사속도) 강화 누적분. Fire()에서 Projectile.speed에 더해준다.
    public int MaxEnhanceLevel => WeaponEnhanceUtil.MaxLevel;
    public int GetEnhanceLevel(ResourceType type) => enhanceLevels[WeaponEnhanceUtil.IndexOf(type)];

    public void ApplyEnhance(ResourceType type)
    {
        int idx = WeaponEnhanceUtil.IndexOf(type);
        if (idx < 0 || enhanceLevels[idx] >= MaxEnhanceLevel) return;
        enhanceLevels[idx]++;

        switch (type)
        {
            case ResourceType.Wood: burstInterval = Mathf.Max(0.05f, burstInterval - 0.015f); break;
            case ResourceType.Iron: damage += 0.3f; break;
            case ResourceType.Copper: autoAttackRange += 0.5f; break;
            case ResourceType.Chemical: critChance = Mathf.Min(1f, critChance + 0.05f); break;
            case ResourceType.Oil: projectileSpeedBonus += 2f; break;
        }
    }

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
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

    private IEnumerator BurstRoutine(bool auto)
    {
        isFiring = true;

        for (int i = 0; i < burstCount; i++)
        {
            Vector2? direction = auto ? FindAutoDirection() : weaponAim.AimDirection;
            if (direction.HasValue)
            {
                weaponAim.Kick(recoilKickAngle);
                Fire(direction.Value);
            }

            // 마지막 발 이후에는 점사 간격 대신 아래의 휴식 시간을 기다린다.
            if (i < burstCount - 1)
                yield return new WaitForSeconds(burstInterval);
        }

        yield return new WaitForSeconds(restDuration);
        isFiring = false;
    }

    // 자동 모드에서 현재 가장 가까운 적을 향한 방향을 구한다. 사거리 안에 적이 없으면 null.
    private Vector2? FindAutoDirection()
    {
        Enemy target = EnemyManager.FindNearest(transform.position, autoAttackRange);
        if (target == null) return null;
        return (Vector2)target.transform.position - (Vector2)transform.position;
    }

    private void Fire(Vector2 direction)
    {
        if (projectilePrefab == null) return;

        float finalDamage = CriticalHit.Roll(damage, critChance, critMultiplier, out bool isCrit);

        GameObject projGO = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Projectile projectile = projGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.maxHits = 1; // Smg 투사체는 관통하지 않고 첫 번째로 맞은 적에게서 사라진다.
            projectile.speed += projectileSpeedBonus;
            projectile.Fire(direction, finalDamage, isCrit);
        }
    }
}
