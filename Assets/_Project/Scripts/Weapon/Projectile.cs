using UnityEngine;

// 무기에서 발사되는 투사체. 적을 관통하며 날아가다가, 정해진 횟수만큼 적과 접촉하거나
// 카메라 화면 밖으로 나가면 스스로 사라진다.
public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    public int maxHits = 3; // 이 횟수만큼 적과 접촉하면 소멸

    private float damage;
    private Vector2 direction;
    private int hitCount;

    private Rigidbody2D rb;
    private Camera mainCamera;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    // WeaponAim이 발사 순간 방향과 데미지를 넘겨줄 때 호출한다.
    public void Fire(Vector2 fireDirection, float fireDamage)
    {
        direction = fireDirection.normalized;
        damage = fireDamage;

        // 스프라이트가 기본적으로 위(90도)를 향하고 있으므로, 실제 진행 방향에 맞춰 -90도 보정해서 회전시킨다.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    void Update()
    {
        // 카메라 시야를 벗어나면 더 이상 필요 없으니 제거한다.
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
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null) return;

        enemy.Hit(direction, damage);

        // Destroy하지 않고 계속 날아가서 다음 적도 관통한다. 정해진 횟수만큼 맞히면 그때 소멸.
        hitCount++;
        if (hitCount >= maxHits)
        {
            Destroy(gameObject);
        }
    }
}
