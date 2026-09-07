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
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f / 1.5f; // 플레이어 무기 투사체 기본 속도(10)보다 1.5배 느리게
    public int projectileDamage = 1;

    [Header("애니메이션 (상태별 프레임, 전부 같은 frameRate로 재생)")]
    public float frameRate = 12f;
    public Sprite[] moveFrames;
    public Sprite[] idleFrames;
    public Sprite[] attackFrames;

    private enum State { Move, Idle, Attack }
    private State state = State.Move;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Transform player;

    private float attackCooldown;
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

        attackCooldown = attackInterval;
    }

    void Update()
    {
        if (enemy.IsDying || player == null) return;
        if (EnemyManager.PlayerDead) return;

        float dist = Vector2.Distance(transform.position, player.position);
        bool inRange = dist <= chaseStopRadius;
        enemy.externalMovementControl = inRange; // 범위 안에 있을 때만 Enemy.cs의 추격 이동을 꺼둔다

        if (!inRange)
        {
            SetState(State.Move);
            attackCooldown = attackInterval; // 다시 사거리에 들어왔을 때 바로 쏘지 않도록 쿨다운을 리셋
            AdvanceLoop(moveFrames);
            return;
        }

        if (enemy.IsHitStunned) return; // 피격 경직 중에는 애니메이션도, 공격 타이머도 멈춘다

        if (state == State.Attack)
        {
            if (AdvanceOnce(attackFrames)) SetState(State.Idle);
            return;
        }

        attackCooldown -= Time.deltaTime;
        if (attackCooldown <= 0f)
        {
            attackCooldown = attackInterval;
            FireProjectile();
            SetState(State.Attack);
            return;
        }

        SetState(State.Idle);
        AdvanceLoop(idleFrames);
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
        float frameDuration = 1f / frameRate;
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
        float frameDuration = 1f / frameRate;
        if (animTimer >= frameDuration)
        {
            animTimer -= frameDuration;
            animFrame++;
            if (animFrame >= frames.Length) return true;
        }
        return false;
    }
}
