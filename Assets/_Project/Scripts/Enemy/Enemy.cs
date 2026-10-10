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
    // 충돌 원(트리거)은 투사체/근접 무기가 이 몬스터를 맞히는 판정에도 같이 쓰이는데, 스프라이트보다 훨씬 크게
    // 잡혀 있어서(예: 스켈레톤은 스프라이트 너비 0.32인데 충돌 지름 0.9) 접촉 데미지가 너무 멀리서 들어갔다.
    // 그래서 접촉 데미지만 "충돌 원 반지름 x 이 비율" 안에 플레이어 몸이 들어왔을 때만 준다 (명중 판정은 그대로).
    [Range(0.1f, 1f)] public float contactRangeScale = 0.5f;

    [Header("무리 짓기(분리) 설정")]
    public float separationRadius = 0.6f;   // 이 거리 안에 다른 적이 있으면 밀어내는 힘이 작용한다
    public float separationStrength = 1.5f; // 밀어내는 힘의 세기. 클수록 서로 더 확실히 벌어진다

    [Header("맵 경계")]
    public Vector2 mapCenter = Vector2.zero; // 맵 경계 상자의 중심 월드 좌표. EnemySpawner가 스폰 시 설정해준다
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

    [Header("드랍 자원 편향 (0이면 5종류 균등, 0보다 크면 biasedDropType이 이 확률로 나오고 나머지는 그 확률을 뺀 나머지를 다른 4종류가 나눠 가짐)")]
    [Range(0f, 1f)] public float biasedDropChance = 0f;
    public ResourceType biasedDropType;

    private float currentHealth;
    private Rigidbody2D rb;
    private Transform player;
    private SpriteRenderer spriteRenderer;
    private SpriteAnimator spriteAnimator;
    private CircleCollider2D contactCircle; // 접촉 데미지 거리 계산용 (OnTriggerStay2D 참고)
    private SpriteRenderer[] hitOutlineRenderers;

    // 4개 테두리 렌더러가 공유하는 머티리얼 인스턴스 하나 (예전엔 렌더러마다 new Material 4개씩
    // 만들어서 적 1마리당 4개 - 적이 많으면 SRP 배칭이 깨지고 OnDestroy에서 안 지워 누수됐다).
    // 4개 테두리는 항상 같은 색이라 인스턴스 하나를 공유하고 색도 한 번만 쓰면 된다.
    private Material outlineMat;

    // 겹침 방지(분리) 계산용. 예전엔 매 FixedUpdate마다 살아있는 모든 적을 순회(O(n^2))했는데,
    // 적이 수백이면 프레임이 무너진다. 물리 브로드페이즈(OverlapCircle)로 반경 안 후보만 뽑아 쓴다.
    private static readonly Collider2D[] separationBuf = new Collider2D[24];
    private static ContactFilter2D separationFilter = new ContactFilter2D { useTriggers = true };

    private float hitStunTimer;
    private Vector2 knockbackVelocity;

    // Stun()으로 걸리는 기절. 피격 경직(hitStunTimer)과 달리 접촉 데미지도 막고, 기절 중에는 받는 피해를
    // 늘릴 수도 있다 (검 화학물질 강화 특수효과). 이동/AI 정지는 hitStunTimer를 같이 늘려서 기존 로직을 그대로 쓴다.
    private float stunTimer;
    private float stunDamageTakenMultiplier = 1f;
    private bool isDying; // Die()가 한 번 호출된 뒤 true. 이후 이동/공격/추가 피격을 전부 무시한다.

    // 죽는 중(페이드아웃 중)인 적은 자동조준 대상에서 제외해야 하므로 외부에서 읽을 수 있게 열어둔다.
    public bool IsDying => isDying;

    // 피격 경직 중인지 외부(원거리 몬스터의 애니메이션/공격 타이머 등)에서 읽을 수 있게 열어둔다.
    public bool IsHitStunned => hitStunTimer > 0f;

    // 기절 중인지 (Stun 참고). 기절 중에는 접촉 데미지를 주지 않는다.
    public bool IsStunned => stunTimer > 0f;

    // 지금 받는 피해에 곱해지는 배율 (기절 중이면 Stun에서 정한 값, 아니면 1).
    private float DamageTakenMultiplier => stunTimer > 0f ? stunDamageTakenMultiplier : 1f;

    // 기믹 "태풍"이 활성화된 동안 moveSpeed에 곱해지는 실제 이동속도. Enemy.cs 자체 추격 이동뿐
    // 아니라 ChargeEnemyAI의 돌진 속도 계산도 이 값을 그대로 가져다 쓴다.
    public float EffectiveMoveSpeed => moveSpeed * StageGimmickManager.EnemySpeedMultiplier;

    // true인 동안 FixedUpdate()의 "플레이어 추격" 이동을 건너뛴다(피격 경직/넉백은 그대로 적용됨).
    // WeaponAim.externalControl과 같은 패턴 - 원거리 공격 몬스터처럼 제자리에 멈춰 서야 하는
    // 특수 행동 컴포넌트가 이 값을 켜고 끄면서 Enemy.cs의 기본 추격 이동만 잠깐 빌려 쓴다.
    [HideInInspector] public bool externalMovementControl;

    // true인 동안 피격 경직 중에도 넉백으로 인한 이동을 하지 않는다(경직 자체와 흰 테두리 표시는
    // 그대로 적용된다). 돌진 몬스터가 돌진 도중 맞아도 밀려나서 궤도가 흐트러지지 않게 하는 용도.
    [HideInInspector] public bool suppressKnockback;

    // true인 동안 Update()의 "플레이어 방향으로 좌우반전" 로직을 건너뛴다 (마지막 값 유지).
    // 돌진 몬스터가 돌진 도중 플레이어를 지나쳐도 갑자기 뒤돌아보지 않게 하는 용도.
    [HideInInspector] public bool externalFlipControl;

    // 돌진 몬스터의 "돌진 대기" 예고 등, 피격 경직이 아닌 다른 이유로 테두리를 보여주고 싶을 때
    // 특수 행동 컴포넌트가 켜고 끄는 값 (SetExternalOutline 참고). 피격 중(흰색)이 항상 우선한다.
    private bool externalOutlineActive;
    private Color externalOutlineColor = Color.white;

    // true면 Die()가 기본 페이드아웃(FadeOutAndDestroy)을 실행하지 않는다. 대신 OnDeathStart를 구독한
    // 별도 컴포넌트(예: ExplosionDeathAnimation)가 자기만의 사망 연출을 재생하고 스스로 Destroy(gameObject)를
    // 호출해야 한다. RangedEnemyAI 등과 같은 "특수 컴포넌트가 기본 동작을 잠깐 빌려 쓴다" 패턴.
    [HideInInspector] public bool externalDeathAnimation;

    // Die()가 시작되는 시점(자원/무기 드랍 이후, 기본 페이드아웃 시작 직전)에 한 번 호출된다.
    // 커스텀 사망 연출 컴포넌트가 이걸 구독해서 자기 애니메이션을 시작한다.
    public event System.Action OnDeathStart;

    void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteAnimator = GetComponent<SpriteAnimator>();
        contactCircle = GetComponent<CircleCollider2D>();
        CreateHitOutline();
    }

    // 스폰 직후(EnemySpawner)에 스테이지 난이도에 따라 체력을 올릴 때 쓴다. Awake()가 이미 원래
    // maxHealth 기준으로 currentHealth를 채운 뒤라, 최대/현재 체력을 같은 비율로 같이 올려줘야 한다
    // (이 시점엔 항상 풀피 상태이므로 currentHealth = maxHealth로 그냥 다시 채워도 된다).
    public void ApplyHealthMultiplier(float multiplier)
    {
        if (multiplier <= 0f) return;
        maxHealth *= multiplier;
        currentHealth = maxHealth;
    }

    // 피격 시 잠깐 보여줄 흰색 테두리를 만든다. 원본 스프라이트를 좌우상하로 살짝 떨어뜨려
    // 흰색으로 복제해두고, 본체 스프라이트보다 한 단계 뒤에 그려서 가장자리만 삐져나와 보이게 한다.
    private void CreateHitOutline()
    {
        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        hitOutlineRenderers = new SpriteRenderer[offsets.Length];

        // 이 적 전용 머티리얼 인스턴스 하나. renderer.color는 SRP 배칭 때문에 무시되므로 색은
        // UpdateHitOutline()에서 이 머티리얼에 직접 쓴다. 4개 렌더러가 같은 인스턴스를 공유한다.
        if (outlineMaterial != null) outlineMat = new Material(outlineMaterial);

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject go = new GameObject("HitOutline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * outlineOffset);

            var outlineSr = go.AddComponent<SpriteRenderer>();
            if (outlineMat != null) outlineSr.sharedMaterial = outlineMat; // .material은 또 인스턴스를 뜨므로 sharedMaterial로 직접 지정
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
        if (outlineMat != null) Destroy(outlineMat); // 인스턴스 머티리얼은 직접 지워야 누수되지 않는다
    }

    void Update()
    {
        if (stunTimer > 0f) stunTimer -= Time.deltaTime;

        if (player == null || spriteRenderer == null) return;

        // 몬스터 기준 플레이어가 오른쪽에 있으면 좌우 반전, 왼쪽에 있으면 원본 그대로.
        // (스프라이트 원본이 왼쪽을 보고 있는 모양이라 기본값 = 왼쪽 방향)
        // externalFlipControl이 켜진 동안은 건드리지 않고 마지막 방향을 그대로 유지한다
        // (돌진 몬스터가 돌진 도중 플레이어를 지나쳐도 뒤돌아보지 않게 하는 용도).
        if (!externalFlipControl)
        {
            if (player.position.x > transform.position.x)
                spriteRenderer.flipX = true;
            else if (player.position.x < transform.position.x)
                spriteRenderer.flipX = false;
        }

        UpdateHitOutline();

        // 기믹 "태풍" 중에는 애니메이션도 같은 배율로 빨라진다 (SpriteAnimator는 자기 자신이 몬스터인지
        // 몰라도 되게, speedMultiplier 필드만 노출하고 갱신은 Enemy.cs가 책임진다).
        if (spriteAnimator != null) spriteAnimator.speedMultiplier = StageGimmickManager.EnemySpeedMultiplier;
    }

    // 피격 경직 중(hitStunTimer > 0)이면 흰색 테두리를, 그렇지 않은데 외부에서 테두리를 요청했으면
    // (예: 돌진 몬스터의 돌진 대기 예고) 그 색으로 테두리를 보여준다. 애니메이션이 멈춰있는 동안이므로
    // 프레임/좌우반전을 매번 본체 스프라이트와 맞춰주기만 하면 된다.
    private void UpdateHitOutline()
    {
        if (isDying) return; // 사망 연출 중에는 테두리를 아예 표시하지 않는다 (Die()에서 이미 꺼둠)

        bool hitStun = hitStunTimer > 0f;
        bool visible = hitStun || externalOutlineActive;
        Color color = hitStun ? Color.white : externalOutlineColor;

        // 색은 공유 머티리얼 인스턴스에 한 번만 쓴다 (renderer.color는 SRP 배칭 때문에 무시됨).
        if (visible && outlineMat != null) outlineMat.color = color;

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

    // 피격 경직이 아닌 다른 이유로 테두리를 보이거나 숨길 때 외부 컴포넌트가 호출한다.
    // 피격으로 인한 흰색 테두리가 항상 우선하며, 이 값은 그 외의 경우에만 적용된다.
    public void SetExternalOutline(bool active, Color color)
    {
        externalOutlineActive = active;
        externalOutlineColor = color;
    }

    void FixedUpdate()
    {
        // 죽어서 페이드아웃 되는 중이면 그 자리에서 멈춘다.
        if (isDying) return;

        // 플레이어가 사망하면 모든 적이 그 자리에서 멈춘다.
        if (EnemyManager.PlayerDead) return;

        float limitX = mapHalfExtent - boundaryMargin;
        float limitY = mapHalfExtent - boundaryMargin;

        // 피격 경직 중에는 추격/분리 로직 대신 넉백만 적용하고, 시간이 지날수록 넉백 속도를 줄인다.
        if (hitStunTimer > 0f)
        {
            hitStunTimer -= Time.fixedDeltaTime;

            if (!suppressKnockback)
            {
                Vector2 knockPos = rb.position + knockbackVelocity * Time.fixedDeltaTime;
                knockbackVelocity = Vector2.MoveTowards(knockbackVelocity, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);

                knockPos.x = Mathf.Clamp(knockPos.x, mapCenter.x - limitX, mapCenter.x + limitX);
                knockPos.y = Mathf.Clamp(knockPos.y, mapCenter.y - limitY, mapCenter.y + limitY);
                rb.MovePosition(knockPos);
            }
            return;
        }

        // 원거리 몬스터 등이 제자리에서 멈춰 공격해야 할 때 이동만 잠깐 꺼둔다.
        if (externalMovementControl) return;

        if (player == null) return;

        // 1) 플레이어를 향하는 방향.
        Vector2 toPlayer = (Vector2)(player.position - transform.position);
        Vector2 chaseDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.zero;

        // 2) 가까운 다른 적들로부터 멀어지는 방향 (겹침 방지용).
        //    거리가 가까울수록 더 강하게 밀어낸다. 전체 목록을 훑는 대신 물리 브로드페이즈로
        //    separationRadius 안의 콜라이더만 뽑아서 검사한다 (적이 많아도 비용이 거의 안 늘어남).
        Vector2 separation = Vector2.zero;
        Vector2 myPos = transform.position;
        float radiusSqr = separationRadius * separationRadius;
        int hitCount = Physics2D.OverlapCircle(myPos, separationRadius, separationFilter, separationBuf);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = separationBuf[i];
            if (col == null || col.gameObject == gameObject) continue;
            Enemy other = col.GetComponent<Enemy>();
            if (other == null || other == this) continue;

            Vector2 diff = myPos - (Vector2)other.transform.position;
            float distSqr = diff.sqrMagnitude;
            if (distSqr > 0f && distSqr < radiusSqr)
            {
                float dist = Mathf.Sqrt(distSqr);
                separation += diff / dist * (1f - dist / separationRadius);
            }
        }

        // 두 방향을 합친 뒤 다시 정규화해서 항상 moveSpeed로 고정된 속도를 유지한다.
        // (분리 힘이 섞여도 최종 이동 속도는 변하지 않고 방향만 살짝 휘어지는 느낌을 준다)
        Vector2 moveDir = chaseDir + separation * separationStrength;
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Vector2 nextPosition = rb.position + moveDir.normalized * EffectiveMoveSpeed * Time.fixedDeltaTime;

            // 플레이어와 마찬가지로 맵 경계 밖으로 못 나가게 좌표를 눌러준다.
            nextPosition.x = Mathf.Clamp(nextPosition.x, mapCenter.x - limitX, mapCenter.x + limitX);
            nextPosition.y = Mathf.Clamp(nextPosition.y, mapCenter.y - limitY, mapCenter.y + limitY);

            rb.MovePosition(nextPosition);
        }
    }

    // 투사체 등에 맞았을 때 호출: 데미지를 주고, 잠깐 애니메이션을 멈추고, 맞은 방향으로 살짝 밀려나고,
    // 흰색 테두리 + 데미지 숫자를 띄운다. isCrit이 true면 치명타 연출(더 크고 연노랑)로 표시된다.
    // knockbackMultiplier: 강타 같은 특수 공격이 평소보다 세게 밀어낼 때 쓴다 (기본 1배).
    public void Hit(Vector2 knockbackDirection, float damage, bool isCrit = false, float knockbackMultiplier = 1f)
    {
        if (isDying) return; // 이미 죽는 중이면 더 이상 반응하지 않는다

        // 기절 중이면 받는 피해가 늘어난다. 표시되는 데미지 숫자도 실제 들어간 값과 같도록 미리 곱해둔다
        // (TakeDamage는 이 배율을 다시 곱하지 않도록 아래에서 원본 damage가 아니라 곱한 값을 넘긴다).
        float dealt = damage * DamageTakenMultiplier;
        TakeDamageRaw(dealt);

        hitStunTimer = Mathf.Max(hitStunTimer, hitStunDuration);
        knockbackVelocity = knockbackDirection.normalized * knockbackForce * knockbackMultiplier;

        if (spriteAnimator != null)
            spriteAnimator.Pause(hitStunDuration);

        SpawnDamageNumber(dealt, isCrit);
    }

    private void SpawnDamageNumber(float damage, bool isCrit)
    {
        if (damageNumberPrefab == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        GameObject go = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
        DamageNumber number = go.GetComponent<DamageNumber>();
        if (number != null) number.Setup(damage, isCrit);
    }

    // 폭발 등 Hit() 없이 직접 데미지를 주는 쪽도 기절 중 받는 피해 증가가 똑같이 적용된다.
    public void TakeDamage(float amount)
    {
        TakeDamageRaw(amount * DamageTakenMultiplier);
    }

    // 배율이 이미 반영된 최종 데미지를 그대로 깎는다 (Hit()에서 숫자 표시와 맞추려고 따로 뺐다).
    private void TakeDamageRaw(float amount)
    {
        if (isDying) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Die(dropLoot: true);
        }
    }

    // 그 자리에서 즉시 사라지는 대신, 애니메이션을 멈춘 채로 서서히 투명해지다가 사라진다.
    // dropLoot가 false면 자원/무기를 전혀 드랍하지 않는다 - 스테이지 클리어로 강제 전멸시킬 때
    // 쓴다(직접 잡은 게 아닌데 자원을 주는 게 이상하다는 피드백으로 추가됨).
    private void Die(bool dropLoot)
    {
        if (isDying) return;
        isDying = true;

        if (dropLoot)
        {
            // 파밍용 자원 드랍. minResourceDrops~maxResourceDrops개 사이로 랜덤하게 여러 개 드랍할 수 있다
            // (일반 슬라임은 항상 1개, 엘리트처럼 더 많이 주는 적은 프리팹에서 범위를 넓게 설정).
            int dropCount = Random.Range(minResourceDrops, maxResourceDrops + 1);
            for (int i = 0; i < dropCount; i++)
            {
                // 특정 맵(생각의 방)은 자원 드랍 확률 자체가 평소보다 낮다. 그 외 맵은 배율이 1이라 항상 드랍된다.
                if (Random.value > StageManager.ResourceDropRateMultiplier) continue;

                if (biasedDropChance > 0f)
                    ResourcePickup.SpawnRandomDrop(transform.position, biasedDropType, biasedDropChance);
                else
                    ResourcePickup.SpawnRandomDrop(transform.position);
            }

            // 낮은 확률(1%)로 무기 아이템도 별도로 드랍한다.
            WeaponPickup.TrySpawnRandomDrop(transform.position);
        }

        if (spriteAnimator != null) spriteAnimator.enabled = false; // 현재 프레임에 고정

        // 죽는 순간 흰색 테두리는 바로 꺼서, 사망 연출 동안에는 아예 보이지 않게 한다.
        for (int i = 0; i < hitOutlineRenderers.Length; i++)
        {
            hitOutlineRenderers[i].enabled = false;
        }

        OnDeathStart?.Invoke();

        // 커스텀 사망 연출(폭발 애니메이션 등)을 재생하는 컴포넌트가 있으면 기본 페이드아웃은 건너뛴다 -
        // 그 컴포넌트가 애니메이션이 끝난 뒤 직접 Destroy(gameObject)를 호출해야 한다.
        if (!externalDeathAnimation) StartCoroutine(FadeOutAndDestroy());
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
    // dropLoot: 스테이지 클리어로 남은 몹을 강제로 쓸어버릴 때는 false로 넘겨서 드랍을 막는다
    // (플레이어가 직접 잡은 게 아니므로 보상을 안 주는 게 맞다는 피드백으로 추가됨).
    public void Kill(bool dropLoot = true)
    {
        Die(dropLoot);
    }

    // duration초 동안 기절시킨다: 이동/행동(AI)이 멈추고(피격 경직을 같이 늘려서 기존 로직 재사용), 접촉 데미지를
    // 주지 않으며, 기절 중에는 받는 피해에 damageTakenMultiplier가 곱해진다. 이미 기절 중이면 더 긴 쪽 시간,
    // 더 큰 쪽 배율만 남는다 (겹쳐도 합산되지 않음).
    public void Stun(float duration, float damageTakenMultiplier = 1f)
    {
        if (isDying || duration <= 0f) return;

        if (stunTimer <= 0f) stunDamageTakenMultiplier = 1f;
        stunDamageTakenMultiplier = Mathf.Max(stunDamageTakenMultiplier, damageTakenMultiplier);
        stunTimer = Mathf.Max(stunTimer, duration);

        hitStunTimer = Mathf.Max(hitStunTimer, duration);
        knockbackVelocity = Vector2.zero; // 기절은 밀어내지 않는다 (제자리에서 멈춘다)
        if (spriteAnimator != null) spriteAnimator.Pause(duration);
    }

    // 플레이어와 계속 겹쳐있는 동안 매 물리 프레임 호출된다. 실제 데미지 빈도는 PlayerHealth의
    // 무적 시간이 알아서 제한해주므로, 여기서는 접촉할 때마다 그냥 계속 시도하면 된다.
    void OnTriggerStay2D(Collider2D other)
    {
        if (isDying) return; // 죽는 중에는 더 이상 접촉 데미지를 주지 않는다
        if (stunTimer > 0f) return; // 기절 중에도 마찬가지

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        // 충돌 원 중심에서 플레이어 몸의 가장 가까운 점까지가 (반지름 x contactRangeScale) 이내일 때만 데미지.
        if (contactCircle != null)
        {
            Vector2 center = contactCircle.bounds.center;
            float reach = contactCircle.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * contactRangeScale;
            if (Vector2.Distance(other.ClosestPoint(center), center) > reach) return;
        }

        // 플레이어가 적의 반대 방향(적 -> 플레이어 방향)으로 밀려나도록 방향을 계산한다.
        Vector2 knockbackDirection = (Vector2)other.transform.position - (Vector2)transform.position;
        playerHealth.TakeHit(Mathf.RoundToInt(contactDamage), knockbackDirection);
    }
}
