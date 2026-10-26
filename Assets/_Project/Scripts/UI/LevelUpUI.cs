using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 레벨업 UI 전체: 화면 맨 위 경험치 바(+레벨, 분노 남은 시간), 부활 횟수 아이콘, 레벨업 카드 창.
// 씬에 따로 배치하지 않고 게임 시작 시 코드로 만들어서(씬 파일을 건드리지 않음) 씬이 바뀌어도 계속 유지된다.
// 그림은 LevelUpConfig에 연결된 PixelArtGUI 스프라이트를 쓰고, 글자는 한글이 되는 neodgm 폰트를 쓴다.
//
// 고르지 않은 레벨업(LevelUpSystem.PendingLevelUps)이 생기면 게임을 멈추고(PauseManager) 반투명 회색 화면에
// 카드 3장을 띄운다. 클릭하거나 1/2/3 키로 고르면 적용하고, 레벨업이 더 남아 있으면 바로 다음 카드를, 아니면 창을 닫고 재개한다.
public class LevelUpUI : MonoBehaviour
{
    private const float BarHeight = 14f;
    private const float InputDelay = 0.35f; // 창이 뜬 직후 실수로 고르지 않게 이 시간(실제 시간) 동안은 입력을 받지 않는다
    private const float CardWidth = 250f;
    private const float CardHeight = 330f;
    private const float CardSpacing = 28f;

    private static readonly Color PanelColor = new Color(0.19f, 0.27f, 0.34f);
    private static readonly Color PanelDark = new Color(0.086f, 0.12f, 0.17f);
    private static readonly Color Gold = new Color(1f, 0.86f, 0.42f);
    private static readonly Color StatTagColor = new Color(0.2f, 0.45f, 0.7f);
    private static readonly Color FillerTagColor = new Color(0.65f, 0.4f, 0.15f);

    private static LevelUpUI instance;

    private LevelUpConfig cfg;
    private Font font;
    private Canvas canvas;

    // HUD
    private GameObject hudRoot;
    private RectTransform barFill;
    private Text levelText;
    private Text rageText;
    private GameObject reviveRoot;
    private Text reviveText;
    private float shownProgress;
    private int shownLevel = -1;
    private Canvas shiftedHudCanvas;
    private PlayerHealth playerHealth;

    // 카드 창
    private GameObject overlay;
    private CanvasGroup overlayGroup;
    private Text subtitle;
    private readonly CardView[] cards = new CardView[3];
    private List<LevelUpCard> currentCards;
    private bool isOpen;
    private bool choosing;
    private float openedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("LevelUpUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LevelUpUI>();
    }

    void Awake()
    {
        cfg = LevelUpConfig.Instance;
        font = cfg.font != null ? cfg.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
    }

    void Update()
    {
        UpdateHud();

        if (!isOpen)
        {
            // 다른 창(ESC/V)이 열려 있거나 죽는 중이면 그게 끝날 때까지 기다렸다가 연다.
            if (LevelUpSystem.PendingLevelUps > 0 && !EnemyManager.PlayerDead && !PauseManager.IsPaused) Open();
            return;
        }

        // 탐험이 끝나서 레벨업이 초기화됐으면 창을 닫는다.
        if (LevelUpSystem.PendingLevelUps <= 0 && !choosing)
        {
            Close();
            return;
        }

        AnimateOverlay();

        if (choosing || Time.unscaledTime < openedAt + InputDelay) return;
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Choose(0);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Choose(1);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Choose(2);
    }

    // ===================== HUD =====================

    private void UpdateHud()
    {
        bool inStage = StageManager.IsInStage;
        if (hudRoot.activeSelf != inStage) hudRoot.SetActive(inStage);
        if (!inStage) return;

        ShiftExistingHud();

        // 경험치 바: 레벨이 바뀌면 처음부터 다시 차오른다.
        if (shownLevel != LevelUpSystem.Level)
        {
            shownLevel = LevelUpSystem.Level;
            shownProgress = 0f;
            levelText.text = $"Lv {shownLevel}";
        }
        shownProgress = Mathf.MoveTowards(shownProgress, LevelUpSystem.Progress, Time.unscaledDeltaTime * 1.5f);
        barFill.anchorMax = new Vector2(shownProgress, 1f);

        float rage = PlayerRunStats.RageRemaining;
        rageText.gameObject.SetActive(rage > 0f);
        if (rage > 0f) rageText.text = $"분노 {Mathf.CeilToInt(rage)}초";
        UpdateRageAura(rage > 0f);

        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
        int revives = playerHealth != null ? playerHealth.ReviveCount : 0;
        reviveRoot.SetActive(revives > 0);
        if (revives > 0) reviveText.text = $"x{revives}";
    }

