using UnityEngine;

// 뱀서라이크 벌떼 몬스터를 "웨이브" 단위(한 번에 여러 마리)로 스폰한다. 일반 EnemySpawner와는
// 스폰 방식 자체가 달라서(한 틱에 1마리씩이 아니라, 트리거될 때 한꺼번에 N마리) 별도 스크립트로 뺐다.
//
// 조건: 플레이어 기준 대각선 방향 + 맵 가장자리 + 카메라 밖을 동시에 만족하는 지점(코너 하나)을
// 찾아서, 그 코너 하나에서 웨이브 전체가 몰려나온다(사방에서 포위하는 다른 패턴과 구분하기 위해
// 웨이브 전체가 같은 코너를 공유한다). 조건을 만족하는 지점을 못 찾으면 그 틱은 그냥 스폰하지 않는다
// (억지로 아무 데나 스폰하지 않음).
public class BeeSwarmSpawner : MonoBehaviour
{
    [Header("이 스포너가 켜지는 맵 (비워두면 모든 맵)")]
    public string restrictToTheme = "모래 황무지";

    [Header("웨이브")]
    public GameObject beePrefab;
    public float waveInterval = 10f;   // 웨이브 사이 대기시간
    public int swarmSize = 10;        // 한 웨이브당 마리 수
    public int maxActiveBees = 30;    // 벌떼 전용 상한 (EnemyManager 전체 상한과는 별도)

    [Header("맵/카메라 (스폰 위치 판정용)")]
    public Vector2 mapCenter = Vector2.zero;
    public float mapHalfExtent = 20f;
    public float edgeMargin = 1f;          // 맵 가장자리에서 살짝 안쪽 여유
    [Range(0f, 1f)] public float diagonalRatioThreshold = 0.5f; // 대각선으로 인정하는 최소 비율(짧은 축/긴 축)
    public int findAttempts = 20;

    [Header("웨이브 내 분산")]
    public float spawnScatterRadius = 1.5f; // 코너 지점 주변으로 스폰 위치를 흩어두는 반경
    public float aimScatterRadius = 2.5f;   // 마리마다 목표점을 플레이어 주변으로 살짝 다르게

    private Camera mainCamera;
    private Transform player;
    private StageTimer stageTimer;
    private float timer;
    private string lastTheme; // 맵 진입 감지용 - CurrentTheme이 바뀐 순간을 잡아서 쿨타임을 새로 돌린다.

    void Start()
    {
        mainCamera = Camera.main;
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;

        // StageTimer.ClearStage()는 EnemySpawner 타입만 찾아서 꺼버리므로 이 스포너는 그 목록에
        // 안 걸린다 - 그래서 스테이지 클리어 후에도 계속 웨이브가 터지던 버그가 있었다.
        // StageManager/StageTimer 쪽 코드를 건드리지 않고, 여기서 직접 클리어 여부를 확인해서 막는다.
        stageTimer = FindFirstObjectByType<StageTimer>();
        if (stageTimer != null) stageTimer.OnStageClear += HandleStageClear;
    }

    void OnDestroy()
    {
        if (stageTimer != null) stageTimer.OnStageClear -= HandleStageClear;
    }

    // 웨이브 타이머와 스테이지 타이머가 같은 프레임에 겹치면, 그 프레임에 막 스폰된 벌떼는 아직
    // Enemy.Start()가 안 돌아서 EnemyManager.ActiveEnemies에 등록되기 전이다 - StageTimer.ClearStage()의
    // 전멸 처리(ActiveEnemies 순회)가 이걸 놓쳐서, 클리어 후에도 벌떼가 한 웨이브 더 날아오는 것처럼
    // 보이는 버그가 있었다. 등록 여부와 상관없이 존재하는 벌떼를 전부 직접 찾아서 지워 확실히 잡는다.
    private void HandleStageClear()
    {
        foreach (BeeAI bee in FindObjectsByType<BeeAI>(FindObjectsSortMode.None))
        {
            Destroy(bee.gameObject);
        }
    }

