using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 플레이어 사망 시 뜨는 결과 화면.
// PlayerHealth.OnDied(= PlayerMovement.Die() 바로 다음에 호출됨)를 받아, 사망 애니메이션(dead 상태)이
// 끝까지 재생될 때까지 기다렸다가 화면을 만들어 띄운다.
//
// 구성(위 -> 아래): "RUN ENDED" 라벨 -> "YOU DIE" 타이틀 -> 구분선 -> "DEPTH REACHED" 라벨 ->
//   도달 층수(예: "14층") -> 현재 위치 텍스트 -> "거점으로 돌아가기" 버튼 -> "PRESS ENTER TO CONTINUE" 힌트.
//
// 버튼 클릭 또는 Enter 키(New Input System)로 거점으로 복귀한다. 중복 입력/클릭으로 씬 전환이 여러 번
// 불리지 않도록 isReturning 가드를 둔다.
//
// 도달 층수는 StageProgress.CurrentStageNo, 위치는 StageManager.CurrentTheme 에서 가져온다.
// "거점 복귀"는 이 프로젝트에선 별도 씬 로드가 아니라 StageManager의 좌표 이동이다
// (RetreatManager / StageExitPortal 과 동일). 그래서 StageManager.ReturnToHub()를 쓰고,
// 만약 그게 불가능한 상황이면 현재 씬을 다시 로드하는 것으로 폴백한다.
//
// UI는 이 프로젝트의 다른 UI(RetreatManager, PlayerHealthUI 등)와 동일하게 코드로 직접 만든다.
// 폰트도 새로 추가하지 않고, 이 프로젝트가 한글 UI에 이미 쓰는 폰트(neodgm.ttf - StageNumberText,
// 무기강화 라벨과 동일)를 타이틀/내용용으로, VT323(영문 라벨용)을 라벨용으로 나눠서 쓴다.
// (TMP는 프로젝트에 한글 글리프를 가진 폰트 에셋이 없어서 legacy UI.Text로 만든다 - 기존 한글 UI 방식과 동일)
public class DeathResultUI : MonoBehaviour
{
    [Header("폰트 (프로젝트 기존 에셋 - 새로 추가하지 않음)")]
    [Tooltip("타이틀/내용용 (한글 지원) - YOU DIE / 층수 / 위치 / 버튼. 프로젝트 한글 UI와 동일한 neodgm 권장")]
    public Font titleFont;
    [Tooltip("영문 라벨용 - RUN ENDED / DEPTH REACHED / PRESS ENTER TO CONTINUE. VT323 권장")]
    public Font labelFont;

    [Header("연출")]
    public float fadeInDuration = 0.6f;
    [Tooltip("사망 애니메이션 감지에 실패했을 때 이 시간이 지나면 그냥 결과 화면을 띄운다")]
    public float animWaitTimeout = 3f;

    [Header("표시 텍스트")]
    public string runEndedLabel = "RUN ENDED";
    public string dieTitle = "YOU DIE";
    public string depthReachedLabel = "DEPTH REACHED";
    public string floorUnit = "층";
    public string returnButtonLabel = "거점으로 돌아가기";
    public string hint = "PRESS ENTER TO CONTINUE";

    // 레퍼런스 디자인 색
    static readonly Color BgColor      = new Color(0.039f, 0.031f, 0.027f, 1f); // #0a0807
    static readonly Color InkTitle     = new Color(0.941f, 0.843f, 0.788f, 1f); // #f0d7c9
    static readonly Color InkTitleDim  = new Color(0.788f, 0.671f, 0.612f, 1f); // #c9ab9c
    static readonly Color InkLabel     = new Color(0.549f, 0.498f, 0.463f, 1f); // #8c7f76
    static readonly Color InkLabelDim  = new Color(0.373f, 0.337f, 0.310f, 1f); // #5f564f
    static readonly Color AccentLine   = new Color(0.478f, 0.165f, 0.149f, 1f); // #7a2a26
    static readonly Color AccentBright = new Color(0.690f, 0.271f, 0.227f, 1f); // #b0453a
    static readonly Color DividerColor = new Color(0.290f, 0.110f, 0.110f, 1f); // #4a1c1c
    static readonly Color ButtonText   = new Color(0.925f, 0.886f, 0.855f, 1f); // #ece2da

    private PlayerHealth playerHealth;
    private Animator playerAnimator;

    private CanvasGroup group;
    private RectTransform root;
    private Text depthText;
    private Text locationText;
    private Text hintText;
    private Button returnButton;

    private bool shown;
    private bool isReturning;

    // 죽은 순간에 캡처 (이후 StageProgress가 초기화돼도 결과 화면엔 죽은 순간의 값이 남도록)
    private int capturedDepth = 1;
    private string capturedLocation = "";

    void Awake()
    {
        // Start()가 아니라 Awake()에서 구독한다. 같은 OnDied를 구독하는 StageExtraction은 Start()에서
        // 등록하므로, 여기서 먼저 등록해두면 층수 초기화가 일어나기 전에 값을 캡처할 수 있다.
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null) playerHealth.OnDied += HandleDeath;

