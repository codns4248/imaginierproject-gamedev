using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 스테이지-거점 이동 및 층수 진행을 담당한다.
// StageTimer가 스테이지 클리어를 알리면 스테이지 위쪽에 색깔이 다른 랜덤 포탈 3개를 띄운다.
// 5층마다(StageProgress.IsExtractionFloor)는 오른쪽에 추출(거점 복귀, 자원 확정) 포탈도 같이 뜬다.
// 플레이어가 어느 포탈에 닿는지는 StageExitPortal이 처리한다.
// 플레이어가 죽으면 파밍한 자원(아직 확정되지 않은 분)을 잃고, 사망 페이드가 끝난 뒤 거점으로 이동한다 (익스트랙션 실패).
// (조장 확인 완료: 사망 시 자원은 확정하지 않고 소실시키는 게 맞는 규칙 - 임시로 CommitRunToStash를 쓰던 걸 원복함)
// MainScene에 빈 오브젝트를 만들어 이 컴포넌트를 붙여두면 된다.
public class StageExtraction : MonoBehaviour
{
    // 스테이지 클리어 시 위에 뜨는 색깔 포탈이 연결되는 테마들 (거점 포탈과 색깔만 다름).
    private static readonly string[] PortalThemes = { "오염된 호수", "광산", "공장", "모래 황무지", "숲" };
    private static readonly Dictionary<string, Color> PortalColors = new Dictionary<string, Color>
    {
        { "오염된 호수", new Color(0.55f, 0.35f, 0.15f) }, // 갈색
        { "광산", new Color(0.35f, 0.35f, 0.37f) },        // 검회색 (순검정은 안 보여서 밝게 조정)
        { "공장", new Color(0.2f, 0.4f, 0.9f) },           // 파란색
        { "모래 황무지", new Color(1f, 0.55f, 0.1f) },      // 주황
        { "숲", new Color(0.2f, 0.8f, 0.3f) },              // 초록
    };
    private static readonly Color ExtractionPortalColor = new Color(1f, 1f, 1f, 0.5f); // 반투명 흰색

    private PlayerHealth playerHealth;
    private GameObject hubPortalTemplate;

    void Start()
    {
        StageTimer timer = FindFirstObjectByType<StageTimer>();
        if (timer != null) timer.OnStageClear += HandleStageClear;

        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null) playerHealth.OnDied += HandleDeath;

        // 색깔 포탈은 이 오브젝트를 복제해서 색만 다시 입힌다 (아트가 바뀌면 같이 따라감).
        StagePortal hubPortal = FindFirstObjectByType<StagePortal>();
        hubPortalTemplate = hubPortal != null ? hubPortal.gameObject : null;
        if (hubPortalTemplate == null) Debug.LogWarning("StageExtraction: 거점 포탈(StagePortal)을 찾지 못해 클리어 포탈을 만들 수 없음");
    }

    void HandleStageClear()
    {
        SpawnExitPortals();
    }

    // 현재 구역 위쪽에 랜덤 색깔 포탈 3개, 5층마다 오른쪽에 추출 포탈 1개를 추가로 띄운다.
    private void SpawnExitPortals()
    {
        Vector2 center = StageManager.CurrentZoneCenter;
        float halfExtent = StageManager.CurrentZoneHalfExtent;

        GameObject group = new GameObject("ExitPortals");

        List<string> pool = new List<string>(PortalThemes);
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int pick = Random.Range(0, pool.Count);
            string theme = pool[pick];
            pool.RemoveAt(pick);

            float x = center.x + (i - 1) * (halfExtent * 0.6f);
            float y = center.y + halfExtent - 2f;
            CreatePortal(group.transform, new Vector2(x, y), theme, PortalColors[theme]);
        }

        if (StageProgress.IsExtractionFloor)
        {
            float x = center.x + halfExtent - 2f;
            CreatePortal(group.transform, new Vector2(x, center.y), null, ExtractionPortalColor);
        }
    }

    private void CreatePortal(Transform parent, Vector2 position, string targetTheme, Color color)
    {
        if (hubPortalTemplate == null) return;

        GameObject go = Instantiate(hubPortalTemplate);
        go.name = string.IsNullOrEmpty(targetTheme) ? "Portal_추출" : "Portal_" + targetTheme;
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        // 복제해 온 거점 포탈 스크립트(랜덤 스테이지 진입)는 여기서 필요 없으니 끄고 지운다.
        StagePortal clonedHubBehaviour = go.GetComponent<StagePortal>();
        if (clonedHubBehaviour != null)
        {
            clonedHubBehaviour.enabled = false;
            Destroy(clonedHubBehaviour);
        }

        StageExitPortal portal = go.AddComponent<StageExitPortal>();
        portal.Init(targetTheme, color);
    }

    void HandleDeath()
    {
        ResourceBank.DiscardRun();
        StageProgress.ResetToFirstStage();
        StartCoroutine(ReturnToHubAfterFade());
    }

    // 죽는 연출(PlayerHealth의 화면 페이드)이 끝날 때까지 기다렸다가 거점으로 보낸다.
    // 페이드 도중에 바로 이동시키면 연출이 다 안 보이고 잘려나간다.
    private IEnumerator ReturnToHubAfterFade()
    {
        float delay = playerHealth != null ? playerHealth.deathFadeDuration : 1.5f;
        yield return new WaitForSeconds(delay);
        StageManager.ReturnToHub();
    }
}
