using UnityEngine;

// 이 무기 오브젝트가 어떤 WeaponType인지 표시해둔다. 손에 들고 있는 무기를 바닥에 버릴 때
// (WeaponSwitcher.DropCurrentWeapon) 어떤 종류의 WeaponPickup을 만들어야 하는지 판단하는 데 쓴다.
public class WeaponIdentity : MonoBehaviour
{
    public WeaponType type;
}