        GameObject player = GameObject.Find("Player");
        if (player != null) playerAnimator = player.GetComponent<Animator>();
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandleDeath;
    }

    void HandleDeath()
    {
        capturedDepth = Mathf.Max(1, StageProgress.CurrentStageNo);
        capturedLocation = ResolveLocation();
        StartCoroutine(ShowAfterDeathAnim());
    }

    private static string ResolveLocation()
    {
        string theme = StageManager.CurrentTheme;
        if (string.IsNullOrEmpty(theme) || theme == "거점") return "미상 구역";
        return theme;
    }

    private IEnumerator ShowAfterDeathAnim()
    {
        isReturning = false; // 새 사망 - 복귀 가드 초기화

        // 사망 애니메이션(dead 상태)이 끝까지 재생될 때까지 기다린다.
        // Player.controller: AnyState -> dead (IsDead), Player_die 클립은 non-loop, 길이 약 0.75초.
        float elapsed = 0f;
        while (elapsed < animWaitTimeout)
        {
            if (playerAnimator == null || !playerAnimator.isActiveAndEnabled) break;

            AnimatorStateInfo st = playerAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("dead") && st.normalizedTime >= 0.98f) break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Build();
        RefreshValues();

        shown = true;
        group.gameObject.SetActive(true);
        group.blocksRaycasts = true;
        root.SetAsLastSibling();

        // 서서히 나타난다 (timeScale=0에서도 동작하도록 unscaled 사용).
        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }
        group.alpha = 1f;

        // 결과 화면이 뜬 뒤에는 게임을 완전히 멈춘다 (StageTimer 카운트다운 등도 함께 정지).
        Time.timeScale = 0f;

        // Enter 로 버튼이 눌리도록 버튼을 선택 상태로 만든다.
        if (EventSystem.current != null && returnButton != null)
            EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
    }

    void Update()
    {
        if (!shown || isReturning) return;

        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            ReturnToHub();
    }

    private void ReturnToHub()
    {
        if (isReturning) return; // 중복 클릭/입력 가드
        isReturning = true;

        if (hintText != null) hintText.text = Track("RETURNING…");

        Time.timeScale = 1f;

        if (group != null)
        {
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
        }
        shown = false;

        StageProgress.ResetToFirstStage();

        // 이 프로젝트의 거점 복귀 = StageManager의 좌표 이동 (별도 거점 씬 없음).
        if (Application.isPlaying)
            StageManager.ReturnToHub();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        // isReturning 은 여기서 풀지 않는다. Enter 키 입력과 버튼 submit이 같은 프레임에 겹쳐서
        // ReturnToHub()가 두 번 불릴 수 있는데, 여기서 풀면 두 번째 호출이 통과해버린다.
        // 다음 사망 시(ShowAfterDeathAnim 시작) 다시 false 로 초기화한다.
    }

    private void RefreshValues()
    {
        if (depthText != null)
        {
            // "14" 는 밝게, "층" 은 한 단계 어둡고 작게.
            depthText.text = $"{capturedDepth}<size=26><color=#8c7f76> {floorUnit}</color></size>";
        }
        if (locationText != null) locationText.text = capturedLocation;
        // 이전 사망에서 "RETURNING…" 으로 바뀌어 있을 수 있으니 매번 되돌린다.
        if (hintText != null) hintText.text = Track(hint);
    }

#if UNITY_EDITOR
    // 에디터에서 컴포넌트 우클릭 -> "결과 화면 미리보기" 로 실제 게임 없이 레이아웃을 확인한다.
    [ContextMenu("결과 화면 미리보기")]
    private void EditorPreview()
    {
        capturedDepth = 14;
        capturedLocation = "잿빛 회랑";
        Build();
        RefreshValues();
        if (root != null)
        {
            root.gameObject.SetActive(true);
            group.alpha = 1f;
            root.SetAsLastSibling();
        }
    }
