using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// V키로 무기 강화 팝업(WeaponUpgradePanel)을 열고 닫는다. ESC의 설정 패널(PauseManager)과는 완전히
// 별도의 상태로 동작하지만, 열려 있는 동안은 PauseManager.SetInventoryPaused()로 알려서
// Time.timeScale을 0으로 만들어 게임을 함께 멈추다.
//
// ESC 설정 패널이 열려 있는 동안에는 V키를 무시한다.
//
// 강화는 스테이지(런) 안에서만 한다 - 거점에서는 V키가 아무 반응도 하지 않는다. 이번 런에 파밍한
// 자원(ResourceBank.runHeld)을 소모해서 "들고 있는"(WeaponAim.isHeld) 무기 하나의 스탯을 올린다.
// 강화 레벨은 무기 한 자루(WeaponIdentity)에 런 단위로만 유지되고, 사망/추출로 런이 끝나면 전부 초기화된다.
// (같은 종류 무기를 여러 자루 들면 지금 들고 있는 그 자루만 강화된다.)
// 포탈로 다음 스테이지에 넘어가는 동안에는 유지된다 (같은 씬 좌표이동).
//
// 참고: docs/schema.sql은 기름을 "이동속도"(플레이어 스탯)로 적어뒀고, 그건 별도의
// permanent_upgrade_type(영구 강화)와 겹치는 부분이 있어 정리가 필요하다. 스키마 정리 전까지는
// 지금 이미 만들어진 UI(발사속도 = 무기 스탯)를 그대로 따른다.
//
// 강화는 자원별 최대 15단계, 5강마다(5/10/15) 무기별 특수효과가 켜진다 (item_enhance_milestone 대응).
// 특수효과 설명은 무기가 IEnhanceableWeapon.GetMilestoneEffect로 제공하고, 패널 아래쪽에 목록으로 보여준다.
// 특수효과 단계에 닿으면 무기 이름 아래에 금색 해금 배너가 잠깐 뜬다.
//
// 창은 프리팹(Prefabs/UI/UI_WeaponEnhancePanel, Resources/UiPrefabs.asset의 weaponEnhancePanel)을 게임 시작 때 자기 캔버스에
// 만들어 쓴다. 부품(강화 줄 UpgradeRowUI, 탭 WeaponTabView, 제작 화면 WeaponCraftingView 등)은 프리팹 안에 있고
// WeaponEnhanceView로 연결돼 있다 - 여기서는 값만 채우고 연출만 한다.
//
// 창 위쪽에 폴더 모양의 [강화] / [제작] 탭이 있다. [제작]은 희귀자원으로 원하는 무기를 만드는 화면(WeaponCraftingView)이고,
// 탭을 바꾸면 내용이 옆으로 밀리며 바뀐다. 창을 열 때는 항상 [강화] 탭으로 시작한다.
public class WeaponEnhancementManager : MonoBehaviour
{
    public PauseManager pauseManager; // ESC 상태 확인 + Time.timeScale 계산에 상태를 알려주기 위한 참조

    private const float BannerDuration = 2.2f;

    private const string EffectOnHex = "FFC94A";
    private const string EffectOffHex = "8C94A6";
    private static readonly Color HeaderNormalColor = new Color(0.9f, 0.9f, 0.9f);
    private static readonly Color HeaderWarnColor = new Color(1f, 0.85f, 0.35f);
    private const string NoWeaponNotice = "무기를 든 상태에서 강화할 수 있습니다";

    private bool isPanelOpen;
    private GameObject enhancementPanel; // 무기 강화 창 (프리팹에서 만든 것)
    private UpgradeRowUI[] rows;

    private Text headerText;     // 무기 아이콘 옆 무기 이름 (무기가 없으면 안내 문구)
    private Text bannerText;     // 특수효과 해금 배너
    private float bannerTimer;
    private Text effectsLabels;  // 특수효과 목록 왼쪽 열 (자원/스탯 이름)
    private Text effectsText;    // 오른쪽 열 (현재 효과 → 다음 효과)

    // 마지막으로 RefreshAllRows()가 반영한 무기. 패널이 열린 채 Q로 무기를 바꾸면 이 값과 달라지므로
    // Update()에서 감지해 행/헤더를 새 무기 기준으로 다시 그린다 (예전엔 이전 무기 강화 상태가 그대로 남았음).
    private MonoBehaviour lastShownWeapon;

