using UnityEngine;

// 공용 UI 프리팹 목록 (Resources/UiPrefabs.asset). 코드로 여는 창들이 씬 참조 없이 공용 프리팹을 가져다 쓴다.
// 프리팹 모양은 Prefabs/UI/Common/에서 에디터로 고치고, 여기는 어떤 프리팹을 쓸지만 정한다.
[CreateAssetMenu(fileName = "UiPrefabs", menuName = "UI/공용 UI 프리팹 목록")]
public class UiPrefabs : ScriptableObject
{
    public PopupWindowView popupWindow;     // UI_PopupWindow: 화면 전체 팝업 창 (일반 강화, 특수 강화, 배낭)
    public UpgradeRowView upgradeRow;       // UI_UpgradeRow: 강화 한 줄 404x84, 지금/다음 효과 두 줄 (일반 강화)
    public UpgradeRowView upgradeRowCompact; // UI_UpgradeRowCompact: 낮은 강화 줄 404x64, 다음 효과 한 줄 (특수 강화 2열)
    public BackpackRowView backpackRow;     // UI_BackpackRow: 배낭 한 줄
    public ConfirmDialogView confirmDialog; // UI_ConfirmDialog: 게임을 멈추지 않는 작은 [예]/[아니오] 창 (긴급탈출)
    public WeaponEnhanceView weaponEnhancePanel; // UI_WeaponEnhancePanel: V 무기 강화 창 ([강화]/[제작] 탭)
    public PixelButton buttonGreen;         // UI_ButtonGreen: 초록 버튼
    public PixelButton buttonGold;          // UI_ButtonGold: 금색 버튼

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
