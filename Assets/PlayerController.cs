using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// 플레이어 캐릭터를 제어하는 스크립트.
/// 이동, 마우스 방향 바라보기, 체력 관리, 피격/무적 처리, 사망 처리를 담당한다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    // 플레이어 이동 속도
    public float speed = 5f;

    [Header("Health")]
    // 최대 체력 (현재 하트 5칸 기준)
    public int maxHealth = 5;
    // 적과 닿았다고 판정하는 거리 (반경)
    public float contactRadius = 0.5f;
    // 피격 후 무적 지속 시간 (초)
    public float invincibilityDuration = 0.5f;
    // 사망 시 표시할 스프라이트 (Farmer 0.png의 Dead 0)
    public Sprite deadSprite;

    // 현재 체력
    private int currentHealth;
    // 사망 여부 - true이면 모든 조작 및 피격 판정이 중단됨
    private bool isDead = false;
    // 무적 여부 - true인 동안에는 피격 판정이 발생하지 않음
    private bool isInvincible = false;

    // 플레이어가 이동 가능한 맵 경계값
    private float minX = -24.5f;
    private float maxX = 24.5f;
    private float minY = -14.45f;
    private float maxY = 14.45f;

    // 스프라이트 렌더러 (방향 전환 및 색상 변경에 사용)
    private SpriteRenderer spriteRenderer;
    // 애니메이터 (이동/대기 애니메이션 전환에 사용)
    private Animator animator;

    void Awake()
    {
        // 컴포넌트 참조를 초기화하고 체력을 최대값으로 설정
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;
    }

    void Start()
    {
        // 게임 시작 시 GameManager의 HP UI를 현재 체력으로 초기화
        if (GameManager.Instance != null)
            GameManager.Instance.UpdateHP(currentHealth);
    }

    void Update()
    {
        // 사망 상태이면 모든 처리를 중단
        if (isDead) return;

        HandleMovement();
        HandleFacing();

        // 무적 상태가 아닐 때만 적 접촉 판정
        if (!isInvincible)
            CheckEnemyContact();
    }

    /// <summary>
    /// WASD 키 입력을 받아 플레이어를 이동시키고, 이동 여부에 따라 애니메이션을 전환한다.
    /// 맵 경계를 벗어나지 않도록 위치를 클램핑한다.
    /// </summary>
    void HandleMovement()
    {
        Vector2 moveInput = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
        }

        // 이동 중이면 Run 애니메이션, 정지 중이면 Idle 애니메이션
        bool isMoving = moveInput != Vector2.zero;
        if (animator != null)
            animator.SetBool("isMoving", isMoving);

        // 대각선 이동 시 속도가 빨라지지 않도록 normalized로 방향 벡터를 정규화
        Vector3 move = new Vector3(moveInput.x, moveInput.y, 0f).normalized * speed * Time.deltaTime;
        Vector3 newPos = transform.position + move;

        // 맵 경계 클램핑
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
        transform.position = newPos;
    }

    /// <summary>
    /// 마우스 위치를 기준으로 플레이어 스프라이트의 좌우 방향을 결정한다.
    /// 마우스가 캐릭터 왼쪽에 있으면 스프라이트를 좌우 반전한다.
    /// </summary>
    void HandleFacing()
    {
        if (Mouse.current != null && Camera.main != null && spriteRenderer != null)
        {
            // 마우스 스크린 좌표를 월드 좌표로 변환
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(
                new Vector3(mouseScreenPos.x, mouseScreenPos.y, Mathf.Abs(Camera.main.transform.position.z)));

            // 마우스가 플레이어보다 왼쪽에 있으면 스프라이트 좌우 반전
            spriteRenderer.flipX = mouseWorldPos.x < transform.position.x;
        }
    }

    /// <summary>
    /// 매 프레임 활성화된 모든 적과의 거리를 체크한다.
    /// contactRadius 이내에 적이 있으면 1 데미지를 받는다.
    /// </summary>
    void CheckEnemyContact()
    {
        var enemies = EnemyController.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            if (Vector2.Distance(transform.position, enemies[i].transform.position) < contactRadius)
            {
                TakeDamage(1);
                return; // 한 프레임에 여러 적에게 동시에 피격되지 않도록 즉시 종료
            }
        }
    }

    /// <summary>
    /// 플레이어가 데미지를 받을 때 호출된다.
    /// 이미 사망했거나 무적 상태이면 아무 효과도 없다.
    /// 체력이 0 이하가 되면 Die()를 호출한다.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead || isInvincible) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth); // 체력이 0 미만이 되지 않도록 제한

        // GameManager에 현재 체력을 전달해 HP UI를 갱신
        if (GameManager.Instance != null)
            GameManager.Instance.UpdateHP(currentHealth);

        if (currentHealth <= 0)
            Die();
        else
            StartCoroutine(InvincibilityCoroutine()); // 체력이 남아 있으면 무적 시간 시작
    }

    /// <summary>
    /// 피격 후 invincibilityDuration 동안 무적 상태를 유지한다.
    /// 무적 동안 스프라이트가 깜빡이는 시각적 피드백을 제공한다.
    /// </summary>
    IEnumerator InvincibilityCoroutine()
    {
        isInvincible = true;
        float elapsed = 0f;

        // 0.05초 간격으로 스프라이트를 켜고 끄며 깜빡임 효과 연출
        while (elapsed < invincibilityDuration)
        {
            spriteRenderer.enabled = false;
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.enabled = true;
            yield return new WaitForSeconds(0.05f);
            elapsed += 0.1f;
        }

        spriteRenderer.enabled = true; // 무적 종료 시 스프라이트 반드시 다시 활성화
        isInvincible = false;
    }

    /// <summary>
    /// 체력이 0이 되었을 때 호출된다.
    /// 사망 스프라이트로 교체하고, 무기를 숨기고, 게임 오버를 GameManager에 알린다.
    /// </summary>
    void Die()
    {
        isDead = true;

        // 애니메이터를 비활성화해 사망 스프라이트가 덮어씌워지지 않도록 방지
        if (animator != null)
            animator.enabled = false;

        // 사망 스프라이트(Dead 0)로 교체
        if (deadSprite != null)
            spriteRenderer.sprite = deadSprite;

        // 깜빡임 도중 사망하면 스프라이트가 꺼진 상태일 수 있으므로 강제 활성화
        spriteRenderer.enabled = true;
        spriteRenderer.color = Color.white;

        // WeaponPivot 오브젝트를 비활성화해 무기를 화면에서 제거
        Transform weaponPivot = transform.Find("WeaponPivot");
        if (weaponPivot != null)
            weaponPivot.gameObject.SetActive(false);

        // 게임 오버 상태로 전환 (적 멈춤 등의 처리를 GameManager에서 관리)
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameOver();
    }
}
