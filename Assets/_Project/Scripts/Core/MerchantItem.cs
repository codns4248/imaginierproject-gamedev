using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 상인이 파는 상품 하나. 플레이어가 interactRadius 안으로 들어오면 흰색 테두리(Enemy.cs의 피격
// 테두리와 같은 방식: 스프라이트를 4방향으로 살짝 띄워 전용 머티리얼로 흰색 실루엣을 겹쳐 그림)와
// 상인 옆 가격표 말풍선이 뜨고, F키를 누르면 지정된 자원(1~2종류)을 전부 가지고 있을 때만 onPurchase를
// 실행한다. onPurchase는 실제로 지급했는지를 bool로 돌려준다 - 무기 슬롯이 꽉 찬 경우처럼 지급이
// 실패하면 자원을 쓰지 않고 상품도 그대로 남는다(예전엔 지급 성공 여부와 무관하게 항상 자원을
// 먼저 쓰고 상품을 지워서, 슬롯이 꽉 찼을 때 자원만 날리고 아무것도 못 받는 문제가 있었다).
public class MerchantItem : MonoBehaviour
{
    // 카펫 위 상품 간격보다 충분히 좁게 잡아서, 두 상품 사이에서 F를 눌렀을 때
    // 둘 다 한꺼번에 구매되는 일이 없게 한다.
    public float interactRadius = 0.3f;

    // true면 이번 런 파밍분(runHeld) 대신 거점 영구 보관 자원(stash)을 소모한다.
    // 스테이지 상인은 기본값(false, runHeld)을 그대로 쓰고, 거점 제작대(HubCrafting)만 켠다.
    public bool useStash;

    private List<ResourceType> costTypes;
    private List<int> costAmounts;
    private System.Func<bool> onPurchase; // 실제로 지급했으면 true. false면 자원을 쓰지 않는다.
    private Transform player;
    private SpriteRenderer[] outlineRenderers;
    private Material outlineMat;
    private bool purchased;

    private GameObject priceBubbleRoot;
    private Text priceText;
    private string priceLabel;

    // targetWorldSize: 아이콘 원본 해상도가 서로 달라도(회복약/무기 실제 아이콘 vs 희귀자원 임시
    // 아이콘) 화면에 보이는 크기가 맞도록, 고정 배율 대신 "가로/세로 중 큰 쪽 기준 목표 크기"로
    // 스케일을 역산한다.
    public void Init(Sprite sprite, float targetWorldSize, List<ResourceType> costTypes, List<int> costAmounts,
        Material outlineMaterial, System.Func<bool> onPurchase,
        GameObject priceBubbleRoot, Text priceText, string priceLabel)
    {
        this.costTypes = costTypes;
        this.costAmounts = costAmounts;
        this.onPurchase = onPurchase;
        this.priceBubbleRoot = priceBubbleRoot;
        this.priceText = priceText;
        this.priceLabel = priceLabel;

        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        float nativeSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float autoScale = nativeSize > 0.0001f ? targetWorldSize / nativeSize : 1f;
        transform.localScale = Vector3.one * autoScale;

        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;

        CreateOutline(outlineMaterial, sr);
    }

    private void CreateOutline(Material outlineMaterial, SpriteRenderer sr)
    {
        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        outlineRenderers = new SpriteRenderer[offsets.Length];

        if (outlineMaterial != null) outlineMat = new Material(outlineMaterial);

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject go = new GameObject("Outline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * 0.06f);

            SpriteRenderer outlineSr = go.AddComponent<SpriteRenderer>();
            outlineSr.sprite = sr.sprite;
            if (outlineMat != null) outlineSr.sharedMaterial = outlineMat;
            outlineSr.sortingOrder = sr.sortingOrder - 1;
            outlineSr.enabled = false;

            outlineRenderers[i] = outlineSr;
        }
        if (outlineMat != null) outlineMat.color = Color.white;
    }

    void Update()
    {
        if (purchased || player == null) return;

        bool inRange = Vector2.Distance(transform.position, player.position) <= interactRadius;
        for (int i = 0; i < outlineRenderers.Length; i++) outlineRenderers[i].enabled = inRange;

        UpdatePriceBubble(inRange);

        // 근처에 바닥 무기 등 다른 F 상호작용이 같이 있어도 한 프레임에 하나만 반응하게 한다
        // (InteractInput 참고) - 안 그러면 상인 무기를 살 때 바닥 무기까지 같이 주워져서
        // "하나 샀는데 두 개 얻는" 중복이 생긴다.
        // 가격표 말풍선이 "이 상품" 가격을 띄우고 있을 때만 구매를 허용한다 - inRange만으로는
        // 충분하지 않을 수 있는 경우(말풍선을 못 만들었거나 다른 상품 표시 중)를 명시적으로 막는다.
        bool bubbleShowingThisItem = priceBubbleRoot != null && priceBubbleRoot.activeSelf
            && priceText != null && priceText.text == priceLabel;

        if (inRange && bubbleShowingThisItem && Keyboard.current.fKey.wasPressedThisFrame && InteractInput.TryConsumeFKey())
            TryPurchase();
    }

    private void UpdatePriceBubble(bool inRange)
    {
        if (priceBubbleRoot == null) return;

        if (inRange)
        {
            priceBubbleRoot.SetActive(true);
            if (priceText != null) priceText.text = priceLabel;
        }
        // 내가 띄워둔 가격표일 때만 끈다 (동시에 여러 상품 범위에 걸치는 경우를 대비한 안전장치).
        else if (priceBubbleRoot.activeSelf && priceText != null && priceText.text == priceLabel)
        {
            priceBubbleRoot.SetActive(false);
        }
    }

    private void TryPurchase()
    {
        for (int i = 0; i < costTypes.Count; i++)
        {
            if (GetHeld(costTypes[i]) < costAmounts[i]) return; // 하나라도 부족하면 구매 취소
        }

        // 자원을 쓰기 전에 실제로 지급이 되는지 먼저 확인한다 (예: 무기 슬롯이 꽉 찬 경우).
        // 실패하면 자원도 안 쓰고 상품도 그대로 남겨서, 나중에 슬롯을 비우고 다시 살 수 있게 한다.
        if (onPurchase == null || !onPurchase()) return;

        for (int i = 0; i < costTypes.Count; i++)
        {
            TrySpend(costTypes[i], costAmounts[i]);
        }

        purchased = true;
        if (priceBubbleRoot != null) priceBubbleRoot.SetActive(false);
        Destroy(gameObject);
    }

    private int GetHeld(ResourceType type) => useStash ? ResourceBank.GetStash(type) : ResourceBank.GetRunHeld(type);
    private bool TrySpend(ResourceType type, int amount) => useStash ? ResourceBank.TrySpendStash(type, amount) : ResourceBank.TrySpendRunResource(type, amount);

    void OnDestroy()
    {
        if (outlineMat != null) Destroy(outlineMat);
    }

    // ── 상인/제작대가 공유하는 UI 헬퍼 ──────────────────────────────────────
    // 원래 StageExtraction 안에 있었는데, 거점 제작대(HubCrafting)도 똑같은 가격표/라벨이
    // 필요해져서 이쪽(공용 상품 컴포넌트)으로 옮겼다.

    public static string ResourceKoreanName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "나무";
            case ResourceType.Iron: return "철";
            case ResourceType.Copper: return "구리";
            case ResourceType.Chemical: return "화학물질";
            case ResourceType.Oil: return "기름";
            case ResourceType.Rare: return "희귀자원";
            default: return type.ToString();
        }
    }

    public static string BuildPriceLabel(List<ResourceType> types, List<int> amounts)
    {
        string[] parts = new string[types.Count];
        for (int i = 0; i < types.Count; i++) parts[i] = ResourceKoreanName(types[i]) + " x" + amounts[i];
        return string.Join(" + ", parts);
    }

    // 상품 옆에 뜨는 가격표 말풍선을 만든다. TextMesh는 URP 폰트 셰이더와 호환 문제가 있어서
    // (DamageNumber.cs 참고) 이 프로젝트 관례대로 World Space Canvas + UI.Text로 만든다.
    public static GameObject CreatePriceBubble(Transform parent, Vector3 position)
    {
        GameObject root = new GameObject("가격표", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // 자원 2종류 섞인 가격("화학물질 x8 + 나무 x8" 등)까지 한 줄로 다 들어가도록 충분히 넓게 잡는다.
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(340f, 60f);

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.8f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(root.transform, false);
        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 6f);
        textRt.offsetMax = new Vector2(-10f, -6f);
        Text text = textGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        // 줄바꿈(Wrap) + 세로 Truncate 기본값 조합 때문에 두 번째 줄("+" 뒤)이 통째로 잘려 보이지
        // 않던 문제가 있었다. 한 줄로 넘치더라도 절대 잘리지 않도록 가로/세로 다 Overflow로 둔다.
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return root;
    }
}
