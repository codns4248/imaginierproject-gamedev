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

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            currentIndex = (currentIndex + 1) % weaponSlots.Length;
            ApplyCurrentSlot();
        }
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
