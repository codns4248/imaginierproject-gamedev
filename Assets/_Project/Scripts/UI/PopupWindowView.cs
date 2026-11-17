using UnityEngine;
using UnityEngine.UI;

// 공용 팝업 창 프리팹(Prefabs/UI/Common/UI_PopupWindow)의 부품 참조. 모양은 프리팹에서 고치고, 코드는 내용만 채운다.
//
//   UI_PopupWindow (화면 전체, 이 컴포넌트)
//     Dim       뒤를 어둡게 + 뒤쪽 클릭 막기
//     Window    창 (내용 크기에 맞춰 자동으로 커진다: VerticalLayoutGroup + ContentSizeFitter)
//       Shadow / Body / Stroke / Corners   창 그림 (레이아웃 무시)
//       Banner    금색 제목줄 (Title)
//       Subtitle  안내 문구
//       Header    한 줄짜리 정보 칸 (보유 자원 등, 가로 정렬). 비워 두면 빈 줄
//       Content   본문 (강화 줄 등을 여기에 넣는다, 세로 정렬)
//       Footer    [닫기] 버튼
//
// 창 폭은 Content 안에서 가장 넓은 것 + 좌우 여백 32씩, 높이는 위아래 고정 부분(196) + Content 높이.
public class PopupWindowView : MonoBehaviour
{
    public RectTransform window;
    public Text title;
    public Text subtitle;
    public RectTransform header;
    public RectTransform content;
    public PixelButton closeButton;

    public bool IsOpen => gameObject.activeSelf;
}
