using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어가 가질 수 있는 무기 슬롯(최대 5칸)을 관리한다.
// Q를 누르면 1 -> 2 -> 3 -> 4 -> 5 -> 1 순서로 다음 슬롯으로 넘어가고,
// 그 슬롯에 무기가 연결되어 있으면 그 무기만 활성화하고 나머지는 비활성화한다.
// 슬롯이 비어있으면(연결된 무기가 없으면) 아무것도 활성화되지 않아 자연스럽게 "빈손" 상태가 된다.
// 지금은 1번 슬롯 = Pistol, 2번 슬롯 = Sword만 채워져 있고 3~5번은 비어있는 임시 상태.
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
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            currentIndex = (currentIndex + 1) % weaponSlots.Length;
            ApplyCurrentSlot();
        }
    }

    // 현재 선택된 슬롯의 무기만 켜고 나머지는 전부 끈다.
    private void ApplyCurrentSlot()
    {
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            if (weaponSlots[i] != null)
            {
                weaponSlots[i].SetActive(i == currentIndex);
            }
        }
    }
}
