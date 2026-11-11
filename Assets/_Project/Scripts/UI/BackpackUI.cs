using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 거점 창고 건물(BackpackStation)의 "배낭" 창: 다음 탐험에 들고 갈 창고 자원을 종류마다 정한다 (Backpack).
// 줄마다 자원 아이콘/이름/창고 보유량, [-10] 담을 양 [+10]. 아래에 [비우기] [닫기].
// 배낭 용량은 특수 강화 "배낭" 단계로 정해지고, 강화 전(용량 0)이면 안내 문구만 보이고 담을 수 없다.
// 열려 있는 동안 게임이 멈춘다 (특수 강화 창과 같은 방식). 씬에 두지 않고 처음 열 때 코드로 만든다.
public class BackpackUI : MonoBehaviour
{
    private const int Step = 10;
    private const float RowWidth = 500f, RowHeight = 56f, RowGap = 8f;
    private static readonly Color StashColor = new Color(0.62f, 0.7f, 0.8f);

    private static BackpackUI instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private class RowView
    {
        public ResourceType type;
        public Text stash, amount;
        public PixelButton minus, plus;
    }

    private Font font;
    private GameObject root;
    private Text capacityText;
    private readonly List<RowView> rows = new List<RowView>();
    private PauseManager pauseManager;

    public static void Toggle()
    {
        if (IsOpen) instance.Close();
        else Open();
    }

    public static void Open()
    {
        if (PauseManager.IsPaused) return;
        if (instance == null) instance = new GameObject("BackpackUI").AddComponent<BackpackUI>();
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
        Backpack.OnChanged += Refresh;
        ResourceBank.OnChanged += Refresh;
        SpecialUpgrades.OnChanged += Refresh;
    }

    void OnDisable()
    {
        Backpack.OnChanged -= Refresh;
        ResourceBank.OnChanged -= Refresh;
        SpecialUpgrades.OnChanged -= Refresh;
    }

    void Update()
    {
        if (IsOpen && StageManager.IsInStage) Close();
    }

    private void Show()
    {
        EnsureEventSystem();
        Backpack.FitToStash();
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
        if (capacityText == null) return;

        int capacity = Backpack.Capacity;
        capacityText.text = capacity <= 0
            ? "<color=#FF8A7A>집(특수 강화)에서 '배낭'을 강화하면 자원을 들고 나갈 수 있습니다</color>"
            : $"담은 양  <color=#FFDB6B>{Backpack.Total} / {capacity}</color>";

        foreach (RowView row in rows)
        {
            int packed = Backpack.Get(row.type);
            row.stash.text = $"창고 x{ResourceBank.GetStash(row.type)}";
            row.amount.text = packed.ToString();
            row.minus.SetInteractable(packed > 0);
            row.plus.SetInteractable(Backpack.MaxFor(row.type) > packed);
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
        canvas.sortingOrder = 50; // 특수 강화 창과 같은 층
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform overlay = PixelUi.NewRect("BackpackOverlay", transform);
        PixelUi.Stretch(overlay);
        PixelUi.NewImage("Dim", overlay, null, new Color(0.05f, 0.06f, 0.09f, 0.6f), 0f, true).raycastTarget = true;
        root = overlay.gameObject;

        int count = Backpack.Carriable.Length;
        float listH = count * RowHeight + (count - 1) * RowGap;

        RectTransform window = PixelUi.NewRect("Window", overlay);
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(RowWidth + 64f, listH + 196f);
        PixelUi.Window(window);

        LevelUpConfig cfg = LevelUpConfig.Instance;
        RectTransform banner = PixelUi.NewRect("Banner", window);
        PixelUi.PlaceTop(banner, new Vector2(0f, 4f), new Vector2(260f, 56f));
        if (cfg.titleBanner != null) PixelUi.NewImage("Plate", banner, cfg.titleBanner, Color.white, cfg.pixelScale, true);
        Text title = PixelUi.NewText("Title", banner, font, 28, PixelUi.DarkText, TextAnchor.MiddleCenter, outline: false);
        PixelUi.Stretch(title.rectTransform, 0f, 0f, 0f, 4f);
        title.text = "배낭";

        Text subtitle = PixelUi.NewText("Subtitle", window, font, 16, new Color(0.78f, 0.86f, 0.95f), TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(subtitle.rectTransform, new Vector2(0f, -50f), new Vector2(RowWidth, 22f));
        subtitle.text = "탐험을 시작할 때 창고에서 꺼내 갑니다 · 죽으면 잃습니다";

        capacityText = PixelUi.NewText("Capacity", window, font, 20, Color.white, TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(capacityText.rectTransform, new Vector2(0f, -78f), new Vector2(RowWidth, 26f));

        RectTransform list = PixelUi.NewRect("List", window);
        PixelUi.PlaceTop(list, new Vector2(0f, -102f - listH * 0.5f), new Vector2(RowWidth, listH));
        for (int i = 0; i < count; i++)
            rows.Add(BuildRow(list, Backpack.Carriable[i], new Vector2(0f, -i * (RowHeight + RowGap))));

        PixelUi.Button(window, "Clear", "비우기", false, font, new Vector2(-90f, 40f), new Vector2(150f, 44f), Backpack.Clear);
        PixelUi.Button(window, "Close", "닫기", false, font, new Vector2(90f, 40f), new Vector2(150f, 44f), Close);
    }

    private RowView BuildRow(RectTransform list, ResourceType type, Vector2 pos)
    {
        RectTransform rt = PixelUi.NewRect("Row_" + type, list);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(RowWidth, RowHeight);
        PixelUi.BevelPanel(rt, PixelUi.PanelColor);

        RowView row = new RowView { type = type };

        Image icon = PixelUi.NewImage("Icon", rt, ResourcePickup.GetIconSprite(type), Color.white, 0f, false);
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(14f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(30f, 30f);

        Text name = PixelUi.NewText("Name", rt, font, 19, PixelUi.Gold, TextAnchor.MiddleLeft);
        Left(name.rectTransform, 54f, new Vector2(110f, 26f));
        name.text = ResourceNames.Korean(type);

        row.stash = PixelUi.NewText("Stash", rt, font, 15, StashColor, TextAnchor.MiddleLeft);
        Left(row.stash.rectTransform, 150f, new Vector2(110f, 24f));

        row.minus = SmallButton(rt, "Minus", $"-{Step}", false, -196f, () => Backpack.Set(type, Backpack.Get(type) - Step));
        row.amount = PixelUi.NewText("Amount", rt, font, 22, Color.white, TextAnchor.MiddleCenter);
        row.amount.rectTransform.anchorMin = row.amount.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        row.amount.rectTransform.anchoredPosition = new Vector2(-128f, 0f);
        row.amount.rectTransform.sizeDelta = new Vector2(64f, 30f);
        row.plus = SmallButton(rt, "Plus", $"+{Step}", true, -60f, () => Backpack.Set(type, Backpack.Get(type) + Step));
        return row;
    }

    private PixelButton SmallButton(RectTransform row, string name, string label, bool yes, float x, System.Action onClick)
    {
        PixelButton b = PixelUi.Button(row, name, label, yes, font, Vector2.zero, new Vector2(64f, 36f), onClick);
        RectTransform brt = (RectTransform)b.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(x, 0f);
        return b;
    }

    // 줄의 왼쪽 가운데 기준으로 놓는다.
    private static void Left(RectTransform rt, float x, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = size;
    }
}
