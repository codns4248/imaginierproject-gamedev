using UnityEngine;

// 맵 출입구. 플레이어가 트리거에 닿으면 targetScene으로 이동하고,
// 그 씬의 targetSpawnId와 일치하는 SpawnPoint 위치에 배치된다.
// Collider2D(Is Trigger 체크)가 필요하다.
[RequireComponent(typeof(Collider2D))]
public class MapPortal : MonoBehaviour
{
    public string targetScene;
    public string targetSpawnId;

    void OnTriggerEnter2D(Collider2D other)
    {
        // 코드베이스 전반에서 Player를 이름으로 찾는 방식과 통일 (태그 설정에 의존하지 않음)
        if (other.gameObject.name != "Player") return;
        if (string.IsNullOrEmpty(targetScene)) return;

        SceneTravel.GoTo(targetScene, targetSpawnId);
    }
}