    // 분노 버프가 켜져 있는 동안 플레이어 뒤에서 일렁이는 붉은 불꽃 (반복 재생). 버프가 끝나거나 거점으로 가면 끈다.
    private PixelFx rageAura;

    private void UpdateRageAura(bool active)
    {
        if (!active || !StageManager.IsInStage)
        {
            if (rageAura != null) { rageAura.Stop(); rageAura = null; }
            return;
        }

        Transform p = PixelFxUtil.Player;
        if (p == null) return;
        if (rageAura == null || !rageAura.IsPlaying)
        {
            SpriteRenderer psr = p.GetComponent<SpriteRenderer>();
            rageAura = PixelFx.Play("rage_aura", p.position, 0.75f, new Color(1f, 1f, 1f, 0.85f), psr != null ? psr.sortingOrder - 1 : 1);
        }
        if (rageAura != null) rageAura.SetPosition((Vector2)p.position + Vector2.down * 0.08f);
    }

    // 경험치 바가 화면 맨 위를 차지하므로, 기존 HUD(체력, 스테이지, 타이머, 자원 등 위쪽에 붙은 것들)를 바 높이만큼 내린다.
    // 씬 파일을 고치지 않으려고 실행 중에 한 번만 옮긴다 (HUD 캔버스가 새로 만들어지면 다시 옮긴다).
    private void ShiftExistingHud()
    {
        if (shiftedHudCanvas != null) return; // 이미 옮겼다 (HUD가 다시 만들어지면 참조가 끊겨 다시 찾는다)

        PlayerHealthUI healthUI = FindFirstObjectByType<PlayerHealthUI>();
        Canvas hud = healthUI != null ? healthUI.GetComponentInParent<Canvas>() : null;
        if (hud == null) return;
        hud = hud.rootCanvas;
        shiftedHudCanvas = hud;
        foreach (Transform child in hud.transform)
        {
            RectTransform rt = child as RectTransform;
            if (rt == null) continue;
            if (Mathf.Approximately(rt.anchorMin.y, 1f) && Mathf.Approximately(rt.anchorMax.y, 1f))
                rt.anchoredPosition -= new Vector2(0f, BarHeight);
        }
    }

    // ===================== 카드 창 =====================

    private void Open()
    {
        isOpen = true;
        choosing = false;
        EnsureEventSystem();
        SetPaused(true);
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        overlayGroup.alpha = 0f;
        ShowNewCards();
    }

    private void ShowNewCards()
    {
        currentCards = LevelUpCardPool.Roll(3);
        subtitle.text = $"Lv {LevelUpSystem.Level - LevelUpSystem.PendingLevelUps + 1} 달성!  능력을 하나 고르세요";
        for (int i = 0; i < cards.Length; i++)
        {
            bool has = i < currentCards.Count;
            cards[i].gameObject.SetActive(has);
            if (has) cards[i].Show(currentCards[i], i * 0.07f);
        }
        openedAt = Time.unscaledTime;
    }

    private void Close()
    {
        isOpen = false;
        choosing = false;
        overlay.SetActive(false);
        SetPaused(false);
    }

    private void AnimateOverlay()
    {
        overlayGroup.alpha = Mathf.MoveTowards(overlayGroup.alpha, 1f, Time.unscaledDeltaTime * 6f);
    }

    public bool CanChoose => isOpen && !choosing && Time.unscaledTime >= openedAt + InputDelay;

    public void Choose(int index)
    {
        if (!CanChoose || currentCards == null || index < 0 || index >= currentCards.Count) return;
        StartCoroutine(ChooseRoutine(index));
    }

