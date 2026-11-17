using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 거점 "일반 강화"(영구 강화) 창. 훈련소 건물(Building_Common)에서 F키로 연다 (TrainingHallStation -> Toggle).
// 스테이지(런) 안에서는 열리지 않고, ESC 설정 패널 등 다른 창이 열려 있으면 열지 않는다. 열려 있는 동안 게임이 멈춘다.
//
// 영구 강화는 무기가 아니라 플레이어 자체(공격력/공격속도/치명타 확률/체력/이동속도)에 적용되고,
// ResourceBank.stash(거점에 영구 보관된 자원)를 소모한다. 레벨은 PermanentUpgradeManager에 저장 파일로 영구히 유지되며,
// 실제 스탯 반영은 각 소비처(PlayerHealth, PlayerMovement, 무기 공격 스크립트)가 PermanentUpgradeManager.OnChanged를
// 구독해서 스스로 처리한다 - 이 매니저는 창(단계/효과/비용/버튼 잠금)만 담당한다.
//
// 모양은 공용 프리팹(Prefabs/UI/Common)을 쓴다: UI_PopupWindow 창 + UI_UpgradeRow 5줄 (Resources/UiPrefabs.asset에서 가져옴).
// 창 모양/간격/색은 프리팹에서 고치고, 여기서는 제목/보유 자원 줄/줄마다 글자와 버튼 상태만 채운다.
// 줄마다 이름, 단계, 지금 효과 -> 다음 효과, 비용(그 스탯의 자원), [강화]. 최대 단계면 [최대].
public class HubUpgradeManager : MonoBehaviour
{
    public PauseManager pauseManager; // ESC 상태 확인 + Time.timeScale 계산에 상태를 알려주기 위한 참조

    private static readonly Color CantAfford = new Color(1f, 0.42f, 0.38f);

    // 창에 보이는 순서. 자원 종류가 곧 강화 스탯의 키다 (PermanentUpgradeManager 주석 참고).
    private struct Entry
    {
        public ResourceType type;
        public string title;
        public System.Func<int, string> describe;
    }

    private static readonly Entry[] Entries =
    {
        new Entry { type = ResourceType.Iron, title = "공격력",
            describe = l => l == 0 ? "기본 공격력" : $"공격력 +{l * 30}%" }, // 무기마다 damagePerIron 0.3 (원래 공격력의 30%)
        new Entry { type = ResourceType.Wood, title = "공격속도",
            describe = l => l == 0 ? "기본 공격속도" : $"공격 간격 {l}단계 단축" }, // 줄어드는 양은 무기마다 다르다
        new Entry { type = ResourceType.Chemical, title = "치명타 확률",
            describe = l => l == 0 ? "기본 치명타 확률" : $"치명타 확률 +{l * 5}%" },
        new Entry { type = ResourceType.Copper, title = "체력",
            describe = l => l == 0 ? "기본 최대 체력" : $"최대 체력 +{l}" },
        new Entry { type = ResourceType.Oil, title = "이동속도",
            describe = l => l == 0 ? "기본 이동속도" : $"이동속도 +{MoveSpeedPercent(l):0.#}%" },
    };

    private static readonly ResourceType[] StashOrder =
        { ResourceType.Wood, ResourceType.Iron, ResourceType.Copper, ResourceType.Chemical, ResourceType.Oil };

    private class RowView
    {
        public ResourceType type;
        public System.Func<int, string> describe;
        public UpgradeRowView view;
    }

    private static HubUpgradeManager instance;
    private static float baseMoveSpeed = 2.5f; // 플레이어를 찾으면 그 값으로 바뀐다

    private PopupWindowView window;
    private GameObject root;
    private readonly List<RowView> rows = new List<RowView>();
    private readonly Dictionary<ResourceType, Text> stashTexts = new Dictionary<ResourceType, Text>();

    private bool IsOpen => root != null && root.activeSelf;

    /// <summary>훈련소 F키 상호작용에서 호출. 열려 있으면 닫고, 닫혀 있으면 연다.</summary>
    public static void Toggle()
    {
        if (instance == null || instance.root == null) return;
        if (instance.IsOpen) { instance.SetOpen(false); return; }
        if (StageManager.IsInStage || PauseManager.IsPaused) return; // ESC/특수 강화 등 다른 창이 열려 있으면 열지 않는다
        Sfx.Play(SfxId.UiOpen);
        instance.SetOpen(true);
    }

    void Awake()
    {
        instance = this;
        Build();
        if (root != null) root.SetActive(false);
    }

    void OnEnable()
    {
        ResourceBank.OnChanged += Refresh;
        PermanentUpgradeManager.OnChanged += Refresh;
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= Refresh;
        PermanentUpgradeManager.OnChanged -= Refresh;
    }

