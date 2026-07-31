using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // 초당 이동 속도(유닛/초). Inspector에서 조절 가능.
    public float moveSpeed = 5f;

    // 맵 경계 (SurvivorMapBuilder의 MapHalfExtent와 맞춰야 함: 현재 맵은 -20 ~ 20 범위의 40x40 유닛 정사각형).
    public float mapHalfExtent = 20f;

    // 캐릭터가 맵 가장자리 타일 끝에 딱 붙어 파고들지 않도록 살짝 여유를 두는 값.
    public float boundaryMargin = 0.5f;

    private Rigidbody2D rb;              // 물리 기반 이동에 사용 (MovePosition으로 밀어줌)
    private Vector2 movement;             // 이번 프레임의 이동 입력 방향 (정규화된 -1~1 범위)
    private SpriteRenderer spriteRenderer; // 좌우 반전(flipX) 제어용
    private Animator animator;            // Speed / IsDead 파라미터로 애니메이션 상태 전환
    private Camera mainCamera;            // 마우스 스크린 좌표 -> 월드 좌표 변환에 필요
    private bool IsDead = false;          // 죽음 처리 이후 모든 입력/이동을 막기 위한 내부 플래그

    void Start()
    {
        // GetComponent는 비용이 있으므로 매 프레임 호출하지 않고 시작 시 한 번만 캐싱해둔다.
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 죽은 상태면 아래 입력 처리/애니메이션 갱신을 전부 건너뛴다.
        if (IsDead) return;

        // WASD 입력을 -1/0/1 값으로 변환해 이동 벡터를 만든다.
        // A/D가 X축, W/S가 Y축을 담당하며, 대각선 이동 시 속도가 더 빨라지지 않도록 normalized로 크기를 1로 맞춘다.
        movement = new Vector2(
            Keyboard.current.dKey.isPressed ? 1 : Keyboard.current.aKey.isPressed ? -1 : 0,
            Keyboard.current.wKey.isPressed ? 1 : Keyboard.current.sKey.isPressed ? -1 : 0
        ).normalized;

        // 마우스 위치 기준 좌우 반전
        // Mouse.current.position은 화면(스크린) 좌표라서, 캐릭터의 월드 좌표와 비교하려면 먼저 월드 좌표로 변환해야 한다.
        // ScreenToWorldPoint의 z값은 "카메라로부터 얼마나 떨어진 평면인가"를 의미하는데,
        // 카메라가 (0,0,-10)에 있고 캐릭터가 z=0 평면에 있으므로 거리는 -mainCamera.transform.position.z(=10)가 된다.
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(screenPos);

        // 이동 방향이 아니라 "마우스가 캐릭터 기준 어느 쪽에 있는가"로 좌우를 결정한다.
        if (mouseWorldPos.x > transform.position.x)
            spriteRenderer.flipX = false; // 마우스가 오른쪽 -> 원래 방향(반전 없음)
        else if (mouseWorldPos.x < transform.position.x)
            spriteRenderer.flipX = true; // 마우스가 왼쪽 -> 스프라이트를 좌우 반전

        // 이동 벡터의 크기(0~1)를 Animator에 넘겨서 idle <-> move 애니메이션 상태를 전환한다.
        // (Player.controller에서 Speed > 0.01이면 move, 아니면 stand로 트랜지션 되도록 설정되어 있음)
        animator.SetFloat("Speed", movement.magnitude);
    }

    void FixedUpdate()
    {
        // 물리 연산(Rigidbody2D)과 관련된 이동은 프레임 속도와 무관하게 일정한 주기로 도는 FixedUpdate에서 처리해야
        // 프레임레이트가 들쭉날쭉해도 이동 속도가 흔들리지 않는다.
        if (IsDead) return;

        // rb.MovePosition은 Transform.position을 직접 바꾸는 대신 물리 엔진에게 "다음 스텝에 여기로 옮겨줘"라고
        // 요청하는 방식이라, 벽 등 다른 콜라이더와의 충돌 처리가 자연스럽게 유지된다.
        Vector2 nextPosition = rb.position + movement * moveSpeed * Time.fixedDeltaTime;

        // 맵 밖으로 못 나가게 좌표를 경계 안으로 강제로 눌러준다.
        // 콜라이더로 벽을 세우는 대신 코드로 직접 클램프하면, 아무리 빠른 속도로 부딪혀도 벽을 뚫고 나가는(터널링)
        // 문제 없이 항상 확실하게 경계 안에 머무른다.
        float limit = mapHalfExtent - boundaryMargin;
        nextPosition.x = Mathf.Clamp(nextPosition.x, -limit, limit);
        nextPosition.y = Mathf.Clamp(nextPosition.y, -limit, limit);

        rb.MovePosition(nextPosition);
    }

    // 외부(적 공격 등)에서 캐릭터를 죽은 상태로 전환할 때 호출하는 함수.
    public void Die()
    {
        if (IsDead) return; // 이미 죽은 상태면 중복 실행 방지

        IsDead = true;
        movement = Vector2.zero;       // 남아있던 이동 입력을 즉시 0으로
        animator.SetFloat("Speed", 0f); // idle 프레임으로 고정
        animator.SetBool("IsDead", true); // Player.controller의 AnyState -> dead 트랜지션을 발동시킴
    }
}
