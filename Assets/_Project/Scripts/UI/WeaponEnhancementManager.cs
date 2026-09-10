using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// V키로 무기 강화 팝업(EnhancementPanel)을 열고 닫는다. ESC의 설정 패널(PauseManager)과는 완전히
// 별도의 상태로 동작하지만, 열려 있는 동안은 PauseManager.SetInventoryPaused()로 알려서
// Time.timeScale을 0으로 만들어 게임을 함께 멈추다.
//
// ESC 설정 패널이 열려 있는 동안에는 V키를 무시한다.
//
// 강화는 스테이지(런) 안에서만 한다 - 거점에서는 V키가 아무 반응도 하지 않는다. 이번 런에 파밍한
// 자원(ResourceBank.runHeld)을 소모해서 "들고 있는"(WeaponAim.isHeld) 무기 하나의 스탯을 올린다.
// 강화 레벨은 WeaponEnhanceStore에 런 단위로만 유지되고, 사망/추출로 런이 끝나면 전부 초기화된다.
// 포탈로 다음 스테이지에 넘어가는 동안에는 유지된다 (같은 씬 좌표이동).
//
// 참고: docs/schema.sql은 기름을 "이동속도"(플레이어 스탯)로 적어뒀고, 그건 별도의
// permanent_upgrade_type(영구 강화)와 겹치는 부분이 있어 정리가 필요하다. 스키마 정리 전까지는
// 지금 이미 만들어진 UI(발사속도 = 무기 스탯)를 그대로 따른다.
//
// 마일스톤 패시브(item_enhance_milestone)는 아직 범위 밖.
//
// 씬에 미리 만들어진 EnhancementPanel 하위의 StatRow_* 5개를 이름으로 찾아서
// 각 EnhanceButton에 코드로 리스너를 붙이고, CurrencyText 옆에 어떤 자원이 얼마나 필요한지
// 아이콘으로 보여준다.
public class WeaponEnhancementManager : MonoBehaviour
{
    public GameObject enhancementPanel; // 무기 강화 팝업 UI 오브젝트 (EnhancementPanel)
    public PauseManager pauseManager; // ESC 상태 확인 + Time.timeScale 계산에 상태를 알려주기 위한 참조

    [Header("강화 바(Step) 색")]
    // 강화가 채워진 칸만 이 색으로 바꾼다 (프리팹 기본색보다 더 밝고 빨갛게).
    // 아직 강화 안 된 칸은 씬/프리팹에 지정된 원래 색 그대로 두고 전혀 건드리지 않는다.
    public Color filledStepColor = new Color(1f, 0.2f, 0.15f, 1f);

    private const int CostPerLevel = 5;
    private const float CostIconSize = 20f;

    private bool isPanelOpen;
    private EnhanceRow[] rows;

    // 강화 안 된 칸의 원래 색. BuildRows()에서 아직 아무것도 칠하기 전에 한 번 캡처해두고,
    // RefreshRow()에서 레벨보다 높은 칸을 이 색으로 되돌린다 (런 리셋 후 빈 상태 표시용).
    private Color defaultStepColor = Color.white;

    // 무기를 안 들고 있을 때 패널에 띄우는 안내 문구 (코드로 생성). 무기를 들면 숨긴다.
    private Text noticeText;
    private const string NoWeaponNotice = "무기를 든 상태에서 강화할 수 있습니다";

    private struct EnhanceRow
    {
        public ResourceType type;
        public Transform stepsRow;
        public Text currencyText;
        public Button button;
    }

    void Awake()
    {
        // 씬에 켜진 채로 저장돼있어도 시작할 땐 항상 닫힌 상태로 맞춘다 (isPanelOpen 기본값과 동기화).
        if (enhancementPanel != null) enhancementPanel.SetActive(false);
        if (enhancementPanel != null) BuildRows();
    }

    void OnEnable()
    {
        ResourceBank.OnChanged += RefreshAllRows;
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= RefreshAllRows;
    }

    void Update()
    {
        // 강화는 스테이지(런) 안에서만. 거점에서는 V키를 무시하고, 혹시 열린 채로 넘어왔으면 닫는다.
        if (!StageManager.IsInStage)
        {
            if (isPanelOpen) SetPanelOpen(false);
            return;
        }

        if (!Keyboard.current.vKey.wasPressedThisFrame) return;

        // ESC 설정 패널이 열려 있으면 V키를 완전히 무시한다.
        if (PauseManager.IsEscPaused) return;

        SetPanelOpen(!isPanelOpen);
    }

    private void SetPanelOpen(bool open)
    {
        isPanelOpen = open;

        if (enhancementPanel != null) enhancementPanel.SetActive(open);
        if (pauseManager != null) pauseManager.SetInventoryPaused(open);

        if (open) RefreshAllRows();
    }

    // StatRow_* 5개를 찾아서 라벨->자원타입 매핑, 자원 아이콘 배치, 버튼 리스너 연결까지 한 번에 처리한다.
    private void BuildRows()
    {
        rows = new EnhanceRow[]
        {
            BindRow("StatRow_공격속도", ResourceType.Wood),
            BindRow("StatRow_공격력", ResourceType.Iron),
            BindRow("StatRow_공격범위", ResourceType.Copper),
            BindRow("StatRow_치명타 확률", ResourceType.Chemical),
            BindRow("StatRow_발사속도", ResourceType.Oil),
        };

        Button closeButton = FindDeep(enhancementPanel.transform, "ButtonX")?.GetComponent<Button>();
        if (closeButton != null) closeButton.onClick.AddListener(() => SetPanelOpen(false));

        // 아직 아무 칸도 칠하기 전에 원래 색을 캡처한다 (RefreshRow에서 빈 칸 되돌릴 때 사용).
        Transform firstStep = rows.Length > 0 && rows[0].stepsRow != null ? rows[0].stepsRow.Find("Step_0") : null;
        Image firstStepImage = firstStep != null ? firstStep.GetComponent<Image>() : null;
        if (firstStepImage != null) defaultStepColor = firstStepImage.color;

        noticeText = CreateNotice();
    }