    void Update()
    {
        // 영구 강화는 거점에서만. 혹시 열린 채로 스테이지에 들어갔으면 닫는다.
        if (IsOpen && StageManager.IsInStage) SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        root.SetActive(open);
        if (pauseManager != null) pauseManager.SetInventoryPaused(open);

        if (!open) return;
        EscClose.Push(this, () => IsOpen, () => SetOpen(false));
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null && player.BaseMoveSpeed > 0f) baseMoveSpeed = player.BaseMoveSpeed;
        Refresh();
    }

    private static float MoveSpeedPercent(int level) =>
        level * PlayerMovement.PermanentMoveSpeedPerLevel / baseMoveSpeed * 100f;

    private void OnUpgradeClicked(ResourceType type)
    {
        if (!PermanentUpgradeManager.TryUpgrade(type)) { Sfx.Play(SfxId.Deny); return; }
        Sfx.Play(SfxId.EnhanceSuccess);
        Refresh();
    }

    private void Refresh()
    {
        if (root == null) return;

        foreach (KeyValuePair<ResourceType, Text> kv in stashTexts)
            kv.Value.text = ResourceBank.GetStash(kv.Key).ToString();

        int max = PermanentUpgradeManager.MaxLevel;
        foreach (RowView row in rows)
        {
            int level = PermanentUpgradeManager.GetLevel(row.type);
            bool maxed = level >= max;
            int cost = WeaponEnhanceUtil.CostForLevel(level);
            bool afford = ResourceBank.GetStash(row.type) >= cost;

            UpgradeRowView v = row.view;
            v.level.text = $"Lv {level}/{max}";
            v.now.text = "지금  " + row.describe(level);
            v.next.text = maxed ? "최대 단계" : "다음  " + row.describe(level + 1);
            v.cost.text = maxed ? "-" : $"x{cost}";
            v.cost.color = !maxed && !afford ? CantAfford : Color.white;
            v.button.label.text = maxed ? "최대" : "강화";
            v.button.SetInteractable(!maxed && afford);
        }
    }

    // ===================== 만들기 =====================

    private void Build()
    {
        UiPrefabs prefabs = UiPrefabs.Instance;
        if (prefabs == null || prefabs.popupWindow == null || prefabs.upgradeRow == null) return;

        GameObject canvasGo = new GameObject("HubUpgrade_Canvas");
        UiLayer.Setup(canvasGo, UiLayer.Popup); // 건물 팝업 층 (ESC 설정 창보다 아래)

        window = Instantiate(prefabs.popupWindow, canvasGo.transform, false);
        window.name = "HubUpgradeWindow";
        root = window.gameObject;
        window.title.text = "General Upgrade";
        window.subtitle.text = "자원으로 하는 영구 강화 · 탐험이 끝나도 유지됩니다";
        window.closeButton.onClick = () => SetOpen(false);

        BuildStashLine(window.header, window.subtitle.font);

        foreach (Entry entry in Entries)
        {
            UpgradeRowView view = Instantiate(prefabs.upgradeRow, window.content, false);
            view.name = "Row_" + entry.type;
            view.nameText.text = entry.title;
            view.costIcon.sprite = ResourcePickup.GetIconSprite(entry.type);
            ResourceType type = entry.type;
            view.button.onClick = () => OnUpgradeClicked(type);
            rows.Add(new RowView { type = entry.type, describe = entry.describe, view = view });
        }
    }

    // 창의 정보 줄(Header, 가로 정렬)에 "보유 자원  [나무] 42  [철] 18 ..."을 넣는다.
    private void BuildStashLine(RectTransform header, Font font)
    {
        Text label = PixelUi.NewText("Label", header, font, 20, Color.white, TextAnchor.MiddleCenter);
        label.text = "보유 자원";
        Spacer(header, 10f);

        for (int i = 0; i < StashOrder.Length; i++)
        {
            ResourceType type = StashOrder[i];
            if (i > 0) Spacer(header, 18f);

            Image icon = PixelUi.NewImage("Icon_" + type, header, ResourcePickup.GetIconSprite(type), Color.white, 0f, false);
            icon.preserveAspect = true;
            LayoutElement le = icon.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 22f;
            le.preferredHeight = 22f;

            Text count = PixelUi.NewText("Count_" + type, header, font, 18, PixelUi.Gold, TextAnchor.MiddleLeft);
            stashTexts[type] = count;
        }
    }

    private static void Spacer(Transform parent, float width)
    {
        LayoutElement le = PixelUi.NewRect("Gap", parent).gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = width;
    }
}
