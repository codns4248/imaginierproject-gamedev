using UnityEngine;
using UnityEngine.InputSystem;

// 트리거 콜라이더 범위 안에 플레이어가 들어오면 흰색 테두리(Enemy.cs의 피격 테두리와 같은 방식:
// 스프라이트를 4방향으로 살짝 띄워 전용 머티리얼로 흰색 실루엣을 겹쳐 그림)를 보여주고,
// F키를 누르면 onInteract 콜백을 실행한다. 포탈(StagePortal, StageExitPortal)이 공통으로 쓴다.
// F키가 다른 상호작용(바닥 무기 줍기, 상인 구매 등)과 겹칠 수 있어 InteractInput으로 프레임당
// 한 번만 반응하게 막는다 (MerchantItem과 동일한 방식).
// proximityRadius가 0보다 크면 트리거 대신 거리로 판정한다: 플레이어가 그림 테두리 상자에서 이 거리 안에 들어오면 범위 안.
// (거점 건물처럼 플레이어가 부딪히는 단단한 충돌체만 있는 오브젝트용 - SpecialUpgradeStation 참고)
[RequireComponent(typeof(Collider2D))]
public class InteractOutline : MonoBehaviour
{
    [HideInInspector] public float proximityRadius;

    // 이 값이 있으면 거리 판정을 통째로 이 함수에 맡긴다 (여러 개가 붙어 있어서 "가장 가까운 하나만" 반응해야 하는 거치대 등 - StartWeaponStand 참고).
    public System.Func<bool> inRangeCheck;
    private bool UsesProximity => proximityRadius > 0f || inRangeCheck != null;

    private const float OutlineWorldOffset = 0.06f; // 테두리를 원본에서 띄우는 거리 (월드 기준이라 크기가 큰 오브젝트도 두께가 같다)

    private SpriteRenderer[] outlineRenderers;
    private Material outlineMat;
    private bool playerInRange;
    private System.Action onInteract;
    private SpriteRenderer mainSprite;
    private Transform player;

    public void Init(System.Action onInteract)
    {
        this.onInteract = onInteract;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        mainSprite = sr;
        if (sr != null) CreateOutline(FindSharedOutlineMaterial(), sr);
    }

    // 적(Enemy)이 이미 쓰는 피격 테두리 머티리얼을 그대로 재사용한다 (MerchantItem과 동일한 방식).
    private static Material FindSharedOutlineMaterial()
    {
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        Enemy enemyPrefab = spawner != null && spawner.enemyPrefab != null ? spawner.enemyPrefab.GetComponent<Enemy>() : null;
        return enemyPrefab != null ? enemyPrefab.outlineMaterial : null;
    }

    private void CreateOutline(Material outlineMaterial, SpriteRenderer sr)
    {
        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        outlineRenderers = new SpriteRenderer[offsets.Length];

        if (outlineMaterial != null) outlineMat = new Material(outlineMaterial);

        float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject go = new GameObject("Outline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * (OutlineWorldOffset / scale));

            SpriteRenderer outlineSr = go.AddComponent<SpriteRenderer>();
            outlineSr.sprite = sr.sprite;
            if (outlineMat != null) outlineSr.sharedMaterial = outlineMat;
            outlineSr.sortingOrder = sr.sortingOrder - 1;
            outlineSr.enabled = false;

            outlineRenderers[i] = outlineSr;
        }
        if (outlineMat != null) outlineMat.color = Color.white;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (UsesProximity || other.gameObject.name != "Player") return;
        playerInRange = true;
        SetOutlineEnabled(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (UsesProximity || other.gameObject.name != "Player") return;
        playerInRange = false;
        SetOutlineEnabled(false);
    }

    private void SetOutlineEnabled(bool isEnabled)
    {
        if (outlineRenderers == null) return;
        for (int i = 0; i < outlineRenderers.Length; i++) outlineRenderers[i].enabled = isEnabled;
    }

    void Update()
    {
        if (UsesProximity) UpdateProximity();
        if (!playerInRange) return;

        if (Keyboard.current.fKey.wasPressedThisFrame && InteractInput.TryConsumeFKey())
            onInteract?.Invoke();
    }

    // 애니메이션이 있는 오브젝트(거점 집 등)는 그림이 바뀌므로, 테두리가 보이는 동안 지금 프레임 모양을 따라가게 한다.
    void LateUpdate()
    {
        if (!playerInRange || mainSprite == null || outlineRenderers == null) return;
        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            if (outlineRenderers[i].sprite != mainSprite.sprite) outlineRenderers[i].sprite = mainSprite.sprite;
            outlineRenderers[i].flipX = mainSprite.flipX;
        }
    }

    // 거리 모드: 그림 테두리 상자(없으면 오브젝트 위치)에서 플레이어까지의 거리로 범위 안/밖을 정한다.
    private void UpdateProximity()
    {
        if (inRangeCheck != null)
        {
            bool custom = inRangeCheck();
            if (custom == playerInRange) return;
            playerInRange = custom;
            SetOutlineEnabled(custom);
            return;
        }

        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            player = p.transform;
        }

        float sqr = mainSprite != null
            ? mainSprite.bounds.SqrDistance(new Vector3(player.position.x, player.position.y, mainSprite.bounds.center.z))
            : ((Vector2)(player.position - transform.position)).sqrMagnitude;
        bool inRange = sqr <= proximityRadius * proximityRadius;
        if (inRange == playerInRange) return;
        playerInRange = inRange;
        SetOutlineEnabled(inRange);
    }

    void OnDestroy()
    {
        if (outlineMat != null) Destroy(outlineMat);
    }
}
