using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 개별 적 캐릭터의 동작을 제어하는 스크립트.
/// 플레이어를 향해 이동하고, 다른 적들과 겹치지 않도록 분리력을 적용한다.
/// 피격 시 넉백 및 Hit 애니메이션을 처리하고, 체력이 0이 되면 사망 처리를 한다.
/// </summary>
public class EnemyController : MonoBehaviour
{
    // 씬에 현재 살아있는 모든 적을 추적하는 정적 리스트.
    // Weapon.cs의 공격 판정, PlayerController의 접촉 판정 등 여러 곳에서 참조한다.
    public static List<EnemyController> ActiveEnemies = new List<EnemyController>();

    [Header("Movement")]
    // 플레이어를 향해 이동하는 기본 속도
    public float speed = 2f;
    // 이 반경 이내의 다른 적에게 분리력을 적용 (적들이 한곳에 뭉치는 것 방지)
    public float separationRadius = 0.6f;
    // 분리력의 세기
    public float separationForce = 3f;

    [Header("Health")]
    // 최대 체력
    public int maxHealth = 10;

    [Header("Knockback")]
    // 피격 시 밀려나는 힘의 세기
    public float knockbackForce = 3f;
    // 넉백이 지속되는 시간 (초)
    public float knockbackDuration = 0.15f;

    // 현재 체력
    private int currentHealth;
    // 플레이어의 Transform (매 프레임 방향 계산에 사용)
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    // 사망 여부 - true이면 ActiveEnemies 목록에서 제거되고 이동이 중단됨
    private bool isDead = false;
    // 넉백 중 여부 - true인 동안 일반 이동 로직을 건너뜀
    private bool isKnockedBack = false;
    // 넉백 방향 벡터 (피격 위치에서 적 방향으로 계산됨)
    private Vector2 knockbackDir;

    // 맵 이동 경계값
    private float minX = -24.5f;
    private float maxX = 24.5f;
    private float minY = -14.45f;
    private float maxY = 14.45f;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;
    }

    void OnEnable()
    {
        // 오브젝트가 활성화될 때 ActiveEnemies 목록에 등록
        if (!isDead)
            ActiveEnemies.Add(this);
    }

    void OnDisable()
    {
        // 오브젝트가 비활성화될 때 ActiveEnemies 목록에서 제거
        ActiveEnemies.Remove(this);
    }

    void Start()
    {
        // 씬에서 "Player" 오브젝트를 찾아 추적 대상으로 설정
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void Update()
    {
        // 사망했거나 플레이어가 없으면 이동하지 않음
        if (isDead || player == null) return;
        // 게임 오버 시 모든 적이 제자리에서 멈춤
        if (GameManager.IsGameOver) return;
        // 넉백 중에는 일반 이동 로직을 건너뜀 (KnockbackCoroutine이 이동을 담당)
        if (isKnockedBack) return;

        Vector2 position = transform.position;

        // 플레이어 방향으로의 이동 벡터 계산
        Vector2 direction = ((Vector2)player.position - position).normalized;
        Vector2 move = direction * speed * Time.deltaTime;

        // 주변 적들과의 분리력 계산 (자연스러운 군중 이동 연출)
        Vector2 separation = Vector2.zero;
        int count = 0;
        for (int i = 0; i < ActiveEnemies.Count; i++)
        {
            EnemyController other = ActiveEnemies[i];
            if (other == null || other == this || other.isDead) continue;

            float dist = Vector2.Distance(position, other.transform.position);
            if (dist < separationRadius && dist > 0f)
            {
                // 거리가 가까울수록 강한 분리력을 적용 (거리의 역수에 비례)
                Vector2 diff = position - (Vector2)other.transform.position;
                separation += diff.normalized / dist;
                count++;
            }
        }

        // 분리력을 평균 내어 최종 이동에 합산
        if (count > 0)
        {
            separation /= count;
            move += separation * separationForce * Time.deltaTime;
        }

        // 맵 경계 클램핑
        Vector3 newPos = transform.position + (Vector3)move;
        newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
        newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
        transform.position = newPos;

        // 플레이어가 자신보다 왼쪽에 있으면 스프라이트 좌우 반전
        if (spriteRenderer != null)
            spriteRenderer.flipX = player.position.x < transform.position.x;
    }

    /// <summary>
    /// 외부(Weapon.cs)에서 호출되어 적에게 데미지를 입힌다.
    /// 피격 애니메이션을 재생하고 넉백을 시작하며, 체력이 0 이하가 되면 Die()를 호출한다.
    /// </summary>
    /// <param name="damage">입힐 데미지 양</param>
    /// <param name="attackerPos">공격자(플레이어)의 월드 위치 - 넉백 방향 계산에 사용</param>
    public void TakeDamage(int damage, Vector2 attackerPos)
    {
        if (isDead) return;

        currentHealth -= damage;

        // Hit 트리거로 피격 애니메이션 재생 (AcEnemy 컨트롤러의 Any State → Hit 전환)
        if (animator != null)
            animator.SetTrigger("Hit");

        // 공격자 반대 방향으로 넉백
        knockbackDir = ((Vector2)transform.position - attackerPos).normalized;
        StartCoroutine(KnockbackCoroutine());

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>
    /// knockbackDuration 동안 knockbackDir 방향으로 밀려난다.
    /// 시간이 지날수록 힘이 줄어드는 감속 효과를 적용한다.
    /// </summary>
    IEnumerator KnockbackCoroutine()
    {
        isKnockedBack = true;
        float elapsed = 0f;

        while (elapsed < knockbackDuration)
        {
            elapsed += Time.deltaTime;
            // t 값이 1→0으로 줄어들며 자연스러운 감속 효과
            float t = 1f - (elapsed / knockbackDuration);
            Vector3 newPos = transform.position + (Vector3)(knockbackDir * knockbackForce * t * Time.deltaTime);
            newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
            transform.position = newPos;
            yield return null;
        }

        isKnockedBack = false;
    }

    /// <summary>
    /// 적을 사망 처리한다.
    /// ActiveEnemies 목록에서 제거하고, 사망 애니메이션을 재생하며,
    /// 사망 위치에 경험치 아이템을 1~3개 드랍하고 1초 후 오브젝트를 삭제한다.
    /// </summary>
    public void Die()
    {
        if (isDead) return;
        isDead = true;
        ActiveEnemies.Remove(this);

        // Dead bool을 true로 설정해 사망 애니메이션 재생
        if (animator != null)
            animator.SetBool("Dead", true);

        // 사망 위치에 경험치 아이템 랜덤 드랍 (1~3개)
        if (GameManager.Instance != null)
            GameManager.Instance.SpawnExpItems(transform.position, Random.Range(1, 4));

        // 사망 애니메이션이 재생될 시간(1초)을 두고 오브젝트 삭제
        Destroy(gameObject, 1f);
    }
}