    // 패널 하단 중앙에 안내 문구 텍스트를 하나 만든다. 폰트는 패널이 이미 쓰는 것(한글 지원)을 그대로 빌려 쓴다.
    private Text CreateNotice()
    {
        GameObject go = new GameObject("EnhanceNotice", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(enhancementPanel.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 10f);
        rt.sizeDelta = new Vector2(420f, 26f);

        Text t = go.GetComponent<Text>();
        t.font = rows.Length > 0 && rows[0].currencyText != null
            ? rows[0].currencyText.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 16;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, 0.85f, 0.35f);
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.text = NoWeaponNotice;
        t.enabled = false; // 기본은 숨김. RefreshAllRows()가 무기 유무에 따라 켠다.
        return t;
    }

    private EnhanceRow BindRow(string rowName, ResourceType type)
    {
        EnhanceRow row = new EnhanceRow { type = type };

        Transform statRow = FindDeep(enhancementPanel.transform, rowName);
        if (statRow == null)
        {
            Debug.LogWarning($"WeaponEnhancementManager: '{rowName}'을 찾지 못했습니다.");
            return row;
        }

        row.stepsRow = statRow.Find("StepsRow");
        row.currencyText = statRow.Find("CurrencyText")?.GetComponent<Text>();
        row.button = statRow.Find("EnhanceButton")?.GetComponent<Button>();

        CreateCostIcon(statRow, type, row.currencyText);

        if (row.button != null)
        {
            ResourceType capturedType = type; // 클로저 캡처용
            row.button.onClick.AddListener(() => OnEnhanceClicked(capturedType));
        }

        return row;
    }

    // CurrencyText 바로 앞에 자원 아이콘을 하나 만들어서, "이 강화는 어떤 자원을 쓰는지" 한눈에 보이게 한다.
    // HorizontalLayoutGroup이 이미 StatRow에 붙어있어서, 형제 순서만 맞추면 자동으로 배치된다.
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
        layout.preferredWidth = CostIconSize;
        layout.preferredHeight = CostIconSize;
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

    private void OnEnhanceClicked(ResourceType type)
    {
        IEnhanceableWeapon weapon = FindHeldWeapon();
        if (weapon == null || weapon.GetEnhanceLevel(type) >= weapon.MaxEnhanceLevel) return;
        if (!ResourceBank.TrySpendRunResource(type, CostPerLevel)) return;

        weapon.ApplyEnhance(type);
        RefreshAllRows();
    }

    // 지금 플레이어가 "들고 있는" 무기(WeaponAim.isHeld) 중 강화 가능한 것을 찾는다.
    private static IEnhanceableWeapon FindHeldWeapon()
    {
        WeaponAim[] aims = FindObjectsByType<WeaponAim>(FindObjectsSortMode.None);
        foreach (WeaponAim aim in aims)
        {
            if (!aim.isHeld) continue;

            IEnhanceableWeapon weapon = aim.GetComponent<IEnhanceableWeapon>();
            if (weapon != null) return weapon;
        }
        return null;
    }

    private void RefreshAllRows()
    {
        if (rows == null) return;

        IEnhanceableWeapon heldWeapon = FindHeldWeapon();
        bool hasWeapon = heldWeapon != null;

        // 무기를 안 들고 있으면 안내 문구를 띄우고 모든 강화 버튼을 잠근다.
        if (noticeText != null) noticeText.enabled = !hasWeapon;

        foreach (EnhanceRow row in rows)
        {
            int level = hasWeapon ? heldWeapon.GetEnhanceLevel(row.type) : 0;
            int maxLevel = hasWeapon ? heldWeapon.MaxEnhanceLevel : WeaponEnhanceUtil.MaxLevel;
            RefreshRow(row, level, maxLevel, hasWeapon);
        }
    }

    private void RefreshRow(EnhanceRow row, int level, int maxLevel, bool hasWeapon)
    {
        if (row.stepsRow != null)
        {
            for (int i = 0; i < WeaponEnhanceUtil.MaxLevel; i++)
            {
                Transform step = row.stepsRow.Find("Step_" + i);
                if (step == null) continue;

                // 강화된 칸은 강조색, 그 위 칸은 원래 색으로 되돌린다 (런 리셋 후 빈 상태가 정확히 보이도록).
                step.GetComponent<Image>().color = i < level ? filledStepColor : defaultStepColor;
            }
        }

        bool atMax = hasWeapon && level >= maxLevel;

        // 무기가 없거나 MAX 레벨이면 이 줄의 강화 버튼을 잠근다.
        if (row.button != null) row.button.interactable = hasWeapon && !atMax;

        if (row.currencyText != null)
        {
            if (atMax)
            {
                row.currencyText.text = "MAX";
            }
            else
            {
                int held = ResourceBank.GetRunHeld(row.type);
                row.currencyText.text = $"{held}/{CostPerLevel}";
            }
        }
    }
}
