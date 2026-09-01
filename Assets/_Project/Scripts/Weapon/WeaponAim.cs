using UnityEngine;
using UnityEngine.InputSystem;

// 무기가 항상 마우스 방향을 바라보도록 회전시키는 공용 컴포넌트.
// orbitRadius가 0이면 Pistol처럼 부모(플레이어)에 고정된 위치에서 제자리 회전만 한다.
// orbitRadius가 0보다 크면 Sword처럼 부모를 중심으로 한 원 궤도 위의 "마우스 방향 지점"에
// 실제로 위치까지 이동시킨다 (플레이어-무기-마우스가 항상 일직선이 되도록).
// 실제 공격(발사/휘두르기 등 무기별로 다른 동작)은 같은 오브젝트의 다른 스크립트
// (PistolAttack, SwordAttack 등)가 따로 처리한다.
//
// isHeld(들고 있는 중인지)는 WeaponSwitcher가 매 프레임 갱신한다. 들고 있지 않은 무기도
// 자동공격을 위해 오브젝트/스크립트 자체는 계속 켜져 있지만, 이 컴포넌트는 마우스 추적을 멈추고
// (공격 스크립트가 필요할 때 직접 위치/회전을 제어한다) 평소엔 보이지 않는다.
public class WeaponAim : MonoBehaviour
{
    [Header("피격/발사 모션")]
    // 공격 스크립트가 Kick()을 호출하면 이 속도로 원래 각도까지 서서히 돌아온다. (Pistol 반동용)
    public float recoilRecoverySpeed = 200f;

    [Header("스프라이트 기본 방향 보정")]
    // 원본 이미지가 오른쪽(0도)을 바라보는 모양이 아닐 때(예: Sword_0은 세로로 서 있는 칼) 쓰는 보정 각도.
    // 조준 각도에 그대로 더해지는 값이라 원본 스프라이트/투사체에는 영향을 주지 않는다.
    public float visualRotationOffset = 0f;

    [Header("궤도 설정 (0이면 Pistol처럼 제자리 고정, 0보다 크면 Sword처럼 궤도를 따라 이동)")]
    // 부모(플레이어)로부터 얼마나 떨어진 원 궤도 위에 위치할지.
    public float orbitRadius = 0f;

    [Header("좌우 반전 여부")]
    // Pistol처럼 마우스가 왼쪽에 있을 때 스프라이트를 세로로 뒤집어야 자연스러운 무기는 true.
    // Sword처럼 어느 방향을 보든 이미지가 항상 같은 모습이어야 하는 무기는 false로 꺼둔다.
    public bool allowFlip = true;

    // true인 동안에는 이 컴포넌트가 위치/회전을 건드리지 않는다. SwordAttack처럼 직접 회전/이동을
    // 커스텀 모션(휘두르기 등)으로 제어해야 하는 공격 스크립트가 켜고 끈다.
    [HideInInspector] public bool externalControl = false;

    // 지금 플레이어가 손에 들고 마우스로 조종 중인 무기인지. WeaponSwitcher가 슬롯을 바꿀 때마다 갱신한다.
    public bool isHeld = false;

    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private Transform pivot; // 궤도의 중심점 = 부모(Player) Transform
    private bool forceVisible; // 자동공격 스크립트가 "지금 이 순간만은 보여줘"라고 요청할 때 true

    // 현재 남아있는 킥(반동) 각도. Kick()으로 세팅되고 매 프레임 0을 향해 줄어든다.
    private float kickAngle;

    // 무기(궤도 모드일 땐 부모) 위치에서 마우스를 향하는 방향. 공격 스크립트가 발사/판정 방향으로 사용한다.
    public Vector2 AimDirection { get; private set; }

    // 궤도의 중심점(보통 Player). 궤도를 직접 계산해야 하는 공격 스크립트(SwordAttack)가 참조한다.
    public Transform Pivot => pivot;

    void Start()
    {
        mainCamera = Camera.main;
        spriteRenderer = GetComponent<SpriteRenderer>();
        pivot = transform.parent != null ? transform.parent : transform;

        // Update()가 아직 한 번도 안 돈 상태에서 다른 스크립트(PistolAttack 등)가 같은 프레임에
        // AimDirection을 먼저 읽어버리면 기본값(0,0)인 채로 발사되어, 투사체가 방향 없이
        // 제자리에 멈춰버리는 버그가 있었다(특히 스테이지 전환 직후 새 무기가 막 생성된 순간).
        // 스크립트 실행 순서는 보장되지 않으므로, Start()에서 미리 한 번 계산해서 방지한다.
        RecalculateAimDirection();
    }

