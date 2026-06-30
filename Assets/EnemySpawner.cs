using UnityEngine;

/// <summary>
/// 일정 시간마다 화면 밖 랜덤 위치에 적을 소환하는 스크립트.
/// 동시에 존재할 수 있는 적의 최대 수를 50마리로 제한한다.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // 적 소환 간격 (초)
    public float spawnInterval = 1f;
    // 소환할 적의 스프라이트 (Inspector에서 할당)
    public Sprite enemySprite;
    // 소환할 적의 애니메이터 컨트롤러 (Inspector에서 할당)
    public RuntimeAnimatorController enemyAnimatorController;

    // 소환 간격을 추적하는 타이머
    private float timer = 0f;

    // 맵 경계값 (이 범위 안에서만 소환 위치를 결정)
    private float minX = -24.5f;
    private float maxX = 24.5f;
    private float minY = -14.45f;
    private float maxY = 14.45f;

    void Update()
    {
        timer += Time.deltaTime;

        // spawnInterval마다 소환 시도
        if (timer >= spawnInterval)
        {
            timer = 0f;

            // 화면에 적이 50마리 미만일 때만 소환
            if (EnemyController.ActiveEnemies.Count < 50)
            {
                SpawnEnemy();
            }
        }
    }

    /// <summary>
    /// 메인 카메라 기준 화면 밖 랜덤 위치에 적 오브젝트를 생성한다.
    /// 카메라 외곽 15~20 유닛 거리의 랜덤 방향에 소환을 시도하며,
    /// 유효한 위치를 찾지 못하면 맵 전체에서 랜덤 위치에 소환한다.
    /// </summary>
    void SpawnEnemy()
    {
        Vector3 spawnPos = Vector3.zero;
        bool validPos = false;
        Camera mainCam = Camera.main;

        if (mainCam == null) return;

        // 화면 밖 위치 탐색 (최대 10회 시도)
        for (int i = 0; i < 10; i++)
        {
            // 카메라 중심에서 랜덤 방향으로 15~20 유닛 거리에 후보 위치 설정
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float dist = Random.Range(15f, 20f);
            Vector3 testPos = mainCam.transform.position + new Vector3(dir.x, dir.y, 0f) * dist;

            // 맵 경계 안으로 클램핑
            testPos.x = Mathf.Clamp(testPos.x, minX, maxX);
            testPos.y = Mathf.Clamp(testPos.y, minY, maxY);

            // 뷰포트 좌표로 변환해 화면 밖인지 확인
            Vector3 screenPoint = mainCam.WorldToViewportPoint(testPos);
            if (screenPoint.x < 0f || screenPoint.x > 1f || screenPoint.y < 0f || screenPoint.y > 1f)
            {
                testPos.z = 0f;
                spawnPos = testPos;
                validPos = true;
                break;
            }
        }

        // 화면 밖 위치를 찾지 못한 경우 맵 전체에서 랜덤 소환
        if (!validPos)
            spawnPos = new Vector3(Random.Range(minX, maxX), Random.Range(minY, maxY), 0f);
        else
            spawnPos.z = 0f;

        // 적 오브젝트 생성 및 컴포넌트 추가
        GameObject enemy = new GameObject("Enemy");
        enemy.transform.position = spawnPos;

        // 스프라이트 렌더러 설정
        SpriteRenderer sr = enemy.AddComponent<SpriteRenderer>();
        sr.sprite = enemySprite;
        sr.sortingOrder = 1;

        // 애니메이터 설정
        Animator anim = enemy.AddComponent<Animator>();
        anim.runtimeAnimatorController = enemyAnimatorController;

        // 적 AI 컴포넌트 추가 (이동, 체력, 피격 처리)
        enemy.AddComponent<EnemyController>();
    }
}
