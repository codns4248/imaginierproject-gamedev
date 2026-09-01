using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어가 가질 수 있는 무기 슬롯(최대 5칸)을 관리한다.
// Q를 누르면 1 -> 2 -> 3 -> 4 -> 5 -> 1 순서로 다음 슬롯으로 넘어가고,
// 그 슬롯의 무기를 "들고 있는" 상태로 바꾼다(WeaponAim.SetHeld).
// 슬롯이 비어있으면(연결된 무기가 없으면) 아무것도 들지 않은 "빈손" 상태가 된다.
//
// 예전에는 선택되지 않은 무기를 SetActive(false)로 완전히 꺼뒀지만, 이제는 선택되지 않은 무기도
// 자동공격을 위해 오브젝트/스크립트가 계속 켜져 있어야 하므로 항상 active 상태로 두고
// isHeld 값만 바꿔서 "들고 있는 무기 vs 자동공격 대기 중인 무기"를 구분한다.
//
// F키: 주변(WeaponPickup.interactRadius 안)에서 가장 가까운 무기 아이템을 1번 슬롯부터 순서대로
// 빈 슬롯에 장착한다. 빈 슬롯이 없으면 아무 일도 일어나지 않는다(바닥에 그대로 남음).
// B키: 지금 들고 있는(선택된) 슬롯의 무기를 그 자리에 드랍하고 슬롯을 비운다.
public class WeaponSwitcher : MonoBehaviour
{
    public GameObject[] weaponSlots = new GameObject[5];

    private int currentIndex;

    void Start()
    {
        ApplyCurrentSlot();
    }

    void Update()
    {
        if (PauseManager.IsPaused) return;
        if (EnemyManager.PlayerDead) return; // 죽은 상태에서는 전환/장착/드랍 전부 막는다

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            currentIndex = (currentIndex + 1) % weaponSlots.Length;
            ApplyCurrentSlot();
        }

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryPickUpNearestWeapon();
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            DropCurrentWeapon();
        }
    }

    private void TryPickUpNearestWeapon()
    {
        WeaponPickup nearest = WeaponPickup.FindNearestInRange(transform.position);
        if (nearest == null) return;

        int emptyIndex = FindFirstEmptySlot();
        if (emptyIndex < 0) return; // 빈 슬롯이 없으면 줍지 않는다

        GameObject prefab = WeaponPickup.GetPrefab(nearest.weaponType);
        if (prefab == null) return;

        GameObject weaponGO = Instantiate(prefab, transform);
        weaponSlots[emptyIndex] = weaponGO;

        Destroy(nearest.gameObject);
        ApplyCurrentSlot();
    }

    // 1번 슬롯부터 순서대로 비어있는 첫 슬롯의 인덱스를 찾는다 (없으면 -1).
    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null) return i;
        }
        return -1;
    }

    private void DropCurrentWeapon()
    {
        GameObject current = weaponSlots[currentIndex];
        if (current == null) return; // 빈 슬롯이면 버릴 게 없다

        var identity = current.GetComponent<WeaponIdentity>();
        if (identity != null)
        {
            WeaponPickup.SpawnDrop(transform.position, identity.type);
        }

        weaponSlots[currentIndex] = null;
        Destroy(current);
        ApplyCurrentSlot();
    }

    // 현재 선택된 슬롯의 무기만 "들고 있는" 상태로 만들고 나머지는 전부 자동공격 대기 상태로 돌린다.
    private void ApplyCurrentSlot()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null) continue;

            var aim = weaponSlots[i].GetComponent<WeaponAim>();
            if (aim != null) aim.SetHeld(i == currentIndex);
        }
    }
}
