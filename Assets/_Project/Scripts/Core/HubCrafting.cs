using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// 거점의 제작대. 스테이지 상인(StageExtraction의 임시 상인)과 달리 씬 시작 시 한 번만 만들어지는
// 영구 시설이고, 거점 영구 보관 자원(stash)의 희귀자원을 소모해서 원하는 무기 종류를 확정 지급한다
// (docs/schema.sql의 recipe: result_item_id + rare_resource_type_id + rare_resource_amount를
// 코드에서는 "무기 5종 전부 같은 희귀자원 비용"으로 단순화함 - 종류별 정확한 비용은 기획 미정).
//
// 지금까지 stash는 CommitRunToStash()로 쌓이기만 하고 쓸 데가 없었는데(강화가 runHeld로 옮겨간 뒤
// TrySpendStash가 죽은 코드였음), 이 제작대가 stash의 유일한 소비처가 된다.
//
// MerchantItem을 그대로 재사용한다(테두리/가격표/F키 구매/실패 시 환불 없음 로직 전부 동일) -
// useStash만 켜서 자원 출처만 바꾼다. 거점 "상인" 오브젝트 옆에 씬 시작 시 한 번 배치된다.
public class HubCrafting : MonoBehaviour
{
    // 무기 하나 제작에 필요한 희귀자원 개수. 종류별 차등은 기획 미정이라 전부 동일하게 둔다.
    private const int RareCostPerWeapon = 3;
    private const float ItemSpacing = 0.9f;
    private const float IconTargetSize = 0.7f; // StageExtraction.GoodsIconTargetSize와 동일 값

    private static readonly WeaponType[] CraftableWeapons =
    {
        WeaponType.Pistol, WeaponType.Sword, WeaponType.Smg, WeaponType.Lance, WeaponType.Grenade
    };

    void Start()
    {
        GameObject hubMerchant = GameObject.Find("상인");
        if (hubMerchant == null)
        {
            Debug.LogWarning("HubCrafting: 거점 상인 오브젝트를 찾지 못해 제작대를 만들 수 없음");
            return;
        }

        Material outlineMaterial = FindOutlineMaterial();
        List<ResourceType> costTypes = new List<ResourceType> { ResourceType.Rare };
        List<int> costAmounts = new List<int> { RareCostPerWeapon };
        string priceLabel = MerchantItem.BuildPriceLabel(costTypes, costAmounts);

        GameObject group = new GameObject("제작대");
        group.transform.position = hubMerchant.transform.position + new Vector3(2.5f, 0f, 0f);

        GameObject priceBubble = MerchantItem.CreatePriceBubble(group.transform, group.transform.position + new Vector3(1.6f, 0.9f, 0f));
        Text priceText = priceBubble.GetComponentInChildren<Text>();
        priceBubble.SetActive(false);

        for (int i = 0; i < CraftableWeapons.Length; i++)
        {
            WeaponType type = CraftableWeapons[i];
            GameObject prefab = WeaponPickup.GetPrefab(type);
            SpriteRenderer prefabSr = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>() : null;
            if (prefabSr == null) continue; // 아이콘을 못 구했으면 진열하지 않는다 (상인과 동일한 규칙)

            Vector3 pos = group.transform.position + new Vector3(i * ItemSpacing, 0f, 0f);
            SpawnCraftItem(group.transform, pos, prefabSr.sprite, type, costTypes, costAmounts,
                priceBubble, priceText, priceLabel, outlineMaterial);
        }
    }

    // 상인 상품과 같은 흰색 테두리 머티리얼을 재사용한다 (StageExtraction.Start()와 동일한 조회).
    private static Material FindOutlineMaterial()
    {
        EnemySpawner anySpawner = FindFirstObjectByType<EnemySpawner>();
        Enemy enemyPrefabComponent = anySpawner != null && anySpawner.enemyPrefab != null
            ? anySpawner.enemyPrefab.GetComponent<Enemy>() : null;
        return enemyPrefabComponent != null ? enemyPrefabComponent.outlineMaterial : null;
    }

    private static void SpawnCraftItem(Transform parent, Vector3 position, Sprite sprite, WeaponType type,
        List<ResourceType> costTypes, List<int> costAmounts,
        GameObject priceBubble, Text priceText, string priceLabel, Material outlineMaterial)
    {
        GameObject go = new GameObject("제작대_" + type);
        go.transform.SetParent(parent);
        go.transform.position = position;

        MerchantItem item = go.AddComponent<MerchantItem>();
        item.useStash = true; // 거점 영구 보관 자원을 쓴다 (런 파밍분 runHeld가 아니라)
        item.Init(sprite, IconTargetSize, costTypes, costAmounts, outlineMaterial,
            () =>
            {
                WeaponSwitcher switcher = FindFirstObjectByType<WeaponSwitcher>();
                // 무기 슬롯이 꽉 찼으면 false - MerchantItem이 이 경우 자원을 쓰지 않고 상품도 남겨둔다.
                return switcher != null && switcher.TryGiveWeapon(type);
            },
            priceBubble, priceText, priceLabel);
    }
}
