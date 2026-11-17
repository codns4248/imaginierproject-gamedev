using UnityEngine;

// 공용 UI 프리팹 목록 (Resources/UiPrefabs.asset). 코드로 여는 창들이 씬 참조 없이 공용 프리팹을 가져다 쓴다.
// 프리팹 모양은 Prefabs/UI/Common/에서 에디터로 고치고, 여기는 어떤 프리팹을 쓸지만 정한다.
[CreateAssetMenu(fileName = "UiPrefabs", menuName = "UI/공용 UI 프리팹 목록")]
public class UiPrefabs : ScriptableObject
{
    public PopupWindowView popupWindow; // UI_PopupWindow: 화면 전체 팝업 창
    public UpgradeRowView upgradeRow;   // UI_UpgradeRow: 강화 한 줄

    private static UiPrefabs instance;

    public static UiPrefabs Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<UiPrefabs>("UiPrefabs");
            if (instance == null) Debug.LogError("Resources/UiPrefabs.asset이 없습니다 (공용 UI 프리팹 목록).");
            return instance;
        }
    }
}
