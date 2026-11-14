using UnityEngine;

// 거점 훈련소 건물(Building_Common, 좌측 하단): 가까이 가면 흰 테두리가 뜨고 F키로 "일반 강화"(영구 강화) 창을 연다
// (HubUpgradeManager.Toggle - 창고 자원으로 공격속도/공격력/체력/치명타/이동속도를 올리는 PermanentUpgradeManager).
// BackpackStation과 같이 씬은 고치지 않고, SpecialUpgradeStation이 게임 시작 때 이름으로 찾아 붙인다 (Install).
// 건물은 단단한 충돌체만 있어서 특수 강화 건물과 같이 거리로 판정한다 (InteractOutline.proximityRadius).
public class TrainingHallStation : MonoBehaviour
{
    private const string BuildingName = "Building_Common";
    private const float ProximityRadius = 0.5f;

    public static void Install()
    {
        GameObject building = GameObject.Find(BuildingName);
        if (building == null || building.GetComponent<TrainingHallStation>() != null) return;
        if (building.GetComponent<SpriteRenderer>() == null || building.GetComponent<Collider2D>() == null) return;
        building.AddComponent<TrainingHallStation>();
    }

    void Start()
    {
        InteractOutline outline = gameObject.AddComponent<InteractOutline>();
        outline.proximityRadius = ProximityRadius;
        outline.Init(OnInteract);
    }

    private void OnInteract()
    {
        if (StageManager.IsInStage || EnemyManager.PlayerDead) return;
        HubUpgradeManager.Toggle();
    }
}
