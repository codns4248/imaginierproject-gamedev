using UnityEngine;

// 거점의 탐험 포탈. 씬 전환 없이 MainScene 안의 랜덤 스테이지 구역으로 플레이어를 이동시키고 전투를 시작한다.
[RequireComponent(typeof(Collider2D))]
public class StagePortal : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.name != "Player") return;
        StageManager.EnterRandomStage();
    }
}
