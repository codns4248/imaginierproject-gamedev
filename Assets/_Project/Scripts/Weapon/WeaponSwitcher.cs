using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어가 가질 수 있는 무기 슬롯(최대 5칸)을 관리한다.
// Q를 누르면 1 -> 2 -> 3 -> 4 -> 5 -> 1 순서로 다음 슬롯을 검사하되, 비어있는 슬롯은 건너뛰고
// 무기가 실제로 있는 슬롯에서만 멈춘다(CycleToNextWeapon 참고). 슬롯이 비어있으면(연결된 무기가
// 없으면) 아무것도 들지 않은 "빈손" 상태가 된다.
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
        if (EnemyManager.PlayerDead) return; // 죽은 상태에서는 전환/장착/드랍 전부 막는다

        // 무기 전환(Q)만 ESC 메뉴가 열렸을 때만 막는다 - 강화 팝업(EnhancementWeaponIcon 참고)은
        // 열려있는 동안에도 Q로 무기를 바꿔가며 다른 무기를 강화할 수 있어야 하기 때문.
        if (!PauseManager.IsEscPaused && Keyboard.current.qKey.wasPressedThisFrame)
        {
            CycleToNextWeapon();
        }

        // 줍기/버리기는 강화 팝업이 떠 있을 때도 막는다(패널 보면서 바닥 무기를 만질 이유가 없음).
        if (PauseManager.IsPaused) return;

        // 줍기 대상이 실제로 있을 때만 F를 "가져간다" - 그래야 근처에 상인 상품이 같이 있어도
        // F 한 번에 상인 구매까지 같이 발동하는 중복 반응이 안 생긴다 (InteractInput 참고).
        if (Keyboard.current.fKey.wasPressedThisFrame
            && WeaponPickup.FindNearestInRange(transform.position) != null
            && InteractInput.TryConsumeFKey())
        {
            TryPickUpNearestWeapon();
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            DropCurrentWeapon();
        }
    }

    // Q 입력 처리: currentIndex 다음 슬롯부터 한 바퀴 돌며 무기가 있는 첫 슬롯을 찾아 장착한다.
    // 빈 슬롯은 그냥 건너뛴다 - 예전엔 인덱스만 +1 하고 ApplyCurrentSlot()을 불러서, 빈 슬롯을
    // 선택한 채로 "빈손" 상태가 되는 게 정상이었다. 무기가 하나도 없으면(FindNextOccupiedSlot이
    // -1) 아무 것도 하지 않고 조용히 끝낸다(요구사항: 무기 0개일 때 무한루프/오류 없이 안전 종료).
    private void CycleToNextWeapon()
    {
        int next = FindNextOccupiedSlot(currentIndex);
        if (next < 0) return; // 무기가 하나도 없음 - 빈손 상태 그대로 유지

        currentIndex = next;
        ApplyCurrentSlot();
    }

    // fromIndex 바로 다음 슬롯부터 한 바퀴(최대 weaponSlots.Length번) 돌며 무기가 있는 첫 슬롯의
    // 인덱스를 찾는다. fromIndex 자신도 마지막 후보로 포함되므로, 무기가 정확히 1개뿐이고 그게
    // 지금 들고 있는 무기라면(혹은 currentIndex 슬롯이 비어버렸어도 다른 유효한 무기가 없다면)
    // 제자리로 돌아와 같은 결과가 된다. 전부 비어있으면 -1.
    private int FindNextOccupiedSlot(int fromIndex)
    {
        int length = weaponSlots.Length;
        for (int step = 1; step <= length; step++)
        {
            int idx = (fromIndex + step) % length;
            if (weaponSlots[idx] != null) return idx;
        }
        return -1;
    }

    private void TryPickUpNearestWeapon()
    {
        WeaponPickup nearest = WeaponPickup.FindNearestInRange(transform.position);
        if (nearest == null) return;

        if (TryGiveWeapon(nearest.weaponType))
            Destroy(nearest.gameObject);
    }

    /// <summary>바닥 픽업을 거치지 않고 빈 슬롯에 무기를 바로 지급한다 (상인 구매 등에서 사용).
    /// 빈 슬롯이 없으면 아무 일도 하지 않고 false.</summary>
    public bool TryGiveWeapon(WeaponType type)
    {
        int emptyIndex = FindFirstEmptySlot();
        if (emptyIndex < 0) return false; // 빈 슬롯이 없으면 지급하지 않는다

        GameObject prefab = WeaponPickup.GetPrefab(type);
        if (prefab == null) return false;

        GameObject weaponGO = Instantiate(prefab, transform);
        weaponSlots[emptyIndex] = weaponGO;
        ApplyCurrentSlot();
        return true;
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

    // 사망 시 호출: 0번 슬롯(처음부터 들고 있던 무기)만 남기고, 필드에서 줍거나 상인에게 산
    // 나머지 무기는 전부 사라진다 (자원/회복약/무기강화와 같은 규칙 - 이번 런에서 얻은 건 잃는다).
    public void ResetToStartingWeapon()
    {
        for (int i = 1; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] == null) continue;
            Destroy(weaponSlots[i]);
            weaponSlots[i] = null;
        }

        currentIndex = 0;
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
