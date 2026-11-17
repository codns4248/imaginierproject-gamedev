using System.Collections.Generic;
using UnityEngine;

// 효과음 재생기. 어디서든 Sfx.Play(SfxId.EnemyHit) 한 줄로 부른다 (소리 파일/볼륨은 SfxLibrary 에셋에 있다).
// - 2D 소리(거리 감쇠 없음)를 미리 만들어 둔 AudioSource 여러 개로 돌려 쓴다.
// - 같은 소리가 매번 똑같이 들리지 않게 음높이를 살짝(±pitchJitter) 흔든다.
// - 같은 소리는 항목의 minInterval 안에 다시 울리지 않는다 (자동공격/다수 피격이 겹쳐 시끄러워지는 것 방지).
// 반복 재생(빗소리 등)은 GetClip/GetVolume으로 클립과 볼륨만 받아서 각자 AudioSource로 돌린다.
public static class Sfx
{
    private const int PoolSize = 16;

    private static AudioSource[] pool;
    private static int next;
    private static readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();

    public static void Play(SfxId id, float volumeScale = 1f, float pitchJitter = 0.06f)
    {
        SfxLibrary lib = SfxLibrary.Instance;
        SfxLibrary.Entry e = lib != null ? lib.Get(id) : null;
        if (e == null || e.clip == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < e.minInterval) return;
        lastPlayed[id] = now;

        EnsurePool();
        AudioSource src = pool[next];
        next = (next + 1) % pool.Length;

        src.Stop();
        src.clip = e.clip;
        src.volume = Mathf.Clamp01(e.volume * lib.masterVolume * volumeScale);
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.Play();
    }

    /// <summary>음높이를 직접 정해서 재생한다 (흔들지 않음). 결과 화면 숫자 합산처럼 점점 올라가는 소리용.</summary>
    public static void PlayAtPitch(SfxId id, float pitch, float volumeScale = 1f)
    {
        SfxLibrary lib = SfxLibrary.Instance;
        SfxLibrary.Entry e = lib != null ? lib.Get(id) : null;
        if (e == null || e.clip == null) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < e.minInterval) return;
        lastPlayed[id] = now;

        EnsurePool();
        AudioSource src = pool[next];
        next = (next + 1) % pool.Length;

        src.Stop();
        src.clip = e.clip;
        src.volume = Mathf.Clamp01(e.volume * lib.masterVolume * volumeScale);
        src.pitch = pitch;
        src.Play();
    }

    public static AudioClip GetClip(SfxId id)
    {
        SfxLibrary lib = SfxLibrary.Instance;
        SfxLibrary.Entry e = lib != null ? lib.Get(id) : null;
        return e != null ? e.clip : null;
    }

    public static float GetVolume(SfxId id)
    {
        SfxLibrary lib = SfxLibrary.Instance;
        SfxLibrary.Entry e = lib != null ? lib.Get(id) : null;
        return e != null ? Mathf.Clamp01(e.volume * lib.masterVolume) : 0f;
    }

    private static void EnsurePool()
    {
        if (pool != null && pool[0] != null) return;

        GameObject go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f; // 2D
            pool[i] = s;
        }
        next = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pool = null;
        next = 0;
        lastPlayed.Clear();
    }
}
