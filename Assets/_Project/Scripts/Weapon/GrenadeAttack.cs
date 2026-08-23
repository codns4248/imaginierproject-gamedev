using UnityEngine;
using UnityEngine.InputSystem;

// Grenade 전용 공격 스크립트. 들고 있는 방식은 Pistol과 같지만(WeaponAim이 제자리에서 회전만 시킴),
// 공격은 투사체를 쏘는 대신 목표 지점까지 무기와 같은 모양의 투사체를 "던지는" 방식이다.
// 들고 있을 때는 마우스 위치로, 들고 있지 않을 때(자동공격)는 가장 가까운 적 위치로 던진다.
// 어느 쪽이든 플레이어로부터 maxRange(맵 타일 6개)를 넘어서는 위치는 그 방향으로 maxRange 거리까지만 날아간다.
[RequireComponent(typeof(WeaponAim))]
public class GrenadeAttack : MonoBehaviour
{
    public float attackInterval = 1f; // 연속 투척 사이의 최소 간격(초)
    public float maxRange = 6f;       // 플레이어 기준 최대 투척 거리(맵 타일 6개 = 6유닛). 자동공격 탐지 반경도 이 값을 그대로 쓴다.

    [Header("공격력 / 투사체")]
    public float damage = 1f;
    public GameObject grenadeProjectilePrefab;

    [Header("치명타")]
    public float critChance = 0.3f;
    public float critMultiplier = 1.5f;

    private WeaponAim weaponAim;
    private Camera mainCamera;
    private float attackCooldown;

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (EnemyManager.PlayerDead) return;

        attackCooldown -= Time.deltaTime;
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

        float finalDamage = CriticalHit.Roll(damage, critChance, critMultiplier, out bool isCrit);

        GameObject projGO = Instantiate(grenadeProjectilePrefab, transform.position, Quaternion.identity);
        GrenadeProjectile projectile = projGO.GetComponent<GrenadeProjectile>();
        if (projectile != null)
        {
            projectile.Launch(transform.position, targetPos, finalDamage, isCrit);
        }
    }
}
