using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 몬스터가 낮은 확률로 드랍하거나 플레이어가 버린 무기 아이템.
// 자원(ResourcePickup)과 달리 자동으로 끌려오지 않고, 드랍된 자리에서 무작위 방향으로 살짝
// 튀어나가 바닥에 떨어지는 연출(ResourcePickup과 동일한 방식)을 보여준 뒤 가만히 있는다.
// 플레이어가 interactRadius 안까지 다가가서 F키를 누르면(WeaponSwitcher가) 빈 무기 슬롯에 장착해준다.
// 이 컴포넌트 자신은 "여기 이런 무기가 떨어져 있다"는 상태 + 시각적 표시만 맡고,
// 실제 장착/드랍 판단(빈 슬롯 찾기 등)은 WeaponSwitcher가 담당한다.
// 트리거 콜라이더가 아니라 거리 비교(FindNearestInRange)로 판단하므로 InteractOutline을 그대로
// 못 쓰고, 같은 방식(Enemy 피격 테두리 재사용, MerchantItem 참고)을 직접 구현한다. 여러 무기가
// 겹쳐 떨어져 있어도 실제로는 F키에 가장 가까운 것 하나만 주워지므로, 테두리도 그 하나에만 뜬다.
public class WeaponPickup : MonoBehaviour
{
    public WeaponType weaponType;

    // 자원 흡입 범위(ResourcePickup.attractRadius=1.5)보다 살짝 좁게 잡아서,
    // 자원과 겹쳐 있어도 무기를 주우려면 그보다 더 바짝 다가가야 하게 한다.
    public float interactRadius = 1f;

    [Header("드랍 연출 (몹에게서 튀어나와 바닥에 떨어지는 모션)")]
    public float popDuration = 0.35f;     // 튀어나왔다가 착지하기까지 걸리는 시간
    public float popArcHeight = 0.5f;     // 튀어오르는 높이(포물선 정점)
    public float popMinDistance = 0.25f;  // 착지 지점까지의 최소 거리
    public float popMaxDistance = 0.7f;   // 착지 지점까지의 최대 거리

    private bool isPopping;

    private Transform player;
    private SpriteRenderer[] outlineRenderers;
    private Material outlineMat;

    private static Material sharedOutlineMaterial;
    private static bool outlineMaterialLoaded;

    // 바닥에 떨어져 있을 때(장착 중이 아닐 때) 무기 종류별로 얼마나 확대/축소해서 보여줄지.
    // Pistol은 원본 스프라이트가 커서 오히려 줄이고, 나머지는 눈에 잘 띄도록 더 키운다.
    private static readonly float[] WorldIconScales =
    {
        2f / 3f, // Pistol
        3f,      // Sword
        3f,      // Smg
        3f,      // Lance
        3f,      // Grenade
    };

    private static readonly List<WeaponPickup> active = new List<WeaponPickup>();

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    private static WeaponCatalog catalog;
    private static bool catalogLoaded;

    private static WeaponCatalog GetCatalog()
    {
        if (!catalogLoaded)
        {
            catalog = Resources.Load<WeaponCatalog>("WeaponCatalog");
            catalogLoaded = true;
        }
        return catalog;
    }

    public static GameObject GetPrefab(WeaponType type)
    {
        WeaponCatalog c = GetCatalog();
        return c != null ? c.GetPrefab(type) : null;
    }

    // TODO: 드랍 확인 테스트용으로 10%로 올려둠. 확인 끝나면 0.01f(1%)로 되돌릴 것.
    public const float DropChance = 0.1f;

    /// <summary>DropChance 확률로 무기 하나(5종류 중 랜덤)를 드랍한다. Enemy.Die() 등에서 호출한다.</summary>
    public static void TrySpawnRandomDrop(Vector3 position)
    {
        if (Random.value > DropChance) return;

        WeaponType type = (WeaponType)Random.Range(0, 5);
        SpawnDrop(position, type);
    }

    /// <summary>지정한 종류의 무기 아이템을 그 자리에 만든다. 플레이어가 무기를 버릴 때도 사용한다.</summary>
    public static void SpawnDrop(Vector3 position, WeaponType type)
    {
        GameObject prefab = GetPrefab(type);
        if (prefab == null) return;

        GameObject go = new GameObject($"{type}WeaponPickup");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * WorldIconScales[(int)type]; // 바닥에 놓였을 때 눈에 잘 띄도록 확대/축소

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        SpriteRenderer prefabSr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (prefabSr != null) sr.sprite = prefabSr.sprite;
        sr.sortingOrder = 0;

        WeaponPickup pickup = go.AddComponent<WeaponPickup>();
        pickup.weaponType = type;
        pickup.StartPop(position);
        pickup.CreateOutline(sr);
    }

    // 적(Enemy)이 이미 쓰는 피격 테두리 머티리얼을 그대로 재사용한다 (InteractOutline/MerchantItem과 동일한 방식).
    private static Material GetSharedOutlineMaterial()
    {
        if (!outlineMaterialLoaded)
        {
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
            Enemy enemyPrefab = spawner != null && spawner.enemyPrefab != null ? spawner.enemyPrefab.GetComponent<Enemy>() : null;
            sharedOutlineMaterial = enemyPrefab != null ? enemyPrefab.outlineMaterial : null;
            outlineMaterialLoaded = true;
        }
        return sharedOutlineMaterial;
    }

    private void CreateOutline(SpriteRenderer sr)
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;

        Material outlineMaterial = GetSharedOutlineMaterial();
        if (outlineMaterial != null) outlineMat = new Material(outlineMaterial);

        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        outlineRenderers = new SpriteRenderer[offsets.Length];

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
        if (outlineRenderers == null || player == null) return;

        bool isNearest = !isPopping && this == FindNearestInRange(player.position);
        for (int i = 0; i < outlineRenderers.Length; i++) outlineRenderers[i].enabled = isNearest;
    }

    void OnDestroy()
    {
        if (outlineMat != null) Destroy(outlineMat);
    }

    // 드랍된 자리(originPos)에서 무작위 방향으로 살짝 떨어진 착지 지점까지 포물선을 그리며 튀어나간다.
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

    // 플레이어 위치 기준으로 interactRadius 안에 있는 것들 중 가장 가까운 픽업을 찾는다 (없으면 null).
    // 튀어오르는 연출 중인 것은 아직 주울 수 없다.
    public static WeaponPickup FindNearestInRange(Vector2 playerPos)
    {
        WeaponPickup nearest = null;
        float nearestDistSqr = float.MaxValue;

        foreach (WeaponPickup pickup in active)
        {
            if (pickup == null || pickup.isPopping) continue;

            float distSqr = ((Vector2)pickup.transform.position - playerPos).sqrMagnitude;
            if (distSqr <= pickup.interactRadius * pickup.interactRadius && distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = pickup;
            }
        }

        return nearest;
    }
}
