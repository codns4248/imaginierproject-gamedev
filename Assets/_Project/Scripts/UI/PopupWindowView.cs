using System;
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
//       Footer    아래 버튼 줄 ([닫기], 가로 정렬 - AddFooterButton으로 버튼을 더 넣을 수 있다)
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

    /// <summary>아래 버튼 줄의 [닫기] 왼쪽에 버튼을 하나 더 넣는다 (buttonPrefab: UiPrefabs의 초록/금색 버튼).</summary>
    public PixelButton AddFooterButton(PixelButton buttonPrefab, string label, Action onClick)
    {
        PixelButton button = Instantiate(buttonPrefab, closeButton.transform.parent, false);
        button.name = "Button_" + label;
        button.transform.SetSiblingIndex(closeButton.transform.GetSiblingIndex());
        button.label.text = label;
        button.onClick = onClick;
        return button;
    }

    /// <summary>본문에 가로 한 줄 묶음을 만든다. 여러 열로 놓을 때(특수 강화 2열) 줄마다 하나씩 만들어 그 안에 칸을 넣는다.</summary>
    public RectTransform AddLine(float spacing)
    {
        RectTransform line = new GameObject("Line", typeof(RectTransform)).GetComponent<RectTransform>();
        line.SetParent(content, false);
        HorizontalLayoutGroup h = line.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.UpperLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return line;
    }
}
