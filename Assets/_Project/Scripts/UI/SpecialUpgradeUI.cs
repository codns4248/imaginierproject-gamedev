using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 거점 "특수 강화" 창: 희귀자원(거점 창고 stash)으로 하는 영구 유틸 강화 (SpecialUpgrades의 10종).
// 무기 강화(V, 탐험 중 한정)와 달리 탐험이 끝나도 유지된다. 거점의 특수 강화 건물(SpecialUpgradeStation)에서 F키로 열고,
// 다시 F키를 누르거나 [닫기]로 닫는다. 열려 있는 동안 게임이 멈춘다 (PauseManager, V키 무기 강화·훈련소 일반 강화 창과 같은 방식).
// 줄마다 이름, 단계, 지금 효과 -> 다음 효과, 비용(희귀자원), [강화] 버튼. 최대 단계면 [최대], 아직 효과를 만들지 않은 강화는 [준비 중].
// 씬에 두지 않고 처음 열 때 코드로 만든다. 모양은 다른 창과 같은 PixelUi(PixelArtGUI 창 테두리)를 쓴다.
public class SpecialUpgradeUI : MonoBehaviour
{
    private const float RowWidth = 404f, RowHeight = 84f, RowGapX = 16f, RowGapY = 8f;
    private const int Columns = 2;
    private const float DescWidth = 264f; // 지금/다음 설명 칸 폭 (왼쪽 14 + 264 = 278, 비용/버튼은 288부터)
    private static readonly Color NowColor = new Color(0.92f, 0.94f, 0.98f);
    private static readonly Color NextColor = new Color(0.62f, 0.7f, 0.8f);
    private static readonly Color CantAfford = new Color(1f, 0.42f, 0.38f);

    private static SpecialUpgradeUI instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private class RowView
    {
        public SpecialUpgradeType type;
        public Text level, now, next, cost;
        public PixelButton button;
    }

    private Font font;
    private GameObject root;
    private Text rareText;
    private readonly List<RowView> rows = new List<RowView>();
    private PauseManager pauseManager;

    public static void Toggle()
    {
        if (IsOpen) instance.Close();
        else Open();
    }

    public static void Open()
    {
        if (PauseManager.IsPaused) return; // ESC/일반 강화 창 등이 이미 열려 있으면 열지 않는다
        if (instance == null)
        {
            GameObject go = new GameObject("SpecialUpgradeUI");
            instance = go.AddComponent<SpecialUpgradeUI>();
        }
        instance.Show();
    }

