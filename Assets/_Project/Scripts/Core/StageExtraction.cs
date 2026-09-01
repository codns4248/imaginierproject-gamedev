using System.Collections;
using UnityEngine;

// 스테이지-거점 이동 및 층수 진행을 담당한다.
// StageTimer가 스테이지 클리어를 알리면: 5층마다(StageProgress.IsExtractionFloor) 자원을 확정하고
// 거점으로 이동, 그 외 층에서는 자원을 유지한 채 다음(랜덤) 스테이지로 넘어간다(익스트랙션 전까진 계속 위험 부담).
// 플레이어가 죽으면 파밍한 자원(아직 확정되지 않은 분)을 잃고, 사망 페이드가 끝난 뒤 거점으로 이동한다 (익스트랙션 실패).
// MainScene에 빈 오브젝트를 만들어 이 컴포넌트를 붙여두면 된다.
public class StageExtraction : MonoBehaviour
{
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
            StageManager.ReturnToHub();
        }
        else
        {
            StageProgress.AdvanceToNextStage();
            StageManager.EnterRandomStage();
        }
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
