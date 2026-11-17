using UnityEngine;

// 일정 간격으로 카메라 시야 밖(화면 주변 띠, FindOffScreenPosition)의 맵 안 랜덤한 위치에 적을 스폰하고, 최대 마리 수를 넘지 않도록 제어한다.
public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 1f;  // 스폰 시도 간격(초)
    public int maxEnemies = 50;       // 동시에 존재할 수 있는 최대 적 수
    public Vector2 mapCenter = Vector2.zero; // 맵 경계 상자의 중심 월드 좌표
    public float mapHalfExtent = 20f; // 맵 경계. Player_Movement의 mapHalfExtent와 맞춰야 한다

    [Tooltip("비워두면 모든 스테이지에서 스폰. 값을 넣으면 StageManager.CurrentTheme이 이 이름과 같을 때만 이 스포너가 켜진다 (StageManager.SetInStage 참고).")]
    public string restrictToTheme = "";

    private Camera mainCamera;
    private bool isEliteSpawner;
    private int specialUnlockStage; // 0이면 일반 몬스터. 특수 패턴 몬스터는 이 층부터 점점 나온다.
    private int specialRampFloors;
    private float timer;

    void Start()
    {
        mainCamera = Camera.main;
        Enemy prefabEnemy = enemyPrefab != null ? enemyPrefab.GetComponent<Enemy>() : null;
        isEliteSpawner = prefabEnemy != null && prefabEnemy.IsEliteOrBoss;

        // 특수 패턴 몬스터는 초반에 안 나오고 층이 오를수록 점점 늘어난다 (MonsterDifficulty.SpecialSpawnChance).
        if (enemyPrefab != null)
        {
            if (enemyPrefab.GetComponent<ChargeEnemyAI>() != null) { specialUnlockStage = 4; specialRampFloors = 6; }
            else if (enemyPrefab.GetComponent<AmbushCasterAI>() != null) { specialUnlockStage = 5; specialRampFloors = 6; }
            else if (enemyPrefab.GetComponent<RangedEnemyAI>() != null) { specialUnlockStage = 6; specialRampFloors = 8; }
            else if (enemyPrefab.GetComponent<SelfDestructAI>() != null) { specialUnlockStage = 5; specialRampFloors = 6; } // 군부대 자폭 드론
            else if (enemyPrefab.GetComponent<OrbitShooterAI>() != null) { specialUnlockStage = 6; specialRampFloors = 8; } // NASA 부유 촉수체
        }
    }

    void Update()
    {
        // 플레이어가 사망하면 스폰도 함께 멈춘다.
        if (EnemyManager.PlayerDead) return;

        // 층이 오를수록 스폰 간격이 짧아진다 (MonsterDifficulty.SpawnRateMultiplier).
        timer += Time.deltaTime;
        if (timer < spawnInterval / MonsterDifficulty.SpawnRateFor(isEliteSpawner)) return;
        timer = 0f;

        // 특수 패턴 몬스터는 확률적으로 건너뛴다 (초반엔 거의 안 나오고 갈수록 100%에 가까워진다).
        if (specialUnlockStage > 0 && Random.value > MonsterDifficulty.SpecialSpawnChance(specialUnlockStage, specialRampFloors)) return;

        // 현재 살아있는 적이 최대치 이상이면 이번 스폰 시도는 건너뛴다.
        // (개수가 다시 최대치 밑으로 내려가면 다음 간격에 자동으로 재개된다)
        // 최대치는 층에 따라 늘어나는 전체 값(MonsterDifficulty.MaxConcurrentEnemies)이다 (이 스포너의 maxEnemies 필드는 더 이상 쓰이지 않는다).
        if (EnemyManager.ActiveEnemies.Count >= MonsterDifficulty.MaxConcurrentEnemies) return;

        Vector2 spawnPos = FindOffScreenPosition();
        GameObject enemyObj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        // 스폰한 적도 스포너와 같은 구역 경계를 갖도록 맞춰준다 (안 그러면 다음 물리 프레임에
        // 자기 기본값(월드 원점 기준)으로 순간이동해버려서 화면 밖으로 사라진다).
        Enemy enemy = enemyObj.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.mapCenter = mapCenter;
            enemy.mapHalfExtent = mapHalfExtent;

            // 스테이지 진행에 따른 난이도 상승 - 체력/이동속도/공격력 (MonsterDifficulty 참고).
            MonsterDifficulty.ApplyTo(enemy);
        }
    }

    // 화면 가장자리 밖으로 이 거리(월드 유닛)까지의 띠 안에서 스폰한다 (2026-11-18: 맵 전체 랜덤이면 멀리서 나온 몬스터가
    // 플레이어에게 오기까지 오래 걸렸다. 너무 붙어서 나오지도 않게 화면 밖 3.5유닛 안쪽 정도로 - 이동속도 0.8 기준 최대 4~5초 거리).
    private const float SpawnReach = 3.5f;
    private static readonly Collider2D[] obstacleBuf = new Collider2D[4];
    private static ContactFilter2D solidFilter = new ContactFilter2D { useTriggers = false };

    // 카메라 시야 밖이면서 맵 범위 안쪽인 랜덤 좌표를 찾는다: 먼저 화면 주변 띠에서, 못 찾으면(플레이어가 맵 구석이라 띠가
    // 대부분 맵 밖일 때 등) 예전처럼 맵 전체에서. 그래도 못 찾으면(맵이 화면보다 작을 때 등) 맵 모서리에 스폰한다.
    private Vector2 FindOffScreenPosition()
    {
        float limit = mapHalfExtent - 0.3f;
        if (mainCamera != null && mainCamera.orthographic)
        {
            Vector2 cam = mainCamera.transform.position;
            float halfH = mainCamera.orthographicSize + SpawnReach;
            float halfW = mainCamera.orthographicSize * mainCamera.aspect + SpawnReach;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                Vector2 world = new Vector2(cam.x + Random.Range(-halfW, halfW), cam.y + Random.Range(-halfH, halfH));
                if (Mathf.Abs(world.x - mapCenter.x) > limit || Mathf.Abs(world.y - mapCenter.y) > limit) continue;
                if (IsOnScreen(world) || IsInsideObstacle(world)) continue;
                return world;
            }
        }

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 world = new Vector2(mapCenter.x + Random.Range(-limit, limit), mapCenter.y + Random.Range(-limit, limit));
            if (IsOnScreen(world) || IsInsideObstacle(world)) continue;
            return world;
        }

        return new Vector2(mapCenter.x + mapHalfExtent - 0.5f, mapCenter.y + mapHalfExtent - 0.5f);
    }

    // WorldToViewportPoint는 화면 안이면 x/y가 0~1 사이, 카메라 뒤쪽이면 z가 음수로 나온다.
    // 살짝(-0.05~1.05) 여유를 둬서 화면 경계에 딱 걸쳐 스폰되는 것도 방지한다.
    private bool IsOnScreen(Vector2 world)
    {
        Vector3 viewport = mainCamera.WorldToViewportPoint(world);
        return viewport.z > 0f && viewport.x > -0.05f && viewport.x < 1.05f
                               && viewport.y > -0.05f && viewport.y < 1.05f;
    }

    // 바위/건물 같은 지형지물(트리거가 아니고 움직이지 않는 충돌체) 안에는 스폰하지 않는다 (몬스터는 이제 지형지물에 막힌다).
    private static bool IsInsideObstacle(Vector2 world)
    {
        int count = Physics2D.OverlapCircle(world, 0.25f, solidFilter, obstacleBuf);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = obstacleBuf[i];
            if (c == null || c.isTrigger) continue;
            Rigidbody2D body = c.attachedRigidbody;
            if (body == null || body.bodyType == RigidbodyType2D.Static) return true;
        }
        return false;
    }
}
