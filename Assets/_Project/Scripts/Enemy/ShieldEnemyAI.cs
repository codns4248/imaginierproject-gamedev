using UnityEngine;

// 군부대 "진압 방패 로봇": 정면에서 날아오는 플레이어 탄(권총/기관단총 탄과 그 파편)을 방패로 막는다.
// 옆이나 뒤에서 맞히면 그대로 맞고, 근접 무기/폭발/검기 같은 탄이 아닌 공격은 막지 못한다.
// 플레이어 쪽으로 몸을 돌리는 데 turnDelay초가 걸려서, 빠르게 돌아 들어가면 등(맨몸)을 노릴 수 있다.
// 막는 판정은 Projectile.ProcessHit이 TryBlock으로 묻는다. 이동/애니메이션은 Enemy + SpriteAnimator 그대로.
[RequireComponent(typeof(Enemy))]
public class ShieldEnemyAI : MonoBehaviour
{
    [Tooltip("정면 기준 이 각도(±) 안에서 날아오는 탄을 막는다")]
    public float blockHalfAngle = 70f;
    [Tooltip("플레이어가 반대편으로 넘어갔을 때 몸을 돌리기까지 걸리는 시간(초)")]
    public float turnDelay = 0.8f;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private float facing = -1f; // -1 = 왼쪽(그림 원본 방향), +1 = 오른쪽
    private float turnTimer;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
        enemy.externalFlipControl = true; // 좌우 방향은 이 컴포넌트가 늦게 돌려서 정한다
        if (player != null) facing = player.position.x >= transform.position.x ? 1f : -1f;
        ApplyFacing();
    }

    void Update()
    {
        if (enemy.IsDying || player == null) return;

        float desired = player.position.x >= transform.position.x ? 1f : -1f;
        if (Mathf.Approximately(desired, facing)) { turnTimer = 0f; return; }

        turnTimer += Time.deltaTime;
        if (turnTimer < turnDelay) return;
        facing = desired;
        turnTimer = 0f;
        ApplyFacing();
    }

    private void ApplyFacing()
    {
        if (spriteRenderer != null) spriteRenderer.flipX = facing > 0f; // 원본 그림이 왼쪽을 보고 있다
    }

    // 탄이 travelDirection으로 날아와 이 몬스터를 맞혔을 때, 방패 정면이면 막고 true. 막으면 불꽃이 튄다.
    public bool TryBlock(Vector2 travelDirection, Vector2 hitPoint)
    {
        if (enemy.IsDying || enemy.IsStunned || travelDirection.sqrMagnitude < 0.0001f) return false;

        // 탄이 날아오는 쪽(진행 방향의 반대)이 방패가 향한 쪽 각도 안이면 막는다.
        Vector2 from = -travelDirection.normalized;
        if (Vector2.Angle(from, new Vector2(facing, 0f)) > blockHalfAngle) return false;

        Vector2 sparkPos = (Vector2)transform.position + new Vector2(facing * 0.15f, 0f);
        if (PixelFx.Play("hit_spark", sparkPos, 0.3f) == null)
            CircleFlash.Spawn(sparkPos, 0.15f, new Color(1f, 0.9f, 0.5f, 0.9f), 0.12f, spriteRenderer);
        return true;
    }
}