    private IEnumerator ChooseRoutine(int index)
    {
        choosing = true;
        for (int i = 0; i < cards.Length; i++)
            if (cards[i].gameObject.activeSelf) cards[i].SetResult(i == index);

        yield return new WaitForSecondsRealtime(0.22f);

        LevelUpCardPool.Apply(currentCards[index]);
        PixelFxUtil.PlayOnPlayer("glow_sparkle", 1.1f, new Color(1f, 0.95f, 0.55f)); // 선택이 끝나 게임이 재개되는 순간 플레이어에게 반짝임
        LevelUpSystem.ConsumePending();
        choosing = false;

        if (LevelUpSystem.PendingLevelUps > 0) ShowNewCards(); // 한꺼번에 여러 레벨이 찬 경우 바로 다음 카드
        else Close();
    }

    private void SetPaused(bool paused)
    {
        PauseManager pm = FindFirstObjectByType<PauseManager>();
        if (pm != null) pm.SetLevelUpPaused(paused);
        else Time.timeScale = paused ? 0f : 1f;
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
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60; // 기존 HUD(0)보다 위
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        BuildHud();
        BuildOverlay();
    }

    private void BuildHud()
    {
        hudRoot = NewRect("Hud", transform).gameObject;
        Stretch(hudRoot.GetComponent<RectTransform>());

        // 경험치 바 (화면 맨 위 전체 폭)
        RectTransform bar = NewRect("XpBar", hudRoot.transform);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, BarHeight);
        bar.anchoredPosition = Vector2.zero;
        NewImage("Bg", bar, null, new Color(0.05f, 0.07f, 0.1f, 0.85f), 0f, true);

        RectTransform fillArea = NewRect("FillArea", bar);
        Stretch(fillArea, 2f, 2f, 2f, 2f);
        barFill = NewImage("Fill", fillArea, null, new Color(0.35f, 0.95f, 0.4f), 0f, false).rectTransform;
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        RectTransform shine = NewImage("Shine", barFill, null, new Color(1f, 1f, 1f, 0.35f), 0f, false).rectTransform;
        shine.anchorMin = new Vector2(0f, 0.6f);
        shine.anchorMax = Vector2.one;
        shine.offsetMin = shine.offsetMax = Vector2.zero;

        levelText = NewText("Level", bar, 13, Color.white, TextAnchor.MiddleLeft);
        Stretch(levelText.rectTransform, 8f, 0f, 0f, 0f);
        rageText = NewText("Rage", bar, 13, new Color(1f, 0.55f, 0.3f), TextAnchor.MiddleRight);
        Stretch(rageText.rectTransform, 0f, 8f, 0f, 0f);

        // 부활 횟수 (왼쪽 위, 회복약 표시 아래)
        RectTransform revive = NewRect("Revive", hudRoot.transform);
        revive.anchorMin = revive.anchorMax = new Vector2(0f, 1f);
        revive.pivot = new Vector2(0f, 1f);
        revive.anchoredPosition = new Vector2(20f, -(BarHeight + 110f));
        revive.sizeDelta = new Vector2(150f, 28f);
        Image icon = NewImage("Icon", revive, cfg.reviveIcon, cfg.reviveIcon != null ? Color.white : Gold, 0f, false);
        icon.preserveAspect = true;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        reviveText = NewText("Count", revive, 20, Color.white, TextAnchor.MiddleLeft);
        Stretch(reviveText.rectTransform, 36f, 0f, 0f, 0f);
        reviveRoot = revive.gameObject;
        reviveRoot.SetActive(false);

