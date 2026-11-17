using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 무기 강화 창 위쪽 테두리에 걸친 폴더 탭 ([강화] / [제작]). 고른 탭은 창 바탕색으로 높게 솟아 창과 이어져 보이고,
// 안 고른 탭은 어둡고 낮게 창 뒤에 깔린다. 마우스를 올리면 살짝 밝아진다. 부품은 프리팹에 있고 여기서는 연출만 한다.
public class WeaponTabView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public const float Width = 104f, LowHeight = 34f, HighHeight = 44f, Overlap = 8f;
    private static readonly Color InactiveBody = new Color(0.07f, 0.09f, 0.13f);
    private static readonly Color InactiveText = new Color(0.62f, 0.66f, 0.72f);

    public RectTransform rect;
    public Image body;
    public Text label;

    [NonSerialized] public Action onClick;

    private bool hovered;
    private float hover;
    private float active;

    /// <summary>activeAmount: 0 = 안 고름, 1 = 고름 (전환 연출 중에는 그 사이).</summary>
    public void Apply(float activeAmount)
    {
        active = activeAmount;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) onClick?.Invoke();
    }

    void OnDisable()
    {
        hovered = false;
        hover = 0f;
    }

    void Update()
    {
        float target = hovered ? 1f : 0f;
        if (Mathf.Approximately(hover, target)) return;
        hover = Mathf.MoveTowards(hover, target, Time.unscaledDeltaTime * 8f);
        Refresh();
    }

    private void Refresh()
    {
        rect.sizeDelta = new Vector2(Width, Mathf.Lerp(LowHeight, HighHeight, active) + 3f * hover * (1f - active));
        body.color = Color.Lerp(Color.Lerp(InactiveBody, PixelUi.PanelColor, hover * 0.6f), PixelUi.WindowColor, active);
        label.color = Color.Lerp(Color.Lerp(InactiveText, Color.white, hover * 0.5f), PixelUi.Gold, active);
    }
}
