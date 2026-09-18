using UnityEngine;

// 거점의 탐험 포탈. 씬 전환 없이 MainScene 안의 랜덤 스테이지 구역으로 플레이어를 이동시키고 전투를 시작한다.
// 근처에서 흰색 테두리가 뜰 때 F키를 눌러야 실제로 이동한다 (InteractOutline 참고).
[RequireComponent(typeof(Collider2D))]
public class StagePortal : MonoBehaviour
{
    void Start()
    {
        InteractOutline outline = gameObject.AddComponent<InteractOutline>();
        outline.Init(() => StageManager.EnterRandomStage());
    }
}
