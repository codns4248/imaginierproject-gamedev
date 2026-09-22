using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 스테이지에 기믹이 뜨면(StageGimmickManager.OnGimmickStart) 화면 중앙에 그 기믹의 이름을
// 잠깐 띄웠다가 페이드아웃시켜서, 이번 스테이지에 어떤 기믹이 걸렸는지 플레이어가 바로 알 수 있게 한다.
public class GimmickAnnounceUI : MonoBehaviour
{
    public float holdDuration = 0.6f;  // 다 보이는 상태로 유지되는 시간
    public float fadeDuration = 1.0f;  // 그 뒤 투명해지는 데 걸리는 시간
    public int fontSize = 64;

    private static readonly Dictionary<GimmickType, string> DisplayNames = new Dictionary<GimmickType, string>
    {
        { GimmickType.Fog, "안개" },
        { GimmickType.HeavyRain, "호우" },
        { GimmickType.Typhoon, "태풍" },
        { GimmickType.Lightning, "벼락" },
        { GimmickType.ColdWave, "한파" },
    };

    private Text label;
    private Outline outline;
    private Coroutine routine;

    void Awake()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) { enabled = false; return; }

        GameObject go = new GameObject("GimmickAnnounceText");
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling(); // 다른 UI 위에 그려지도록 맨 위로

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(800f, 200f);

        label = go.AddComponent<Text>();
        label.font = FindExistingFont(canvas); // 기존 HUD 텍스트와 같은 폰트를 재사용 (한글 깨짐 방지)
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.text = "";
        label.raycastTarget = false;

        outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2f, -2f);

        SetAlpha(0f);

        StageGimmickManager.OnGimmickStart += HandleStart;
    }

    void OnDestroy()
    {
        StageGimmickManager.OnGimmickStart -= HandleStart;
    }

    // 씬에 이미 있는 HUD 텍스트(StageNumberText 등)의 폰트를 그대로 재사용해서, 별도 폰트 로딩 없이
    // 한글이 깨지지 않게 한다.
    private static Font FindExistingFont(GameObject canvas)
    {
        Text[] texts = canvas.GetComponentsInChildren<Text>(true);
        foreach (Text t in texts)
        {
            if (t.font != null) return t.font;
        }
        return null;
    }

    private void HandleStart(GimmickType type)
    {
        if (label == null || !DisplayNames.TryGetValue(type, out string name)) return;

        label.text = name;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowAndFade());
    }

    private IEnumerator ShowAndFade()
    {
        SetAlpha(1f);
        yield return new WaitForSeconds(holdDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        if (label == null) return;

        Color c = label.color;
        c.a = alpha;
        label.color = c;

        if (outline != null)
        {
            Color oc = outline.effectColor;
            oc.a = alpha;
            outline.effectColor = oc;
        }
    }
}