    void Update()
    {
        // 들고 있는 중이거나(항상 보임), 자동공격 스크립트가 공격 연출 중이라 강제로 보여달라고
        // 요청한 경우에만 스프라이트를 그린다. 그 외(자동공격 대기 중)에는 숨긴다.
        if (spriteRenderer != null) spriteRenderer.enabled = isHeld || forceVisible;

        // 들고 있지 않은 무기는 여기서 더 이상 처리하지 않는다. 자동공격 스크립트가 필요할 때
        // (근접무기는 공격 순간에만) transform.position/rotation을 직접 제어한다.
        if (!isHeld) return;

        // Time.timeScale = 0이어도 Update()는 계속 호출되고, 여기서는 deltaTime이 아니라
        // 마우스 좌표를 직접 읽어 회전/위치를 갱신하므로 일시정지 중에는 별도로 멈춰줘야 한다.
        if (PauseManager.IsPaused) return;

        RecalculateAimDirection();
        Vector2 dir = AimDirection;

        // 외부(SwordAttack 등)가 휘두르기 같은 커스텀 모션으로 위치/회전을 직접 제어하는 중이면
        // 조준 방향 계산만 갱신해주고 나머지(회전/위치 적용)는 건드리지 않는다.
        if (externalControl) return;

        float aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        // 킥 각도를 매 프레임 0으로 서서히 되돌린다 (MoveTowards는 목표를 넘어서지 않는 선형 감소).
        kickAngle = Mathf.MoveTowards(kickAngle, 0f, recoilRecoverySpeed * Time.deltaTime);

        // 마우스가 왼쪽에 있을 때(각도가 90도를 넘어감) 스프라이트를 그대로 회전시키면 위아래가 뒤집힌 것처럼
        // 보이므로, allowFlip인 무기는 flipY로 세로 방향을 보정해서 항상 자연스러운 모습을 유지한다.
        // allowFlip이 꺼져있으면(Sword) 항상 원본 그대로 두고, 킥 방향도 뒤집지 않는다.
        bool facingLeft = allowFlip && Mathf.Abs(aimAngle) > 90f;
        spriteRenderer.flipY = facingLeft;

        // 킥 각도를 그대로 더하면 flipY로 뒤집힌 상태에서는 반대 방향으로 튀는 것처럼 보이게 된다.
        // 따라서 왼쪽을 볼 때는 부호를 반대로 뒤집어서, 좌우 어느 쪽을 보든 항상 같은 방향으로 튀어 보이게 한다.
        float kickSign = facingLeft ? -1f : 1f;
        float finalAngle = aimAngle + kickAngle * kickSign;

        // 궤도 모드: 킥까지 반영된 최종 각도로 위치 자체를 궤도 위로 옮긴다.
        if (orbitRadius > 0f)
        {
            float rad = finalAngle * Mathf.Deg2Rad;
            Vector3 orbitOffset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * orbitRadius;
            transform.position = pivot.position + orbitOffset;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, finalAngle + visualRotationOffset);
    }

    // 마우스의 스크린 좌표를 월드 좌표로 변환해서 AimDirection을 갱신한다.
    // 궤도 모드일 때는 궤도 중심(플레이어) 기준으로 계산해야 무기가 궤도 반지름을 벗어나지 않고
    // 안정적으로 마우스를 따라간다. 궤도 모드가 아니면(Pistol) 무기 자신의 위치 기준으로 계산한다.
    private void RecalculateAimDirection()
    {
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(screenPos);

        Vector3 originPoint = orbitRadius > 0f ? pivot.position : transform.position;
        AimDirection = mouseWorldPos - originPoint;
    }

    // 공격 스크립트가 발사 순간 호출한다. 무기가 즉시 angle만큼 튀었다가 서서히 원래 각도로 돌아온다. (Pistol 반동용)
    public void Kick(float angle)
    {
        kickAngle = angle;
    }

    // WeaponSwitcher가 슬롯을 바꿀 때마다 모든 슬롯에 대해 호출해서 "지금 손에 들고 있는지"를 갱신한다.
    public void SetHeld(bool held)
    {
        isHeld = held;
    }

    // 자동공격 스크립트가 공격 연출(등장~공격~소멸)을 진행하는 동안 강제로 보이게 하고 싶을 때 사용한다.
    public void SetForceVisible(bool visible)
    {
        forceVisible = visible;
    }
}
