using UnityEngine;

// 무기 종류별 프리팹 모음. 드랍/장착 시 이 목록에서 프리팹을 찾아 인스턴스화한다.
// Resources 폴더에 두고 런타임에 Resources.Load로 불러오기 때문에 빌드에도 정상 포함된다.
// (ResourceIconSet.cs와 동일한 패턴)
[CreateAssetMenu(fileName = "WeaponCatalog", menuName = "뱀서라이크/무기 카탈로그")]
public class WeaponCatalog : ScriptableObject
{
    // WeaponType의 순서(Pistol, Sword, Smg, Lance, Grenade)와 정확히 맞춰서 채운다.
    public GameObject[] prefabs = new GameObject[5];

    public GameObject GetPrefab(WeaponType type)
    {
        int idx = (int)type;
        return idx < prefabs.Length ? prefabs[idx] : null;
    }
}
