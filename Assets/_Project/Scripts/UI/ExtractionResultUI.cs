using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 탈출 결과 화면: 탈출 포탈(StageExitPortal)이나 긴급탈출(RetreatManager, H)로 탐험을 끝내면 뜬다.
// 사망 결과 화면(DeathResultUI)과 같은 짜임새(라벨 -> 큰 제목 -> 구분선 -> 도달 층수 -> 버튼 -> 안내)에
// 자원 결산 연출(ResourceMergeView: 가져온 수가 창고 수로 들어가다 창고 최대치에 닿으면 잃은 수로)을 더한다. 사망 화면의 붉은색 대신 금색.
//
// 흐름: Extract() 순간 게임을 멈추고(SetResultPaused) 자원을 바로 창고로 확정·저장한 뒤 화면을 띄운다
//   -> 버튼 또는 Enter로 [거점으로 돌아가기]를 누르면 그때 무기/레벨/층수를 초기화하고 거점으로 이동한다.
// 거점 복귀 페이드(StageSceneTransition)는 게임 시간(WaitForSeconds)으로 기다리므로, 멈춘 채로 거점에
// 넘어가면 검은 화면에서 멈춘다 - 그래서 결과 화면은 스테이지에서 띄우고, 닫을 때 거점으로 간다 (사망 화면과 같은 순서).
//
// 씬을 건드리지 않도록 자기 캔버스(UiLayer.Death 층)를 코드로 만든다. 폰트는 사망 결과 화면에 연결된 것
// (큰 글씨 neodgm / 작은 글씨 Pretendard)을 빌려 쓰고, 없으면 레벨업 UI 폰트를 쓴다.
public class ExtractionResultUI : MonoBehaviour
{
    /// <summary>탈출 결과 화면이 떠 있는지 (창고 넘침 알림은 이 화면이 대신 보여 주므로 ResourceBankUI가 띄우지 않는다).</summary>
    public static bool IsShowing { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsShowing = false;

    private const float FadeInDuration = 0.5f;

    private static readonly ResourceType[] Order =
        { ResourceType.Wood, ResourceType.Iron, ResourceType.Copper, ResourceType.Chemical, ResourceType.Oil, ResourceType.Rare };

    // 사망 결과 화면과 같은 명도, 색만 금색 계열
    static readonly Color Backdrop     = new Color(0.039f, 0.035f, 0.027f, 0.94f); // #0a0907
    static readonly Color InkTitle     = new Color(0.953f, 0.902f, 0.769f, 1f);    // #f3e6c4
    static readonly Color InkTitleDim  = new Color(0.851f, 0.780f, 0.604f, 1f);    // #d9c79a
    static readonly Color InkLabel     = new Color(0.561f, 0.525f, 0.439f, 1f);    // #8f8670
    static readonly Color InkLabelDim  = new Color(0.380f, 0.353f, 0.286f, 1f);    // #615a49
    static readonly Color AccentLine   = new Color(0.478f, 0.388f, 0.149f, 1f);    // #7a6326
    static readonly Color AccentBright = new Color(0.788f, 0.635f, 0.290f, 1f);    // #c9a24a
    static readonly Color DividerColor = new Color(0.290f, 0.247f, 0.110f, 1f);    // #4a3f1c
    static readonly Color ButtonText   = new Color(0.937f, 0.910f, 0.847f, 1f);    // #efe8d8
    static readonly Color LostColor    = new Color(1f, 0.541f, 0.478f, 1f);        // #ff8a7a

    private bool emergency;
    private int depth;
    private string location;
    private int level;
    private readonly int[] stashBefore = new int[Order.Length]; // 탈출 직전 거점 창고 수
    private readonly int[] secured = new int[Order.Length];     // 창고로 확정된 양
    private readonly int[] lost = new int[Order.Length];        // 창고가 가득 차서 사라진 양
    private static readonly int[] NoneLostFirst = new int[6];   // 탈출은 먼저 잃는 몫이 없다 (사망 보험과 달리)

    private CanvasGroup group;
    private Button returnButton;
    private ResourceMergeView merge;
    private int skipFrame = -1;
    private bool canContinue;
    private bool finished;

    /// <summary>탐험을 성공적으로 끝낸다: 자원을 창고로 확정하고 결과 화면을 띄운다.
    /// emergency = 긴급탈출(H)로 나온 경우 (라벨만 다르다).</summary>
    public static void Extract(bool emergency)
    {
        if (IsShowing) return;
        IsShowing = true;

        ExtractionResultUI ui = new GameObject("ExtractionResult").AddComponent<ExtractionResultUI>();
        ui.Capture(emergency);
        SetResultPaused(true); // 화면이 나타나는 동안에도 몬스터에게 맞지 않게 바로 멈춘다
        EnsureEventSystem();
        ui.Build();
        ui.StartCoroutine(ui.FadeIn());
    }

    // 결과에 쓸 값을 지금 붙잡아 두고, 자원은 바로 창고로 확정·저장한다 (결과 화면에서 게임을 꺼도 자원은 남게).
    private void Capture(bool emergency)
    {
        this.emergency = emergency;
        depth = Mathf.Max(1, StageProgress.CurrentStageNo);
        string theme = StageManager.CurrentTheme;
        location = string.IsNullOrEmpty(theme) || theme == "거점" ? "미상 구역" : theme;
        level = LevelUpSystem.Level;

        int[] held = new int[Order.Length];
        foreach (ResourceType type in Order)
        {
            held[(int)type] = ResourceBank.GetRunHeld(type);
            stashBefore[(int)type] = ResourceBank.GetStash(type);
        }

        ResourceBank.CommitRunToStash();

        foreach (ResourceType type in Order)
        {
            int i = (int)type;
            secured[i] = Mathf.Max(0, ResourceBank.GetStash(type) - stashBefore[i]);
            lost[i] = Mathf.Max(0, held[i] - secured[i]);
        }
    }

    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < FadeInDuration)
        {
            if (!PauseManager.IsEscPaused) t += Time.unscaledDeltaTime; // ESC로 멈춘 동안엔 연출도 멈춤
            group.alpha = Mathf.Clamp01(t / FadeInDuration);
            yield return null;
        }
        group.alpha = 1f;
        canContinue = true;

