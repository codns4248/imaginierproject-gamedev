using UnityEngine;
using UnityEngine.UI;

// 강화 팝업(무기 강화 WeaponUpgradePanel / 거점 영구 강화 HubUpgradePanel)의 한 줄(StatRow_*)을 그리는 공용 UI.
// 프리팹의 칸(StepsRow)은 숨기고 연속 진행바 + (선택) 특수효과 단계 다이아몬드 마커 + "Lv N" 텍스트로 바꾼다.
// 강화하면 바가 부드럽게 차오르고 흰 플래시/살짝 튀는 연출이 나온다 (팝업이 열려 있으면 timeScale=0이라
// Animate에는 Time.unscaledDeltaTime을 넘긴다).
//
// 팝업 스케일 문제: 두 팝업 프리팹은 2400x1680 크기로 만든 뒤 PopupWindow를 0.3배로 줄여 쓰는데, 레거시 Text는
// 글자를 "폰트 크기 x 캔버스 배율"로 래스터화하고 Transform 축소는 무시한다. 그래서 42pt로 그린 도트 폰트를
// 0.3배로 다시 축소해 그리게 되어 글자/아이콘이 뭉개졌다. BakeScale이 그 축소를 크기 값에 녹여 넣어서
// (스케일 1, 크기는 0.3배) 글자가 실제 화면 크기로 래스터화되게 한다. 프리팹은 수정하지 않는다.
public class UpgradeRowUI
{
    // ===== 스타일 (두 팝업 공통) =====
    public static readonly Color BarBack = new Color(0.08f, 0.11f, 0.16f, 1f);
    public static readonly Color BarFill = new Color(0.93f, 0.36f, 0.24f, 1f);
    public static readonly Color Gold = new Color(1f, 0.79f, 0.29f, 1f);
    public static readonly Color MarkerOff = new Color(0.17f, 0.2f, 0.26f, 1f);
    public static readonly Color MarkerRim = new Color(0.55f, 0.45f, 0.22f, 1f);
    public static readonly Color TextNormal = new Color(0.92f, 0.92f, 0.92f);
    public static readonly Color TextCantAfford = new Color(1f, 0.42f, 0.38f);

    // 크기는 BakeScale 이후(= 캔버스 기준 해상도 1280x720) 단위. 도트 폰트는 16의 배수에서 가장 또렷하다.
    public const int RowFontSize = 16;
    private const float IconSize = 32f;       // 16px 아이콘의 2배
    private const float BarWidth = 192f;
    private const float BarHeight = 14f;
    private const float MarkerSize = 19f;
    private const float LevelTextWidth = 48f;

    private const float FillLerpSpeed = 10f;
    private const float FlashDuration = 0.35f;
    private const float PunchDuration = 0.2f;

    public readonly ResourceType type;
    public readonly string label;
    public Button button;

    private Text currencyText, buttonText, levelText;
    private RectTransform bar, fill;
    private Image fillImage, flash;
    private int[] markerLevels;
    private Image[] markerCores;
    private float shownRatio, targetRatio, flashTimer, punchTimer;

    // statRow: 프리팹의 StatRow_* (자식: Label / StepsRow / Spacer / CurrencyIcon / CurrencyText / EnhanceButton).
    // markerLevels: 다이아몬드 마커를 찍을 레벨 (특수효과가 없으면 빈 배열).
    // 반드시 BakeScale 이후에 호출한다 (크기 값이 기준 해상도 단위).
    public UpgradeRowUI(Transform statRow, ResourceType type, int maxLevel, int[] markerLevels, Font font, System.Action onClick)
    {
        this.type = type;
        label = statRow.name.Substring("StatRow_".Length);
        this.markerLevels = markerLevels;

        Text labelText = statRow.Find("Label")?.GetComponent<Text>();
        if (labelText != null) labelText.fontSize = RowFontSize;

        currencyText = statRow.Find("CurrencyText")?.GetComponent<Text>();
        if (currencyText != null) currencyText.fontSize = RowFontSize;

        button = statRow.Find("EnhanceButton")?.GetComponent<Button>();
        buttonText = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (buttonText != null) buttonText.fontSize = RowFontSize;
        if (button != null && onClick != null) button.onClick.AddListener(() => onClick());

        SetupCostIcon(statRow, type, currencyText);

        Transform steps = statRow.Find("StepsRow");
        if (steps != null)
        {
            CreateBar(statRow, steps.GetSiblingIndex(), maxLevel, font);
            steps.gameObject.SetActive(false); // 예전 칸 UI는 숨긴다 (프리팹은 그대로)
        }
    }

    // ===================== 표시 =====================