    void Update()
    {
        if (EnemyManager.PlayerDead || player == null) return;
        if (stageTimer != null && stageTimer.IsCleared) return; // 스테이지 클리어 후에는 새 웨이브를 내지 않는다

        bool inTargetTheme = string.IsNullOrEmpty(restrictToTheme) || StageManager.CurrentTheme == restrictToTheme;

        // 이 맵에 막 들어온 순간(테마가 바뀌어서 이 맵이 된 순간)엔 쿨타임을 0부터 다시 돌린다.
        // 그래야 맵에 들어가자마자 웨이브가 터지지 않고, 들어간 뒤 waveInterval만큼 지나야 첫 웨이브가 나온다.
        if (StageManager.CurrentTheme != lastTheme)
        {
            lastTheme = StageManager.CurrentTheme;
            if (inTargetTheme) timer = 0f;
        }

        if (!inTargetTheme) return;

        timer += Time.deltaTime;
        if (timer < waveInterval) return;

        if (BeeAI.ActiveCount >= maxActiveBees) return; // 상한 넘으면 타이머만 흐르고 웨이브는 안 터짐(다음 틱에 재시도)

        if (!TryFindWaveCorner(out Vector2 corner)) return; // 조건 안 맞으면 그냥 스킵 (강제로 아무 데나 스폰 안 함)

        timer = 0f;
        SpawnWave(corner);
    }

    // 플레이어 기준 대각선 + 맵 가장자리 + 카메라 밖을 동시에 만족하는 코너 지점을 찾는다.
    private bool TryFindWaveCorner(out Vector2 corner)
    {
        Vector2 playerPos = player.position;

        for (int attempt = 0; attempt < findAttempts; attempt++)
        {
            float signX = Random.value < 0.5f ? -1f : 1f;
            float signY = Random.value < 0.5f ? -1f : 1f;

            Vector2 candidate = new Vector2(
                mapCenter.x + signX * (mapHalfExtent - edgeMargin),
                mapCenter.y + signY * (mapHalfExtent - edgeMargin));

            Vector2 toCandidate = candidate - playerPos;
            if (toCandidate.sqrMagnitude < 0.01f) continue;

            float absX = Mathf.Abs(toCandidate.x);
            float absY = Mathf.Abs(toCandidate.y);
            float longer = Mathf.Max(absX, absY);
            float shorter = Mathf.Min(absX, absY);
            bool diagonalEnough = longer > 0f && shorter / longer >= diagonalRatioThreshold;
            if (!diagonalEnough) continue;

            Vector3 viewport = mainCamera.WorldToViewportPoint(candidate);
            bool offCamera = viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f
                                                || viewport.y < -0.05f || viewport.y > 1.05f;
            if (!offCamera) continue;

            corner = candidate;
            return true;
        }

        corner = Vector2.zero;
        return false;
    }

    private void SpawnWave(Vector2 corner)
    {
        Vector2 playerPos = player.position;

        for (int i = 0; i < swarmSize; i++)
        {
            Vector2 spawnPos = corner + Random.insideUnitCircle * spawnScatterRadius;
            Vector2 targetPos = playerPos + Random.insideUnitCircle * aimScatterRadius;
            Vector2 dir = targetPos - spawnPos;

            GameObject go = Instantiate(beePrefab, spawnPos, Quaternion.identity);

            Enemy enemy = go.GetComponent<Enemy>();
            if (enemy != null)
            {
                // 맵 밖까지 날아갈 수 있으니, 피격 경직 중 넉백 클램프(맵 경계)에 걸리지 않도록 아주 크게 잡아둔다.
                enemy.mapCenter = mapCenter;
                enemy.mapHalfExtent = mapHalfExtent + 500f;
                enemy.ApplyHealthMultiplier(MonsterDifficulty.HealthMultiplier);
            }

            BeeAI bee = go.GetComponent<BeeAI>();
            if (bee != null) bee.Launch(dir);
        }
    }
}
