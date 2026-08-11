using UnityEngine;

// 일정 간격으로 카메라 시야 밖의 맵 안 랜덤한 위치에 적을 스폰하고, 최대 마리 수를 넘지 않도록 제어한다.
public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnInterval = 1f;  // 스폰 시도 간격(초)
    public int maxEnemies = 50;       // 동시에 존재할 수 있는 최대 적 수
    public float mapHalfExtent = 20f; // 맵 경계. Player_Movement의 mapHalfExtent와 맞춰야 한다

    private Camera mainCamera;
    private float timer;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 플레이어가 사망하면 스폰도 함께 멈춘다.
        if (EnemyManager.PlayerDead) return;

        timer += Time.deltaTime;
        if (timer < spawnInterval) return;
        timer = 0f;

        // 현재 살아있는 적이 최대치 이상이면 이번 스폰 시도는 건너뛴다.
        // (개수가 다시 최대치 밑으로 내려가면 다음 간격에 자동으로 재개된다)
        if (EnemyManager.ActiveEnemies.Count >= maxEnemies) return;

        Vector2 spawnPos = FindOffScreenPosition();
        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
    }

    // 카메라 시야 밖이면서 맵 범위 안쪽인 랜덤 좌표를 찾는다.
    // 최대 30번까지 시도하고, 그래도 못 찾으면(맵이 화면보다 작을 때 등) 맵 모서리에 스폰한다.
    private Vector2 FindOffScreenPosition()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float x = Random.Range(-mapHalfExtent, mapHalfExtent);
            float y = Random.Range(-mapHalfExtent, mapHalfExtent);
            Vector3 world = new Vector3(x, y, 0f);

            // WorldToViewportPoint는 화면 안이면 x/y가 0~1 사이, 카메라 뒤쪽이면 z가 음수로 나온다.
            // 살짝(-0.05~1.05) 여유를 둬서 화면 경계에 딱 걸쳐 스폰되는 것도 방지한다.
            Vector3 viewport = mainCamera.WorldToViewportPoint(world);
            bool onScreen = viewport.z > 0f && viewport.x > -0.05f && viewport.x < 1.05f
                                             && viewport.y > -0.05f && viewport.y < 1.05f;
            if (!onScreen)
                return world;
        }

        return new Vector2(mapHalfExtent - 0.5f, mapHalfExtent - 0.5f);
    }
}