    // held: 이 강화에 쓰는 자원 보유량. enabled: 무기 미장착 등 외부 조건으로 강화 불가면 false (바/레벨도 비움).
    // capped: 이 종류는 최대 전이 아니지만 무기 전체 강화 횟수 상한에 걸려서 더 못 올리는 경우 (버튼에 "한도"를 보여준다).
    public void Refresh(int level, int maxLevel, int held, int cost, bool enabled, bool capped = false)
    {
        bool atMax = enabled && level >= maxLevel;
        capped = enabled && capped && !atMax;
        bool canAfford = held >= cost;

        targetRatio = enabled && maxLevel > 0 ? Mathf.Clamp01((float)level / maxLevel) : 0f;
        if (fillImage != null) fillImage.color = atMax ? Gold : BarFill;

        if (markerCores != null)
            for (int i = 0; i < markerCores.Length; i++)
                markerCores[i].color = enabled && level >= markerLevels[i] ? Gold : MarkerOff;

        if (levelText != null)
        {
            levelText.text = !enabled ? "" : atMax ? "MAX" : $"Lv {level}";
            levelText.color = atMax ? Gold : TextNormal;
        }

        if (button != null) button.interactable = enabled && !atMax && !capped && canAfford;
        if (buttonText != null) buttonText.text = atMax ? "MAX" : capped ? "한도" : $"강화  {cost}";

        // 보유량. 부족하면 빨간색 (비용은 버튼에 표시).
        if (currencyText != null)
        {
            currencyText.text = $"x{held}";
            currencyText.color = atMax || capped || canAfford ? TextNormal : TextCantAfford;
        }
    }

    // 강화 직후 호출: 플래시 + 바/레벨 텍스트가 살짝 튄다.
    public void Pulse()
    {
        flashTimer = FlashDuration;
        punchTimer = PunchDuration;
    }

    // 팝업을 열 때: 차오르는 연출 없이 현재 값으로 바로 맞춘다.
    public void Snap()
    {
        shownRatio = targetRatio;
        flashTimer = punchTimer = 0f;
        Animate(0f);
    }

    public void Animate(float dt)
    {
        if (fill == null) return;

        shownRatio = Mathf.Lerp(shownRatio, targetRatio, 1f - Mathf.Exp(-FillLerpSpeed * dt));
        if (Mathf.Abs(shownRatio - targetRatio) < 0.001f) shownRatio = targetRatio;
        fill.anchorMax = new Vector2(shownRatio, 1f);

        flashTimer = Mathf.Max(0f, flashTimer - dt);
        flash.color = new Color(1f, 1f, 1f, 0.75f * (flashTimer / FlashDuration));

        punchTimer = Mathf.Max(0f, punchTimer - dt);
        float punch = punchTimer / PunchDuration; // 1 -> 0
        bar.localScale = new Vector3(1f, 1f + 0.35f * punch, 1f);
        levelText.transform.localScale = Vector3.one * (1f + 0.3f * punch);
    }

    // ===================== 구성 =====================

    // [바 배경 > 채움(+윗부분 밝은 띠) > 흰 플래시] + 마커 + 오른쪽 "Lv N" 텍스트.
    // StatRow의 HorizontalLayoutGroup이 형제 순서대로 배치하므로 StepsRow가 있던 자리에 끼워 넣는다.
    private void CreateBar(Transform statRow, int siblingIndex, int maxLevel, Font font)
    {
        RectTransform slot = NewRect("EnhanceBarSlot", statRow);
        slot.SetSiblingIndex(siblingIndex);
        LayoutElement slotLayout = slot.gameObject.AddComponent<LayoutElement>();
        slotLayout.preferredWidth = slotLayout.minWidth = BarWidth + MarkerSize * 0.5f;

        bar = NewRect("Bar", slot);
        bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
        bar.pivot = new Vector2(0f, 0.5f);
        bar.sizeDelta = new Vector2(BarWidth, BarHeight);
        AddImage(bar, BarBack);

        fill = NewRect("Fill", bar);
        Stretch(fill, Vector2.zero, new Vector2(0f, 1f));
        fillImage = AddImage(fill, BarFill);

        // 위쪽 1/3에 밝은 띠를 얹어 평면 바가 아니라 살짝 입체감이 나게 한다.
        AddImage(Stretch(NewRect("Shine", fill), new Vector2(0f, 0.62f), Vector2.one), new Color(1f, 1f, 1f, 0.18f));
        flash = AddImage(Stretch(NewRect("Flash", fill), Vector2.zero, Vector2.one), new Color(1f, 1f, 1f, 0f));

        // 다이아몬드 마커: 테두리(큰 마름모) 위에 안쪽(작은 마름모).
        markerCores = new Image[markerLevels.Length];
        for (int i = 0; i < markerLevels.Length; i++)
        {
            Image rim = NewDiamond("Marker" + markerLevels[i], bar, (float)markerLevels[i] / maxLevel, MarkerSize, MarkerRim, true);
            markerCores[i] = NewDiamond("Core", rim.rectTransform, 0.5f, MarkerSize * 0.62f, MarkerOff, false);
        }

        levelText = CreateText(statRow, "LevelText", font, RowFontSize, TextAnchor.MiddleLeft);
        levelText.transform.SetSiblingIndex(slot.GetSiblingIndex() + 1);
        LayoutElement levelLayout = levelText.gameObject.AddComponent<LayoutElement>();
        levelLayout.preferredWidth = levelLayout.minWidth = LevelTextWidth;
    }

