using UnityEngine;

// 스테이지-로비 이동을 담당한다.
// StageTimer가 스테이지 클리어를 알리면 파밍한 자원을 영구 보관함에 확정하고 로비로 이동한다.
// 플레이어가 죽으면 파밍한 자원(아직 확정되지 않은 분)을 잃는다 (익스트랙션 실패).
// MainScene에 빈 오브젝트를 만들어 이 컴포넌트를 붙여두면 된다.
public class StageExtraction : MonoBehaviour
{
    public string lobbySceneName = "LobbyScene";
    public string lobbySpawnId = "FromStage";

    void Start()
    {
        StageTimer timer = FindFirstObjectByType<StageTimer>();
        if (timer != null) timer.OnStageClear += HandleStageClear;

        PlayerHealth health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) health.OnDied += ResourceBank.DiscardRun;
    }

    void HandleStageClear()
    {
        ResourceBank.CommitRunToStash();
        SceneTravel.GoTo(lobbySceneName, lobbySpawnId);
    }
}
