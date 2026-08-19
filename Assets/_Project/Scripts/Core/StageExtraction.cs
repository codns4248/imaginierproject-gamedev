using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 스테이지-로비 이동 및 층수 진행을 담당한다.
// StageTimer가 스테이지 클리어를 알리면: 5층마다(StageProgress.IsExtractionFloor) 자원을 확정하고
// 로비로 이동, 그 외 층에서는 자원을 유지한 채 다음 층으로 넘어간다(익스트랙션 전까진 계속 위험 부담).
// 플레이어가 죽으면 파밍한 자원(아직 확정되지 않은 분)을 잃고, 사망 페이드가 끝난 뒤 로비로 이동한다 (익스트랙션 실패).
// MainScene에 빈 오브젝트를 만들어 이 컴포넌트를 붙여두면 된다.
//
// ponytail: 아직 구역별 실제 씬(숲/공장/오염호수 등)이 없어서 "다음 층"을 같은 씬 재시작으로 흉내낸다.
// 재시작하면 Player도 새로 생성되어 체력/부활 횟수가 초기화된다 - 구역 씬이 붙으면(김성철 브랜치 머지 후)
// 실제 씬 전환 + 플레이어 상태 유지 방식으로 교체해야 한다.
public class StageExtraction : MonoBehaviour
{
    public string lobbySceneName = "LobbyScene";
    public string lobbySpawnId = "FromStage";

    private PlayerHealth playerHealth;

    void Start()
    {
        StageTimer timer = FindFirstObjectByType<StageTimer>();
        if (timer != null) timer.OnStageClear += HandleStageClear;

        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null) playerHealth.OnDied += HandleDeath;
    }

    void HandleStageClear()
    {
        if (StageProgress.IsExtractionFloor)
        {
            ResourceBank.CommitRunToStash();
            StageProgress.ResetToFirstStage();
            SceneTravel.GoTo(lobbySceneName, lobbySpawnId);
        }
        else
        {
            StageProgress.AdvanceToNextStage();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    void HandleDeath()
    {
        ResourceBank.DiscardRun();
        StageProgress.ResetToFirstStage();
        StartCoroutine(ReturnToLobbyAfterFade());
    }

    // 죽는 연출(PlayerHealth의 화면 페이드)이 끝날 때까지 기다렸다가 로비로 보낸다.
    // 페이드 도중에 바로 씬을 끊으면 연출이 다 안 보이고 잘려나간다.
    private IEnumerator ReturnToLobbyAfterFade()
    {
        float delay = playerHealth != null ? playerHealth.deathFadeDuration : 1.5f;
        yield return new WaitForSeconds(delay);
        SceneTravel.GoTo(lobbySceneName, lobbySpawnId);
    }
}
