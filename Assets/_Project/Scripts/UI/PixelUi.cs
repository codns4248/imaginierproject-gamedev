using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 코드로 만드는 PixelArtGUI 창/패널/버튼 공용 도우미 (긴급탈출 확인창, 무기 제작 탭).
// V 무기 강화 창(씬 UI)과 같은 모양이 나오도록 같은 그림(LevelUpConfig의 창/탭/버튼 그림)과 같은 픽셀 배율을 쓴다:
//   창 = Window 바탕 + WindowStroke 테두리 + CornersGold2 모서리 장식
//   안쪽 칸 = UniversalPanel1 + 금색 윗/아랫 베벨 (강화 행과 같은 색)
//   버튼 = Green1(예) / Gold1(아니오), 마우스를 올리면 Hover 그림으로 바뀐다
public static class PixelUi
{
    // 그림 1픽셀을 UI 몇 칸으로 보일지. V 강화 창(원본 2400x1680을 0.3배로 줄인 창)이 약 1.9라서 맞춘다.
    public const float FrameScale = 1.9f;

    public static readonly Color WindowColor = new Color(0.125f, 0.173f, 0.239f);
    public static readonly Color PanelColor = new Color(0.188f, 0.271f, 0.341f);
    public static readonly Color PanelDark = new Color(0.086f, 0.12f, 0.17f);
    public static readonly Color BevelTop = new Color(0.867f, 0.78f, 0.459f);
    public static readonly Color BevelBottom = new Color(0.557f, 0.455f, 0.212f);
    public static readonly Color Gold = new Color(1f, 0.86f, 0.42f);
    public static readonly Color DarkText = new Color(0.3f, 0.17f, 0.05f);

    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // 부모 위쪽 가운데 기준으로 놓는다.
    public static void PlaceTop(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    // scale: 그림 1픽셀을 UI 몇 칸으로 보일지 (9칸 나눔 그림의 테두리 두께). 0이면 그림 원래 비율 그대로.
    public static Image NewImage(string name, Transform parent, Sprite sprite, Color color, float scale, bool stretch)
    {
        RectTransform rt = NewRect(name, parent);
        if (stretch) Stretch(rt);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        SetSprite(img, sprite, scale);
        return img;
    }

    public static void SetSprite(Image img, Sprite sprite, float scale)
    {
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        if (sprite == null || sprite.border == Vector4.zero) return;
        img.type = Image.Type.Sliced;
        if (scale > 0f) img.pixelsPerUnitMultiplier = 100f / (sprite.pixelsPerUnit * scale);
    }

    public static Text NewText(string name, Transform parent, Font font, int size, Color color, TextAnchor align, bool outline = true)
    {
        RectTransform rt = NewRect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (outline)
        {
            Outline o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.65f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return t;
    }

    // V 강화 창과 같은 창: 그림자 + 바탕 + 테두리 + 모서리 금장식. 창 빈 곳을 눌러도 뒤로 클릭이 새지 않는다.
    public static void Window(RectTransform root)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        Sprite body = cfg.window != null ? cfg.window : cfg.panel;
        RectTransform shadow = NewImage("Shadow", root, body, new Color(0f, 0f, 0f, 0.45f), FrameScale, true).rectTransform;
        shadow.offsetMin = new Vector2(5f, -8f);
        shadow.offsetMax = new Vector2(5f, -8f);
        NewImage("Body", root, body, WindowColor, FrameScale, true).raycastTarget = true;
        if (cfg.windowStroke != null) NewImage("Stroke", root, cfg.windowStroke, Color.white, FrameScale, true);
        if (cfg.windowCorners != null) NewImage("Corners", root, cfg.windowCorners, Color.white, FrameScale, true);
    }

    // V 강화 창의 강화 행과 같은 안쪽 칸: UniversalPanel1 + 금색 윗/아랫 베벨. 몸통 Image를 돌려준다.
    public static Image BevelPanel(RectTransform root, Color body)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        Image img = NewImage("Body", root, cfg.panel, body, FrameScale, true);
        if (cfg.panelTopBevel != null) NewImage("TopBevel", root, cfg.panelTopBevel, BevelTop, FrameScale, true);
        if (cfg.panelBottomBevel != null) NewImage("BottomBevel", root, cfg.panelBottomBevel, BevelBottom, FrameScale, true);
        return img;
    }

    // [예](초록) / [아니오](금색) 버튼.
    public static PixelButton Button(RectTransform parent, string name, string label, bool yes, Font font, Vector2 position, Vector2 size, Action onClick)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Sprite normal = yes ? cfg.buttonYes : cfg.buttonNo;
        Sprite hover = yes ? cfg.buttonYesHover : cfg.buttonNoHover;
        Color fallback = yes ? new Color(0.25f, 0.55f, 0.32f) : new Color(0.72f, 0.58f, 0.3f);
        Image img = NewImage("Body", rt, normal != null ? normal : cfg.panel, normal != null ? Color.white : fallback, FrameScale * 1.25f, true);
        img.raycastTarget = true;

        Text text = NewText("Label", rt, font, 20, yes ? Color.white : DarkText, TextAnchor.MiddleCenter, outline: yes);
        Stretch(text.rectTransform, 0f, 0f, 3f, 0f); // 버튼 그림 아랫면 두께만큼 글자를 살짝 올린다
        text.text = label;

        PixelButton button = rt.gameObject.AddComponent<PixelButton>();
        button.image = img;
        button.normal = img.sprite;
        button.hover = hover != null ? hover : img.sprite;
        button.onClick = onClick;
        return button;
    }
}

// PixelUi.Button이 붙이는 버튼 동작: 마우스를 올리면 Hover 그림 + 살짝 커지고, 누르는 동안 살짝 눌린다.
// 게임이 멈춰 있어도(강화 창) 움직이도록 실제 시간을 쓴다.
public class PixelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public Image image;
    public Sprite normal;
    public Sprite hover;
    public Action onClick;

    private bool hovered;
    private bool pressed;
    private float hoverAmount;

    public void OnPointerEnter(PointerEventData e) { hovered = true; Swap(); }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; Swap(); }
    public void OnPointerDown(PointerEventData e) => pressed = e.button == PointerEventData.InputButton.Left;
    public void OnPointerUp(PointerEventData e) => pressed = false;
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) onClick?.Invoke();
    }

    void OnDisable()
    {
        hovered = pressed = false;
        hoverAmount = 0f;
        Swap();
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        hoverAmount = Mathf.MoveTowards(hoverAmount, hovered ? 1f : 0f, Time.unscaledDeltaTime * 10f);
        transform.localScale = Vector3.one * ((1f + 0.05f * hoverAmount) * (pressed ? 0.94f : 1f));
    }

    private void Swap()
    {
        if (image != null) image.sprite = hovered && hover != null ? hover : normal;
    }
}