    // [강화] / [제작] 탭
    private const float TabSwitchTime = 0.2f; // 탭 전환 연출 시간(실제 시간)
    private const float TabSlide = 40f;       // 전환할 때 내용이 옆으로 밀리는 거리
    private WeaponCraftingView craftView;
    private Vector2 craftBasePosition;
    private RectTransform enhanceContent;     // 강화 내용(아이콘, 이름, 강화 행, 효과 목록)을 묶은 그룹
    private CanvasGroup enhanceGroup;
    private WeaponTabView enhanceTab, craftTab;
    private TMPro.TMP_Text windowTitle;
    private string enhanceTitle;
    private bool showCraft;   // 고른 탭
    private float tabBlend;   // 전환 연출 진행: 0 = 강화, 1 = 제작

    void Awake()
    {
        Build();
        if (enhancementPanel != null) enhancementPanel.SetActive(false); // 시작할 땐 항상 닫힌 상태 (isPanelOpen 기본값과 동기화)
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

        if (isPanelOpen)
        {
            // 패널이 열린 채 Q로 무기를 바꾸면 강화 행/헤더를 새 무기 기준으로 다시 그린다.
            if (!ReferenceEquals(FindHeldWeapon(), lastShownWeapon)) RefreshAllRows();
            Animate(Time.unscaledDeltaTime);
            UpdateTabs(Time.unscaledDeltaTime);
        }

        if (!Keyboard.current.vKey.wasPressedThisFrame) return;

        // ESC 설정 패널이나 레벨업 카드 창이 열려 있으면 V키를 완전히 무시한다.
        if (PauseManager.IsEscPaused || PauseManager.IsLevelUpOpen || PauseManager.IsResultOpen) return; // 탈출/사망 결과 화면 중에도 무시

        SetPanelOpen(!isPanelOpen);
    }

    private void SetPanelOpen(bool open)
    {
        if (enhancementPanel == null) return;
        isPanelOpen = open;

        enhancementPanel.SetActive(open);
        if (pauseManager != null) pauseManager.SetInventoryPaused(open);

        if (!open) return;
        EscClose.Push(this, () => isPanelOpen, () => SetPanelOpen(false));
        SetTab(false, true);
        RefreshAllRows();
        foreach (UpgradeRowUI row in rows) row.Snap();
        bannerTimer = 0f;
        Animate(0f);
    }

    // 프리팹에서 창을 만들고 부품에 동작을 연결한다.
    private void Build()
    {
        UiPrefabs prefabs = UiPrefabs.Instance;
        if (prefabs == null || prefabs.weaponEnhancePanel == null) return;

        GameObject canvasGo = new GameObject("WeaponUpgrade_Canvas");
        UiLayer.Setup(canvasGo, UiLayer.Popup); // 건물/강화 팝업 층 (ESC 설정 창보다 아래)

        WeaponEnhanceView view = Instantiate(prefabs.weaponEnhancePanel, canvasGo.transform, false);
        view.name = "WeaponUpgradePanel";
        enhancementPanel = view.gameObject;

        rows = view.rows;
        foreach (UpgradeRowUI row in rows)
        {
            ResourceType type = row.type;
            row.Init(() => OnEnhanceClicked(type));
        }
        if (view.closeButton != null) view.closeButton.onClick.AddListener(() => SetPanelOpen(false));

        headerText = view.headerText;
        bannerText = view.bannerText;
        bannerText.color = new Color(UpgradeRowUI.Gold.r, UpgradeRowUI.Gold.g, UpgradeRowUI.Gold.b, 0f);
        effectsLabels = view.effectsLabels;
        effectsText = view.effectsText;

        enhanceContent = view.enhanceContent;
        enhanceGroup = view.enhanceGroup;
        craftView = view.craftView;
        craftBasePosition = ((RectTransform)craftView.transform).anchoredPosition;
        windowTitle = view.windowTitle;
        enhanceTitle = windowTitle != null ? windowTitle.text : null;
        enhanceTab = view.enhanceTab;
        craftTab = view.craftTab;
        enhanceTab.onClick = () => SetTab(false, false);
        craftTab.onClick = () => SetTab(true, false);
        SetTab(false, true);
    }

    // ===================== [강화] / [제작] 탭 =====================

