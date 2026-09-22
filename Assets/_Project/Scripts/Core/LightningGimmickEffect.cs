using System.Collections;
using UnityEngine;

// 기믹 "벼락": 활성화된 동안 일정 주기로 카메라가 비추는 영역 안의 무작위 지점에 붉은 경고 장판을
// 띄우고, warningDuration(1.5초) 뒤 그 자리에 벼락이 떨어져 반경 안의 플레이어와 몬스터 모두에게
// 고정 데미지를 준다. 경고 장판은 전용 아트가 없어 런타임 생성 원형 텍스처를 쓴다.
// 데미지 수치는 기획 확정 전까지의 플레이스홀더 (Inspector에서 바로 조절 가능).
public class LightningGimmickEffect : MonoBehaviour
{
    [Header("주기/타이밍")]
    public float strikeInterval = 2.5f;   // 장판이 새로 뜨는 간격
    public float warningDuration = 1.5f;  // 장판이 뜬 뒤 실제로 벼락이 치기까지 걸리는 시간
    public float strikeRadius = 1.5f;

    [Header("데미지 (플레이스홀더 수치)")]
    public int playerDamage = 2;
    public float enemyDamage = 5f;

    private Coroutine loopRoutine;

    void Awake()
    {
        StageGimmickManager.OnGimmickStart += HandleStart;
        StageGimmickManager.OnGimmickEnd += HandleEnd;
    }

    void OnDestroy()
    {
        StageGimmickManager.OnGimmickStart -= HandleStart;
        StageGimmickManager.OnGimmickEnd -= HandleEnd;
    }

    private void HandleStart(GimmickType type)
    {
        if (type != GimmickType.Lightning) return;
        if (loopRoutine != null) StopCoroutine(loopRoutine);
        loopRoutine = StartCoroutine(StrikeLoop());
    }

    private void HandleEnd(GimmickType type)
    {
        if (type != GimmickType.Lightning) return;
        if (loopRoutine != null) { StopCoroutine(loopRoutine); loopRoutine = null; }
    }

    private IEnumerator StrikeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(strikeInterval);
            Vector2 point = PickRandomPointInCameraView();
            StartCoroutine(StrikeAt(point));
        }
    }

    // 카메라(직교 투영)가 지금 비추고 있는 사각 영역 안에서 무작위 지점을 고른다.
    private Vector2 PickRandomPointInCameraView()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.zero;

        Vector2 center = cam.transform.position;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        // 화면 가장자리에 너무 붙지 않도록 살짝 여유를 둔다.
        const float margin = 0.85f;
        return center + new Vector2(Random.Range(-halfW, halfW) * margin, Random.Range(-halfH, halfH) * margin);
    }

    private IEnumerator StrikeAt(Vector2 point)
    {
        GameObject marker = CreateWarningMarker(point);

        yield return new WaitForSeconds(warningDuration);

        ApplyStrikeDamage(point);

        if (marker != null) Destroy(marker);
    }

    private GameObject CreateWarningMarker(Vector2 point)
    {
        GameObject go = new GameObject("LightningWarning");
        go.transform.position = point;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GimmickTextureUtil.CreateFilledCircle(128, new Color(0.9f, 0.1f, 0.1f, 0.55f), 0.85f);
        sr.sortingOrder = 5; // 캐릭터/이펙트보다 위에 보이도록

        // CreateFilledCircle은 pixelsPerUnit을 텍스처 크기로 맞춰 스프라이트가 정확히 1유닛이 되므로,
        // strikeRadius*2(지름)만큼 그대로 스케일하면 실제 피해 반경과 시각적 크기가 일치한다.
        float scale = strikeRadius * 2f;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        return go;
    }

    private void ApplyStrikeDamage(Vector2 point)
    {
        GameObject player = GameObject.Find("Player");
        if (player != null && Vector2.Distance(player.transform.position, point) <= strikeRadius)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null) health.TakeHit(playerDamage, Vector2.zero);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(point, strikeRadius);
        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeDamage(enemyDamage);
        }
    }
}
