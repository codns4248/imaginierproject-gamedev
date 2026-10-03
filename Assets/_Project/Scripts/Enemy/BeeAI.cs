using UnityEngine;

// 뱀서라이크의 "벌떼" 몬스터: 호밍 없이 스폰 시 정해진 방향으로만 직진 관통한다.
// 플레이어를 그냥 지나쳐서 계속 날아가다가 카메라 시야를 완전히 벗어나면 사라진다
// (맵 경계는 상관하지 않는다 - BeeSwarmSpawner가 Enemy.mapHalfExtent를 아주 크게 잡아둔다).
// 맞아도 넉백/경직으로 멈추지 않는다 - Bee.prefab의 Enemy.knockbackForce를 0으로 둬서
// Enemy.cs의 피격 경직 이동(넉백)이 사실상 아무 것도 안 하게 만들고, 이 컴포넌트는 피격 상태와
// 상관없이 매 프레임 그대로 계속 날아간다.
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class BeeAI : MonoBehaviour
{
    public float speed = 6f;
    public float frameRate = 12f;
    public Sprite[] moveFrames;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private Camera mainCamera;

    private Vector2 direction;
    private bool launched;
    private bool hasEnteredScreen; // 스폰 직후(화면 밖) 바로 소멸하지 않도록, 한 번 화면에 들어온 뒤에만 밖으로 나가면 소멸시킨다.

    private int animFrame;
    private float animTimer;

    // 벌떼만 별도로 세는 카운트 (EnemyManager.ActiveEnemies는 전체 몬스터 공용이라, 벌떼 전용 상한은
    // 따로 관리해야 한다 - BeeSwarmSpawner가 웨이브 스폰 전에 이 값을 확인한다).
    public static int ActiveCount { get; private set; }

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        // 추격/좌우반전 둘 다 Enemy.cs의 기본 로직을 쓰지 않는다 - 직진 방향에 맞춰 이 컴포넌트가 직접 정한다.
        enemy.externalMovementControl = true;
        enemy.externalFlipControl = true;
    }

    void OnEnable() => ActiveCount++;
    void OnDisable() => ActiveCount--;

    void Start()
    {
        mainCamera = Camera.main;
    }

    // BeeSwarmSpawner가 스폰 직후 한 번 호출해서 날아갈 방향을 고정한다. 이후로는 절대 바뀌지 않는다.
    public void Launch(Vector2 fireDirection)
    {
        direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : Vector2.right;
        spriteRenderer.flipX = direction.x > 0f; // 원본 스프라이트가 왼쪽을 보는 관례 - 오른쪽으로 갈 때만 반전
        launched = true;
    }

    void Update()
    {
        if (enemy.IsDying || !launched) return;

        AdvanceLoop();

        Vector3 viewport = mainCamera.WorldToViewportPoint(transform.position);
        bool onScreen = viewport.z > 0f && viewport.x > -0.05f && viewport.x < 1.05f
                                         && viewport.y > -0.05f && viewport.y < 1.05f;

        if (onScreen) hasEnteredScreen = true;
        else if (hasEnteredScreen) Destroy(gameObject); // 화면에 한 번 들어왔다가 다시 나가면 소멸
    }

    void FixedUpdate()
    {
        if (enemy.IsDying || !launched) return;

        // 피격 경직 중이어도(Enemy.cs가 넉백 이동을 시도하지만 knockbackForce=0이라 실질적으로 제자리 유지)
        // 이 컴포넌트는 상관없이 항상 정해진 방향으로 계속 날아간다.
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    private void AdvanceLoop()
    {
        if (moveFrames == null || moveFrames.Length == 0) return;

        animTimer += Time.deltaTime;
        float frameDuration = 1f / frameRate;
        if (animTimer >= frameDuration)
        {
            animTimer -= frameDuration;
            animFrame = (animFrame + 1) % moveFrames.Length;
        }
        spriteRenderer.sprite = moveFrames[animFrame];
    }
}
