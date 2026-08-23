using UnityEngine;

// 치명타 판정 공용 유틸. 각 무기는 자기만의 critChance/critMultiplier 필드를 갖고 있고
// (나중에 강화/아이템으로 이 값들이 바뀔 수 있음), 공격할 때마다 이 함수를 호출해서
// 이번 공격의 최종 데미지와 치명타 여부를 계산한다.
public static class CriticalHit
{
    public static float Roll(float baseDamage, float critChance, float critMultiplier, out bool isCrit)
    {
        isCrit = Random.value < critChance;
        return isCrit ? baseDamage * critMultiplier : baseDamage;
    }
}