        // Enter로 버튼이 눌리도록 선택해 둔다.
        if (EventSystem.current != null && returnButton != null)
            EventSystem.current.SetSelectedGameObject(returnButton.gameObject);

        // 가져온 자원이 창고 수에 합쳐지는 연출 (디리리리 + 링). 가져온 게 없으면 탈출 효과음만.
        if (merge != null && merge.HasAny) yield return merge.Play();
        else Sfx.Play(SfxId.StageClear);
    }

    void Update()
    {
        if (!canContinue || finished || PauseManager.IsEscPaused) return; // ESC 설정 창이 위에 떠 있으면 Enter로 넘어가지 않는다

        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            Continue();
    }

    void OnDestroy()
    {
        if (!finished) IsShowing = false;
    }

    // [거점으로 돌아가기]: 이번 런의 무기(강화 + 필드/상인에서 얻은 여분 무기)와 레벨은 사망과 같은 규칙으로 초기화하고 거점으로.
    private void Continue()
    {
        if (!canContinue || finished || PauseManager.IsEscPaused) return; // Enter와 버튼이 같은 프레임에 겹쳐도 한 번만

        // 합산 연출 중에 누르면 먼저 연출만 끝낸다 (같은 프레임에 Enter와 버튼이 겹쳐 바로 넘어가지 않게 그 프레임은 막는다).
        if (merge != null && !merge.Done) { merge.Finish(); skipFrame = Time.frameCount; return; }
        if (Time.frameCount == skipFrame) return;

        finished = true;
        IsShowing = false;
        SetResultPaused(false);

        WeaponEnhanceStore.ResetForNewRun();
        LevelUpSystem.ResetForNewRun(); // 레벨/경험치/레벨업 능력/부활 횟수
        WeaponSwitcher weaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();
        if (weaponSwitcher != null) weaponSwitcher.ResetToStartingWeapon();
        StageProgress.ResetToFirstStage();
        StageManager.ReturnToHub();

        Destroy(gameObject);
    }

    private static void SetResultPaused(bool paused)
    {
        PauseManager pm = FindFirstObjectByType<PauseManager>();
        if (pm != null) pm.SetResultPaused(paused);
        else Time.timeScale = paused ? 0f : 1f;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // ── UI 생성 ────────────────────────────────────────────────────────────

    private void Build()
    {
        UiLayer.Setup(gameObject, UiLayer.Death, true);

        DeathResultUI death = FindFirstObjectByType<DeathResultUI>(FindObjectsInactive.Include);
        LevelUpConfig cfg = LevelUpConfig.Instance;
        Font fallback = cfg != null && cfg.font != null ? cfg.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Font tFont = death != null && death.titleFont != null ? death.titleFont : fallback;
        Font lFont = death != null && death.labelFont != null ? death.labelFont : fallback;

        // 화면 전체를 덮는 어두운 바탕 (뒤의 게임 화면 클릭도 막는다)
        GameObject rootGo = new GameObject("Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        rootGo.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)rootGo.transform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        rootGo.GetComponent<Image>().color = Backdrop;
        group = rootGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        RectTransform content = NewRect("Content", root, new Vector2(620f, 560f), Vector2.zero, new Vector2(0.5f, 0.5f));

        // 자원 칸 수에 따라 높이가 달라지므로 전체 높이를 먼저 구해 세로 가운데에서 시작한다 (y = 요소 윗변, 콘텐츠 가운데 기준).
        // 칸: (아이콘) 창고 수 +가져온 수 -잃은 수 -> 가져온 수가 창고로 들어가다 창고 최대치에 닿으면 잃은 수로 (ResourceMergeView).
        int rows = ResourceMergeView.RowCount(NoneLostFirst, secured, lost);
        float mergeHeight = rows > 0 ? ResourceMergeView.HeightFor(rows) : 30f;
        float total = 446f + mergeHeight + 10f + 16f; // 마지막 16 = 자원 칸과 버튼 사이 여유

        float y = total / 2f;
        MakeText(content, "Eyebrow", lFont, 15, InkLabelDim, Track(emergency ? "EMERGENCY ESCAPE" : "RUN COMPLETE"), ref y, 22f, 22f);
        MakeText(content, "Title", tFont, 70, InkTitle, PadTitle("EXTRACTED"), ref y, 80f, 20f, glow: true);
        MakeDivider(content, ref y);
        MakeText(content, "DepthLabel", lFont, 14, InkLabelDim, Track("DEPTH REACHED"), ref y, 18f, 10f);
        MakeText(content, "Depth", tFont, 50, InkTitleDim, $"{depth}<size=24><color=#8f8670> 층</color></size>", ref y, 56f, 6f);
        MakeText(content, "Location", lFont, 19, InkLabel, $"{location}   ·   레벨 {level}", ref y, 24f, 28f);
        MakeText(content, "ResourceLabel", lFont, 14, InkLabelDim, Track("RESOURCES SECURED"), ref y, 18f, 10f);
        if (rows > 0)
        {
            merge = new ResourceMergeView(content, y, lFont, stashBefore, NoneLostFirst, secured, lost, InkTitle, AccentBright, LostColor);
            y -= mergeHeight + 26f;
        }
        else
        {
            MakeText(content, "None", lFont, 18, InkLabel, "가져온 자원 없음", ref y, mergeHeight, 26f);
        }
        returnButton = MakeButton(content, lFont, "거점으로 돌아가기", ref y);
        MakeText(content, "Hint", lFont, 14, InkLabelDim, Track("PRESS ENTER TO CONTINUE"), ref y, 18f, 0f);

        returnButton.onClick.AddListener(Continue);
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    // parent를 꽉 채우는 글자
    private static Text NewText(Transform parent, string name, Font font, int size, Color color, string text, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Text t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    private static Text MakeText(RectTransform parent, string name, Font font, int size, Color color, string text,
        ref float y, float height, float gapAfter, bool glow = false)
    {
        RectTransform box = NewRect(name, parent, new Vector2(620f, height), new Vector2(0f, y), new Vector2(0.5f, 1f));
        Text t = NewText(box, "Text", font, size, color, text, TextAnchor.MiddleCenter);
        if (glow)
        {
            Shadow shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(AccentBright.r, AccentBright.g, AccentBright.b, 0.5f);
            shadow.effectDistance = new Vector2(0f, -2f);
        }
        y -= height + gapAfter;
        return t;
    }

    private static void MakeDivider(RectTransform parent, ref float y)
    {
        const float w = 420f, lineW = 190f;
        RectTransform box = NewRect("Divider", parent, new Vector2(w, 16f), new Vector2(0f, y), new Vector2(0.5f, 1f));
        MakeBar(box, "LineL", new Vector2(-(w / 2f - lineW / 2f), 0f), new Vector2(lineW, 1f), DividerColor);
        MakeBar(box, "LineR", new Vector2(w / 2f - lineW / 2f, 0f), new Vector2(lineW, 1f), DividerColor);
        RectTransform mark = MakeBar(box, "Mark", Vector2.zero, new Vector2(6f, 6f), new Color(AccentBright.r, AccentBright.g, AccentBright.b, 0.85f));
        mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
        y -= 16f + 26f;
    }

    private static RectTransform MakeBar(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rt = NewRect(name, parent, size, pos, new Vector2(0.5f, 0.5f));
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // 사망 결과 화면과 같은 테두리 버튼 (테두리 색이 호버/선택 때 밝아진다)
    private static Button MakeButton(RectTransform parent, Font font, string label, ref float y)
    {
        const float bw = 400f, bh = 50f;
        RectTransform border = NewRect("ReturnButton", parent, new Vector2(bw, bh), new Vector2(0f, y), new Vector2(0.5f, 1f));
        Image borderImg = border.gameObject.AddComponent<Image>();
        borderImg.color = Color.white; // 실제 색은 Button ColorBlock에서

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(border, false);
        RectTransform frt = (RectTransform)fill.transform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(2f, 2f);
        frt.offsetMax = new Vector2(-2f, -2f);
        Image fillImg = fill.GetComponent<Image>();
        fillImg.color = new Color(0.051f, 0.047f, 0.035f, 1f);
        fillImg.raycastTarget = false;

        NewText(border, "Text", font, 18, ButtonText, label, TextAnchor.MiddleCenter);

        Button btn = border.gameObject.AddComponent<Button>();
        btn.targetGraphic = borderImg;
        ColorBlock cb = btn.colors;
        cb.colorMultiplier = 1f;
        cb.normalColor = AccentLine;
        cb.highlightedColor = AccentBright;
        cb.selectedColor = AccentBright;
        cb.pressedColor = new Color(0.90f, 0.76f, 0.42f, 1f);
        cb.disabledColor = AccentLine;
        cb.fadeDuration = 0.15f;
        btn.colors = cb;

        y -= bh + 22f;
        return btn;
    }

    // legacy Text엔 자간이 없어서 글자 사이에 공백을 끼워 넓은 자간처럼 보이게 한다 (사망 결과 화면과 같은 방식).
    private static string Track(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length * 2);
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append(s[i]);
            if (i < s.Length - 1) sb.Append(s[i] == ' ' ? "  " : " ");
        }
        return sb.ToString();
    }

    private static string PadTitle(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length * 2);
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append(s[i]);
            if (i < s.Length - 1 && s[i] != ' ') sb.Append(' ');
        }
        return sb.ToString();
    }
}
