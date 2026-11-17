using UnityEngine;
using UnityEngine.UI;

// 공용 배낭 줄 프리팹(Prefabs/UI/Common/UI_BackpackRow)의 부품 참조:
//   [아이콘] 이름  창고 xN            [-10]  담을 양  [+10]
// 모양은 프리팹에서 고치고, 코드는 글자/아이콘/버튼 상태만 바꾼다.
public class BackpackRowView : MonoBehaviour
{
    public Image icon;
    public Text nameText;
    public Text stash;
    public Text amount;
    public PixelButton minus;
    public PixelButton plus;
}