    private void SetTab(bool craft, bool instant)
    {
        showCraft = craft;
        if (instant) tabBlend = craft ? 1f : 0f;
        if (windowTitle != null && enhanceTitle != null) windowTitle.text = craft ? "Weapon Craft" : enhanceTitle;
        if (!craft) bannerTimer = 0f; // 제작 화면에서 돌아올 때 지난 해금 배너가 다시 보이지 않게

        // 고른 탭이 창 테두리 위에 이어져 보이도록 맨 앞으로, 안 고른 탭은 창 테두리 뒤로 보낸다.
        WeaponTabView front = craft ? craftTab : enhanceTab, back = craft ? enhanceTab : craftTab;
        if (front != null) front.transform.SetAsLastSibling();
        if (back != null) back.transform.SetSiblingIndex(0);
        ApplyTabs();
    }

    private void UpdateTabs(float dt)
    {
        float target = showCraft ? 1f : 0f;
        if (Mathf.Approximately(tabBlend, target)) return;
        tabBlend = Mathf.MoveTowards(tabBlend, target, dt / TabSwitchTime);
        ApplyTabs();
    }

    // tabBlend 0 = 강화, 1 = 제작. 나가는 쪽은 흐려지며 반대쪽으로, 들어오는 쪽은 옆에서 제자리로.
    private void ApplyTabs()
    {
        if (enhanceGroup == null || craftView == null) return;
        float e = tabBlend * tabBlend * (3f - 2f * tabBlend); // 부드럽게 (smoothstep)

        enhanceGroup.alpha = 1f - e;
        enhanceGroup.interactable = enhanceGroup.blocksRaycasts = tabBlend < 0.5f;
        enhanceContent.anchoredPosition = new Vector2(-TabSlide * e, 0f);

        // 제작 화면은 [제작]을 골랐거나 아직 사라지는 중일 때만 켜 둔다 (고른 직후 진행도가 0이어도 꺼지면 안 된다).
        bool craftVisible = showCraft || tabBlend > 0f;
        if (craftView.gameObject.activeSelf != craftVisible) craftView.gameObject.SetActive(craftVisible);
        craftView.Group.alpha = e;
        craftView.Group.interactable = craftView.Group.blocksRaycasts = tabBlend >= 0.5f;
        ((RectTransform)craftView.transform).anchoredPosition = craftBasePosition + new Vector2(TabSlide * (1f - e), 0f);

        if (enhanceTab != null) enhanceTab.Apply(1f - e);
        if (craftTab != null) craftTab.Apply(e);
    }

