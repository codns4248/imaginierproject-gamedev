using System.Collections.Generic;
using UnityEngine;

// 거점 집(특수 강화 건물) 아래 무기 거치대 5개(WeaponStand_01~05)에 무기 5종을 진열하고,
// 특수 강화 "시작 무기 선택"을 해금하면 무기에 다가가 F키로 시작 무기(0번 슬롯)를 고를 수 있게 한다.
// - 지금 시작 무기로 쓰고 있는 무기는 플레이어 손에 있으니 그 거치대는 비워 보인다.
// - 해금 전에는 무기가 어두운 그림자로만 보이고 상호작용(테두리/F)이 없다.
// - 고른 무기는 SpecialUpgrades에 저장되고, WeaponSwitcher가 0번 슬롯을 바로 바꾼다 (사망/귀환 뒤에도 이 무기로 시작).
// 거치대는 팀 소품 프리팹(Prop_WeaponStand) 인스턴스라 씬은 고치지 않고, SpecialUpgradeStation이 게임 시작 때 이름으로 찾아 붙인다.
public class StartWeaponStand : MonoBehaviour
{
    // 거치대 01~05(왼쪽 -> 오른쪽)에 놓는 순서 (무기 제작 탭과 같은 순서)
    private static readonly WeaponType[] Order = { WeaponType.Sword, WeaponType.Lance, WeaponType.Pistol, WeaponType.Smg, WeaponType.Grenade };

    private const float DisplaySize = 0.3f;        // 거치대 위 무기 그림의 긴 변 길이 (월드 유닛, 거치대는 0.38)
    private const float DisplayLift = 0.1f;        // 거치대 중심에서 위로 (거치대 위에 올려 전시한 느낌이 나게, 거치대 높이는 0.38)
    // 거치대 중심에서 플레이어 중심까지 이 거리 안이면 상호작용. 플레이어 몸(충돌체)이 커서 거치대에 붙어도
    // 옆에서 약 0.41, 위/아래에서 약 0.49까지만 다가갈 수 있다. 이웃 거치대(간격 0.9)와 범위가 겹치므로 가장 가까운 하나만 반응한다.
    private const float InteractRange = 0.6f;
    private const int DisplaySortingOrder = 2;     // 거치대(0) 위, 테두리는 그 한 칸 아래(1)
    private static readonly Color LockedColor = new Color(0.12f, 0.12f, 0.15f, 0.85f);

    private static readonly List<StartWeaponStand> stands = new List<StartWeaponStand>();
    private static Transform player;

    private WeaponType type;
    private SpriteRenderer display;
    private bool interactable;
    private WeaponSwitcher weaponSwitcher;

    /// <summary>씬의 WeaponStand_01~05를 찾아 무기를 하나씩 진열한다 (이미 붙어 있으면 건너뜀).</summary>
    public static void InstallAll()
    {
        for (int i = 0; i < Order.Length; i++)
        {
            GameObject stand = GameObject.Find($"WeaponStand_{i + 1:00}");
            if (stand == null || stand.GetComponent<StartWeaponStand>() != null) continue;
            stand.AddComponent<StartWeaponStand>().Init(Order[i]);
        }
    }

    private void Init(WeaponType weaponType)
    {
        type = weaponType;
        weaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();

        GameObject prefab = WeaponPickup.GetPrefab(type);
        SpriteRenderer prefabSr = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>() : null;
        if (prefabSr == null || prefabSr.sprite == null) return;

        GameObject go = new GameObject($"Display_{type}");
        go.transform.SetParent(transform, false);

        // 긴 변이 DisplaySize가 되게 맞추고, 무기 그림의 피벗(손잡이 쪽일 수 있음)과 상관없이 그림 한가운데가 거치대 위에 오게 놓는다.
        Bounds spriteBounds = prefabSr.sprite.bounds;
        float s = DisplaySize / Mathf.Max(spriteBounds.size.x, spriteBounds.size.y, 0.0001f);
        Vector3 standScale = transform.lossyScale;
        go.transform.localScale = new Vector3(s / Mathf.Max(0.0001f, standScale.x), s / Mathf.Max(0.0001f, standScale.y), 1f);
        go.transform.position = transform.position + Vector3.up * DisplayLift - new Vector3(spriteBounds.center.x * s, spriteBounds.center.y * s, 0f);

        display = go.AddComponent<SpriteRenderer>();
        display.sprite = prefabSr.sprite;
        display.sortingOrder = DisplaySortingOrder;

        stands.Add(this);
        Refresh();
    }

    void OnEnable() => SpecialUpgrades.OnChanged += Refresh;
    void OnDisable() => SpecialUpgrades.OnChanged -= Refresh;
    void OnDestroy() => stands.Remove(this);

    // 고를 수 있는(무기가 놓여 있는) 거치대 중 InteractRange 안에서 플레이어와 가장 가까운 것이 나일 때만 true.
    private bool IsNearestInRange()
    {
        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return false;
            player = p.transform;
        }

        StartWeaponStand nearest = null;
        float best = InteractRange * InteractRange;
        foreach (StartWeaponStand stand in stands)
        {
            if (stand == null || stand.display == null || !stand.display.gameObject.activeInHierarchy) continue;
            float sqr = ((Vector2)(stand.transform.position - player.position)).sqrMagnitude;
            if (sqr <= best) { best = sqr; nearest = stand; }
        }
        return nearest == this;
    }

    private void Refresh()
    {
        if (display == null) return;

        bool unlocked = SpecialUpgrades.StartWeaponUnlocked;
        bool inHand = weaponSwitcher != null && weaponSwitcher.TryGetStartingWeaponType(out WeaponType current) && current == type;

        display.gameObject.SetActive(!inHand);
        display.color = unlocked ? Color.white : LockedColor;
        if (unlocked) EnsureInteractable();
    }

    // 해금된 뒤에만 테두리 + F 상호작용을 붙인다 (해금 전에 테두리가 뜨면 고를 수 있는 것처럼 보이므로).
    private void EnsureInteractable()
    {
        if (interactable) return;
        interactable = true;

        // InteractOutline은 충돌체가 필요하다 - 판정은 거리(IsNearestInRange)로 하니 물리에 걸리지 않는 트리거로 둔다.
        BoxCollider2D box = display.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;

        InteractOutline outline = display.gameObject.AddComponent<InteractOutline>();
        outline.inRangeCheck = IsNearestInRange;
        outline.Init(OnInteract);
    }

    private void OnInteract()
    {
        if (StageManager.IsInStage || EnemyManager.PlayerDead || PauseManager.IsPaused) return;
        if (!SpecialUpgrades.StartWeaponUnlocked) return;

        SpecialUpgrades.SetStartWeapon(type); // OnChanged -> WeaponSwitcher가 0번 슬롯을 바꾸고, 거치대들이 다시 그려진다
    }
}
