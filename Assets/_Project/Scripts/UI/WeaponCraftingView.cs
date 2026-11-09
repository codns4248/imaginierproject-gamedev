using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// V 무기 강화 창의 [제작] 탭 내용: 이번 탐험에서 주운 희귀자원(runHeld) 1개로 원하는 무기를 확정 제작한다.
//  - 무기 5종을 카드로 보여 주고, 카드를 누르면 "제작하시겠습니까?" 확인창 -> [예]면 희귀자원 1개를 쓰고
//    0강 무기를 플레이어가 바라보는 방향 조금 앞에 떨어뜨린다 (인벤토리로 바로 넣지 않는다: F로 줍는다).
//  - 슬롯이 꽉 차도 만들 수 있다 (B로 버리고 새로 만든 걸 주워 바꿀 수 있게). 같은 무기도 여러 번 만들 수 있다.
//  - 희귀자원이 부족하면 카드가 어둡게 비활성화되고 눌리지 않는다.
// 강화 창(씬 UI)의 바탕을 그대로 같이 쓰는 탭 내용이라 자기 바탕은 없다. 탭 전환(밀려 들어오고 사라지는 연출)은
// WeaponEnhancementManager가 Group(CanvasGroup)으로 한다. 칸/버튼 모양은 강화 창과 같은 PixelUi를 쓴다.
public class WeaponCraftingView : MonoBehaviour
{
    public const int Cost = 1;
    private const float DropDistance = 0.6f; // 플레이어 앞 몇 유닛에 떨어뜨릴지 (줍기 반경 0.5보다 살짝 멀어서 한 걸음 다가가 줍는다)

    // 카드 순서 (창에서 왼쪽부터)
    private static readonly WeaponType[] Order = { WeaponType.Sword, WeaponType.Lance, WeaponType.Pistol, WeaponType.Smg, WeaponType.Grenade };
    private static readonly Color CantAfford = new Color(1f, 0.42f, 0.38f);

    public CanvasGroup Group { get; private set; }

    private Font font;
    private Text infoText;
    private CraftCard[] cards;
    private GameObject confirmRoot;
    private Text confirmText;
    private WeaponType pending;

    private bool CanAfford => ResourceBank.GetRunHeld(ResourceType.Rare) >= Cost;

    public static WeaponCraftingView Create(RectTransform window, Font font)
    {
        RectTransform root = PixelUi.NewRect("CraftView", window);
        PixelUi.Stretch(root, 30f, 30f, 24f, 70f); // 창 제목줄 아래, 테두리 안쪽
        WeaponCraftingView view = root.gameObject.AddComponent<WeaponCraftingView>();
        view.Group = root.gameObject.AddComponent<CanvasGroup>();
        view.font = LevelUpConfig.Instance.font != null ? LevelUpConfig.Instance.font : font;
        view.Build(root);
        return view;
    }

    void OnEnable()
    {
        ResourceBank.OnChanged += Refresh;
        HideConfirm();
        Refresh();
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        if (cards == null) return;
        int held = ResourceBank.GetRunHeld(ResourceType.Rare);
        infoText.text = $"희귀 자원 {Cost}개로 원하는 무기를 만듭니다   <color=#{(CanAfford ? "FFDB6B" : "FF6B61")}>(보유 {held}개)</color>";
        foreach (CraftCard card in cards) card.SetEnabled(CanAfford);
    }

    private void OnCardClicked(WeaponType type)
    {
        if (!CanAfford || confirmRoot.activeSelf) return;
        pending = type;
        string name = WeaponEnhancementManager.WeaponDisplayName(type);
        confirmText.text = $"{name}{ObjectParticle(name)} 제작하시겠습니까?";
        confirmRoot.SetActive(true);
    }

    private void OnConfirmYes()
    {
        HideConfirm();
        if (!StageManager.IsInStage || EnemyManager.PlayerDead) return;
        if (!ResourceBank.TrySpendRunResource(ResourceType.Rare, Cost)) { Refresh(); return; }

        GameObject player = GameObject.Find("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        Vector2 facing = movement != null ? movement.FacingDirection : Vector2.right;
        WeaponPickup.SpawnDrop(origin + (Vector3)(facing * DropDistance), pending); // 새 무기라 강화 레벨 없음(0강)
        Refresh();
    }

    // 받침이 있으면 "을", 없으면 "를" (검을 / 권총을 ...).
    private static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "을";
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return "을(를)";
        return (last - 0xAC00) % 28 != 0 ? "을" : "를";
    }