    public static string WeaponDisplayName(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return "권총";
            case WeaponType.Sword: return "검";
            case WeaponType.Smg: return "기관단총";
            case WeaponType.Lance: return "창";
            case WeaponType.Grenade: return "수류탄";
            default: return type.ToString();
        }
    }

    private void OnEnhanceClicked(ResourceType type)
    {
        IEnhanceableWeapon weapon = FindHeldWeapon() as IEnhanceableWeapon;
        // 종류별 최대(10)와 무기 전체 강화 횟수 상한(15)을 자원을 쓰기 전에 먼저 확인한다 (못 올리는데 자원만 나가면 안 되므로).
        if (weapon == null || !weapon.CanEnhance(type)) return;
        // 비용은 종류별 단계가 아니라 이 무기에 올린 강화 횟수의 합으로 정해진다 (WeaponEnhanceUtil.CostForWeapon).
        if (!ResourceBank.TrySpendRunResource(type, WeaponEnhanceUtil.CostForWeapon(weapon.GetTotalEnhanceLevel()))) return;

        weapon.ApplyEnhance(type);
        RefreshAllRows();

        UpgradeRowUI row = System.Array.Find(rows, r => r.type == type);
        if (row == null) return;
        row.Pulse();

        int level = weapon.GetEnhanceLevel(type);
        if (System.Array.IndexOf(WeaponEnhanceUtil.MilestoneTiers, level) >= 0)
        {
            string effect = weapon.GetMilestoneEffect(type, level);
            bannerText.text = effect != null ? $"◆ {row.label} {level}강 해금 — {effect}" : $"◆ {row.label} {level}강 달성";
            bannerTimer = BannerDuration;
        }
    }

    // 지금 플레이어가 "들고 있는"(WeaponAim.isHeld) 강화 가능한 무기의 컴포넌트를 찾는다.
    private static MonoBehaviour FindHeldWeapon()
    {
        WeaponAim[] aims = FindObjectsByType<WeaponAim>(FindObjectsSortMode.None);
        foreach (WeaponAim aim in aims)
        {
            if (!aim.isHeld) continue;

            IEnhanceableWeapon weapon = aim.GetComponent<IEnhanceableWeapon>();
            if (weapon != null) return (MonoBehaviour)weapon;
        }
        return null;
    }

    private void RefreshAllRows()
    {
        if (rows == null) return;

        MonoBehaviour heldComponent = FindHeldWeapon();
        IEnhanceableWeapon heldWeapon = heldComponent as IEnhanceableWeapon;
        bool hasWeapon = heldWeapon != null;
        lastShownWeapon = heldComponent;

        // 무기 한 자루가 5개 종류를 통틀어 올릴 수 있는 총 횟수(15)와 지금까지 올린 횟수.
        int total = hasWeapon ? heldWeapon.GetTotalEnhanceLevel() : 0;
        bool totalFull = hasWeapon && total >= WeaponEnhanceUtil.TotalMaxLevel;

        if (headerText != null)
        {
            WeaponIdentity id = hasWeapon ? heldComponent.GetComponent<WeaponIdentity>() : null;
            string title = id != null ? WeaponDisplayName(id.type) : "강화 대상";
            string count = totalFull
                ? $"<color=#FFCA4A>강화 {total}/{WeaponEnhanceUtil.TotalMaxLevel}</color>"
                : $"강화 {total}/{WeaponEnhanceUtil.TotalMaxLevel}";
            headerText.text = !hasWeapon ? NoWeaponNotice : $"{title}  <size=16>{count}</size>";
            headerText.color = hasWeapon ? HeaderNormalColor : HeaderWarnColor;
        }

        foreach (UpgradeRowUI row in rows)
        {
            int level = hasWeapon ? heldWeapon.GetEnhanceLevel(row.type) : 0;
            int maxLevel = hasWeapon ? heldWeapon.MaxEnhanceLevel : WeaponEnhanceUtil.MaxLevel;
            row.Refresh(level, maxLevel, ResourceBank.GetRunHeld(row.type), WeaponEnhanceUtil.CostForWeapon(total), hasWeapon, totalFull);
        }

        RefreshEffects(heldWeapon);
    }

    // 자원별로 "지금 켜진 효과(금색) → 다음 단계 효과(회색)"를 한 줄씩.
    // 같은 자원 안에서는 상위 단계가 하위를 대신하므로(SwordAttack.EffectTier) 켜진 효과는 가장 높은 달성 단계 하나다.
    private void RefreshEffects(IEnhanceableWeapon weapon)
    {
        if (effectsText == null) return;
        if (weapon == null) { effectsText.text = effectsLabels.text = ""; return; }

        var labels = new System.Text.StringBuilder("<color=#DDC775>특수효과</color>\n");
        var effects = new System.Text.StringBuilder($"<color=#{EffectOffHex}>◆ 5강마다 해금</color>\n");
        foreach (UpgradeRowUI row in rows)
        {
            int level = weapon.GetEnhanceLevel(row.type);
            string current = null;
            int nextTier = 0;
            foreach (int tier in WeaponEnhanceUtil.MilestoneTiers)
            {
                string effect = weapon.GetMilestoneEffect(row.type, tier);
                if (level >= tier) { if (effect != null) current = effect; }
                else if (nextTier == 0) nextTier = tier;
            }

            labels.Append(row.label).Append('\n');
            effects.Append(current != null ? $"<color=#{EffectOnHex}>{current}</color>" : $"<color=#{EffectOffHex}>없음</color>");
            if (nextTier > 0)
                effects.Append($"<color=#{EffectOffHex}>  →  {nextTier}강 {weapon.GetMilestoneEffect(row.type, nextTier) ?? "미정"}</color>");
            effects.Append('\n');
        }
        effectsLabels.text = labels.ToString();
        effectsText.text = effects.ToString();
    }

    private void Animate(float dt)
    {
        if (rows == null) return;
        foreach (UpgradeRowUI row in rows) row.Animate(dt);

        if (bannerText != null)
        {
            bannerTimer = Mathf.Max(0f, bannerTimer - dt);
            Color c = bannerText.color;
            c.a = Mathf.Clamp01(bannerTimer / 0.5f); // 마지막 0.5초 동안 서서히 사라진다
            bannerText.color = c;
        }
    }
}
