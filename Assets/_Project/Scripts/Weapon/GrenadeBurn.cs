using System.Collections.Generic;
using UnityEngine;

// 수류탄 기름 강화 "화염": 폭발에 맞은 적에게 붙는 화상. 폭발에 맞을 때마다 1중첩씩 쌓이고(최대 maxStacks),
// 지속시간이 갱신된다. 중첩 수만큼 일정 간격으로 피해가 들어간다 (경직/넉백 없음).
// canSpread(10강)인 화상은 불타는 동안 바로 옆 적에게 1중첩짜리 불을 옮기는데, 옮겨 붙은 불은 다시 옮기지 못한다
// (A -> B는 되지만 B -> C는 안 됨). 적 위에 따라다니는 불꽃 표시로 보여주며, 적 프리팹은 건드리지 않는다.
public class GrenadeBurn : MonoBehaviour
{
    private const float TickInterval = 0.5f;

    private static readonly Dictionary<Enemy, GrenadeBurn> burns = new Dictionary<Enemy, GrenadeBurn>();
    private static readonly List<Collider2D> spreadBuffer = new List<Collider2D>();
    private static ContactFilter2D triggerFilter = new ContactFilter2D { useTriggers = true };
    private static Sprite flameSprite;

    private Enemy target;
    private SpriteRenderer targetSprite;
    private SpriteRenderer sr;
    private Sprite[] frames;     // 불꽃 그림 프레임 (없으면 코드로 그린 불꽃)
    private float frameFps = 8f;
    private float framePixels = 1f;
    private int stacks;
    private int maxStacks;
    private float damagePerStackPerSecond;
    private float duration;
    private float timer;
    private float tickTimer;
    private bool canSpread;
    private float spreadRadius;

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 목록이 남지 않게 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        burns.Clear();
    }

    // 폭발에 직접 맞았을 때: 1중첩 추가 + 지속시간 갱신. canSpread면 이 불은 옆 적에게 옮겨 붙을 수 있다.
    public static void Ignite(Enemy enemy, float damagePerStackPerSecond, float duration, int maxStacks, bool canSpread, float spreadRadius)
    {
        if (enemy == null || enemy.IsDying) return;

        GrenadeBurn burn = GetOrCreate(enemy);
        burn.maxStacks = Mathf.Max(burn.maxStacks, maxStacks);
        burn.stacks = Mathf.Min(burn.maxStacks, burn.stacks + 1);
        burn.damagePerStackPerSecond = Mathf.Max(burn.damagePerStackPerSecond, damagePerStackPerSecond);
        burn.duration = Mathf.Max(burn.duration, duration);
        burn.timer = burn.duration;
        if (canSpread)
        {
            burn.canSpread = true;
            burn.spreadRadius = spreadRadius;
        }
    }

    // 다른 적에게서 옮겨 붙은 불: 아직 불타지 않는 적에게만 1중첩으로 붙고, 다시 옮기지는 못한다.
    private static void CatchFrom(GrenadeBurn source, Enemy enemy)
    {
        if (enemy == null || enemy.IsDying || burns.ContainsKey(enemy)) return;

        GrenadeBurn burn = GetOrCreate(enemy);
        burn.maxStacks = source.maxStacks;
        burn.stacks = 1;
        burn.damagePerStackPerSecond = source.damagePerStackPerSecond;
        burn.duration = source.duration;
        burn.timer = source.duration;
        burn.canSpread = false;
    }

    private static GrenadeBurn GetOrCreate(Enemy enemy)
    {
        if (burns.TryGetValue(enemy, out GrenadeBurn existing) && existing != null) return existing;

        GameObject go = new GameObject("GrenadeBurn");
        GrenadeBurn burn = go.AddComponent<GrenadeBurn>();
        burn.target = enemy;
        burn.targetSprite = enemy.GetComponent<SpriteRenderer>();
        burn.sr = go.AddComponent<SpriteRenderer>();
        burn.sr.sprite = GetFlameSprite();
        burn.sr.color = new Color(1f, 0.5f, 0.12f, 0.85f);
        burn.frames = PixelFx.GetFrames("burn_flame", out burn.frameFps, out burn.framePixels); // 그림이 있으면 그걸 쓴다
        if (burn.targetSprite != null)
        {
            burn.sr.sortingLayerID = burn.targetSprite.sortingLayerID;
            burn.sr.sortingOrder = burn.targetSprite.sortingOrder + 4;
        }
        burns[enemy] = burn;
        burn.Follow();
        return burn;
    }

    void OnDestroy()
    {
        if (target != null && burns.TryGetValue(target, out GrenadeBurn current) && current == this) burns.Remove(target);
    }

    void Update()
    {
        if (target == null || target.IsDying)
        {
            Destroy(gameObject);
            return;
        }

        timer -= Time.deltaTime;
        tickTimer += Time.deltaTime;
        if (tickTimer >= TickInterval)
        {
            tickTimer -= TickInterval;
            target.TakeDotDamage(stacks * damagePerStackPerSecond * TickInterval);
            if (canSpread) Spread();
        }

        if (timer <= 0f || target == null || target.IsDying)
        {
            Destroy(gameObject);
            return;
        }
        Follow();
    }

    // 바로 옆(spreadRadius 안)에 있는, 아직 불타지 않는 적에게 불을 옮긴다.
    private void Spread()
    {
        if (target == null || target.IsDying) return;

        int count = Physics2D.OverlapCircle(target.transform.position, spreadRadius, triggerFilter, spreadBuffer);
        for (int i = 0; i < count; i++)
        {
            Enemy other = spreadBuffer[i] != null ? spreadBuffer[i].GetComponent<Enemy>() : null;
            if (other == null || other == target) continue;
            CatchFrom(this, other);
        }
    }

    // 적 몸 한가운데에 일렁이는 불꽃을 띄운다. 중첩이 많을수록 크게 보인다 (적마다 스케일이 달라 부모로 두지 않는다).
    private void Follow()
    {
        Vector3 center = targetSprite != null ? targetSprite.bounds.center : target.transform.position;
        if (frames != null)
        {
            // 그림 불꽃: 프레임을 돌려 재생하고, 불꽃 밑동(그림의 기준점)을 적 몸 아래쪽에 둔다. 중첩이 많을수록 커진다.
            sr.sprite = frames[Mathf.FloorToInt(Time.time * frameFps + (GetInstanceID() & 7)) % frames.Length];
            sr.color = Color.white;
            float baseY = targetSprite != null ? targetSprite.bounds.center.y - targetSprite.bounds.extents.y * 0.5f : center.y;
            float s = (0.28f + 0.05f * stacks) / framePixels;
            transform.position = new Vector3(center.x, baseY, target.transform.position.z);
            transform.localScale = new Vector3(s, s, 1f);
            return;
        }

        float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 17f + GetInstanceID());
        float size = 0.2f + 0.04f * stacks;

        transform.position = new Vector3(center.x, center.y, target.transform.position.z);
        transform.localScale = new Vector3(size, size * flicker, 1f);
    }

    // 64x64 불꽃(아래는 둥글고 위로 뾰족한 물방울 모양). 색은 SpriteRenderer.color로 입힌다. 지름 1유닛.
    private static Sprite GetFlameSprite()
    {
        if (flameSprite != null) return flameSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f; // -0.5 ~ 0.5
                float v = (y + 0.5f) / size;        // 0(아래) ~ 1(위)

                // 아래쪽 원(중심 v=0.35, 반지름 0.3) + 위로 갈수록 좁아지는 꼬리
                float body = 0.3f - Mathf.Sqrt(u * u + (v - 0.35f) * (v - 0.35f));
                float tailWidth = Mathf.Lerp(0.3f, 0f, Mathf.InverseLerp(0.35f, 1f, v));
                float tail = v > 0.35f ? tailWidth - Mathf.Abs(u) : -1f;
                float edge = Mathf.Max(body, tail);
                float a = Mathf.Clamp01(edge / 0.06f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        flameSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        flameSprite.hideFlags = HideFlags.HideAndDontSave;
        return flameSprite;
    }
}
