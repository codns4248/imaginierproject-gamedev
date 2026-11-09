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
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f / 1.5f;
    public int projectileDamage = 1;
    public Color projectileTint = new Color(0.55f, 1f, 0.45f); // 산성 침 (초록)

    private Enemy enemy;
    private Rigidbody2D rb;
    private Transform player;
    private float orbitSign;
    private float attackTimer;
    private bool orbiting;

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
        attackTimer = attackInterval;
    }

    void Update()
    {
        if (enemy.IsDying || player == null || EnemyManager.PlayerDead) return;

        float dist = Vector2.Distance(transform.position, player.position);
        // 궤도보다 꽤 멀어지면 다시 평소 추격으로 다가오고, 궤도 근처에 오면 직접 돈다.
        if (!orbiting && dist <= orbitRadius + 0.2f) orbiting = true;
        else if (orbiting && dist > orbitRadius + 1.2f) orbiting = false;
        enemy.externalMovementControl = orbiting;

        if (!orbiting || enemy.IsHitStunned) return;
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f) return;
        attackTimer = attackInterval;
        Fire();
    }

    void FixedUpdate()
    {
        if (!orbiting || enemy.IsDying || enemy.IsHitStunned || player == null || EnemyManager.PlayerDead) return;

        // 지금 각도에서 조금 더 돈 궤도 위의 점을 향해, 평소 이동속도로 다가간다 (반경도 그쪽으로 조금씩 맞춰진다).
        Vector2 center = player.position;
        Vector2 offset = rb.position - center;
        float angle = Mathf.Atan2(offset.y, offset.x) + orbitSign * orbitDegreesPerSecond * Mathf.Deg2Rad * Time.fixedDeltaTime * 4f;
        Vector2 target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * orbitRadius;
        Vector2 next = Vector2.MoveTowards(rb.position, target, enemy.EffectiveMoveSpeed * Time.fixedDeltaTime);

        float limit = enemy.mapHalfExtent - enemy.boundaryMargin;
        next.x = Mathf.Clamp(next.x, enemy.mapCenter.x - limit, enemy.mapCenter.x + limit);
        next.y = Mathf.Clamp(next.y, enemy.mapCenter.y - limit, enemy.mapCenter.y + limit);
        rb.MovePosition(next);
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
