using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 거점 "특수 강화" 창: 희귀자원(거점 창고 stash)으로 하는 영구 유틸 강화 (SpecialUpgrades의 10종).
// 무기 강화(V, 탐험 중 한정)와 달리 탐험이 끝나도 유지된다. 거점의 특수 강화 건물(SpecialUpgradeStation)에서 F키로 열고,
// 다시 F키를 누르거나 [닫기]로 닫는다. 열려 있는 동안 게임이 멈춘다 (PauseManager, V키 무기 강화·훈련소 일반 강화 창과 같은 방식).
// 줄마다 이름, 단계, 지금 효과 -> 다음 효과, 비용(희귀자원), [강화] 버튼. 최대 단계면 [최대], 아직 효과를 만들지 않은 강화는 [준비 중].
// 씬에 두지 않고 처음 열 때 만든다. 모양은 공용 프리팹(UI_PopupWindow 창 + 낮은 강화 줄 UI_UpgradeRowCompact 10개, 2열 5줄)이고,
// 창 높이를 줄이려고 줄마다 "지금" 효과는 빼고 단계(Lv)와 "다음" 효과만 보여 준다.
// 여기서는 제목/보유 희귀 자원/줄마다 글자와 버튼 상태만 채운다 (Resources/UiPrefabs.asset에서 가져옴).
public class SpecialUpgradeUI : MonoBehaviour
{
    private const int Columns = 2;
    private const float ColumnGap = 16f;
    private static readonly Color CantAfford = new Color(1f, 0.42f, 0.38f);

    private static SpecialUpgradeUI instance;

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    private class RowView
    {
        public SpecialUpgradeType type;
        public UpgradeRowView view;
    }

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
        pauseManager = FindFirstObjectByType<PauseManager>();
        Build();
        if (root != null) root.SetActive(false);
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
        if (root == null) return;
        Sfx.Play(SfxId.UiOpen);
        EnsureEventSystem();
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
        if (rareText == null) return;
        int rare = ResourceBank.GetStash(ResourceType.Rare);
        rareText.text = $"보유 희귀 자원  <color=#FFDB6B>{rare}개</color>";

        foreach (RowView row in rows)
        {
            SpecialUpgrades.Def def = SpecialUpgrades.Get(row.type);
            int level = SpecialUpgrades.GetLevel(row.type);
            bool maxed = SpecialUpgrades.IsMaxed(row.type);
            int cost = SpecialUpgrades.NextCost(row.type);

            UpgradeRowView v = row.view;
            v.level.text = $"Lv {level}/{def.MaxLevel}";
            if (v.now != null) v.now.text = "지금  " + def.describe(level);
            v.next.text = maxed ? "최대 단계" : "다음  " + def.describe(level + 1);
            v.cost.text = maxed ? "-" : $"x{cost}";
            v.cost.color = !maxed && rare < cost ? CantAfford : Color.white;
            v.button.label.text = !def.implemented ? "준비 중" : maxed ? "최대" : "강화";
            v.button.SetInteractable(SpecialUpgrades.CanUpgrade(row.type));
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
        UpgradeRowView rowPrefab = prefabs != null && prefabs.upgradeRowCompact != null ? prefabs.upgradeRowCompact : prefabs?.upgradeRow;
        if (prefabs != null && prefabs.upgradeRowCompact == null)
            Debug.LogWarning("SpecialUpgradeUI: UiPrefabs에 낮은 강화 줄(upgradeRowCompact)이 비어 있어 기본 강화 줄로 그립니다.");
        if (prefabs == null || prefabs.popupWindow == null || rowPrefab == null) return;

        UiLayer.Setup(gameObject, UiLayer.Popup); // 건물 팝업 층 (ESC 설정 창보다 아래)

        PopupWindowView window = Instantiate(prefabs.popupWindow, transform, false);
        window.name = "SpecialUpgradeWindow";
        root = window.gameObject;
        window.title.text = "Special Upgrade";
        window.subtitle.text = "희귀 자원으로 하는 영구 강화 · 탐험이 끝나도 유지됩니다";
        window.closeButton.onClick = Close;

        rareText = PixelUi.NewText("Rare", window.header, window.subtitle.font, 20, Color.white, TextAnchor.MiddleCenter);

        // 2열: 가로 줄 묶음마다 강화 칸 2개
        RectTransform line = null;
        for (int i = 0; i < SpecialUpgrades.All.Length; i++)
        {
            if (i % Columns == 0) line = window.AddLine(ColumnGap);
            SpecialUpgrades.Def def = SpecialUpgrades.All[i];
            UpgradeRowView view = Instantiate(rowPrefab, line, false);
            view.name = "Row_" + def.type;
            view.nameText.text = def.title;
            view.costIcon.sprite = ResourcePickup.GetIconSprite(ResourceType.Rare);
            SpecialUpgradeType type = def.type;
            view.button.onClick = () => SpecialUpgrades.TryUpgrade(type);
            rows.Add(new RowView { type = def.type, view = view });
        }
    }
}
