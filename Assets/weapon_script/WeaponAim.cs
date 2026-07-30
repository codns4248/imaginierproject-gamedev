using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAim : MonoBehaviour
{
    public float recoilKickAngle = 25f;
    public float recoilRecoverySpeed = 200f;
    public float attackInterval = 0.3f;

    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private float recoilAngle;
    private float attackCooldown;

    void Start()
    {
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        attackCooldown -= Time.deltaTime;
        if (Mouse.current.leftButton.isPressed && attackCooldown <= 0f)
        {
            recoilAngle = recoilKickAngle;
            attackCooldown = attackInterval;
        }

        recoilAngle = Mathf.MoveTowards(recoilAngle, 0f, recoilRecoverySpeed * Time.deltaTime);

        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(screenPos);

        Vector3 dir = mouseWorldPos - transform.position;
        float aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 마우스가 왼쪽에 있을 때 총이 위아래로 뒤집혀 보이지 않도록 보정
        bool facingLeft = Mathf.Abs(aimAngle) > 90f;
        spriteRenderer.flipY = facingLeft;

        // 반동은 좌우 반전과 무관하게 항상 위로 튀어 보이도록 부호 보정
        float kickSign = facingLeft ? -1f : 1f;
        transform.rotation = Quaternion.Euler(0f, 0f, aimAngle + recoilAngle * kickSign);
    }
}
