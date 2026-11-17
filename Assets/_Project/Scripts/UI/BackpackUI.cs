using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 거점 창고 건물(BackpackStation)의 "배낭" 창: 다음 탐험에 들고 갈 창고 자원을 종류마다 정한다 (Backpack).
// 줄마다 자원 아이콘/이름/창고 보유량, [-10] 담을 양 [+10]. 아래에 [비우기] [닫기].
// 배낭 용량은 특수 강화 "배낭" 단계로 정해지고, 강화 전(용량 0)이면 안내 문구만 보이고 담을 수 없다.
// 열려 있는 동안 게임이 멈춘다 (특수 강화 창과 같은 방식). 씬에 두지 않고 처음 열 때 만든다.
// 모양은 공용 프리팹(UI_PopupWindow 창 + UI_BackpackRow 줄, Resources/UiPrefabs.asset)이고 여기서는 글자/버튼 동작만 채운다.
public class BackpackUI : MonoBehaviour
{
    private const int Step = 10;

    private static BackpackUI instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private class RowView
    {
        public ResourceType type;
        public BackpackRowView view;
    }

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
        pauseManager = FindFirstObjectByType<PauseManager>();
        Build();
        if (root != null) root.SetActive(false);
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
        if (root == null) return;
        Sfx.Play(SfxId.UiOpen);
        EnsureEventSystem();
        Backpack.FitToStash();
        root.SetActive(true);
        Refresh();
        if (pauseManager != null) pauseManager.SetInventoryPaused(true);
        EscClose.Push(this, () => IsOpen, Close);
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
            ? "<color=#FF8A7A>집(특수 강화)에서 '창고'를 강화하면 자원을 들고 나갈 수 있습니다</color>"
            : $"담은 양  <color=#FFDB6B>{Backpack.Total} / {capacity}</color>";

        foreach (RowView row in rows)
        {
            int packed = Backpack.Get(row.type);
            BackpackRowView v = row.view;
            v.stash.text = $"창고 x{ResourceBank.GetStash(row.type)}";
            v.amount.text = packed.ToString();
            v.minus.SetInteractable(packed > 0);
            v.plus.SetInteractable(Backpack.MaxFor(row.type) > packed);
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
        UiPrefabs prefabs = UiPrefabs.Instance;
        if (prefabs == null || prefabs.popupWindow == null || prefabs.backpackRow == null || prefabs.buttonGold == null) return;

        UiLayer.Setup(gameObject, UiLayer.Popup); // 건물 팝업 층 (특수 강화 창과 같은 층)

        PopupWindowView window = Instantiate(prefabs.popupWindow, transform, false);
        window.name = "BackpackWindow";
        root = window.gameObject;
        window.title.text = "Storage";
        window.subtitle.text = "탐험을 시작할 때 창고에서 꺼내 갑니다 · 죽으면 잃습니다";
        window.closeButton.onClick = Close;
        window.AddFooterButton(prefabs.buttonGold, "비우기", Backpack.Clear);

        capacityText = PixelUi.NewText("Capacity", window.header, window.subtitle.font, 20, Color.white, TextAnchor.MiddleCenter);

        foreach (ResourceType type in Backpack.Carriable)
        {
            BackpackRowView view = Instantiate(prefabs.backpackRow, window.content, false);
            view.name = "Row_" + type;
            view.icon.sprite = ResourcePickup.GetIconSprite(type);
            view.nameText.text = ResourceNames.Korean(type);
            view.minus.label.text = $"-{Step}";
            view.plus.label.text = $"+{Step}";
            ResourceType t = type;
            view.minus.onClick = () => Backpack.Set(t, Backpack.Get(t) - Step);
            view.plus.onClick = () => Backpack.Set(t, Backpack.Get(t) + Step);
            rows.Add(new RowView { type = type, view = view });
        }
    }
}
