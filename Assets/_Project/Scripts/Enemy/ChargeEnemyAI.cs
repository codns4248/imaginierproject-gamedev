using UnityEngine;

// 추격하다가 감지 범위 안에 플레이어가 들어오면 잠깐 멈췄다가 빠르게 직선으로 돌진하는 몬스터용
// 행동 컴포넌트. 피격/사망/테두리/좌우반전/드랍 등 공통 기능은 Enemy.cs가 그대로 처리하고, 이
// 컴포넌트는 "이동 상태(추격/대기/돌진)"와 그에 맞는 애니메이션, 돌진 이동만 담당한다
// (RangedEnemyAI와 같은 패턴으로 Enemy.externalMovementControl을 빌려 쓴다).
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class ChargeEnemyAI : MonoBehaviour
{
    [Header("돌진 트리거 범위")]
    public float detectRadius = 3.375f; // 원거리 몬스터 감지범위(6.75)의 절반

    [Header("돌진 대기(예고)")]
    public float windupDuration = 1f;
    public Sprite windupSprite; // 대기 중 고정으로 표시할 프레임 (Boar_Attack_11)
    public Color windupOutlineColor = Color.yellow;

    [Header("돌진")]
    public float chargeSpeedMultiplier = 1.5f;   // 평소 이동속도 대비 돌진 속도 배율
    public float chargeDistanceMultiplier = 2f;  // 돌진 거리 = detectRadius * 이 배율

    [Header("애니메이션 (이동/돌진 공용, 돌진 중엔 chargeSpeedMultiplier배로 재생)")]
    public float frameRate = 12f;
    public Sprite[] moveFrames;

    private enum State { Chase, Windup, Charging }
    private State state = State.Chase;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Transform player;

    private float windupTimer;
    private Vector2 chargeDirection;
    private float chargeRemainingDistance;

    private int animFrame;
    private float animTimer;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (enemy.IsDying || player == null) return;
        if (EnemyManager.PlayerDead) return;

        // 대기(예고)나 돌진 중에 피격당하면 그 자리에서 패턴을 취소한다 (이후 넉백은 Enemy.cs가 처리).
        if (enemy.IsHitStunned)
        {
            if (state != State.Chase) CancelCharge();
            return;
        }

        switch (state)
        {
            case State.Chase:
                enemy.externalMovementControl = false;
                float dist = Vector2.Distance(transform.position, player.position);
                if (dist <= detectRadius)
                {
                    StartWindup();
                    return; // 대기 스프라이트로 바로 전환 - 이번 프레임의 이동 프레임 갱신은 건너뛴다
                }
                AdvanceLoop(1f);
                break;

            case State.Windup:
                // 대기 중엔 플레이어가 범위를 벗어나도 취소하지 않는다 - 타이머만 흐른다.
                windupTimer -= Time.deltaTime;
                spriteRenderer.sprite = windupSprite;
                if (windupTimer <= 0f) StartCharge();
                break;

            case State.Charging:
                AdvanceLoop(chargeSpeedMultiplier);
                break;
        }
    }

    void FixedUpdate()
    {
        if (enemy.IsDying || player == null) return;
        if (EnemyManager.PlayerDead) return;
        if (enemy.IsHitStunned) return; // 이번 프레임 이동은 Enemy.cs의 넉백 처리에 맡긴다
        if (state != State.Charging) return;

        float limitX = enemy.mapHalfExtent - enemy.boundaryMargin;
        float limitY = enemy.mapHalfExtent - enemy.boundaryMargin;

        float step = Mathf.Min(enemy.moveSpeed * chargeSpeedMultiplier * Time.fixedDeltaTime, chargeRemainingDistance);
        Vector2 nextPos = rb.position + chargeDirection * step;
        nextPos.x = Mathf.Clamp(nextPos.x, enemy.mapCenter.x - limitX, enemy.mapCenter.x + limitX);
        nextPos.y = Mathf.Clamp(nextPos.y, enemy.mapCenter.y - limitY, enemy.mapCenter.y + limitY);
        rb.MovePosition(nextPos);

        chargeRemainingDistance -= step;
        if (chargeRemainingDistance <= 0f) EndCharge();
    }

    private void StartWindup()
    {
        state = State.Windup;
        windupTimer = windupDuration;
        enemy.externalMovementControl = true;
        enemy.SetExternalOutline(true, windupOutlineColor);
        ResetAnim();
    }

    // 대기가 끝나는 "이 순간"의 플레이어 위치 방향으로, 감지범위의 chargeDistanceMultiplier배만큼
    // (플레이어를 넘어서) 직선으로 돌진한다. 이후로는 플레이어 위치를 다시 참조하지 않는다.
    private void StartCharge()
    {
        state = State.Charging;
        Vector2 dir = (Vector2)(player.position - transform.position);
        chargeDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        chargeRemainingDistance = detectRadius * chargeDistanceMultiplier;
        enemy.SetExternalOutline(false, Color.white);
        enemy.externalFlipControl = true; // 돌진 도중엔 방향을 고정 - 플레이어를 지나쳐도 뒤돌아보지 않는다
        ResetAnim();
    }

    private void EndCharge()
    {
        state = State.Chase;
        enemy.externalMovementControl = false;
        enemy.externalFlipControl = false; // 돌진이 끝났으니 다시 플레이어 위치에 따라 좌우반전
        ResetAnim();
    }

    private void CancelCharge()
    {
        state = State.Chase;
        enemy.externalMovementControl = false;
        enemy.externalFlipControl = false;
        enemy.SetExternalOutline(false, Color.white);
        ResetAnim();
    }

    private void ResetAnim()
    {
        animFrame = 0;
        animTimer = 0f;
    }

    private void AdvanceLoop(float speedMultiplier)
    {
        if (moveFrames == null || moveFrames.Length == 0) return;

        animTimer += Time.deltaTime * speedMultiplier;
        float frameDuration = 1f / frameRate;
        if (animTimer >= frameDuration)
        {
            animTimer -= frameDuration;
            animFrame = (animFrame + 1) % moveFrames.Length;
        }
        spriteRenderer.sprite = moveFrames[animFrame];
    }
}
