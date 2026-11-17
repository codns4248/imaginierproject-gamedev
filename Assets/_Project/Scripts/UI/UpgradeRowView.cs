using UnityEngine;
using UnityEngine.UI;

// 공용 강화 줄 프리팹(Prefabs/UI/Common/UI_UpgradeRow)의 부품 참조. 특수 강화 창의 한 줄과 같은 배치:
//   [이름 Lv n/m]                 [자원 아이콘 xN]
//   지금  ...                     [   강화   ]
//   다음  ...
// 모양은 프리팹에서 고치고, 코드는 글자/아이콘/버튼 상태만 바꾼다.
public class UpgradeRowView : MonoBehaviour
{
    public Text nameText;
    public Text level;
    public Text now;
    public Text next;
    public Image costIcon;
    public Text cost;
    public PixelButton button;
}
