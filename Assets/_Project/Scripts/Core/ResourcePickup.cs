using System.Collections;
using UnityEngine;

// 적 처치 시 드랍되는 자원 아이템. 죽은 자리에서 무작위 방향으로 살짝 튀어나가 바닥에 떨어지는
// 연출을 보여준 뒤, 플레이어가 가까이 오면 끌려와서 자동으로 흡수된다.
// (Assets/GoldItem.cs 등 이전 PickupItem 패턴과 동일한 흡입/획득 로직, 이 프로젝트엔 자원 하나뿐이라 단일 클래스로 둠)
public class ResourcePickup : MonoBehaviour
{
    public ResourceType resourceType;
    public int amount = 1;

    public float attractRadius = 1.5f;
    public float moveSpeed = 6f;
    public float pickupRadius = 0.2f;

    [Header("드랍 연출 (몹에게서 튀어나와 바닥에 떨어지는 모션)")]
    public float popDuration = 0.35f;     // 튀어나왔다가 착지하기까지 걸리는 시간
    public float popArcHeight = 0.5f;     // 튀어오르는 높이(포물선 정점)
    public float popMinDistance = 0.25f;  // 착지 지점까지의 최소 거리
    public float popMaxDistance = 0.7f;   // 착지 지점까지의 최대 거리

    private Transform player;
    private bool isAttracting;
    private bool isPopping;

    // 실제 아이콘이 없는 자원 종류를 위한 단색 임시 스프라이트 (즉석 생성 후 캐싱).
    private static readonly Color[] FallbackColors =
    {
        new Color(0.55f, 0.35f, 0.15f), // Wood
        new Color(0.65f, 0.65f, 0.7f),  // Iron
        new Color(0.85f, 0.5f, 0.2f),   // Copper
        new Color(0.3f, 0.9f, 0.3f),    // Chemical
        new Color(0.15f, 0.15f, 0.15f), // Oil
    };
    private static readonly Sprite[] fallbackSprites = new Sprite[5];

    // 자원 타입별 실제 아이콘 세트. Resources 폴더에서 한 번만 불러와 캐싱한다.
    private static ResourceIconSet iconSet;
    private static bool iconSetLoaded;

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (isPopping || player == null) return; // 착지 연출 중에는 흡입 로직을 건드리지 않는다

        // 플레이어가 죽으면 흡입/획득을 즉시 멈춘다. 안 그러면 사망 처리(runHeld 초기화) 이후에도
        // 근처 자원을 마저 주워버려서 초기화된 runHeld에 자원이 도로 남는 문제가 생긴다.
        if (EnemyManager.PlayerDead) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (!isAttracting && dist <= attractRadius)
            isAttracting = true;

        if (isAttracting)
        {
            float speed = moveSpeed + (attractRadius - dist) * 4f;
            transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.deltaTime);

            if (dist <= pickupRadius)
            {
                ResourceBank.AddRunResource(resourceType, amount);
                Destroy(gameObject);
            }
        }
    }

    /// <summary>지정 위치에 랜덤 자원 타입 드랍 아이템을 스폰한다. Enemy.Die() 등에서 호출한다.</summary>
    public static void SpawnRandomDrop(Vector3 position)
    {
        ResourceType type = (ResourceType)Random.Range(0, 5);

        GameObject go = new GameObject($"{type}Pickup");
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetIconSprite(type);
        sr.sortingOrder = 0;

        float scale = GetIconScale(type);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        ResourcePickup pickup = go.AddComponent<ResourcePickup>();
        pickup.resourceType = type;
        pickup.StartPop(position);
    }

    // 죽은 자리(originPos)에서 무작위 방향으로 살짝 떨어진 착지 지점까지 포물선을 그리며 튀어나간다.
    private void StartPop(Vector3 originPos)
    {
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float distance = Random.Range(popMinDistance, popMaxDistance);
        Vector3 landPos = originPos + (Vector3)(randomDir * distance);

        StartCoroutine(PopRoutine(originPos, landPos));
    }

    private IEnumerator PopRoutine(Vector3 startPos, Vector3 landPos)
    {
        isPopping = true;
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);

            Vector3 flatPos = Vector3.Lerp(startPos, landPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * popArcHeight; // 튀어올랐다가 다시 내려오는 포물선
            transform.position = flatPos + new Vector3(0f, arc, 0f);

            yield return null;
        }

        transform.position = landPos;
        isPopping = false;
    }

    // UI 등 다른 곳에서도 월드 드랍과 완전히 같은 아이콘을 쓸 수 있도록 공개해둔다.
    public static Sprite GetIconSprite(ResourceType type)
    {
        EnsureIconSetLoaded();

        int idx = (int)type;
        if (iconSet != null && idx < iconSet.icons.Length && iconSet.icons[idx] != null)
            return iconSet.icons[idx];

        return GetFallbackSprite(type);
    }

    // 실제 아이콘이 지정된 자원만 iconSet의 배율을 쓰고, 단색 임시 스프라이트는 항상 원래 크기(1배)로 둔다.
    private static float GetIconScale(ResourceType type)
    {
        EnsureIconSetLoaded();

        int idx = (int)type;
        if (iconSet != null && idx < iconSet.icons.Length && iconSet.icons[idx] != null
            && idx < iconSet.scales.Length)
            return iconSet.scales[idx];

        return 1f;
    }

    private static void EnsureIconSetLoaded()
    {
        if (iconSetLoaded) return;
        iconSet = Resources.Load<ResourceIconSet>("ResourceIconSet");
        iconSetLoaded = true;
    }

    private static Sprite GetFallbackSprite(ResourceType type)
    {
        int idx = (int)type;
        if (fallbackSprites[idx] != null) return fallbackSprites[idx];

        Texture2D tex = new Texture2D(8, 8);
        Color[] pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = FallbackColors[idx];
        tex.SetPixels(pixels);
        tex.Apply();

        fallbackSprites[idx] = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 16f);
        return fallbackSprites[idx];
    }
}
