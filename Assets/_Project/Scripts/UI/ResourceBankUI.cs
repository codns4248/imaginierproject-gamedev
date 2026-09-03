using UnityEngine;
using UnityEngine.UI;

// 자원 개수를 화면 오른쪽 위에 자원별 아이콘 + 개수로 세로로 표시한다 (나무/철/구리/화학물질/기름 순서).
// 거점(StageManager.IsInStage == false)에서는 확정 보관된 stash를, 스테이지 도중에는
// 이번 런 파밍분(runHeld)을 보여준다. 예전엔 거점/스테이지가 다른 씬이라 인스턴스를 따로 뒀지만,
// 이제 한 씬 안에서 좌표만 이동하므로 이 인스턴스 하나가 상황에 따라 자동으로 바뀐다.
public class ResourceBankUI : MonoBehaviour
{
    [Header("배치")]
    public float iconSize = 28f;
    public float rowSpacing = 32f; // 세로 한 줄 높이
    public Font font;

    private static readonly ResourceType[] Order =
    {
        ResourceType.Wood, ResourceType.Iron, ResourceType.Copper, ResourceType.Chemical, ResourceType.Oil
    };

    private Text[] countTexts;
    private bool lastShowStash;

    void Awake()
    {
        BuildRows();
    }

    void OnEnable()
    {
        ResourceBank.OnChanged += Refresh;
        lastShowStash = !StageManager.IsInStage;
        Refresh();
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= Refresh;
    }

    // 거점<->스테이지 이동은 자원 개수 변화가 없어 ResourceBank.OnChanged가 안 터지므로,
    // 표시 모드(stash/runHeld)가 바뀌었는지는 매 프레임 직접 확인해서 갱신한다.
    void Update()
    {
        bool showStash = !StageManager.IsInStage;
        if (showStash != lastShowStash)
        {
            lastShowStash = showStash;
            Refresh();
        }
    }

    // 자원 종류마다 [아이콘][xN] 한 줄씩, 위에서 아래로 나무→철→구리→화학물질→기름 순서로 만든다.
    private void BuildRows()
    {
        countTexts = new Text[Order.Length];

        for (int i = 0; i < Order.Length; i++)
        {
            ResourceType type = Order[i];

            GameObject row = new GameObject(type + "Row", typeof(RectTransform));
            row.transform.SetParent(transform, false);
            RectTransform rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(0f, 1f);
            rowRT.pivot = new Vector2(0f, 1f);
            rowRT.sizeDelta = new Vector2(120f, iconSize);
            rowRT.anchoredPosition = new Vector2(0f, -i * rowSpacing);

            GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(row.transform, false);
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 1f);
            iconRT.anchorMax = new Vector2(0f, 1f);
            iconRT.pivot = new Vector2(0f, 1f);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            iconRT.anchoredPosition = Vector2.zero;
            Image icon = iconGO.AddComponent<Image>();
            icon.sprite = ResourcePickup.GetIconSprite(type);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            GameObject textGO = new GameObject("CountText", typeof(RectTransform));
            textGO.transform.SetParent(row.transform, false);
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 1f);
            textRT.anchorMax = new Vector2(0f, 1f);
            textRT.pivot = new Vector2(0f, 1f);
            textRT.sizeDelta = new Vector2(80f, iconSize);
            textRT.anchoredPosition = new Vector2(iconSize + 4f, -2f);
            Text text = textGO.AddComponent<Text>();
            text.font = font;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            countTexts[i] = text;
        }
    }

    private void Refresh()
    {
        if (countTexts == null) return;

        bool showStash = !StageManager.IsInStage;
        System.Func<ResourceType, int> get = showStash ? ResourceBank.GetStash : (System.Func<ResourceType, int>)ResourceBank.GetRunHeld;
        for (int i = 0; i < Order.Length; i++)
        {
            countTexts[i].text = "x" + get(Order[i]);
        }
    }
}
