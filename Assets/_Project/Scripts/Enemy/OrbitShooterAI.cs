using UnityEngine;

// NASA "부유 촉수체": 멀리 있으면 평소처럼 다가오다가, 플레이어와 orbitRadius 근처까지 오면 플레이어 주위를 원을 그리며 돌고
// attackInterval마다 산성 침(적 투사체)을 쏜다. 도는 방향은 마리마다 무작위. 맞으면(피격 경직) 잠깐 도는 것을 멈추고 밀려난다.
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(Rigidbody2D))]
public class OrbitShooterAI : MonoBehaviour
{
    public float orbitRadius = 1.8f;
    [Tooltip("궤도에 들어간 뒤 원을 따라 도는 속도(초당 각도)")]
    public float orbitDegreesPerSecond = 45f;

    [Header("공격")]
    public float attackInterval = 2.2f;
    // 준비 동작: 쏘기 전 이 시간 동안 도는 것을 멈추고 주황 테두리를 깜빡인다 (예고 없이 날아오는 탄은 피하기 어렵다는 피드백).
    public float windupTime = 0.5f;
    public Color windupOutlineColor = new Color(1f, 0.5f, 0.1f, 1f);
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f / 1.5f;
    public int projectileDamage = 1;
    public Color projectileTint = new Color(0.55f, 1f, 0.45f); // 산성 침 (초록)

    private Enemy enemy;
    private Rigidbody2D rb;
    private Transform player;
    private float orbitSign;
    private float attackTimer;
    private float windupTimer = -1f; // 0 이상이면 준비 동작 중
    private bool orbiting;
    private Camera mainCamera;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody2D>();
        orbitSign = Random.value < 0.5f ? -1f : 1f;
    }

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
        mainCamera = Camera.main;
        attackTimer = attackInterval;
    }

    void Update()
    {
        if (enemy.IsDying) { StopWindup(); return; }
        if (player == null || EnemyManager.PlayerDead) return;

        float dist = Vector2.Distance(transform.position, player.position);
        // 궤도보다 꽤 멀어지면 다시 평소 추격으로 다가오고, 궤도 근처에 오면 직접 돈다.
        if (!orbiting && dist <= orbitRadius + 0.2f) orbiting = true;
        else if (orbiting && dist > orbitRadius + 1.2f) orbiting = false;
        enemy.externalMovementControl = orbiting;

        if (!orbiting) { StopWindup(); return; }
        if (enemy.IsHitStunned) return;

        if (windupTimer >= 0f)
        {
            windupTimer -= Time.deltaTime;
            bool outlineOn = windupTimer < windupTime * 0.35f || Mathf.Repeat(windupTimer, 0.16f) > 0.08f;
            enemy.SetExternalOutline(outlineOn, windupOutlineColor);
            if (windupTimer > 0f) return;
            StopWindup();
            Fire();
            return;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;
        attackTimer = attackInterval;
        if (!IsOnScreen()) return; // 화면 밖에서는 쏘지 않는다 (이번 발사는 건너뛴다)
        windupTimer = windupTime;
    }

    private void StopWindup()
    {
        if (windupTimer < 0f) return;
        windupTimer = -1f;
        enemy.SetExternalOutline(false, windupOutlineColor);
    }

    private bool IsOnScreen()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return true;
        Vector3 v = mainCamera.WorldToViewportPoint(transform.position);
        return v.x > 0.03f && v.x < 0.97f && v.y > 0.05f && v.y < 0.95f;
    }

    void FixedUpdate()
    {
        if (!orbiting || enemy.IsDying || enemy.IsHitStunned || player == null || EnemyManager.PlayerDead) return;
        if (windupTimer >= 0f) return; // 준비 동작 중에는 제자리에 떠 있는다

        // 지금 각도에서 조금 더 돈 궤도 위의 점을 향해, 평소 이동속도로 다가간다 (반경도 그쪽으로 조금씩 맞춰진다).
        Vector2 center = player.position;
        Vector2 offset = rb.position - center;
        float angle = Mathf.Atan2(offset.y, offset.x) + orbitSign * orbitDegreesPerSecond * Mathf.Deg2Rad * Time.fixedDeltaTime * 4f;
        Vector2 target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
        Vector2 next = Vector2.MoveTowards(rb.position, target, enemy.EffectiveMoveSpeed * Time.fixedDeltaTime);

        float limit = enemy.mapHalfExtent - enemy.boundaryMargin;
        next.x = Mathf.Clamp(next.x, enemy.mapCenter.x - limit, enemy.mapCenter.x + limit);
        next.y = Mathf.Clamp(next.y, enemy.mapCenter.y - limit, enemy.mapCenter.y + limit);
        rb.MovePosition(enemy.BlockByObstacles(next, detour: true)); // 지형지물은 통과하지 않는다
    }

    private void Fire()
    {
        if (projectilePrefab == null) return;
        Vector2 dir = (Vector2)(player.position - transform.position);
        GameObject go = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        EnemyProjectile proj = go.GetComponent<EnemyProjectile>();
        if (proj == null) return;
        proj.speed = projectileSpeed;
        proj.damage = projectileDamage;
        proj.Launch(dir);
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = projectileTint;
    }
}
