using UnityEngine;
using UnityEngine.UI;

// 공용 확인창 프리팹(Prefabs/UI/Common/UI_ConfirmDialog)의 부품 참조. 화면을 어둡게 하지 않는 작은 창으로,
// 화면 아래쪽(플레이어 발밑보다 조금 아래)에 뜬다 - 게임을 멈추지 않는 질문용 (긴급탈출).
//   질문 (금색)
//   설명 (작은 글씨)
//   [예]  [아니오]
public class ConfirmDialogView : MonoBehaviour
{
    public Text question;
    public Text detail;
    public PixelButton yes;
    public PixelButton no;

    public RectTransform Rect => (RectTransform)transform;
}
