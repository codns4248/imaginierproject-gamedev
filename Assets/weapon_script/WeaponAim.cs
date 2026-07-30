using UnityEngine;
using UnityEngine.InputSystem;

// 캐릭터 손에 들린 무기(권총)를 마우스 방향으로 회전시키고, 좌클릭 시 발사 반동 모션과 공격속도 제한을 처리한다.
// 이 오브젝트는 Player의 자식으로 배치되어 있고, 스프라이트의 피벗(회전축)이 총의 손잡이 부분에 맞춰져 있어서
// 여기서 transform을 회전시키면 손잡이는 캐릭터 중심에 고정된 채 총구(오른쪽 끝)만 마우스 쪽으로 돌아간다.
public class WeaponAim : MonoBehaviour
{
    // 발사 시 순간적으로 튀어 오르는 반동 각도(도 단위). 클수록 반동이 커 보인다.
    public float recoilKickAngle = 25f;

    // 반동이 원래 각도로 돌아오는 속도(초당 도). 클수록 더 빨리 원위치로 돌아온다.
    public float recoilRecoverySpeed = 200f;

    // 연속 발사 사이의 최소 간격(초). 이 시간이 지나기 전에는 좌클릭해도 발사되지 않는다.
    public float attackInterval = 0.3f;

    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;

    // 현재 남아있는 반동 각도. 발사 순간 recoilKickAngle로 세팅되고, 매 프레임 0을 향해 서서히 줄어든다.
    private float recoilAngle;

    // 다음 발사까지 남은 쿨다운 시간(초). 0 이하가 되어야 다시 발사할 수 있다.
    private float attackCooldown;

    void Start()
    {
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 매 프레임 쿨다운을 줄여나간다.
        attackCooldown -= Time.deltaTime;

        // leftButton.isPressed는 "눌려있는 동안 계속 true"이기 때문에,
        // 마우스를 꾹 누르고 있으면 쿨다운이 풀리는 즉시 자동으로 재발사되고(연사),
        // 아무리 빠르게 연타해도 attackCooldown이 0보다 클 때는 무시되어 0.3초에 한 번으로 제한된다.
        if (Mouse.current.leftButton.isPressed && attackCooldown <= 0f)
        {
            recoilAngle = recoilKickAngle; // 반동 시작 (즉시 최대 각도로 튐)
            attackCooldown = attackInterval; // 다음 발사 가능 시점까지 쿨다운 설정
        }

        // 반동 각도를 매 프레임 0으로 서서히 되돌린다 (MoveTowards는 목표를 넘어서지 않는 선형 감소).
        recoilAngle = Mathf.MoveTowards(recoilAngle, 0f, recoilRecoverySpeed * Time.deltaTime);

        // 마우스의 스크린 좌표를 월드 좌표로 변환한다.
        // z에는 카메라와 무기(z=0 평면) 사이의 거리를 넣어줘야 정확한 위치가 나온다 (Player_Movement.cs와 동일한 방식).
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(screenPos);

        // 무기 위치에서 마우스 위치를 향하는 방향 벡터의 각도(도 단위)를 계산한다.
        // Atan2(y, x)는 (1,0) 방향을 0도로 놓고 반시계 방향으로 각도가 증가하는 표준적인 2D 각도 계산 방식이다.
        Vector3 dir = mouseWorldPos - transform.position;
        float aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 마우스가 왼쪽에 있을 때(각도가 90도를 넘어감) 총 스프라이트를 그대로 회전시키면 위아래가 뒤집힌 것처럼
        // 보이므로, flipY로 세로 방향을 보정해서 항상 자연스러운 모습을 유지한다.
        bool facingLeft = Mathf.Abs(aimAngle) > 90f;
        spriteRenderer.flipY = facingLeft;

        // 반동 각도를 그대로 더하면 flipY로 뒤집힌 상태에서는 반동이 아래로 튀는 것처럼 보이게 된다.
        // 따라서 왼쪽을 볼 때는 반동 부호를 반대로 뒤집어서, 좌우 어느 쪽을 보든 항상 "위로" 튀어 보이도록 만든다.
        float kickSign = facingLeft ? -1f : 1f;

        // 최종 회전 = 마우스를 향한 조준 각도 + (부호가 보정된) 반동 각도.
        transform.rotation = Quaternion.Euler(0f, 0f, aimAngle + recoilAngle * kickSign);
    }
}
