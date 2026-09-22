using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 스테이지-거점 이동 및 층수 진행을 담당한다.
// StageTimer가 스테이지 클리어를 알리면 스테이지 위쪽에 색깔이 다른 랜덤 포탈 3개를 띄운다.
// 5층마다(StageProgress.IsExtractionFloor)는 오른쪽에 추출(거점 복귀, 자원 확정) 포탈도 같이 뜬다.
// 플레이어가 어느 포탈에 닿는지는 StageExitPortal이 처리한다.
// 플레이어가 죽으면 파밍한 자원(아직 확정되지 않은 분)을 잃고, 사망 페이드가 끝난 뒤 거점으로 이동한다 (익스트랙션 실패).
// (조장 확인 완료: 사망 시 자원은 확정하지 않고 소실시키는 게 맞는 규칙 - 임시로 CommitRunToStash를 쓰던 걸 원복함)
// MainScene에 빈 오브젝트를 만들어 이 컴포넌트를 붙여두면 된다.
public class StageExtraction : MonoBehaviour
{
    // 스테이지 클리어 시 위에 뜨는 색깔 포탈이 연결되는 테마들 (거점 포탈과 색깔만 다름).
    private static readonly string[] PortalThemes = { "오염된 호수", "광산", "공장", "모래 황무지", "숲" };
    private static readonly Dictionary<string, Color> PortalColors = new Dictionary<string, Color>
    {
        { "오염된 호수", new Color(0.55f, 0.35f, 0.15f) }, // 갈색
        { "광산", new Color(0.35f, 0.35f, 0.37f) },        // 검회색 (순검정은 안 보여서 밝게 조정)
        { "공장", new Color(0.2f, 0.4f, 0.9f) },           // 파란색
        { "모래 황무지", new Color(1f, 0.55f, 0.1f) },      // 주황
        { "숲", new Color(0.2f, 0.8f, 0.3f) },              // 초록
    };
    private static readonly Color ExtractionPortalColor = new Color(1f, 1f, 1f, 0.5f); // 반투명 흰색

    // 스테이지 클리어마다(추출 포탈 유무와 무관) 일정 확률로 등장하는 떠돌이 상인 (거점 앞에 있는 것과 같은 오브젝트를 복제).
    private const float MerchantSpawnChance = 0.3f;

    // 상인이 나타났을 때, 그중에서도 희귀자원까지 파는 경우의 확률 (회복약/무기는 항상 판다).
    private const float RareGoodsChance = 0.2f;

    // 판매 가격 (자원 1~2종류 섞어서 지불). 회복약은 싸게, 무기는 비싸게, 희귀자원은 매우매우 비싸게.
    private const int PotionCost = 3;
    private const int WeaponCostPerType = 8;
    private const int RareGoodsCostPerType = 20;

    private static readonly ResourceType[] CommonResourceTypes =
    {
        ResourceType.Wood, ResourceType.Iron, ResourceType.Copper, ResourceType.Chemical, ResourceType.Oil
    };

    private PlayerHealth playerHealth;
    private GameObject hubPortalTemplate;
    private GameObject merchantTemplate;
    private GameObject merchantCarpetTemplate;
    private Material merchantOutlineMaterial;
    private Sprite potionIconSprite;

    void Start()
    {
        StageTimer timer = FindFirstObjectByType<StageTimer>();
        if (timer != null) timer.OnStageClear += HandleStageClear;

        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null) playerHealth.OnDied += HandleDeath;

        // 색깔 포탈은 이 오브젝트를 복제해서 색만 다시 입힌다 (아트가 바뀌면 같이 따라감).
        StagePortal hubPortal = FindFirstObjectByType<StagePortal>();
        hubPortalTemplate = hubPortal != null ? hubPortal.gameObject : null;
        if (hubPortalTemplate == null) Debug.LogWarning("StageExtraction: 거점 포탈(StagePortal)을 찾지 못해 클리어 포탈을 만들 수 없음");

        merchantTemplate = GameObject.Find("상인");
        merchantCarpetTemplate = GameObject.Find("상인_카펫");
        if (merchantTemplate == null || merchantCarpetTemplate == null)
            Debug.LogWarning("StageExtraction: 상인/상인_카펫 오브젝트를 찾지 못해 상인 등장 연출을 만들 수 없음");

        // 상품 진열용 흰색 테두리는 적(Enemy)이 이미 쓰는 것과 같은 머티리얼을 재사용한다.
        EnemySpawner anySpawner = FindFirstObjectByType<EnemySpawner>();
        Enemy enemyPrefabComponent = anySpawner != null && anySpawner.enemyPrefab != null
            ? anySpawner.enemyPrefab.GetComponent<Enemy>() : null;
        merchantOutlineMaterial = enemyPrefabComponent != null ? enemyPrefabComponent.outlineMaterial : null;

