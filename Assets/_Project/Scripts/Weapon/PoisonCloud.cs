using System.Collections.Generic;
using UnityEngine;

// 수류탄 화학물질 강화 "독구름": 폭발 자리에 남는 초록 구름. 일정 간격으로 구름 안의 모든 적에게 피해를 준다.
// 구름끼리 겹치면 각 구름이 따로 피해를 줘서 겹친 만큼 강해진다 (연쇄폭발로 구름이 여러 개 생기면 가운데가 제일 아프다).
// spawnsOnDeath(10강)인 구름 안에서 적이 죽으면 그 자리에 작은 독구름이 생긴다. 이렇게 생긴 작은 구름은
// 다시 구름을 만들지 않는다 (A가 죽어 생긴 구름에서 B가 죽어도 또 생기지 않음). 프리팹 없이 코드로 만든다.
public class PoisonCloud : MonoBehaviour
{
    private const float TickInterval = 0.5f;
    private const int MaxClouds = 40; // 한 번에 떠 있을 수 있는 구름 수 (넘으면 새 구름은 생기지 않는다)
    private const float FadeTime = 0.3f;

    private static readonly List<PoisonCloud> active = new List<PoisonCloud>();
    private static readonly List<Collider2D> hitBuffer = new List<Collider2D>();
    private static ContactFilter2D triggerFilter = new ContactFilter2D { useTriggers = true };

    private static readonly Color CloudColor = new Color(0.45f, 0.95f, 0.25f, 0.32f);

    private SpriteRenderer sr;
    private float radius;
    private float damagePerSecond;
    private float duration;
    private float elapsed;
    private float tickTimer;
    private bool spawnsOnDeath;
    private float deathCloudRadiusScale;
    private float deathCloudDuration;

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 목록/구독이 겹치지 않게 다시 맞춘다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
        Enemy.Died -= OnEnemyDied;
        Enemy.Died += OnEnemyDied;
    }

    public static void Spawn(Vector2 position, float radius, float damagePerSecond, float duration,
        bool spawnsOnDeath, float deathCloudRadiusScale = 0f, float deathCloudDuration = 0f)
    {
        if (active.Count >= MaxClouds || radius <= 0f || duration <= 0f) return;

        GameObject go = new GameObject("PoisonCloud");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * (radius * 2f); // 원 스프라이트 지름이 1유닛

        PoisonCloud cloud = go.AddComponent<PoisonCloud>();
        cloud.sr = go.AddComponent<SpriteRenderer>();
        cloud.sr.sprite = ExplosionZone.GetCircleSprite();
        cloud.sr.sortingOrder = 3; // 바닥보다 위, 캐릭터/적보다는 아래 (폭발 경고 범위와 같은 층)
        // 그림(반복 재생되는 초록 구름)이 있으면 그걸 쓰고 원 스프라이트는 끈다. 그림 속 구름이 프레임의 약 85%라 그만큼 키워 반경에 맞춘다.
        cloud.fx = PixelFx.Play("poison_cloud", position, radius * 2f * 1.18f, new Color(1f, 1f, 1f, 0f), 3);
        if (cloud.fx != null) cloud.sr.enabled = false;
        cloud.radius = radius;
        cloud.damagePerSecond = damagePerSecond;
        cloud.duration = duration;
        cloud.spawnsOnDeath = spawnsOnDeath;
        cloud.deathCloudRadiusScale = deathCloudRadiusScale;
        cloud.deathCloudDuration = deathCloudDuration;
        cloud.ApplyAlpha();
        active.Add(cloud);
    }

    void OnDestroy()
    {
        active.Remove(this);
        if (fx != null) fx.Stop();
    }

    private PixelFx fx; // 반복 재생 중인 구름 그림 (없으면 예전 원 스프라이트를 쓴다)

    void Update()
    {
        elapsed += Time.deltaTime;
        tickTimer += Time.deltaTime;
        if (tickTimer >= TickInterval)
        {
            tickTimer -= TickInterval;
            Tick();
        }

        if (elapsed >= duration)
        {
            Destroy(gameObject);
            return;
        }
        ApplyAlpha();
    }

    private void Tick()
    {
        int count = Physics2D.OverlapCircle(transform.position, radius, triggerFilter, hitBuffer);
        for (int i = 0; i < count; i++)
        {
            Enemy enemy = hitBuffer[i] != null ? hitBuffer[i].GetComponent<Enemy>() : null;
            if (enemy == null || enemy.IsDying) continue;
            enemy.TakeDotDamage(damagePerSecond * TickInterval);
        }
    }

    // 처음과 끝에 살짝 페이드되고, 그 사이에는 천천히 일렁인다.
    private void ApplyAlpha()
    {
        float fade = Mathf.Min(1f, Mathf.Min(elapsed, duration - elapsed) / FadeTime);
        float wobble = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f + GetInstanceID());
        if (fx != null)
        {
            // 그림 구름은 불투명하게 그려져 있어서 반투명하게 깔고, 처음과 끝에 서서히 나타나고 사라진다.
            fx.SetColor(new Color(1f, 1f, 1f, 0.7f * Mathf.Clamp01(fade)));
            return;
        }

        Color c = CloudColor;
        c.a *= Mathf.Clamp01(fade) * wobble;
        sr.color = c;
    }

    // 적이 죽은 자리가 "죽으면 퍼지는" 구름 안이면 그 자리에 작은 구름을 하나 만든다 (구름이 여러 개 겹쳐 있어도 하나만).
    private static void OnEnemyDied(Enemy enemy)
    {
        if (enemy == null) return;
        Vector2 pos = enemy.transform.position;

        for (int i = 0; i < active.Count; i++)
        {
            PoisonCloud cloud = active[i];
            if (cloud == null || !cloud.spawnsOnDeath || cloud.elapsed >= cloud.duration) continue;
            if (((Vector2)cloud.transform.position - pos).sqrMagnitude > cloud.radius * cloud.radius) continue;

            Spawn(pos, cloud.radius * cloud.deathCloudRadiusScale, cloud.damagePerSecond, cloud.deathCloudDuration, false);
            return;
        }
    }
}
