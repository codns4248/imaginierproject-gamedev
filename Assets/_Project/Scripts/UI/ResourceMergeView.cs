using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 결과 화면(탈출 ExtractionResultUI / 사망 DeathResultUI) 공용 "자원 결산" 연출.
// 이번 탐험에서 가지고 있던 자원이 있는 종류마다 한 칸:  (아이콘)  거점 창고 수   +탐험 자원 수   -잃은 수
// Play()를 돌리면 "+탐험 자원 수"가 디리리리 소리를 내며 한 개씩 줄어들고, 줄어든 만큼이 규칙대로 창고 수나 잃은 수로 들어간다.
//   1단계 (먼저 잃는 몫, lostFirst): 사망 보험이 지키지 못한 몫 -> 잃은 수가 오른다 (낮아지는 소리)
//   2단계 (창고로 가는 몫, toStash): 탈출 / 사망 보험이 지킨 몫 -> 창고 수가 오른다 (높아지는 소리)
//        그러다 창고 최대치에 닿으면 (lostAfter) -> 창고 수는 주황색으로 멈추고 나머지는 다시 잃은 수로
// 글자 설명 대신 숫자가 오가는 것만으로 "얼마나 지키고 얼마나 잃었는지"를 보여 주는 게 목적이다 (2026-11-18 요청).
// Finish()는 연출을 건너뛰고 바로 끝 상태로 만든다. 2열로 늘어놓는다 (하나뿐이면 가운데, 홀수면 마지막 칸 가운데).
public class ResourceMergeView
{
    private const float CellW = 300f, CellH = 38f, IconSize = 30f;
    private const float StartDelay = 0.2f;
    private const float PhaseGap = 0.15f;
    private const float TickInterval = 0.05f;

    private static readonly ResourceType[] Order =
        { ResourceType.Wood, ResourceType.Iron, ResourceType.Copper, ResourceType.Chemical, ResourceType.Oil, ResourceType.Rare };
    private static readonly Color CapColor = new Color(1f, 0.62f, 0.25f, 1f); // 창고 최대치에 닿은 창고 수

    private class Cell
    {
        public int before, lostFirst, toStash, lostAfter;
        public int Held => lostFirst + toStash + lostAfter;
        public int stashAdd, lostAdd; // 지금까지 옮겨진 양 (연출 진행)
        public Text stashText, runText, lostText;
    }

    private readonly Cell[] cells;
    private readonly Color numberColor, runColor, lostColor;

    public float Height { get; }
    public bool HasAny => cells.Length > 0;
    public bool Done { get; private set; }

    public static int RowCount(int[] lostFirst, int[] toStash, int[] lostAfter)
    {
        int count = 0;
        for (int i = 0; i < Order.Length; i++) if (lostFirst[i] + toStash[i] + lostAfter[i] > 0) count++;
        return count;
    }

    public static float HeightFor(int rowCount) => (rowCount + 1) / 2 * CellH;

    /// <summary>parent(가운데 기준) 안 y(윗변)에 결산 칸들을 만든다. 배열은 모두 ResourceType 순서.
    /// stashBefore = 결산 전 거점 창고 수, lostFirst = 먼저 잃는 몫, toStash = 창고로 들어가는 몫, lostAfter = 창고가 가득 차서 잃는 몫.</summary>
    public ResourceMergeView(RectTransform parent, float y, Font font, int[] stashBefore, int[] lostFirst, int[] toStash, int[] lostAfter,
        Color numberColor, Color runColor, Color lostColor)
    {
        this.numberColor = numberColor;
        this.runColor = runColor;
        this.lostColor = lostColor;

        int count = RowCount(lostFirst, toStash, lostAfter);
        cells = new Cell[count];
        Height = HeightFor(count);
        if (count == 0) { Done = true; return; }

        // 결과 화면 콘텐츠는 가운데 기준 좌표라 y(윗변)도 가운데 기준
        RectTransform root = NewRect("ResourceMerge", parent, new Vector2(CellW * 2f, Height), new Vector2(0f, y), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f));
        ResourceIconSet icons = Resources.Load<ResourceIconSet>("ResourceIconSet");

