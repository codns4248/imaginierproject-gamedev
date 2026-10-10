using System;
using UnityEngine;

// 이 무기 오브젝트가 어떤 WeaponType인지 표시해둔다. 손에 들고 있는 무기를 바닥에 버릴 때
// (WeaponSwitcher.DropCurrentWeapon) 어떤 종류의 WeaponPickup을 만들어야 하는지 판단하는 데 쓴다.
//
// 또한 이 무기 "한 자루"의 강화 레벨(자원 5종류별)을 들고 있다. 같은 종류의 무기를 여러 자루 들어도 각자 따로
// 강화되고, 새로 얻은 무기는 0에서 시작한다. 레벨은 런(원정) 동안만 의미가 있는 값이라 저장하지 않는다
// (사망/추출로 런이 끝나면 WeaponEnhanceStore.ResetForNewRun이 비운다). 읽고 쓰는 건 WeaponEnhanceStore를 거친다.
public class WeaponIdentity : MonoBehaviour
{
    public WeaponType type;

    [NonSerialized] private int[] enhanceLevels;

    // WeaponEnhanceUtil.IndexOf(자원) 순서로 저장된다. 처음 쓸 때 만들어서 직렬화/초기화 순서에 영향받지 않게 한다.
    private int[] Levels => enhanceLevels ?? (enhanceLevels = new int[WeaponEnhanceUtil.AllTypes.Length]);

    public int GetLevel(int index) => index >= 0 && index < Levels.Length ? Levels[index] : 0;

    public void SetLevel(int index, int level)
    {
        if (index >= 0 && index < Levels.Length) Levels[index] = level;
    }

    public void ResetLevels() => Array.Clear(Levels, 0, Levels.Length);

    // 버릴 때 바닥 아이템에 실어 보낼 복사본.
    public int[] CopyLevels() => (int[])Levels.Clone();

    // 줍거나 지급받을 때 실려 온 레벨을 그대로 덮어쓴다 (null이면 0레벨).
    public void SetLevels(int[] source)
    {
        ResetLevels();
        if (source == null) return;
        Array.Copy(source, Levels, Math.Min(source.Length, Levels.Length));
    }
}