    // 자원 아이콘을 드랍 아이콘과 같은 스프라이트로, 16px 아이콘이 정수배(2배)로 보이는 크기로 맞춘다.
    private static void SetupCostIcon(Transform statRow, ResourceType type, Text currencyText)
    {
        if (currencyText == null) return;

        Image icon = statRow.Find("CurrencyIcon")?.GetComponent<Image>();
        if (icon == null)
        {
            GameObject go = new GameObject("CurrencyIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(statRow, false);
            go.transform.SetSiblingIndex(currencyText.transform.GetSiblingIndex());
            icon = go.GetComponent<Image>();
        }
        icon.sprite = ResourcePickup.GetIconSprite(type);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        LayoutElement le = icon.GetComponent<LayoutElement>();
        if (le == null) le = icon.gameObject.AddComponent<LayoutElement>(); // ??는 Unity 가짜 null을 못 걸러서 쓰지 않는다
        le.preferredWidth = le.minWidth = IconSize;
        le.preferredHeight = le.minHeight = IconSize;
    }

    // ===================== 공용 헬퍼 =====================

    // PopupWindow처럼 localScale로 축소해 둔 UI를 "스케일 1 + 크기 값 축소"로 바꾼다 (위 설명 참고).
    public static void BakeScale(RectTransform root)
    {
        float s = root.localScale.x;
        if (Mathf.Approximately(s, 1f) || s <= 0f) return;

        root.localScale = Vector3.one;
        root.sizeDelta *= s;

        foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt == root) continue;
            rt.anchoredPosition *= s;
            rt.sizeDelta *= s;
        }
        foreach (HorizontalOrVerticalLayoutGroup g in root.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true))
        {
            g.spacing *= s;
            g.padding = ScalePadding(g.padding, s);
        }
        foreach (LayoutElement le in root.GetComponentsInChildren<LayoutElement>(true))
        {
            if (le.minWidth > 0f) le.minWidth *= s;
            if (le.minHeight > 0f) le.minHeight *= s;
            if (le.preferredWidth > 0f) le.preferredWidth *= s;
            if (le.preferredHeight > 0f) le.preferredHeight *= s;
        }
        foreach (Text t in root.GetComponentsInChildren<Text>(true))
            t.fontSize = Mathf.Max(1, Mathf.RoundToInt(t.fontSize * s));
        foreach (Shadow sh in root.GetComponentsInChildren<Shadow>(true)) // Outline도 Shadow를 상속
            sh.effectDistance *= s;
        // 9-slice 테두리는 Transform 축소로 같이 얇아져 있었으므로, 같은 두께로 보이게 배율을 되돌린다.
        foreach (Image img in root.GetComponentsInChildren<Image>(true))
            if (img.type == Image.Type.Sliced || img.type == Image.Type.Tiled)
                img.pixelsPerUnitMultiplier /= s;
    }

    private static RectOffset ScalePadding(RectOffset p, float s) =>
        new RectOffset(Mathf.RoundToInt(p.left * s), Mathf.RoundToInt(p.right * s), Mathf.RoundToInt(p.top * s), Mathf.RoundToInt(p.bottom * s));

    // 이름으로 자손을 재귀 탐색한다 (비활성 오브젝트도 찾을 수 있게 transform.Find 대신 직접 순회).
    public static Transform FindDeep(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;

            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public static Text CreateText(Transform parent, string name, Font font, int fontSize, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // anchor 한 점에 붙는 텍스트 (anchor.y가 0이면 왼쪽 아래 기준, 아니면 세로 가운데 기준).
    public static Text CreateText(Transform parent, string name, Font font, int fontSize, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        Text t = CreateText(parent, name, font, fontSize, align);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor.y == 0f ? Vector2.zero : new Vector2(anchor.x, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return t;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Image AddImage(RectTransform rt, Color color)
    {
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // parent 안의 가로 위치 xRatio(0~1), 세로 가운데에 마름모(45도 돌린 정사각형)를 만든다.
    // 안쪽 마름모는 부모(테두리)가 이미 돌아가 있으므로 rotate=false.
    private static Image NewDiamond(string name, RectTransform parent, float xRatio, float size, Color color, bool rotate)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(xRatio, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size) / Mathf.Sqrt(2f); // 돌리면 대각선 = size
        if (rotate) rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        return AddImage(rt, color);
    }
}
