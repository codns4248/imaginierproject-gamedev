using UnityEngine;
using UnityEngine.InputSystem;

// Pistol 전용 공격 스크립트. 좌클릭을 누르고 있으면 attackInterval마다 자동으로 투사체를 발사하고,
// 발사 순간 WeaponAim에 반동 모션을 재생시킨다.
// 조준 방향/회전은 같은 오브젝트의 WeaponAim이 담당하므로, 여기서는 "언제, 무엇을" 발사할지만 처리한다.
[RequireComponent(typeof(WeaponAim))]
public class PistolAttack : MonoBehaviour
{
    // 연속 발사 사이의 최소 간격(초). 이 시간이 지나기 전에는 좌클릭해도 발사되지 않는다.
    public float attackInterval = 0.3f;

    // 발사 순간 무기가 튀어 오르는 반동 각도(도 단위).
    public float recoilKickAngle = 25f;

    [Header("공격력 / 투사체")]
    // Pistol의 공격력. 나중에 무기 강화나 공격력을 올려주는 아이템 등으로 이 값을 바꾸는 식으로
    // 확장할 예정이라, 데미지 계산은 전부 이 값 하나만 참조하게 해뒀다.
    public float damage = 1f;
    public GameObject projectilePrefab;

    private WeaponAim weaponAim;

    // 다음 발사까지 남은 쿨다운 시간(초). 0 이하가 되어야 다시 발사할 수 있다.
    private float attackCooldown;

    void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
    }

    void Update()
    {
        attackCooldown -= Time.deltaTime;

        // leftButton.isPressed는 "눌려있는 동안 계속 true"이기 때문에,
        // 마우스를 꾹 누르고 있으면 쿨다운이 풀리는 즉시 자동으로 재발사되고(연사),
        // 아무리 빠르게 연타해도 attackCooldown이 0보다 클 때는 무시되어 attackInterval에 한 번으로 제한된다.
        if (Mouse.current.leftButton.isPressed && attackCooldown <= 0f)
        {
            attackCooldown = attackInterval;
            weaponAim.Kick(recoilKickAngle);
            Fire(weaponAim.AimDirection);
        }
    }

    // Pistol 위치에서 조준 방향으로 투사체를 하나 생성해서 날려보낸다.
    private void Fire(Vector2 direction)
    {
        if (projectilePrefab == null) return;

        GameObject projGO = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Projectile projectile = projGO.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Fire(direction, damage);
        }
    }
}
