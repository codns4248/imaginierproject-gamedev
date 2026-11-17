using UnityEngine;
using UnityEngine.UI;

// 무기 강화 창 프리팹(Prefabs/UI/UI_WeaponEnhancePanel)의 부품 참조. 모양은 프리팹에서 고치고,
// WeaponEnhancementManager는 여기 연결된 부품에 값만 채우고 탭 전환/연출만 한다.
//
//   WeaponUpgradePanel (화면 전체, 이 컴포넌트)
//     ... PopupWindow (창)
//         WindowTitle          창 제목 (TMP, [제작] 탭에서는 "Weapon Craft")
//         EnhanceContent       [강화] 탭 내용 (무기 아이콘, 무기 이름, 해금 배너, 강화 줄 5개, 특수효과 목록)
//         CraftView            [제작] 탭 내용 (WeaponCraftingView)
//         Tab_강화 / Tab_제작  창 위쪽 폴더 탭
//         ButtonX              닫기
public class WeaponEnhanceView : MonoBehaviour
{
    public RectTransform window;
    public TMPro.TMP_Text windowTitle;
    public Button closeButton;

    [Header("[강화] 탭")]
    public RectTransform enhanceContent;
    public CanvasGroup enhanceGroup;
    public Text headerText;      // 무기 아이콘 옆 무기 이름 (무기가 없으면 안내 문구)
    public Text bannerText;      // 특수효과 해금 배너
    public Text effectsLabels;   // 특수효과 목록 왼쪽 열 (자원/스탯 이름)
    public Text effectsText;     // 오른쪽 열 (현재 효과 -> 다음 효과)
    public UpgradeRowUI[] rows;  // 강화 줄 5개

    [Header("[제작] 탭")]
    public WeaponCraftingView craftView;

    [Header("탭")]
    public WeaponTabView enhanceTab;
    public WeaponTabView craftTab;
}
