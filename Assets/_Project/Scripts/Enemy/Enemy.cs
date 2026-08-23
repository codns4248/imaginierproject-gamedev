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

    [Header("피격 표시")]
    public float outlineOffset = 0.04f; // 흰색 테두리용 복제 스프라이트를 원본에서 얼마나 떨어뜨릴지
    // 텍스처 알파만 읽어서 무조건 흰색으로 칠하는 전용 머티리얼 (Sprites-Default는 색을 "곱하기"만 해서
    // 흰색을 줘도 원본 색이 그대로 나오기 때문에, 진짜 흰색 실루엣을 만들려면 이 머티리얼이 필요하다).
    public Material outlineMaterial;
    public GameObject damageNumberPrefab; // 피격 시 위에 띄울 데미지 숫자 프리팹

    [Header("사망 시 자원 드랍")]
    public int minResourceDrops = 1; // 죽을 때 드랍되는 자원 개수의 최소값
    public int maxResourceDrops = 1; // 죽을 때 드랍되는 자원 개수의 최대값 (기본은 항상 1개, 엘리트 등은 더 크게 설정)

    private float currentHealth;
    private Rigidbody2D rb;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private SpriteAnimator spriteAnimator;
    private SpriteRenderer[] hitOutlineRenderers;

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
        CreateHitOutline();
    }

    // 피격 시 잠깐 보여줄 흰색 테두리를 만든다. 원본 스프라이트를 좌우상하로 살짝 떨어뜨려
    // 흰색으로 복제해두고, 본체 스프라이트보다 한 단계 뒤에 그려서 가장자리만 삐져나와 보이게 한다.
    private void CreateHitOutline()
    {
        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        hitOutlineRenderers = new SpriteRenderer[offsets.Length];

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject go = new GameObject("HitOutline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * outlineOffset);

            var outlineSr = go.AddComponent<SpriteRenderer>();
            if (outlineMaterial != null) outlineSr.material = outlineMaterial; // 없으면 기본 머티리얼로 남고, 그냥 원본 색으로 보임
            outlineSr.sortingOrder = spriteRenderer.sortingOrder - 1;
            outlineSr.enabled = false;

            hitOutlineRenderers[i] = outlineSr;
        }
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

        UpdateHitOutline();
    }

    // 피격 경직 중(hitStunTimer > 0)에만 흰색 테두리를 보여준다. 애니메이션이 그동안 멈춰있으므로
    // 프레임/좌우반전을 매번 본체 스프라이트와 맞춰주기만 하면 된다.
    private void UpdateHitOutline()
    {
        if (isDying) return; // 사망 연출 중에는 테두리를 아예 표시하지 않는다 (Die()에서 이미 꺼둠)

        bool visible = hitStunTimer > 0f;
        for (int i = 0; i < hitOutlineRenderers.Length; i++)
        {
            hitOutlineRenderers[i].enabled = visible;
            if (visible)
            {
                hitOutlineRenderers[i].sprite = spriteRenderer.sprite;
                hitOutlineRenderers[i].flipX = spriteRenderer.flipX;
            }
        }
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

    // 투사체 등에 맞았을 때 호출: 데미지를 주고, 잠깐 애니메이션을 멈추고, 맞은 방향으로 살짝 밀려나고,
    // 흰색 테두리 + 데미지 숫자를 띄운다. isCrit이 true면 치명타 연출(더 크고 연노랑)로 표시된다.
    public void Hit(Vector2 knockbackDirection, float damage, bool isCrit = false)
    {
        if (isDying) return; // 이미 죽는 중이면 더 이상 반응하지 않는다

        TakeDamage(damage);

        hitStunTimer = hitStunDuration;
        knockbackVelocity = knockbackDirection.normalized * knockbackForce;

        if (spriteAnimator != null)
            spriteAnimator.Pause(hitStunDuration);

        SpawnDamageNumber(damage, isCrit);
    }

    private void SpawnDamageNumber(float damage, bool isCrit)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        GameObject go = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
        DamageNumber number = go.GetComponent<DamageNumber>();
        if (number != null) number.Setup(damage, isCrit);
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

        // 파밍용 자원 드랍. minResourceDrops~maxResourceDrops개 사이로 랜덤하게 여러 개 드랍할 수 있다
        // (일반 슬라임은 항상 1개, 엘리트처럼 더 많이 주는 적은 프리팹에서 범위를 넓게 설정).
        int dropCount = Random.Range(minResourceDrops, maxResourceDrops + 1);
        for (int i = 0; i < dropCount; i++)
        {
            ResourcePickup.SpawnRandomDrop(transform.position);
        }

        if (spriteAnimator != null) spriteAnimator.enabled = false; // 현재 프레임에 고정

        // 죽는 순간 흰색 테두리는 바로 꺼서, 사망 연출(페이드아웃) 동안에는 아예 보이지 않게 한다.
        for (int i = 0; i < hitOutlineRenderers.Length; i++)
        {
            hitOutlineRenderers[i].enabled = false;
        }

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
