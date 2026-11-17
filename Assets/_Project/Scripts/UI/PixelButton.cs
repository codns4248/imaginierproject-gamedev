using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 픽셀 버튼 동작 (PixelUi.Button으로 만든 버튼, 공용 버튼 프리팹 UI_ButtonGreen/UI_ButtonGold): 마우스를 올리면 Hover 그림 + 살짝 커지고, 누르는 동안 살짝 눌린다.
// SetInteractable(false)면 회색으로 어둡게 보이고 눌리지 않는다. 게임이 멈춰 있어도(강화 창) 움직이도록 실제 시간을 쓴다.
public class PixelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public Image image;
    public Text label;
    public Sprite normal;
    public Sprite hover;
    public Action onClick;

    private bool hovered;
    private bool pressed;
    private float hoverAmount;
    private bool interactable = true;
    private Color labelColor;
    private bool labelColorSaved;

    public bool Interactable => interactable;

    public void SetInteractable(bool on)
    {
        if (label != null && !labelColorSaved) { labelColor = label.color; labelColorSaved = true; }
        interactable = on;
        if (image != null) image.color = on ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
        if (label != null) label.color = on ? labelColor : new Color(0.75f, 0.75f, 0.78f, 1f);
        if (!on) { hovered = pressed = false; }
        Swap();
    }

    public void OnPointerEnter(PointerEventData e) { hovered = interactable; Swap(); }
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; Swap(); }
    public void OnPointerDown(PointerEventData e) => pressed = interactable && e.button == PointerEventData.InputButton.Left;
    public void OnPointerUp(PointerEventData e) => pressed = false;
    public void OnPointerClick(PointerEventData e)
    {
        if (interactable && e.button == PointerEventData.InputButton.Left) { Sfx.Play(SfxId.UiClick); onClick?.Invoke(); }
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
