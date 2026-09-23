using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// U키로 거점 영구 강화 팝업(HubUpgradePanel)을 열고 닫는다. WeaponEnhancementManager(V키, 런 한정)와
// 대칭되는 거점 한정 버전 - 스테이지(런) 안에서는 U키가 아무 반응도 하지 않는다.
// ESC 설정 패널이 열려 있는 동안에는 U키를 무시한다.
//
// 영구 강화는 무기가 아니라 플레이어 자체(공격력/체력/이동속도/공격속도/치명타 확률)에 적용되고,
// ResourceBank.stash(거점에 영구 보관된 자원)를 소모한다. 레벨은 PermanentUpgradeManager에
// 저장 파일로 영구히 유지되며(WeaponEnhanceStore와 달리 런 종료로 리셋되지 않음), 실제 스탯 반영은
// 각 소비처(PlayerHealth, PlayerMovement, Pistol/Sword/Smg/LanceAttack)가 PermanentUpgradeManager.OnChanged를
// 구독해서 스스로 처리한다 - 이 매니저는 UI(레벨 칸 채우기/버튼 잠금/자원 표시)만 담당한다.
//
// EnhancementPanel을 복제한 HubUpgradePanel 하위의 StatRow_* 5개를 이름으로 찾아서 그대로 재사용한다.
public class HubUpgradeManager : MonoBehaviour
{
    public GameObject hubUpgradePanel; // 거점 영구 강화 팝업 UI 오브젝트 (HubUpgradePanel)
    public PauseManager pauseManager; // ESC 상태 확인 + Time.timeScale 계산에 상태를 알려주기 위한 참조

    [Header("강화 바(Step) 색")]
    public Color filledStepColor = new Color(1f, 0.2f, 0.15f, 1f);

    private const int CostPerLevel = 5;

    private bool isPanelOpen;
    private EnhanceRow[] rows;
    private Color defaultStepColor = Color.white;

    private struct EnhanceRow
    {
        public ResourceType type;
        public Transform stepsRow;
        public Text currencyText;
        public Button button;
    }

    void Awake()
    {
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
        // 영구 강화는 거점에서만. 런 중에는 U키를 무시하고, 혹시 열린 채로 스테이지에 들어갔으면 닫는다.
        if (StageManager.IsInStage)
        {
            if (isPanelOpen) SetPanelOpen(false);
            return;
        }

        if (!Keyboard.current.uKey.wasPressedThisFrame) return;

        // ESC 설정 패널이 열려 있으면 U키를 완전히 무시한다.
        if (PauseManager.IsEscPaused) return;

        SetPanelOpen(!isPanelOpen);
    }

    private void SetPanelOpen(bool open)
    {
        isPanelOpen = open;

        if (hubUpgradePanel != null) hubUpgradePanel.SetActive(open);
        if (pauseManager != null) pauseManager.SetInventoryPaused(open);

        if (open) RefreshAllRows();
    }

    // StatRow_* 5개를 찾아서 라벨->자원타입 매핑, 버튼 리스너 연결까지 한 번에 처리한다.
    private void BuildRows()
    {
        rows = new EnhanceRow[]
        {
            BindRow("StatRow_공격속도", ResourceType.Wood),
            BindRow("StatRow_공격력", ResourceType.Iron),
            BindRow("StatRow_체력", ResourceType.Copper),
            BindRow("StatRow_치명타 확률", ResourceType.Chemical),
            BindRow("StatRow_이동속도", ResourceType.Oil),
        };

        Button closeButton = FindDeep(hubUpgradePanel.transform, "ButtonX")?.GetComponent<Button>();
        if (closeButton != null) closeButton.onClick.AddListener(() => SetPanelOpen(false));

        // 아직 아무 칸도 칠하기 전에 원래 색을 캡처한다 (RefreshRow에서 빈 칸 되돌릴 때 사용).
        Transform firstStep = rows.Length > 0 && rows[0].stepsRow != null ? rows[0].stepsRow.Find("Step_0") : null;
        Image firstStepImage = firstStep != null ? firstStep.GetComponent<Image>() : null;
        if (firstStepImage != null) defaultStepColor = firstStepImage.color;
    }

    private EnhanceRow BindRow(string rowName, ResourceType type)
    {
        EnhanceRow row = new EnhanceRow { type = type };

        Transform statRow = FindDeep(hubUpgradePanel.transform, rowName);
        if (statRow == null)
        {
            Debug.LogWarning($"HubUpgradeManager: '{rowName}'을 찾지 못했습니다.");
            return row;
        }

        row.stepsRow = statRow.Find("StepsRow");
        row.currencyText = statRow.Find("CurrencyText")?.GetComponent<Text>();
        row.button = statRow.Find("EnhanceButton")?.GetComponent<Button>();

        CreateCostIcon(statRow, type, row.currencyText);

        if (row.button != null)
        {
            ResourceType capturedType = type; // 클로저 캡처용
            row.button.onClick.AddListener(() => OnUpgradeClicked(capturedType));
        }

        return row;
    }

    // CurrencyText 바로 앞에 자원 아이콘을 하나 만들어서, "이 강화는 어떤 자원을 쓰는지" 한눈에 보이게 한다.
    private static void CreateCostIcon(Transform statRow, ResourceType type, Text currencyText)
    {
        if (currencyText == null) return;

        GameObject iconGO = new GameObject("CostIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGO.transform.SetParent(statRow, false);
        iconGO.transform.SetSiblingIndex(currencyText.transform.GetSiblingIndex());

        Image img = iconGO.GetComponent<Image>();
        img.sprite = ResourcePickup.GetIconSprite(type);
        img.preserveAspect = true;
        img.raycastTarget = false;

        LayoutElement layout = iconGO.GetComponent<LayoutElement>();
        layout.preferredWidth = 20f;
        layout.preferredHeight = 20f;
    }

    // 이름으로 자손을 재귀 탐색한다 (비활성 오브젝트도 찾을 수 있게 transform.Find 대신 직접 순회).
    private static Transform FindDeep(Transform root, string name)
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

    private void OnUpgradeClicked(ResourceType type)
    {
        PermanentUpgradeManager.TryUpgrade(type);
        RefreshAllRows();
    }

    private void RefreshAllRows()
    {
        if (rows == null) return;

        foreach (EnhanceRow row in rows)
        {
            RefreshRow(row);
        }
    }

    private void RefreshRow(EnhanceRow row)
    {
        int level = PermanentUpgradeManager.GetLevel(row.type);

        if (row.stepsRow != null)
        {
            for (int i = 0; i < PermanentUpgradeManager.MaxLevel; i++)
            {
                Transform step = row.stepsRow.Find("Step_" + i);
                if (step == null) continue;

                step.GetComponent<Image>().color = i < level ? filledStepColor : defaultStepColor;
            }
        }

        bool atMax = level >= PermanentUpgradeManager.MaxLevel;
        bool canAfford = ResourceBank.GetStash(row.type) >= CostPerLevel;

        if (row.button != null) row.button.interactable = !atMax && canAfford;

        if (row.currencyText != null)
        {
            row.currencyText.text = atMax ? "MAX" : $"{ResourceBank.GetStash(row.type)}/{CostPerLevel}";
        }
    }
}
