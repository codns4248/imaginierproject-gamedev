using UnityEngine;

// 근접 추격형 몬스터와 달리, 플레이어가 chaseStopRadius 안에 들어오면 추격을 멈추고 제자리에서
// 원거리 공격을 하는 몬스터용 행동 컴포넌트. 피격/사망/테두리/좌우반전/드랍 등 공통 기능은
// Enemy.cs가 그대로 처리하고, 이 컴포넌트는 "이동 상태(추격/대기/공격)"와 그에 맞는 애니메이션,
// 원거리 공격만 담당한다 (Enemy.externalMovementControl로 Enemy.cs의 추격 이동만 잠깐 빌린다).
//
// SpriteAnimator는 반복 재생 애니메이션 하나만 지원해서(상태 전환 불가), 이동/대기/공격 3가지
// 모션이 필요한 이 몬스터는 SpriteAnimator를 쓰지 않고 스프라이트를 직접 갈아끼운다.
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(SpriteRenderer))]
public class RangedEnemyAI : MonoBehaviour
{
    [Header("추격 중지 범위")]
    // 이 범위 안에 플레이어가 들어오면 추격을 멈추고 그 자리에서 원거리 공격을 시작한다.
    public float chaseStopRadius = 3f;

    [Header("공격")]
    public float attackInterval = 2f;
    // 준비 동작: 쏘기 전에 공격 모션 첫 프레임으로 멈춰 서서 주황 테두리를 깜빡이다가, 이 시간이 지나면 발사한다
    // (2026-11-18: 가까이서 아무 예고 없이 날아오는 탄은 피하기 어렵다는 플레이 피드백).
    public float windupTime = 0.5f;
    public Color windupOutlineColor = new Color(1f, 0.5f, 0.1f, 1f);
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f / 1.5f; // 플레이어 무기 투사체 기본 속도(10)보다 1.5배 느리게
    public int projectileDamage = 1;

    [Header("애니메이션 (상태별 프레임, 전부 같은 frameRate로 재생)")]
    public float frameRate = 12f;
    public Sprite[] moveFrames;
    public Sprite[] idleFrames;
    public Sprite[] attackFrames;

    private enum State { Move, Idle, Windup, Attack }
    private State state = State.Move;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private Camera mainCamera;

    private float attackCooldown;
    private float windupTimer;
    private int animFrame;
    private float animTimer;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
        mainCamera = Camera.main;

        attackCooldown = attackInterval;
    }

    void Update()
    {
        if (enemy.IsDying) { CancelWindup(); return; }
        if (player == null) return;
        if (EnemyManager.PlayerDead) return;

        // 화면 밖에서는 쏘지 않는다: 사거리 안이어도 화면에 들어올 때까지는 계속 다가온다
        // (카메라가 세로로 좁아서 위/아래 사거리 끝은 화면 밖이었다).
        float dist = Vector2.Distance(transform.position, player.position);
        bool inRange = dist <= chaseStopRadius && IsOnScreen();
        enemy.externalMovementControl = inRange; // 범위 안에 있을 때만 Enemy.cs의 추격 이동을 꺼둔다

        if (!inRange)
        {
            CancelWindup();
            SetState(State.Move);
            attackCooldown = attackInterval; // 다시 사거리에 들어왔을 때 바로 쏘지 않도록 쿨다운을 리셋
            AdvanceLoop(moveFrames);
            return;
        }

        if (enemy.IsHitStunned) return; // 피격 경직 중에는 애니메이션도, 공격 타이머도, 준비 동작도 멈춘다

        if (state == State.Attack)
        {
            if (AdvanceOnce(attackFrames)) SetState(State.Idle);
            return;
        }

        if (state == State.Windup)
        {
            windupTimer -= Time.deltaTime;
            // 테두리 깜빡임: 처음엔 깜빡이다가 발사 직전(남은 35%)엔 계속 켜 둔다
            bool outlineOn = windupTimer < windupTime * 0.35f || Mathf.Repeat(windupTimer, 0.16f) > 0.08f;
            enemy.SetExternalOutline(outlineOn, windupOutlineColor);
            if (windupTimer > 0f) return;

            enemy.SetExternalOutline(false, windupOutlineColor);
            FireProjectile();
            SetState(State.Attack);
            return;
        }

        attackCooldown -= Time.deltaTime;
        if (attackCooldown <= 0f)
        {
            attackCooldown = attackInterval;
            StartWindup();
            return;
        }

        SetState(State.Idle);
        AdvanceLoop(idleFrames);
    }

    // 준비 동작 시작: 공격 모션 첫 프레임으로 멈춰 선다 (깜빡이는 테두리는 Update에서).
    private void StartWindup()
    {
        SetState(State.Windup);
        windupTimer = windupTime;
        if (attackFrames != null && attackFrames.Length > 0) spriteRenderer.sprite = attackFrames[0];
    }

    private void CancelWindup()
    {
        if (state != State.Windup) return;
        enemy.SetExternalOutline(false, windupOutlineColor);
        SetState(State.Idle);
    }

    private bool IsOnScreen()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return true;
        Vector3 v = mainCamera.WorldToViewportPoint(transform.position);
        return v.x > 0.03f && v.x < 0.97f && v.y > 0.05f && v.y < 0.95f;
    }

    private void FireProjectile()
    {
        if (projectilePrefab == null || player == null) return;

        Vector2 dir = (Vector2)(player.position - transform.position);
        GameObject go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        EnemyProjectile proj = go.GetComponent<EnemyProjectile>();
        if (proj != null)
        {
            proj.speed = projectileSpeed;
            proj.damage = projectileDamage;
            proj.Launch(dir);
        }
    }

    // 상태가 바뀔 때만 애니메이션 프레임을 처음(0)부터 다시 시작한다.
    private void SetState(State newState)
    {
        if (state == newState) return;
        state = newState;
        animFrame = 0;
        animTimer = 0f;
    }

    private void AdvanceLoop(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) return;

        animTimer += Time.deltaTime;
        float frameDuration = 1f / (frameRate * StageGimmickManager.EnemySpeedMultiplier);
        if (animTimer >= frameDuration)
        {
            animTimer -= frameDuration;
            animFrame = (animFrame + 1) % frames.Length;
        }
        spriteRenderer.sprite = frames[animFrame];
    }

    // 반복하지 않고 한 번만 끝까지 재생한다. 다 재생했으면 true를 반환한다 (공격 모션용).
    private bool AdvanceOnce(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) return true;

        spriteRenderer.sprite = frames[Mathf.Min(animFrame, frames.Length - 1)];

        animTimer += Time.deltaTime;
        float frameDuration = 1f / (frameRate * StageGimmickManager.EnemySpeedMultiplier);
        if (animTimer >= frameDuration)
        {
            animTimer -= frameDuration;
            animFrame++;
            if (animFrame >= frames.Length) return true;
        }
        return false;
    }
}
