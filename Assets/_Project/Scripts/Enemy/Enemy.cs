using System.Collections;
using UnityEngine;

// 모든 적 종류가 공통으로 쓰는 기본 동작.
// 플레이어를 향해 일정한 속도로 쫓아가면서, 근처의 다른 적과는 겹치지 않도록 서로를 부드럽게 밀어내며
// 이동해서 자연스럽게 무리(군집)를 이루도록 한다.
// HP, 이동속도 등은 Inspector(프리팹)에서 종류별로 다르게 설정해서 슬라임 외의 다른 적에도 그대로 재사용한다.
public class Enemy : MonoBehaviour
{
    [Header("스탯 (종류별로 프리팹에서 다르게 설정)")]
    public float maxHealth = 10f;
    public float moveSpeed = 2f;
    public float contactDamage = 1f; // 플레이어와 접촉했을 때 주는 데미지

    [Header("무리 짓기(분리) 설정")]
    public float separationRadius = 0.6f;   // 이 거리 안에 다른 적이 있으면 밀어내는 힘이 작용한다
    public float separationStrength = 1.5f; // 밀어내는 힘의 세기. 클수록 서로 더 확실히 벌어진다

    [Header("맵 경계")]
    public float mapHalfExtent = 20f;  // Player_Movement의 mapHalfExtent와 맞춰야 함
    public float boundaryMargin = 0.3f; // 가장자리 타일 끝에 딱 붙지 않도록 살짝 여유

    [Header("피격 반응")]
    public float hitStunDuration = 0.2f; // 맞았을 때 애니메이션/추격이 멈추는 시간
    public float knockbackForce = 3f;    // 맞은 직후의 넉백 속도
    public float knockbackDecay = 15f;   // 넉백 속도가 줄어드는 속도 (클수록 빨리 멈춤)

    [Header("사망 연출")]
    public float deathFadeDuration = 0.5f; // 죽은 뒤 점점 투명해지며 사라지는 데 걸리는 시간

    private float currentHealth;
    private Rigidbody2D rb;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private SpriteAnimator spriteAnimator;

    private float hitStunTimer;
    private Vector2 knockbackVelocity;
    private bool isDying; // Die()가 한 번 호출된 뒤 true. 이후 이동/공격/추가 피격을 전부 무시한다.

