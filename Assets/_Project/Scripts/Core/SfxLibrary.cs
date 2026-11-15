using System;
using System.Collections.Generic;
using UnityEngine;

// 효과음 이름. Resources/SfxLibrary.asset의 entries에서 id 숫자로 저장되므로 순서를 바꾸지 말고 새 소리는 맨 뒤에 추가한다.
public enum SfxId
{
    SwordSwing, LanceThrust, PistolShot, SmgShot, GrenadeThrow, GrenadeExplode, GravityHum, BurnIgnite, PoisonCloud,
    Thunder, EnemyHit, EnemyCrit, EnemyDie, EliteDie, PlayerHurt, PlayerDie, PlayerRevive, ShieldBlock, DroneBeep,
    MortarFire, EnemyShot, PickupResource, PickupRare, PickupXp, LevelUp, CardSelect, PotionUse, WeaponPickup,
    EnhanceSuccess, StageClear, PortalEnter, PortalMystery, Escape, Deny, UiClick, UiOpen, RainLoop, WindLoop,
    MerchantBuy,
    AoeBlast, SweepWindup, SweepImpact, BurrowDive, GroundErupt, ChargeWindup, ChargeDash, SplitBurst, BeeSwarm,
}

// 게임 효과음 목록. Resources/SfxLibrary.asset 하나에 소리 파일·볼륨·같은 소리 최소 간격을 모아 두고, 코드에서는 Sfx.Play(SfxId.xxx)로 재생한다.
// 새 효과음: SfxId 맨 뒤에 이름 추가 -> 에셋 entries에 항목 추가(파일 연결) -> 재생할 곳에서 Sfx.Play.
// 소리 파일은 팀 규칙대로 Assets/ThirdParty/sfx/ 에 sfx_<분류>_<대상>_<동작>.wav 로 둔다.
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "뱀서라이크/효과음 목록")]
public class SfxLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.5f;
        public float minInterval = 0.04f; // 같은 소리가 이 시간(초) 안에 다시 울리지 않게 (자동공격/다수 피격이 겹칠 때 시끄럽지 않게)
    }

    [Range(0f, 1f)] public float masterVolume = 0.8f; // 모든 효과음에 곱해지는 전체 볼륨
    public Entry[] entries = new Entry[0];

    private Dictionary<SfxId, Entry> lookup;

    public Entry Get(SfxId id)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<SfxId, Entry>();
            foreach (Entry e in entries) if (e != null) lookup[e.id] = e;
        }
        return lookup.TryGetValue(id, out Entry entry) ? entry : null;
    }

    private static SfxLibrary instance;
    private static bool loaded;

    public static SfxLibrary Instance
    {
        get
        {
            if (!loaded)
            {
                instance = Resources.Load<SfxLibrary>("SfxLibrary");
                loaded = true;
            }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        loaded = false;
    }
}