    private void HideConfirm()
    {
        if (confirmRoot != null) confirmRoot.SetActive(false);
    }

    // ===================== 만들기 =====================

    private void Build(RectTransform root)
    {
        infoText = PixelUi.NewText("Info", root, font, 20, Color.white, TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(infoText.rectTransform, new Vector2(0f, -18f), new Vector2(640f, 30f));

        const float cardW = 114f, cardH = 236f, gap = 12f;
        cards = new CraftCard[Order.Length];
        for (int i = 0; i < Order.Length; i++)
        {
            float x = (i - (Order.Length - 1) * 0.5f) * (cardW + gap);
            cards[i] = BuildCard(root, Order[i], new Vector2(x, -8f), new Vector2(cardW, cardH));
        }

        Text hint = PixelUi.NewText("Hint", root, font, 16, new Color(0.85f, 0.9f, 0.95f, 0.75f), TextAnchor.MiddleCenter);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        hint.rectTransform.sizeDelta = new Vector2(640f, 24f);
        hint.text = "만든 무기는 0강으로 플레이어 앞에 떨어집니다 (F로 줍기)";

        BuildConfirm(root);
    }

    // 강화 행과 같은 금색 베벨 칸 + 금테 아이콘 칸 + 이름 + 비용.
    private CraftCard BuildCard(RectTransform parent, WeaponType type, Vector2 position, Vector2 size)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        RectTransform rt = PixelUi.NewRect($"Card_{type}", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        CraftCard card = rt.gameObject.AddComponent<CraftCard>();
        card.type = type;
        card.onClick = OnCardClicked;
        card.basePosition = position;
        card.group = rt.gameObject.AddComponent<CanvasGroup>();

        // 마우스를 올리면 보이는 바깥 금테
        card.outline = PixelUi.NewImage("Outline", rt, cfg.panelOutline != null ? cfg.panelOutline : cfg.panel,
            new Color(PixelUi.Gold.r, PixelUi.Gold.g, PixelUi.Gold.b, 0f), PixelUi.FrameScale, true);
        PixelUi.Stretch(card.outline.rectTransform, -5f, -5f, -5f, -5f);
        PixelUi.BevelPanel(rt, PixelUi.PanelColor).raycastTarget = true;

        // 무기 그림 칸 (강화 창의 무기 아이콘 칸과 같은 금테)
        RectTransform slot = PixelUi.NewRect("Slot", rt);
        PixelUi.PlaceTop(slot, new Vector2(0f, -62f), new Vector2(88f, 88f));
        PixelUi.NewImage("SlotBg", slot, cfg.panel, PixelUi.PanelDark, PixelUi.FrameScale, true);
        if (cfg.iconFrame != null) PixelUi.NewImage("SlotFrame", slot, cfg.iconFrame, Color.white, PixelUi.FrameScale, true);
        GameObject prefab = WeaponPickup.GetPrefab(type);
        SpriteRenderer prefabSr = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>() : null;
        Image icon = PixelUi.NewImage("Icon", slot, prefabSr != null ? prefabSr.sprite : null, Color.white, 0f, false);
        icon.preserveAspect = true;
        icon.enabled = icon.sprite != null;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(66f, 66f);
        card.icon = icon;

        Text name = PixelUi.NewText("Name", rt, font, 20, PixelUi.Gold, TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(name.rectTransform, new Vector2(0f, -126f), new Vector2(size.x, 26f));
        name.text = WeaponEnhancementManager.WeaponDisplayName(type);

        // 비용: 아래쪽 어두운 띠 안에 희귀자원 아이콘 + 개수
        RectTransform costBar = PixelUi.NewRect("CostBar", rt);
        costBar.anchorMin = costBar.anchorMax = new Vector2(0.5f, 0f);
        costBar.anchoredPosition = new Vector2(0f, 30f);
        costBar.sizeDelta = new Vector2(size.x - 20f, 34f);
        PixelUi.NewImage("Bg", costBar, cfg.panel, PixelUi.PanelDark, PixelUi.FrameScale, true);
        Image rare = PixelUi.NewImage("RareIcon", costBar, ResourcePickup.GetIconSprite(ResourceType.Rare), Color.white, 0f, false);
        rare.preserveAspect = true;
        rare.rectTransform.anchorMin = rare.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rare.rectTransform.anchoredPosition = new Vector2(-14f, 0f);
        rare.rectTransform.sizeDelta = new Vector2(24f, 24f);
        Text cost = PixelUi.NewText("Cost", costBar, font, 18, Color.white, TextAnchor.MiddleLeft);
        cost.rectTransform.anchorMin = cost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        cost.rectTransform.pivot = new Vector2(0f, 0.5f);
        cost.rectTransform.anchoredPosition = new Vector2(2f, 0f);
        cost.rectTransform.sizeDelta = new Vector2(40f, 24f);
        cost.text = $"x{Cost}";
        card.costText = cost;
        return card;
    }

    // 카드를 누르면 뜨는 확인창: 탭 내용 전체를 어둡게 덮고, 가운데에 강화 창과 같은 모양의 작은 창 + [예]/[아니오].
    private void BuildConfirm(RectTransform root)
    {
        RectTransform dim = PixelUi.NewRect("Confirm", root);
        PixelUi.Stretch(dim, -16f, -16f, -10f, -6f);
        PixelUi.NewImage("Dim", dim, null, new Color(0f, 0f, 0f, 0.55f), 0f, true).raycastTarget = true; // 뒤의 카드가 눌리지 않게
        confirmRoot = dim.gameObject;

        RectTransform box = PixelUi.NewRect("Box", dim);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(440f, 168f);
        PixelUi.Window(box);

        confirmText = PixelUi.NewText("Question", box, font, 22, PixelUi.Gold, TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(confirmText.rectTransform, new Vector2(0f, -50f), new Vector2(400f, 30f));

        PixelUi.Button(box, "Yes", "예", true, font, new Vector2(-84f, 46f), new Vector2(136f, 44f), OnConfirmYes);
        PixelUi.Button(box, "No", "아니오", false, font, new Vector2(84f, 46f), new Vector2(136f, 44f), HideConfirm);
        confirmRoot.SetActive(false);
    }

    // 무기 카드: 마우스를 올리면 금테가 보이고 살짝 떠오르며 커진다. 비활성화되면 어둡게 보이고 눌리지 않는다.
    // (창이 열려 있는 동안 게임이 멈춰 있으므로 실제 시간으로 움직인다)
    private class CraftCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public WeaponType type;
        public Action<WeaponType> onClick;
        public Vector2 basePosition;
        public CanvasGroup group;
        public Image outline;
        public Image icon;
        public Text costText;

        private bool hovered;
        private bool enabledState = true;
        private float hover;

        public void SetEnabled(bool on)
        {
            enabledState = on;
            if (group != null) group.alpha = on ? 1f : 0.4f;
            if (icon != null) icon.color = on ? Color.white : new Color(0.45f, 0.45f, 0.45f);
            if (costText != null) costText.color = on ? Color.white : CantAfford;
            if (!on) hovered = false;
        }

        public void OnPointerEnter(PointerEventData e) => hovered = enabledState;
        public void OnPointerExit(PointerEventData e) => hovered = false;
        public void OnPointerClick(PointerEventData e)
        {
            if (enabledState && e.button == PointerEventData.InputButton.Left) onClick?.Invoke(type);
        }

        void OnDisable()
        {
            hovered = false;
            hover = 0f;
            Apply();
        }

        void Update()
        {
            hover = Mathf.MoveTowards(hover, hovered && enabledState ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            Apply();
        }

        private void Apply()
        {
            transform.localScale = Vector3.one * (1f + 0.04f * hover);
            ((RectTransform)transform).anchoredPosition = basePosition + new Vector2(0f, 6f * hover);
            if (outline != null) outline.color = new Color(PixelUi.Gold.r, PixelUi.Gold.g, PixelUi.Gold.b, hover);
        }
    }
}
