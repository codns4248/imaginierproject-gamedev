using UnityEngine;

// 적 처치 시 드랍되는 자원 아이템. 플레이어가 가까이 오면 끌려와서 자동으로 흡수된다.
// (Assets/GoldItem.cs 등 이전 PickupItem 패턴과 동일한 흡입/획득 로직, 이 프로젝트엔 자원 하나뿐이라 단일 클래스로 둠)
public class ResourcePickup : MonoBehaviour
{
    public ResourceType resourceType;
    public int amount = 1;

    public float attractRadius = 1.5f;
    public float moveSpeed = 6f;
    public float pickupRadius = 0.2f;

    private Transform player;
    private bool isAttracting;

    // 실제 아이콘이 아직 안 붙어있어도 눈에 보이도록 자원 타입별 단색 스프라이트를 즉석에서 만들어 캐싱한다.
    private static readonly Color[] FallbackColors =
    {
        new Color(0.55f, 0.35f, 0.15f), // Wood
        new Color(0.65f, 0.65f, 0.7f),  // Iron
        new Color(0.85f, 0.5f, 0.2f),   // Copper
        new Color(0.3f, 0.9f, 0.3f),    // Chemical
        new Color(0.15f, 0.15f, 0.15f), // Oil
    };
    private static readonly Sprite[] fallbackSprites = new Sprite[5];

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (player == null) return;

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
        sr.sprite = GetFallbackSprite(type);
        sr.sortingOrder = 0;

        ResourcePickup pickup = go.AddComponent<ResourcePickup>();
        pickup.resourceType = type;
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