        // 회복약 아이콘은 이미 있는 PotionUI의 아이콘 이미지를 그대로 빌려 쓴다.
        GameObject potionUI = FindInactiveByName("PotionUI");
        Image potionIcon = potionUI != null ? potionUI.GetComponentInChildren<Image>(true) : null;
        potionIconSprite = potionIcon != null ? potionIcon.sprite : null;
    }

    // GameObject.Find는 비활성 오브젝트를 못 찾으므로, 씬 전체를 뒤져서 이름으로 찾는다.
    private static GameObject FindInactiveByName(string name)
    {
        foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name == name) return go;
        }
        return null;
    }

    void HandleStageClear()
    {
        // 스테이지를 클리어해서 포탈이 뜨는 순간부터는 전투 구간이 아니므로 진행 중이던 기믹을 멈춘다.
        StageGimmickManager.ClearGimmick();
        SpawnExitPortals();
    }

    // 현재 구역 위쪽에 랜덤 색깔 포탈 3개, 5층마다 오른쪽에 추출 포탈 1개를 추가로 띄운다.
    private void SpawnExitPortals()
    {
        Vector2 center = StageManager.CurrentZoneCenter;
        float halfExtent = StageManager.CurrentZoneHalfExtent;

        GameObject group = new GameObject("ExitPortals");

        List<string> pool = new List<string>(PortalThemes);
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int pick = Random.Range(0, pool.Count);
            string theme = pool[pick];
            pool.RemoveAt(pick);

            float x = center.x + (i - 1) * (halfExtent * 0.6f);
            float y = center.y + halfExtent - 2f;
            CreatePortal(group.transform, new Vector2(x, y), theme, PortalColors[theme]);
        }

        if (StageProgress.IsExtractionFloor)
        {
            float x = center.x + halfExtent - 2f;
            CreatePortal(group.transform, new Vector2(x, center.y), null, ExtractionPortalColor);
        }

        // 추출 포탈 유무와 무관하게 스테이지를 클리어할 때마다 확률적으로 상인이 나타난다.
        if (Random.value < MerchantSpawnChance)
            SpawnMerchant(group.transform, center, halfExtent);
    }

    // 구역 왼쪽 가장자리, 추출 포탈과 같은 높이(y)에 상인+카펫을 띄운다 (추출 포탈은 오른쪽,
    // 상인은 왼쪽이라 서로 마주보는 배치). 원본(거점 앞) 오브젝트를 복제하고, 상인과 카펫의
    // 원래 상대 위치(오프셋)를 그대로 유지해서 카펫 위에 서 있는 배치가 흐트러지지 않게 한다.
    private void SpawnMerchant(Transform parent, Vector2 center, float halfExtent)
    {
        if (merchantTemplate == null || merchantCarpetTemplate == null) return;

        Vector3 offset = merchantTemplate.transform.position - merchantCarpetTemplate.transform.position;
        Vector3 carpetPos = new Vector3(center.x - halfExtent + 2f, center.y, 0f);

        GameObject carpet = Instantiate(merchantCarpetTemplate, parent);
        carpet.name = "상인_카펫";
        carpet.transform.position = carpetPos;

        GameObject merchant = Instantiate(merchantTemplate, parent);
        merchant.name = "상인";
        merchant.transform.position = carpetPos + offset;

        // 원본 둘 다 sortingOrder가 같아서(0) 그리는 순서가 들쭉날쭉했다.
        // 상인이 카펫에 가려지지 않도록 상인을 항상 한 단계 앞에 그리게 고정한다.
        SpriteRenderer carpetSr = carpet.GetComponent<SpriteRenderer>();
        SpriteRenderer merchantSr = merchant.GetComponent<SpriteRenderer>();
        int carpetOrder = carpetSr != null ? carpetSr.sortingOrder : 0;
        if (merchantSr != null) merchantSr.sortingOrder = carpetOrder + 1;

        SpawnMerchantGoods(parent, merchant, carpetSr, carpetOrder);
    }

    // 카펫 위에 왼쪽=회복약, 가운데=무기(둘 다 확정 등장), 오른쪽=희귀자원(낮은 확률)을 늘어놓는다.
    private void SpawnMerchantGoods(Transform parent, GameObject merchant, SpriteRenderer carpetSr, int carpetOrder)
    {
        if (carpetSr == null) return;

        Bounds bounds = carpetSr.bounds;
        float y = bounds.center.y;
        float leftX = bounds.center.x - bounds.extents.x * 0.5f;
        float centerX = bounds.center.x;
        float rightX = bounds.center.x + bounds.extents.x * 0.5f;

        // 상품에 가까이 가면 이 말풍선에 가격이 뜬다. 상인 오른쪽에 하나만 만들어서 상품들이 공유한다.
        GameObject priceBubble = CreatePriceBubble(parent, merchant.transform.position + new Vector3(1.6f, 0.4f, 0f));
        Text priceText = priceBubble.GetComponentInChildren<Text>();
        priceBubble.SetActive(false);

        // 회복약: 싸게, 자원 1종류.
        ResourceType potionCostType = CommonResourceTypes[Random.Range(0, CommonResourceTypes.Length)];
        List<ResourceType> potionCostTypes = new List<ResourceType> { potionCostType };
        List<int> potionCostAmounts = new List<int> { PotionCost };
        SpawnGoodsItem(parent, new Vector3(leftX, y, 0f), carpetOrder, potionIconSprite,
            potionCostTypes, potionCostAmounts, priceBubble, priceText,
            () =>
            {
                HealthPotion potion = FindFirstObjectByType<HealthPotion>();
                if (potion == null) return false;
                potion.AddPotions(1);
                return true; // 회복약은 개수 제한이 없어 항상 지급 성공
            });

        // 무기: 비싸게, 자원 2종류 섞어서. 5종류 중 랜덤 한 가지를 그 자리에서 바로 지급한다.
        WeaponType weaponType = (WeaponType)Random.Range(0, 5);
        GameObject weaponPrefab = WeaponPickup.GetPrefab(weaponType);
        SpriteRenderer weaponPrefabSr = weaponPrefab != null ? weaponPrefab.GetComponentInChildren<SpriteRenderer>() : null;
        List<ResourceType> weaponCostTypes = PickDistinctResourceTypes(2);
        List<int> weaponCostAmounts = new List<int> { WeaponCostPerType, WeaponCostPerType };
        SpawnGoodsItem(parent, new Vector3(centerX, y, 0f), carpetOrder, weaponPrefabSr != null ? weaponPrefabSr.sprite : null,
            weaponCostTypes, weaponCostAmounts, priceBubble, priceText,
            () =>
            {
                WeaponSwitcher switcher = FindFirstObjectByType<WeaponSwitcher>();
                // 무기 슬롯이 꽉 찼으면 false - MerchantItem이 이 경우 자원을 쓰지 않고 상품도 남겨둔다.
                return switcher != null && switcher.TryGiveWeapon(weaponType);
            });

        // 희귀자원: 낮은 확률로만 진열되고, 매우매우 비싸게(자원 2종류 섞어서 대량) 판다.
        if (Random.value < RareGoodsChance)
        {
            List<ResourceType> rareCostTypes = PickDistinctResourceTypes(2);
            List<int> rareCostAmounts = new List<int> { RareGoodsCostPerType, RareGoodsCostPerType };
            SpawnGoodsItem(parent, new Vector3(rightX, y, 0f), carpetOrder, ResourcePickup.GetIconSprite(ResourceType.Rare),
                rareCostTypes, rareCostAmounts, priceBubble, priceText,
                () => { ResourceBank.AddRunResource(ResourceType.Rare, 1); return true; }); // 개수 제한 없어 항상 성공
        }
    }

    // 상품 아이콘의 목표 표시 크기(월드 유닛). 원본 해상도가 다른 스프라이트끼리도 이 크기에 맞춰진다.
    private const float GoodsIconTargetSize = 0.7f;

    private void SpawnGoodsItem(Transform parent, Vector3 position, int carpetOrder, Sprite sprite,
        List<ResourceType> costTypes, List<int> costAmounts, GameObject priceBubble, Text priceText, System.Func<bool> onPurchase)
    {
        if (sprite == null) return; // 아이콘을 못 구했으면(무기 프리팹 못 찾음 등) 진열하지 않는다

        GameObject go = new GameObject("상인_상품");
        go.transform.SetParent(parent);
        go.transform.position = position;

        MerchantItem item = go.AddComponent<MerchantItem>();
        item.Init(sprite, GoodsIconTargetSize, costTypes, costAmounts, merchantOutlineMaterial, onPurchase,
            priceBubble, priceText, BuildPriceLabel(costTypes, costAmounts));

        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = carpetOrder + 1;
    }

    private static List<ResourceType> PickDistinctResourceTypes(int count)
    {
        List<ResourceType> pool = new List<ResourceType>(CommonResourceTypes);
        List<ResourceType> picked = new List<ResourceType>();
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
    }

    private static string BuildPriceLabel(List<ResourceType> types, List<int> amounts)
    {
        string[] parts = new string[types.Count];
        for (int i = 0; i < types.Count; i++) parts[i] = ResourceKoreanName(types[i]) + " x" + amounts[i];
        return string.Join(" + ", parts);
    }

    private static string ResourceKoreanName(ResourceType type)
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

    // 상인 옆에 뜨는 가격표 말풍선을 만든다. TextMesh는 URP 폰트 셰이더와 호환 문제가 있어서
    // (DamageNumber.cs 참고) 이 프로젝트 관례대로 World Space Canvas + UI.Text로 만든다.
    private static GameObject CreatePriceBubble(Transform parent, Vector3 position)
    {
        GameObject root = new GameObject("상인_가격표", typeof(RectTransform), typeof(Canvas));
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

    private void CreatePortal(Transform parent, Vector2 position, string targetTheme, Color color)
    {
        if (hubPortalTemplate == null) return;

        GameObject go = Instantiate(hubPortalTemplate);
        go.name = string.IsNullOrEmpty(targetTheme) ? "Portal_추출" : "Portal_" + targetTheme;
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        // 복제해 온 거점 포탈 스크립트(랜덤 스테이지 진입)는 여기서 필요 없으니 끄고 지운다.
        StagePortal clonedHubBehaviour = go.GetComponent<StagePortal>();
        if (clonedHubBehaviour != null)
        {
            clonedHubBehaviour.enabled = false;
            Destroy(clonedHubBehaviour);
        }

        // 거점 포탈이 Start()에서 자기 몫으로 붙여둔 InteractOutline도 같이 복제돼 온다.
        // StageExitPortal.Init()이 새로 하나 붙이므로, 안 지우면 같은 오브젝트에 두 개가 남아
        // F키 반응이 뒤섞인다(둘 중 하나가 랜덤 스테이지 이동을 일으킬 수 있음).
        InteractOutline clonedOutline = go.GetComponent<InteractOutline>();
        if (clonedOutline != null)
        {
            clonedOutline.enabled = false;
            Destroy(clonedOutline);
        }

        StageExitPortal portal = go.AddComponent<StageExitPortal>();
        portal.Init(targetTheme, color);
    }

    void HandleDeath()
    {
        // 사망 = 하드 리셋: 파밍한(아직 확정 안 된) 자원 + 이번 런의 무기 강화를 전부 잃는다.
        // (결과 화면 유무와 무관하게 항상. 층수 초기화는 아래 결과 화면 쪽/자동 복귀 쪽에서 처리.)
        ResourceBank.DiscardRun();

        // 상인에게 사서 늘렸던 회복약도 자원과 같은 규칙: 이번 런에서 늘어난 만큼은 잃고 기본 개수로 되돌아간다.
        HealthPotion potion = FindFirstObjectByType<HealthPotion>();
        if (potion != null) potion.ResetToStarting();

        // 필드/상인에게서 얻은 무기도 자원과 같은 규칙: 처음부터 들고 있던 무기(슬롯 0) 하나만
        // 남기고 전부 사라진다. 강화 초기화보다 먼저 해서, 어차피 사라질 무기의 스탯을 되돌리는
        // 낭비를 피한다.
        WeaponSwitcher weaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();
        if (weaponSwitcher != null) weaponSwitcher.ResetToStartingWeapon();

        // 무기 강화 초기화: 저장소를 비우는 것과 함께, 지금 씬에 남아있는(=위에서 안 사라진)
        // 무기 인스턴스들의 스탯도 직접 되돌려준다 - 거점 복귀가 씬 재로드가 아니라 좌표 이동이라
        // Awake()를 다시 타지 않기 때문.
        WeaponEnhanceStore.ResetForNewRun();

        // 결과 화면(DeathResultUI)이 씬에 있으면 층수 초기화 + 거점 복귀는 그 쪽이 담당한다
        // (플레이어가 버튼/Enter를 누를 때까지 기다렸다가 복귀).
        // 결과 화면이 없는 씬에서만 예전처럼 자동으로 거점에 돌려보낸다.
        if (FindFirstObjectByType<DeathResultUI>() == null)
        {
            StageProgress.ResetToFirstStage();
            StartCoroutine(ReturnToHubAfterFade());
        }
    }

    // 죽는 연출(PlayerHealth의 화면 페이드)이 끝날 때까지 기다렸다가 거점으로 보낸다.
    // 페이드 도중에 바로 이동시키면 연출이 다 안 보이고 잘려나간다.
    private IEnumerator ReturnToHubAfterFade()
    {
        float delay = playerHealth != null ? playerHealth.deathFadeDuration : 1.5f;
        yield return new WaitForSeconds(delay);
        StageManager.ReturnToHub();
    }
}