#endif

    // ── UI 생성 ────────────────────────────────────────────────────────────

    private void Build()
    {
        if (root != null) return;

        Font tFont = titleFont != null ? titleFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Font lFont = labelFont != null ? labelFont : tFont;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = GameObject.Find("Canvas");
            canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : FindFirstObjectByType<Canvas>();
        }
        if (canvas == null)
        {
            Debug.LogWarning("DeathResultUI: Canvas를 찾지 못해 결과 화면을 만들 수 없음");
            return;
        }

        // 루트: 화면 전체를 덮는 불투명 배경 + CanvasGroup
        GameObject rootGo = new GameObject("DeathResultScreen",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        rootGo.transform.SetParent(canvas.transform, false);
        root = rootGo.GetComponent<RectTransform>();
        Stretch(root);
        rootGo.GetComponent<Image>().color = BgColor;
        group = rootGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        // 콘텐츠 컨테이너 (화면 중앙, 폭 고정)
        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(root, false);
        RectTransform crt = content.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(620f, 560f);
        crt.anchoredPosition = Vector2.zero;

        // 위에서 아래로 쌓는다. y = 요소 상단(pivot 0.5,1 기준), 콘텐츠 중앙 기준.
        // 아래 요소들의 (높이+간격) 합이 약 450 이라, 225 에서 시작하면 세로 중앙에 온다.
        float y = 225f;

        MakeText(content.transform, "Eyebrow", lFont, 15, InkLabelDim, Track(runEndedLabel), ref y, 22f, 24f);
        MakeText(content.transform, "Title", tFont, 74, InkTitle, dieTitle, ref y, 86f, 22f, addGlow: true, letterSpacePad: true);
        MakeDivider(content.transform, ref y);
        MakeText(content.transform, "StatLabel", lFont, 14, InkLabelDim, Track(depthReachedLabel), ref y, 18f, 12f);
        depthText = MakeText(content.transform, "Depth", tFont, 54, InkTitleDim, "", ref y, 60f, 8f);
        locationText = MakeText(content.transform, "Location", tFont, 20, InkLabel, "", ref y, 24f, 42f);
        returnButton = MakeButton(content.transform, "ReturnButton", tFont, returnButtonLabel, ref y);
        hintText = MakeText(content.transform, "Hint", lFont, 14, InkLabelDim, Track(hint), ref y, 18f, 0f);

        returnButton.onClick.AddListener(ReturnToHub);

        rootGo.SetActive(false);
    }

    private Text MakeText(Transform parent, string name, Font font, int size, Color color, string text,
        ref float y, float height, float gapAfter, bool addGlow = false, bool letterSpacePad = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        // 콘텐츠 컨테이너 중앙을 기준으로 y(= 요소 상단)를 잡는다.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(620f, height);
        rt.anchoredPosition = new Vector2(0f, y);

        Text t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.text = letterSpacePad ? PadTitle(text) : text;

        if (addGlow)
        {
            Shadow glow = go.AddComponent<Shadow>();
            glow.effectColor = new Color(AccentBright.r, AccentBright.g, AccentBright.b, 0.5f);
            glow.effectDistance = new Vector2(0f, -2f);
        }

        y -= height + gapAfter;
        return t;
    }

    private void MakeDivider(Transform parent, ref float y)
    {
        const float w = 420f, lineW = 190f;

        GameObject go = new GameObject("Divider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, 16f);
        rt.anchoredPosition = new Vector2(0f, y);

        MakeBar(go.transform, "LineL", new Vector2(-(w / 2f - lineW / 2f), -8f), new Vector2(lineW, 1f), DividerColor);
        MakeBar(go.transform, "LineR", new Vector2(w / 2f - lineW / 2f, -8f), new Vector2(lineW, 1f), DividerColor);

        GameObject mark = MakeBar(go.transform, "Mark", new Vector2(0f, -8f), new Vector2(6f, 6f), Color.clear);
        Image mimg = mark.GetComponent<Image>();
        mimg.color = new Color(AccentBright.r, AccentBright.g, AccentBright.b, 0.85f);
        mark.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 45f);

        y -= 16f + 26f;
    }

    private GameObject MakeBar(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }

    private Button MakeButton(Transform parent, string name, Font font, string label, ref float y)
    {
        const float bw = 400f, bh = 50f;

        // 테두리(바깥) + 안쪽 배경으로 1~2px 테두리 박스를 만든다.
        GameObject border = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        border.transform.SetParent(parent, false);
        RectTransform brt = border.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(bw, bh);
        brt.anchoredPosition = new Vector2(0f, y);

        Image borderImg = border.GetComponent<Image>();
        borderImg.color = Color.white; // 실제 색은 Button ColorBlock 에서 (호버 시 밝은 테두리)

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(border.transform, false);
        RectTransform frt = fill.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(2f, 2f);
        frt.offsetMax = new Vector2(-2f, -2f);
        Image fillImg = fill.GetComponent<Image>();
        fillImg.color = new Color(0.055f, 0.043f, 0.04f, 1f);
        fillImg.raycastTarget = false;

        GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGo.transform.SetParent(border.transform, false);
        RectTransform trt = txtGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        Text txt = txtGo.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = 18;
        txt.color = ButtonText;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        txt.text = label;

        Button btn = border.GetComponent<Button>();
        btn.targetGraphic = borderImg;
        ColorBlock cb = btn.colors;
        cb.colorMultiplier = 1f;
        cb.normalColor = AccentLine;                                    // #7a2a26
        cb.highlightedColor = AccentBright;                             // #b0453a
        cb.selectedColor = AccentBright;
        cb.pressedColor = new Color(0.82f, 0.36f, 0.31f, 1f);
        cb.disabledColor = AccentLine;
        cb.fadeDuration = 0.15f;
        btn.colors = cb;

        y -= bh + 22f;
        return btn;
    }

    // 소문자/대문자 라벨을 넓은 자간처럼 보이게 글자 사이에 공백을 끼워 넣는다 (legacy Text는 자간이 없음).
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

    // 타이틀은 살짝만 벌린다.
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
