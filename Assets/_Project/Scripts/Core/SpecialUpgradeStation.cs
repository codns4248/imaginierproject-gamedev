using UnityEngine;

// 거점의 "특수 강화" 건물: 플레이어가 건물 가까이(proximityRadius) 오면 흰 테두리가 뜨고, F키로 특수 강화 창(SpecialUpgradeUI)을 연다.
// 특수 강화는 희귀자원(거점 창고 stash)으로 하는 영구 유틸 강화다 (부활 횟수, 시작 무기 등 - 항목은 SpecialUpgradeUI 참고).
// 건물은 플레이어가 부딪히는 단단한 충돌체만 있어서 트리거 대신 거리로 판정한다 (InteractOutline.proximityRadius).
// 거점에서만 동작하고, 탐험 중에는 아무 반응도 하지 않는다. 씬의 희귀 건물 오브젝트(Building_Rare, 천문대)에 붙여 둔다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class SpecialUpgradeStation : MonoBehaviour
{
    [Tooltip("건물 그림 테두리에서 이 거리(월드 유닛) 안에 들어오면 상호작용할 수 있다")]
    public float proximityRadius = 0.5f;

    void Start()
    {
        InteractOutline outline = gameObject.AddComponent<InteractOutline>();
        outline.proximityRadius = proximityRadius;
        outline.Init(OnInteract);

        StartWeaponStand.InstallAll(); // 대장간 아래 거치대 5개(두 줄)에 시작 무기 진열 (특수 강화 "시작 무기 선택")
        BackpackStation.Install();     // 우측 하단 창고(Building_Storehouse)에서 배낭 꾸리기 (특수 강화 "배낭")
        TrainingHallStation.Install(); // 좌측 하단 훈련소(Building_Common)에서 일반 강화 (HubUpgradeManager)
    }

    private void OnInteract()
    {
        if (StageManager.IsInStage || EnemyManager.PlayerDead) return;
        SpecialUpgradeUI.Toggle();
    }
}