    // 죽는 중(페이드아웃 중)인 적은 자동조준 대상에서 제외해야 하므로 외부에서 읽을 수 있게 열어둔다.
    public bool IsDying => isDying;

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteAnimator = GetComponent<SpriteAnimator>();
    }

    void Start()
    {
        // 씬 안의 "Player"라는 이름의 오브젝트를 찾아 계속 쫓아갈 대상으로 삼는다.
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;

        // EnemySpawner가 현재 몇 마리가 살아있는지 셀 수 있도록 자신을 등록한다.
        EnemyManager.Register(this);
    }

    void OnDestroy()
    {
        EnemyManager.Unregister(this);
    }

    void Update()
    {
        if (player == null || spriteRenderer == null) return;

        // 몬스터 기준 플레이어가 오른쪽에 있으면 좌우 반전, 왼쪽에 있으면 원본 그대로.
        // (스프라이트 원본이 왼쪽을 보고 있는 모양이라 기본값 = 왼쪽 방향)
        if (player.position.x > transform.position.x)
            spriteRenderer.flipX = true;
        else if (player.position.x < transform.position.x)
            spriteRenderer.flipX = false;
    }

    void FixedUpdate()
    {
        // 죽어서 페이드아웃 되는 중이면 그 자리에서 멈춘다.
        if (isDying) return;

        // 플레이어가 사망하면 모든 적이 그 자리에서 멈춘다.
        if (EnemyManager.PlayerDead) return;

        float limit = mapHalfExtent - boundaryMargin;

        // 피격 경직 중에는 추격/분리 로직 대신 넉백만 적용하고, 시간이 지날수록 넉백 속도를 줄인다.
        if (hitStunTimer > 0f)
        {
            hitStunTimer -= Time.fixedDeltaTime;

            Vector2 knockPos = rb.position + knockbackVelocity * Time.fixedDeltaTime;
            knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);

            knockPos.x = Mathf.Clamp(knockPos.x, -limit, limit);
            knockPos.y = Mathf.Clamp(knockPos.y, -limit, limit);
            rb.MovePosition(knockPos);
            return;
        }

        if (player == null) return;

        // 1) 플레이어를 향하는 방향.
        Vector2 toPlayer = (Vector2)(player.position - transform.position);
        Vector2 chaseDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;

        // 2) 가까운 다른 적들로부터 멀어지는 방향 (겹침 방지용).
        //    거리가 가까울수록 더 강하게 밀어내서 자연스럽게 서로 간격이 벌어지게 한다.
        Vector2 separation = Vector2.zero;
        foreach (Enemy other in EnemyManager.ActiveEnemies)
        {
            if (other == this) continue;
            Vector2 diff = (Vector2)(transform.position - other.transform.position);
            float dist = diff.magnitude;
            if (dist > 0f && dist < separationRadius)
            {
                separation += diff.normalized * (1f - dist / separationRadius);
            }
        }

        // 두 방향을 합친 뒤 다시 정규화해서 항상 moveSpeed로 고정된 속도를 유지한다.
        // (분리 힘이 섞여도 최종 이동 속도는 변하지 않고 방향만 살짝 휘어지는 느낌을 준다)
        Vector2 moveDir = chaseDir + separation * separationStrength;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Vector2 nextPosition = rb.position + moveDir.normalized * moveSpeed * Time.fixedDeltaTime;

            // 플레이어와 마찬가지로 맵 경계 밖으로 못 나가게 좌표를 눌러준다.
            nextPosition.x = Mathf.Clamp(nextPosition.x, -limit, limit);
            nextPosition.y = Mathf.Clamp(nextPosition.y, -limit, limit);

            rb.MovePosition(nextPosition);
        }
    }

    // 투사체 등에 맞았을 때 호출: 데미지를 주고, 잠깐 애니메이션을 멈추고, 맞은 방향으로 살짝 밀려난다.
    public void Hit(Vector2 knockbackDirection, float damage)
    {
        if (isDying) return; // 이미 죽는 중이면 더 이상 반응하지 않는다

        TakeDamage(damage);

        hitStunTimer = hitStunDuration;
        knockbackVelocity = knockbackDirection.normalized * knockbackForce;

        if (spriteAnimator != null)
            spriteAnimator.Pause(hitStunDuration);
    }

    public void TakeDamage(float amount)
    {
        if (isDying) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // 그 자리에서 즉시 사라지는 대신, 애니메이션을 멈춘 채로 서서히 투명해지다가 사라진다.
    private void Die()
    {
        if (isDying) return;
        isDying = true;

        ResourcePickup.SpawnRandomDrop(transform.position); // 파밍용 자원 드랍

        if (spriteAnimator != null) spriteAnimator.enabled = false; // 현재 프레임에 고정
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        Color startColor = spriteRenderer.color;
        float elapsed = 0f;

        while (elapsed < deathFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / deathFadeDuration);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }

        Destroy(gameObject);
    }

    // 외부(StageTimer의 스테이지 클리어 처리 등)에서 데미지 계산 없이 즉시 제거할 때 호출한다.
    public void Kill()
    {
        Die();
    }

    // 플레이어와 계속 겹쳐있는 동안 매 물리 프레임 호출된다. 실제 데미지 빈도는 PlayerHealth의
    // 무적 시간이 알아서 제한해주므로, 여기서는 접촉할 때마다 그냥 계속 시도하면 된다.
    void OnTriggerStay2D(Collider2D other)
    {
        if (isDying) return; // 죽는 중에는 더 이상 접촉 데미지를 주지 않는다

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        // 플레이어가 적의 반대 방향(적 -> 플레이어 방향)으로 밀려나도록 방향을 계산한다.
        Vector2 knockbackDirection = (Vector2)other.transform.position - (Vector2)transform.position;
        playerHealth.TakeHit(Mathf.RoundToInt(contactDamage), knockbackDirection);
    }
}
