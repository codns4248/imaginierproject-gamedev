using UnityEngine;

// 거점 창고 건물(Building_Common, 나무 창고 그림): 가까이 가면 흰 테두리가 뜨고 F키로 "배낭" 창(BackpackUI)을 연다.
// 팀이 배치한 건물이라 씬은 고치지 않고, SpecialUpgradeStation이 게임 시작 때 이름으로 찾아 붙인다 (Install).
// 건물은 단단한 충돌체만 있어서 특수 강화 건물과 같이 거리로 판정한다 (InteractOutline.proximityRadius).
public class BackpackStation : MonoBehaviour
{
    private const string BuildingName = "Building_Common";
    private const float ProximityRadius = 0.5f;

    public static void Install()
    {
        GameObject building = GameObject.Find(BuildingName);
        if (building == null || building.GetComponent<BackpackStation>() != null) return;
        if (building.GetComponent<SpriteRenderer>() == null || building.GetComponent<Collider2D>() == null) return;
        building.AddComponent<BackpackStation>();
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
        BackpackUI.Toggle();
    }
}