        int n = 0;
        foreach (ResourceType type in Order)
        {
            int i = (int)type;
            if (lostFirst[i] + toStash[i] + lostAfter[i] <= 0) continue;

            int row = n / 2;
            bool alone = n % 2 == 0 && n == count - 1; // 한 줄에 혼자 남은 칸은 가운데
            float x = alone ? 0f : (n % 2 == 0 ? -CellW / 2f : CellW / 2f);
            RectTransform cellRt = NewRect(type.ToString(), root, new Vector2(CellW, CellH), new Vector2(x, -row * CellH), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            Sprite icon = icons != null && icons.icons != null && i < icons.icons.Length ? icons.icons[i] : null;
            if (icon != null)
            {
                RectTransform iconRt = NewRect("Icon", cellRt, new Vector2(IconSize, IconSize), new Vector2(-118f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                Image img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else
            {
                MakeText(cellRt, "Name", font, 14, numberColor, ResourceNames.Korean(type), TextAnchor.MiddleCenter, -140f, -96f);
            }

            cells[n] = new Cell
            {
                before = stashBefore[i],
                lostFirst = lostFirst[i],
                toStash = toStash[i],
                lostAfter = lostAfter[i],
                stashText = MakeText(cellRt, "Stash", font, 24, numberColor, "", TextAnchor.MiddleRight, -96f, -26f),
                runText = MakeText(cellRt, "Run", font, 22, runColor, "", TextAnchor.MiddleLeft, -14f, 56f),
                lostText = MakeText(cellRt, "Lost", font, 20, lostColor, "", TextAnchor.MiddleLeft, 62f, 140f),
            };
            n++;
        }
        Refresh();
    }

    /// <summary>결산 연출. 결과 화면이 다 나타난 뒤 owner.StartCoroutine으로 돌린다 (게임이 멈춘 상태라 실제 시간 기준).</summary>
    public IEnumerator Play()
    {
        if (Done) yield break;

        yield return WaitRoutine(StartDelay);
        if (Done) yield break;

        // 1단계: 먼저 잃는 몫 (사망 보험이 지키지 못한 만큼)
        int maxA = 0;
        foreach (Cell c in cells) maxA = Mathf.Max(maxA, c.lostFirst);
        if (maxA > 0)
        {
            yield return RunPhase(maxA, firstPhase: true);
            if (Done) yield break;
            yield return WaitRoutine(PhaseGap);
            if (Done) yield break;
        }

        // 2단계: 창고로 가는 몫 (창고 최대치를 넘는 만큼은 잃은 수로)
        int maxB = 0;
        foreach (Cell c in cells) maxB = Mathf.Max(maxB, c.toStash + c.lostAfter);
        if (maxB > 0) yield return RunPhase(maxB, firstPhase: false);
        if (Done) yield break;

        Finish();

        // 바뀐 숫자가 한 번 통통 튄다
        float pop = 0f;
        while (pop < 0.18f)
        {
            pop += Time.unscaledDeltaTime;
            float s = 1f + 0.25f * Mathf.Sin(Mathf.Clamp01(pop / 0.18f) * Mathf.PI);
            foreach (Cell c in cells)
            {
                if (c.stashAdd > 0) c.stashText.rectTransform.localScale = new Vector3(s, s, 1f);
                if (c.lostAdd > 0) c.lostText.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }
        foreach (Cell c in cells)
        {
            c.stashText.rectTransform.localScale = Vector3.one;
            c.lostText.rectTransform.localScale = Vector3.one;
        }
    }

    // 한 단계: 칸마다 자기 몫을 같은 시간 동안 한 개씩 옮긴다. 소리는 이번 틱에 창고가 늘었으면 높아지는 틱, 잃은 것만 늘었으면 낮아지는 틱.
    private IEnumerator RunPhase(int maxUnits, bool firstPhase)
    {
        float duration = Mathf.Clamp(0.35f + maxUnits * 0.025f, 0.5f, 1.4f);
        float t = 0f;
        int lastTick = -1;
        int tickStash = TotalStash(), tickLost = TotalLost();

        while (t < duration)
        {
            if (Done) yield break; // Finish()로 건너뜀
            if (!PauseManager.IsEscPaused) t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            SetPhaseProgress(firstPhase, p);

            int tick = Mathf.FloorToInt(t / TickInterval);
            if (tick != lastTick)
            {
                lastTick = tick;
                int s = TotalStash(), l = TotalLost();
                if (s > tickStash) Sfx.PlayAtPitch(SfxId.CountTick, 1f + 0.8f * p, 0.8f);      // 창고로: 디리리리 (올라감)
                else if (l > tickLost) Sfx.PlayAtPitch(SfxId.CountTick, 0.8f - 0.2f * p, 0.7f); // 잃음: 낮아지는 틱
                tickStash = s;
                tickLost = l;
            }
            yield return null;
        }
        SetPhaseProgress(firstPhase, 1f);
    }

    private void SetPhaseProgress(bool firstPhase, float p)
    {
        foreach (Cell c in cells)
        {
            if (firstPhase)
            {
                c.lostAdd = Mathf.RoundToInt(c.lostFirst * p);
            }
            else
            {
                int moved = Mathf.RoundToInt((c.toStash + c.lostAfter) * p);
                c.stashAdd = Mathf.Min(moved, c.toStash);
                c.lostAdd = c.lostFirst + Mathf.Max(0, moved - c.toStash);
            }
        }
        Refresh();
    }

    /// <summary>연출을 건너뛰고 다 옮겨진 상태로 만든다 (Enter/버튼을 연출 중에 누르면).</summary>
    public void Finish()
    {
        if (Done) return;
        Done = true;
        bool anyStash = false, anyLost = false;
        foreach (Cell c in cells)
        {
            c.stashAdd = c.toStash;
            c.lostAdd = c.lostFirst + c.lostAfter;
            anyStash |= c.stashAdd > 0;
            anyLost |= c.lostAdd > 0;
        }
        Refresh();
        if (anyStash) Sfx.Play(SfxId.CountDone, 1f, 0f);      // 링
        else if (anyLost) Sfx.Play(SfxId.Deny, 0.6f, 0f);     // 전부 잃음
    }

    private void Refresh()
    {
        foreach (Cell c in cells)
        {
            int run = c.Held - c.stashAdd - c.lostAdd;
            c.stashText.text = (c.before + c.stashAdd).ToString();
            c.runText.text = $"+{run}";
            c.lostText.text = $"-{c.lostAdd}";

            // 창고 최대치에 닿아 넘치기 시작하면 창고 수를 주황색으로
            bool capped = c.lostAfter > 0 && c.stashAdd >= c.toStash && c.lostAdd > c.lostFirst;
            c.stashText.color = capped ? CapColor : numberColor;
            c.runText.color = run > 0 ? runColor : Dim(runColor);
            c.lostText.color = c.lostAdd > 0 ? lostColor : Dim(lostColor);
        }
    }

    private int TotalStash() { int s = 0; foreach (Cell c in cells) s += c.stashAdd; return s; }
    private int TotalLost() { int s = 0; foreach (Cell c in cells) s += c.lostAdd; return s; }

    private static Color Dim(Color c) => new Color(c.r, c.g, c.b, 0.25f);

    // ESC 설정 창이 떠 있는 동안은 멈추는 실제 시간 대기. Finish()되면 중간에 끝난다.
    private IEnumerator WaitRoutine(float seconds)
    {
        float w = 0f;
        while (w < seconds && !Done)
        {
            if (!PauseManager.IsEscPaused) w += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos, Vector2 pivot, Vector2 anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return rt;
    }

    // 칸(cell) 안 가로 구간 [left, right] (칸 가운데 기준)에 세로 가운데 정렬 글자
    private static Text MakeText(RectTransform cell, string name, Font font, int size, Color color, string text, TextAnchor align, float left, float right)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(cell, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(right - left, CellH);
        rt.anchoredPosition = new Vector2((left + right) / 2f, 0f);

        Text t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }
}
