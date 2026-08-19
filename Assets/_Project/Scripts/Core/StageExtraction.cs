using System.Collections;
using UnityEngine;

// 스테이지-로비 이동을 담당한다.
// StageTimer가 스테이지 클리어를 알리면 파밍한 자원을 영구 보관함에 확정하고 로비로 이동한다.
// 플레이어가 죽으면 파밍한 자원(아직 확정되지 않은 분)을 잃고, 사망 페이드가 끝난 뒤 로비로 이동한다 (익스트랙션 실패).
// MainScene에 빈 오브젝트를 만들어 이 컴포넌트를 붙여두면 된다.
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
        ResourceBank.CommitRunToStash();
        SceneTravel.GoTo(lobbySceneName, lobbySpawnId);
    }

    void HandleDeath()
    {
        ResourceBank.DiscardRun();
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
