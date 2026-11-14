using UnityEngine;
using UnityEngine.UI;

// 거점 "일반 강화"(영구 강화) 팝업(HubUpgradePanel)을 열고 닫는다. WeaponEnhancementManager(V키, 런 한정)와
// 대칭되는 거점 한정 버전. 단축키는 없고, 훈련소 건물(Building_Common)에서 F키로 연다 (TrainingHallStation -> Toggle).
// 스테이지(런) 안에서는 열리지 않고, ESC 설정 패널 등 다른 창이 열려 있으면 열지 않는다.
//
// 영구 강화는 무기가 아니라 플레이어 자체(공격력/체력/이동속도/공격속도/치명타 확률)에 적용되고,
// ResourceBank.stash(거점에 영구 보관된 자원)를 소모한다. 레벨은 PermanentUpgradeManager에
// 저장 파일로 영구히 유지되며(WeaponEnhanceStore와 달리 런 종료로 리셋되지 않음), 실제 스탯 반영은
// 각 소비처(PlayerHealth, PlayerMovement, Pistol/Sword/Smg/LanceAttack)가 PermanentUpgradeManager.OnChanged를
// 구독해서 스스로 처리한다 - 이 매니저는 UI(진행바/버튼 잠금/자원 표시)만 담당한다.
//
// WeaponUpgradePanel을 복제한 HubUpgradePanel 하위의 StatRow_* 5개를 이름으로 찾아서 그대로 재사용한다.
// 줄 표시는 무기 강화와 같은 UpgradeRowUI를 쓴다 (영구 강화엔 특수효과가 없어서 다이아몬드 마커는 없음).
public class HubUpgradeManager : MonoBehaviour
{
    public GameObject hubUpgradePanel; // 거점 영구 강화 팝업 UI 오브젝트 (HubUpgradePanel)
    public PauseManager pauseManager; // ESC 상태 확인 + Time.timeScale 계산에 상태를 알려주기 위한 참조

    private static HubUpgradeManager instance;

    private bool isPanelOpen;
    private UpgradeRowUI[] rows;

    /// <summary>훈련소 F키 상호작용에서 호출. 열려 있으면 닫고, 닫혀 있으면 연다.</summary>
    public static void Toggle()
    {
        if (instance == null || instance.hubUpgradePanel == null) return;
        if (instance.isPanelOpen) { instance.SetPanelOpen(false); return; }
        if (StageManager.IsInStage || PauseManager.IsPaused) return; // ESC/특수 강화 등 다른 창이 열려 있으면 열지 않는다
        instance.SetPanelOpen(true);
    }

    void Awake()
    {
        instance = this;
        if (hubUpgradePanel != null) hubUpgradePanel.SetActive(false);
        if (hubUpgradePanel != null) BuildRows();
    }

    void OnEnable()
    {
        ResourceBank.OnChanged += RefreshAllRows;
        PermanentUpgradeManager.OnChanged += RefreshAllRows;
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= RefreshAllRows;
        PermanentUpgradeManager.OnChanged -= RefreshAllRows;
    }

    void Update()
    {
        // 영구 강화는 거점에서만. 혹시 열린 채로 스테이지에 들어갔으면 닫는다.
        if (StageManager.IsInStage)
        {
            if (isPanelOpen) SetPanelOpen(false);
            return;
        }

        if (isPanelOpen && rows != null)
            foreach (UpgradeRowUI row in rows) row.Animate(Time.unscaledDeltaTime);
    }

    private void SetPanelOpen(bool open)
    {
        isPanelOpen = open;

        if (hubUpgradePanel != null) hubUpgradePanel.SetActive(open);
        if (pauseManager != null) pauseManager.SetInventoryPaused(open);

        if (!open) return;
        RefreshAllRows();
        foreach (UpgradeRowUI row in rows) row.Snap();
    }

    private void BuildRows()
    {
        Transform window = UpgradeRowUI.FindDeep(hubUpgradePanel.transform, "PopupWindow") ?? hubUpgradePanel.transform;
        UpgradeRowUI.BakeScale((RectTransform)window);
        Font font = hubUpgradePanel.GetComponentInChildren<Text>(true)?.font;

        rows = new[]
        {
            BindRow("StatRow_공격속도", ResourceType.Wood, font),
            BindRow("StatRow_공격력", ResourceType.Iron, font),
            BindRow("StatRow_체력", ResourceType.Copper, font),
            BindRow("StatRow_치명타 확률", ResourceType.Chemical, font),
            BindRow("StatRow_이동속도", ResourceType.Oil, font),
        };
        rows = System.Array.FindAll(rows, r => r != null);

        Button closeButton = UpgradeRowUI.FindDeep(hubUpgradePanel.transform, "ButtonX")?.GetComponent<Button>();
        if (closeButton != null) closeButton.onClick.AddListener(() => SetPanelOpen(false));
    }

    private UpgradeRowUI BindRow(string rowName, ResourceType type, Font font)
    {
        Transform statRow = UpgradeRowUI.FindDeep(hubUpgradePanel.transform, rowName);
        if (statRow == null)
        {
            Debug.LogWarning($"HubUpgradeManager: '{rowName}'을 찾지 못했습니다.");
            return null;
        }
        return new UpgradeRowUI(statRow, type, PermanentUpgradeManager.MaxLevel, new int[0], font, () => OnUpgradeClicked(type));
    }

    private void OnUpgradeClicked(ResourceType type)
    {
        if (!PermanentUpgradeManager.TryUpgrade(type)) return;
        RefreshAllRows();
        System.Array.Find(rows, r => r.type == type)?.Pulse();
    }

    private void RefreshAllRows()
    {
        if (rows == null) return;

        foreach (UpgradeRowUI row in rows)
        {
            int level = PermanentUpgradeManager.GetLevel(row.type);
            row.Refresh(level, PermanentUpgradeManager.MaxLevel, ResourceBank.GetStash(row.type), WeaponEnhanceUtil.CostForLevel(level), true);
        }
    }
}