        hudRoot.SetActive(false);
    }

    private void BuildOverlay()
    {
        RectTransform root = NewRect("LevelUpOverlay", transform);
        Stretch(root);
        overlay = root.gameObject;
        overlayGroup = overlay.AddComponent<CanvasGroup>();
        Image dim = NewImage("Dim", root, null, new Color(0.1f, 0.1f, 0.12f, 0.72f), 0f, true);
        dim.raycastTarget = true; // 뒤쪽 UI/게임이 클릭되지 않게 막는다

        // 제목 배너
        RectTransform banner = NewRect("Banner", root);
        banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
        banner.pivot = new Vector2(0.5f, 1f);
        banner.anchoredPosition = new Vector2(0f, -70f);
        banner.sizeDelta = new Vector2(440f, 76f);
        NewImage("Plate", banner, cfg.titleBanner, cfg.titleBanner != null ? Color.white : Gold, cfg.pixelScale, true);
        Text title = NewText("Title", banner, 38, new Color(0.3f, 0.17f, 0.05f), TextAnchor.MiddleCenter, outline: false);
        Stretch(title.rectTransform, 0f, 0f, 0f, 4f);
        title.text = "LEVEL UP!";

        subtitle = NewText("Subtitle", root, 20, Color.white, TextAnchor.MiddleCenter);
        subtitle.rectTransform.anchorMin = subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, -156f);
        subtitle.rectTransform.sizeDelta = new Vector2(700f, 30f);

        for (int i = 0; i < cards.Length; i++)
        {
            float x = (i - 1) * (CardWidth + CardSpacing);
            cards[i] = BuildCard(root, i, new Vector2(x, -40f));
        }

        Text hint = NewText("Hint", root, 17, new Color(0.85f, 0.9f, 0.95f, 0.75f), TextAnchor.MiddleCenter);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 52f);
        hint.rectTransform.sizeDelta = new Vector2(600f, 26f);
        hint.text = "클릭하거나 1 · 2 · 3 키로 선택";

        overlay.SetActive(false);
    }

    private CardView BuildCard(Transform parent, int index, Vector2 position)
    {
        RectTransform card = NewRect($"Card{index + 1}", parent);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(CardWidth, CardHeight);
        card.anchoredPosition = position;

        CardView view = card.gameObject.AddComponent<CardView>();
        view.owner = this;
        view.index = index;
        view.basePosition = position;
        view.group = card.gameObject.AddComponent<CanvasGroup>();

        float ps = cfg.pixelScale;

        // 그림자 / 바깥 금테(마우스를 올리면 보임) / 몸통 / 윗면 반사 / 금색 모서리 장식
        RectTransform shadow = NewImage("Shadow", card, cfg.panel, new Color(0f, 0f, 0f, 0.45f), ps, true).rectTransform;
        shadow.offsetMin = new Vector2(4f, -8f);
        shadow.offsetMax = new Vector2(4f, -8f);
        view.outline = NewImage("Outline", card, cfg.panelOutline != null ? cfg.panelOutline : cfg.panel, new Color(Gold.r, Gold.g, Gold.b, 0f), ps, true);
        Stretch(view.outline.rectTransform, -6f, -6f, -6f, -6f);
        NewImage("Body", card, cfg.panel, PanelColor, ps, true);
        if (cfg.panelTopBevel != null) NewImage("Highlight", card, cfg.panelTopBevel, new Color(1f, 1f, 1f, 0.1f), ps, true);
        if (cfg.corners != null) NewImage("Corners", card, cfg.corners, Color.white, ps * 0.5f, true);

        // 종류 표시 (능력 / 보급)
        RectTransform tag = NewRect("Tag", card);
        tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 1f);
        tag.pivot = new Vector2(0.5f, 0.5f);
        tag.anchoredPosition = new Vector2(0f, -24f);
        tag.sizeDelta = new Vector2(96f, 24f);
        view.tagBg = NewImage("TagBg", tag, cfg.panel, StatTagColor, ps * 0.66f, true);
        view.tagText = NewText("TagText", tag, 15, Color.white, TextAnchor.MiddleCenter);
        Stretch(view.tagText.rectTransform);

        // 아이콘 칸
        RectTransform slot = NewRect("Slot", card);
        slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 1f);
        slot.pivot = new Vector2(0.5f, 1f);
        slot.anchoredPosition = new Vector2(0f, -46f);
        slot.sizeDelta = new Vector2(104f, 104f);
        NewImage("SlotBg", slot, cfg.panel, PanelDark, ps, true);
        view.icon = NewImage("Icon", slot, null, Color.white, 0f, false);
        view.icon.preserveAspect = true;
        view.icon.rectTransform.anchorMin = view.icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        view.icon.rectTransform.sizeDelta = new Vector2(72f, 72f);
        view.icon.rectTransform.anchoredPosition = Vector2.zero;

        view.title = NewText("Title", card, 26, Gold, TextAnchor.MiddleCenter);
        Place(view.title.rectTransform, -168f, 230f, 34f);
        view.level = NewText("Level", card, 16, new Color(0.72f, 0.84f, 0.95f), TextAnchor.MiddleCenter);
        Place(view.level.rectTransform, -200f, 230f, 22f);
        view.desc = NewText("Desc", card, 18, Color.white, TextAnchor.UpperCenter);
        Place(view.desc.rectTransform, -228f, 214f, 66f);
        view.desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        view.desc.verticalOverflow = VerticalWrapMode.Overflow;

        Text key = NewText("Key", card, 17, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
        key.rectTransform.anchorMin = key.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        key.rectTransform.anchoredPosition = new Vector2(0f, 24f);
        key.rectTransform.sizeDelta = new Vector2(80f, 24f);
        key.text = $"[ {index + 1} ]";

        // 카드 전체가 클릭을 받도록 투명한 판을 맨 위에 깐다.
        Image hit = NewImage("Hit", card, null, new Color(0f, 0f, 0f, 0f), 0f, true);
        hit.raycastTarget = true;

        return view;
    }

    // 카드 위쪽 기준으로 y 위치와 크기를 정한다.
    private static void Place(RectTransform rt, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // pixelScale: 그림 1픽셀을 UI 몇 칸으로 보일지 (9칸 나눔 그림의 테두리 두께). 0이면 그림 원래 비율 그대로.
    private Image NewImage(string name, Transform parent, Sprite sprite, Color color, float pixelScale, bool stretch)
    {
        RectTransform rt = NewRect(name, parent);
        if (stretch) Stretch(rt);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            if (sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                if (pixelScale > 0f) img.pixelsPerUnitMultiplier = 100f / (sprite.pixelsPerUnit * pixelScale);
            }
        }
        return img;
    }

    private Text NewText(string name, Transform parent, int size, Color color, TextAnchor align, bool outline = true)
    {
        RectTransform rt = NewRect(name, parent);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        if (outline)
        {
            Outline o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.65f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return t;
    }

    // ===================== 카드 한 장 =====================

    private class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public LevelUpUI owner;
        public int index;
        public Vector2 basePosition;
        public CanvasGroup group;
        public Image outline;
        public Image tagBg;
        public Text tagText;
        public Image icon;
        public Text title;
        public Text level;
        public Text desc;

        private bool hovered;
        private float hover;       // 0~1 마우스 올림 연출
        private float appear;      // 0~1 나타나는 연출
        private float appearDelay;
        private int result;        // 0 = 고르는 중, 1 = 선택됨, -1 = 선택 안 됨
        private float resultTime;

        public void Show(LevelUpCard card, float delay)
        {
            tagText.text = card.isStat ? "능력" : "보급";
            tagBg.color = card.isStat ? StatTagColor : FillerTagColor;
            icon.sprite = card.icon;
            icon.enabled = card.icon != null;
            title.text = card.title;
            level.text = card.levelText;
            desc.text = card.description;

            hovered = false;
            hover = 0f;
            appear = 0f;
            appearDelay = delay;
            result = 0;
            Apply();
        }

        public void SetResult(bool chosen)
        {
            result = chosen ? 1 : -1;
            resultTime = 0f;
        }

        public void OnPointerEnter(PointerEventData e) => hovered = true;
        public void OnPointerExit(PointerEventData e) => hovered = false;
        public void OnPointerClick(PointerEventData e) => owner.Choose(index);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (appearDelay > 0f) appearDelay -= dt;
            else appear = Mathf.MoveTowards(appear, 1f, dt * 6f);

            bool lit = result == 1 || (result == 0 && hovered && owner.CanChoose);
            hover = Mathf.MoveTowards(hover, lit ? 1f : 0f, dt * 8f);
            if (result != 0) resultTime += dt;
            Apply();
        }

        private void Apply()
        {
            float ease = 1f - (1f - appear) * (1f - appear);
            float scale = (0.85f + 0.15f * ease) * (1f + 0.05f * hover);
            float alpha = ease;

            if (result == 1) scale *= 1f + 0.08f * Mathf.Sin(Mathf.Clamp01(resultTime / 0.22f) * Mathf.PI); // 고른 카드는 한 번 툭 튄다
            else if (result == -1) alpha *= Mathf.Clamp01(1f - resultTime / 0.15f);                      // 나머지는 사라진다

            transform.localScale = new Vector3(scale, scale, 1f);
            ((RectTransform)transform).anchoredPosition = basePosition + new Vector2(0f, 10f * hover - 20f * (1f - ease));
            group.alpha = alpha;
            outline.color = new Color(Gold.r, Gold.g, Gold.b, hover);
        }
    }
}