    void Awake()
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        font = cfg.font != null ? cfg.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        pauseManager = FindFirstObjectByType<PauseManager>();
        Build();
        root.SetActive(false);
    }

    void OnEnable()
    {
        ResourceBank.OnChanged += Refresh;
        SpecialUpgrades.OnChanged += Refresh;
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= Refresh;
        SpecialUpgrades.OnChanged -= Refresh;
    }

    void Update()
    {
        // 탐험에 들어가면(거점이 아니면) 닫는다.
        if (IsOpen && StageManager.IsInStage) Close();
    }

    private void Show()
    {
        EnsureEventSystem();
        root.SetActive(true);
        Refresh();
        if (pauseManager != null) pauseManager.SetInventoryPaused(true);
    }

    private void Close()
    {
        root.SetActive(false);
        if (pauseManager != null) pauseManager.SetInventoryPaused(false);
    }

    private void Refresh()
    {
        if (rareText == null) return;
        int rare = ResourceBank.GetStash(ResourceType.Rare);
        rareText.text = $"보유 희귀 자원  <color=#FFDB6B>{rare}개</color>";

        foreach (RowView row in rows)
        {
            SpecialUpgrades.Def def = SpecialUpgrades.Get(row.type);
            int level = SpecialUpgrades.GetLevel(row.type);
            bool maxed = SpecialUpgrades.IsMaxed(row.type);
            int cost = SpecialUpgrades.NextCost(row.type);

            row.level.text = $"Lv {level}/{def.MaxLevel}";
            row.now.text = "지금  " + def.describe(level);
            row.next.text = maxed ? "최대 단계" : "다음  " + def.describe(level + 1);
            row.cost.text = maxed ? "-" : $"x{cost}";
            row.cost.color = !maxed && rare < cost ? CantAfford : Color.white;
            row.button.label.text = !def.implemented ? "준비 중" : maxed ? "최대" : "강화";
            row.button.SetInteractable(SpecialUpgrades.CanUpgrade(row.type));
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // ===================== 만들기 =====================

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // HUD보다 위, ESC 설정 창/레벨업 창보다는 아래
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform overlay = PixelUi.NewRect("SpecialUpgradeOverlay", transform);
        PixelUi.Stretch(overlay);
        PixelUi.NewImage("Dim", overlay, null, new Color(0.05f, 0.06f, 0.09f, 0.6f), 0f, true).raycastTarget = true; // 뒤쪽이 눌리지 않게
        root = overlay.gameObject;

        float gridW = Columns * RowWidth + (Columns - 1) * RowGapX;
        int rowCount = Mathf.CeilToInt(SpecialUpgrades.All.Length / (float)Columns);
        float gridH = rowCount * RowHeight + (rowCount - 1) * RowGapY;

        RectTransform window = PixelUi.NewRect("Window", overlay);
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(gridW + 64f, gridH + 196f);
        PixelUi.Window(window);

        // 제목줄 (레벨업 창과 같은 금색 배너)
        LevelUpConfig cfg = LevelUpConfig.Instance;
        RectTransform banner = PixelUi.NewRect("Banner", window);
        PixelUi.PlaceTop(banner, new Vector2(0f, 4f), new Vector2(300f, 56f));
        if (cfg.titleBanner != null) PixelUi.NewImage("Plate", banner, cfg.titleBanner, Color.white, cfg.pixelScale, true);
        Text title = PixelUi.NewText("Title", banner, font, 28, PixelUi.DarkText, TextAnchor.MiddleCenter, outline: false);
        PixelUi.Stretch(title.rectTransform, 0f, 0f, 0f, 4f);
        title.text = "특수 강화";

        Text subtitle = PixelUi.NewText("Subtitle", window, font, 16, new Color(0.78f, 0.86f, 0.95f), TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(subtitle.rectTransform, new Vector2(0f, -50f), new Vector2(gridW, 22f));
        subtitle.text = "희귀 자원으로 하는 영구 강화 · 탐험이 끝나도 유지됩니다";

        rareText = PixelUi.NewText("Rare", window, font, 20, Color.white, TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(rareText.rectTransform, new Vector2(0f, -78f), new Vector2(gridW, 26f));

        // 강화 줄 (2열)
        RectTransform grid = PixelUi.NewRect("Grid", window);
        PixelUi.PlaceTop(grid, new Vector2(0f, -102f - gridH * 0.5f), new Vector2(gridW, gridH));
        for (int i = 0; i < SpecialUpgrades.All.Length; i++)
        {
            int col = i % Columns, line = i / Columns;
            Vector2 pos = new Vector2(col * (RowWidth + RowGapX), -line * (RowHeight + RowGapY));
            rows.Add(BuildRow(grid, SpecialUpgrades.All[i], pos));
        }

        PixelUi.Button(window, "Close", "닫기", false, font, new Vector2(0f, 40f), new Vector2(150f, 44f), Close);
    }

    // 왼쪽 위 기준 pos에 강화 한 줄: 이름 / 단계 / 지금 효과 / 다음 효과 + 오른쪽에 비용과 [강화].
    private RowView BuildRow(RectTransform grid, SpecialUpgrades.Def def, Vector2 pos)
    {
        RectTransform rt = PixelUi.NewRect("Row_" + def.type, grid);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(RowWidth, RowHeight);
        PixelUi.BevelPanel(rt, PixelUi.PanelColor);

        RowView row = new RowView { type = def.type };

        Text name = PixelUi.NewText("Name", rt, font, 19, PixelUi.Gold, TextAnchor.MiddleLeft);
        Corner(name.rectTransform, new Vector2(14f, -8f), new Vector2(200f, 24f));
        name.text = def.title;

        row.level = PixelUi.NewText("Level", rt, font, 15, new Color(0.72f, 0.84f, 0.95f), TextAnchor.MiddleLeft);
        Corner(row.level.rectTransform, new Vector2(14f + name.preferredWidth + 10f, -9f), new Vector2(80f, 24f));

        row.now = PixelUi.NewText("Now", rt, font, 14, NowColor, TextAnchor.MiddleLeft);
        Corner(row.now.rectTransform, new Vector2(14f, -36f), new Vector2(DescWidth, 20f));
        FitInLine(row.now);
        row.next = PixelUi.NewText("Next", rt, font, 14, NextColor, TextAnchor.MiddleLeft);
        Corner(row.next.rectTransform, new Vector2(14f, -57f), new Vector2(DescWidth, 20f));
        FitInLine(row.next);

        // 비용: 희귀자원 아이콘 + 개수 (버튼 위)
        Image rare = PixelUi.NewImage("RareIcon", rt, ResourcePickup.GetIconSprite(ResourceType.Rare), Color.white, 0f, false);
        rare.preserveAspect = true;
        rare.rectTransform.anchorMin = rare.rectTransform.anchorMax = new Vector2(1f, 1f);
        rare.rectTransform.anchoredPosition = new Vector2(-86f, -20f);
        rare.rectTransform.sizeDelta = new Vector2(20f, 20f);
        row.cost = PixelUi.NewText("Cost", rt, font, 16, Color.white, TextAnchor.MiddleLeft);
        row.cost.rectTransform.anchorMin = row.cost.rectTransform.anchorMax = new Vector2(1f, 1f);
        row.cost.rectTransform.pivot = new Vector2(0f, 0.5f);
        row.cost.rectTransform.anchoredPosition = new Vector2(-72f, -20f);
        row.cost.rectTransform.sizeDelta = new Vector2(50f, 22f);

        SpecialUpgradeType type = def.type;
        row.button = PixelUi.Button(rt, "Upgrade", "강화", true, font, Vector2.zero, new Vector2(104f, 36f), () => SpecialUpgrades.TryUpgrade(type));
        RectTransform brt = (RectTransform)row.button.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0f);
        brt.anchoredPosition = new Vector2(-64f, 26f);
        return row;
    }

    // 설명 글자가 오른쪽 비용/[강화] 버튼(줄 왼쪽에서 약 288px부터)까지 넘어가지 않게, 칸 폭 안에서 넘치면 글자를 줄인다 (14 -> 최소 11).
    private static void FitInLine(Text t)
    {
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 11;
        t.resizeTextMaxSize = t.fontSize;
    }

    // 줄의 왼쪽 위 기준으로 놓는다.
    private static void Corner(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
