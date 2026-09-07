using UnityEngine;

// 원거리 몬스터(RangedEnemyAI)가 플레이어에게 쏘는 투사체. 발사 시 정해진 방향으로 직진하다가
// 플레이어에 닿으면 데미지를 주고 사라진다. 플레이어 무기의 Projectile.cs와 반대로,
// 이건 적 -> 플레이어 방향으로 데미지를 준다는 점만 다르고 나머지(직진, 화면 밖 자동 소멸)는 동일하다.
public class EnemyProjectile : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 1;

    private Vector2 direction;
    private Rigidbody2D rb;
    private Camera mainCamera;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // 붉은 원 스프라이트는 코드로 즉석에서 그린 것(에셋 파일이 없음)이라 프리팹에 그대로
        // 저장되지 않는다. ExplosionZone과 마찬가지로 매번 실행 시점에 직접 채워준다.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null) sr.sprite = ExplosionZone.GetCircleSprite();
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    // 발사한 몬스터가 생성 직후 호출해서 날아갈 방향을 정해준다.
    public void Launch(Vector2 fireDirection)
    {
        direction = fireDirection.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    void Update()
    {
        // 카메라 시야를 벗어나면 더 이상 필요 없으니 제거한다 (Projectile.cs와 동일한 방식).
        Vector3 viewport = mainCamera.WorldToViewportPoint(transform.position);
        bool onScreen = viewport.z > 0f && viewport.x > -0.05f && viewport.x < 1.05f
                                         && viewport.y > -0.05f && viewport.y < 1.05f;
        if (!onScreen)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        Vector2 knockbackDirection = (Vector2)other.transform.position - (Vector2)transform.position;
        playerHealth.TakeHit(damage, knockbackDirection);

        Destroy(gameObject);
    }
}
