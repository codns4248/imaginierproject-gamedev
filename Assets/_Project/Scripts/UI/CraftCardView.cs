using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 무기 제작 탭의 무기 카드: 마우스를 올리면 금테가 보이고 살짝 떠오르며 커진다. 비활성화되면 어둡게 보이고 눌리지 않는다.
// (창이 열려 있는 동안 게임이 멈춰 있으므로 실제 시간으로 움직인다). 부품은 프리팹에 있다.
public class CraftCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private static readonly Color CantAfford = new Color(1f, 0.42f, 0.38f);

    public WeaponType type;
    public Vector2 basePosition;
    public CanvasGroup group;
    public Image outline;
    public Image icon;
    public Text costText;

    [NonSerialized] public Action<WeaponType> onClick;

    private bool hovered;
    private bool enabledState = true;
    private float hover;

    public void SetEnabled(bool on)
    {
        enabledState = on;
        if (group != null) group.alpha = on ? 1f : 0.4f;
        if (icon != null) icon.color = on ? Color.white : new Color(0.45f, 0.45f, 0.45f);
        if (costText != null) costText.color = on ? Color.white : CantAfford;
        if (!on) hovered = false;
    }

    public void OnPointerEnter(PointerEventData e) => hovered = enabledState;
    public void OnPointerExit(PointerEventData e) => hovered = false;
    public void OnPointerClick(PointerEventData e)
    {
        if (enabledState && e.button == PointerEventData.InputButton.Left) onClick?.Invoke(type);
    }

    void OnDisable()
    {
        hovered = false;
        hover = 0f;
        Apply();
    }

    void Update()
    {
        hover = Mathf.MoveTowards(hover, hovered && enabledState ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        Apply();
    }

    private void Apply()
    {
        transform.localScale = Vector3.one * (1f + 0.04f * hover);
        ((RectTransform)transform).anchoredPosition = basePosition + new Vector2(0f, 6f * hover);
        if (outline != null) outline.color = new Color(PixelUi.Gold.r, PixelUi.Gold.g, PixelUi.Gold.b, hover);
    }
}
