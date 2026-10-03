using UnityEngine;

// 사거리 무제한 + 절대 이동하지 않는 매복형 몬스터용 행동 컴포넌트.
// Idle로 가만히 대기하다가(이 상태일 때만 공격당할 수 있다) 일정 주기마다:
//   Idle -> Diving(Dive 모션 재생, 모션에 맞춰 살짝 앞으로 이동) -> Gone(완전히 사라짐, preAoeDelay 뒤
//   플레이어 위치에 경고 장판 생성) -> Emerging(Dive 이동분을 되돌려 원래 있던 자리에서 Emerge 모션 재생)
//   -> Idle
// 순서로 무한 반복한다. Diving/Gone/Emerging 구간에는 콜라이더를 꺼서 아예 공격받지 않는다.
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(SpriteRenderer))]
public class AmbushCasterAI : MonoBehaviour
{
    [Header("대기/공격 주기")]
    public float idleDuration = 8f; // Idle로 대기하다가 다음 공격 시전까지 걸리는 시간

    [Header("애니메이션 (전부 같은 frameRate로 재생)")]
    public float frameRate = 12f;
    public Sprite[] idleFrames;   // 반복 재생
    public Sprite[] diveFrames;   // 한 번만 재생 후 사라짐
    public Sprite[] emergeFrames; // 한 번만 재생하며 다시 나타남

    [Header("Dive 이동 (모션에 맞춰 살짝 앞으로 나가는 느낌 - 실제 위치엔 영향 없음, Emerge는 항상 원래 자리)")]
    public float diveMoveDistance = 0.5f; // Dive 모션 재생 시간 동안 앞으로 이동하는 총 거리

    [Header("범위 공격 (PlayerAoeZone - 프리팹 없이 코드로 직접 생성)")]
    public float preAoeDelay = 0.5f; // Dive가 끝나고(완전히 숨은 뒤) 장판이 뜨기까지 기다리는 시간
    public float aoeWarningDuration = 1.5f; // 장판이 뜬 뒤 실제로 터지기까지
    public float aoeRadius = 1.5f;
    public int aoeDamage = 1;
    public float reemergeDelay = 0.5f; // 장판이 터진 뒤 다시 나타나기까지 추가로 기다리는 시간

    private enum State { Idle, Diving, Gone, Emerging }
    private State state = State.Idle;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private Transform player;

    private float idleTimer;
    private float goneTimer;
    private bool zoneSpawned;
    private int animFrame;
    private float animTimer;

    private Vector3 idlePosition; // Dive 시작 직전 위치 - Emerge는 항상 이 자리에서 재생한다
    private float diveElapsed;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        enemy.externalMovementControl = true; // 사거리 무제한 - 이 몬스터는 절대 움직이지 않는다
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

        switch (state)
        {
            case State.Idle:
                AdvanceLoop(idleFrames);
                idleTimer += Time.deltaTime;
                if (idleTimer >= idleDuration)
                {
                    idleTimer = 0f;
                    StartDive();
                }
                break;

            case State.Diving:
                AdvanceDiveMove();
                if (AdvanceOnce(diveFrames)) StartGone();
                break;

            case State.Gone:
                goneTimer += Time.deltaTime;
                if (!zoneSpawned && goneTimer >= preAoeDelay)
                {
                    zoneSpawned = true;
                    SpawnAoeZone();
                }
                if (goneTimer >= preAoeDelay + aoeWarningDuration + reemergeDelay) StartEmerge();
                break;

            case State.Emerging:
                if (AdvanceOnce(emergeFrames)) StartIdle();
                break;
        }
    }

    private void StartDive()
    {
        state = State.Diving;
        enemy.externalFlipControl = true; // 시퀀스 도중엔 방향을 고정
        if (col != null) col.enabled = false; // 이제부터 다시 Idle로 돌아오기 전까지 공격받지 않는다
        idlePosition = transform.position; // Dive로 이동하기 전 원래 자리를 기억해둔다 (Emerge 복귀용)
        diveElapsed = 0f;
        ResetAnim();
    }

    // Dive 모션이 재생되는 동안 스프라이트가 보고 있는 방향으로 diveMoveDistance만큼 서서히 이동한다.
    // 실제 이동일 뿐 "원래 자리"(idlePosition) 자체를 바꾸는 건 아니라서 Emerge 복귀엔 영향이 없다.
    private void AdvanceDiveMove()
    {
        if (diveFrames == null || diveFrames.Length == 0 || frameRate <= 0f) return;

        diveElapsed += Time.deltaTime;
        float diveDuration = diveFrames.Length / frameRate;
        float t = diveDuration > 0f ? Mathf.Clamp01(diveElapsed / diveDuration) : 1f;

        Vector3 dir = spriteRenderer.flipX ? Vector3.right : Vector3.left;
        transform.position = idlePosition + dir * diveMoveDistance * t;
    }

    private void StartGone()
    {
        state = State.Gone;
        goneTimer = 0f;
        zoneSpawned = false;
        spriteRenderer.enabled = false; // 완전히 사라짐 - 장판은 preAoeDelay만큼 지난 뒤에야 뜬다
    }

    private void SpawnAoeZone()
    {
        if (player == null) return;

        GameObject zoneGO = new GameObject("PlayerAoeZone");
        zoneGO.transform.position = player.position;
        PlayerAoeZone zone = zoneGO.AddComponent<PlayerAoeZone>();
        zone.Init(aoeDamage, aoeWarningDuration, aoeRadius);
    }

    private void StartEmerge()
    {
        state = State.Emerging;
        transform.position = idlePosition; // Dive 중 이동했던 거리를 되돌려서, 항상 처음 대기하던 자리에서 나타난다
        spriteRenderer.enabled = true;
        ResetAnim();
    }

    private void StartIdle()
    {
        state = State.Idle;
        enemy.externalFlipControl = false; // 다시 평소처럼 플레이어 방향 보고 좌우반전
        if (col != null) col.enabled = true; // 다시 공격받을 수 있는 상태로
        ResetAnim();
    }

    private void ResetAnim()
    {
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

    // 반복하지 않고 한 번만 끝까지 재생한다. 다 재생했으면 true를 반환한다 (Dive/Emerge용).
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
