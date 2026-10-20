using UnityEngine;

// Grenade가 착탄한 자리에 생기는 붉고 반투명한 경고 범위.
// 딜레이 동안 보이기만 하다가, 시간이 다 되면 그 순간 범위 안에 있는 모든 적에게
// 한 번씩 데미지를 주고 스스로 사라진다. 수류탄 한 발(GrenadeShot)로 초기화되면 피해와 특수효과(바운스, 연쇄폭발,
// 독구름, 화상)는 던진 무기의 GrenadeAttack.Detonate가 처리하고, 중력탄이면 딜레이 동안 적을 끌어당긴다.
[RequireComponent(typeof(SpriteRenderer))]
public class ExplosionZone : MonoBehaviour
{
    public float radius = 1.5f;         // 지름 3유닛(맵 타일 3개) = 반지름 1.5
    public float warningDuration = 0.5f; // 경고 범위가 유지되는 시간

    // 데미지를 주는 순간(스스로 사라지기 직전) 불린다. 수류탄 투사체가 이 순간에 같이 사라지게 하는 데 쓴다.
    public System.Action Detonated;

    private float damage;
    private bool isCrit;
    private float timer;

    // 수류탄이 넘겨준 한 발의 정보 (Init(GrenadeShot)). 있으면 터질 때 피해/특수효과를 던진 무기(GrenadeAttack)가 처리한다.
    private GrenadeShot shot;

    // 중력탄(수류탄 구리 강화): 경고 시간 동안 이 반경 안의 적을 가운데로 끌어당긴다. 0이면 끌어당기지 않는다.
    // 끌어당기는 범위는 보라색 원으로 보여주며, 터질 때까지 점점 폭발 범위 크기로 줄어든다.
    private float pullRadius;
    private float totalDelay;
    private Transform pullRing;

    private static Sprite cachedCircleSprite; // 매번 새로 안 만들고 한 번 만든 걸 재사용한다

    void Awake()
    {
        var sr = GetComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = new Color(1f, 0f, 0f, 0.4f); // 붉고 반투명
        sr.sortingOrder = 3; // 바닥보다 위, 캐릭터/적보다는 아래

        // 스프라이트는 지름 1유닛짜리로 만들어뒀으니, 원하는 지름(반지름*2)만큼 늘려서 크기를 맞춘다.
        transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
    }

    // Grenade가 폭발 위치에 이 컴포넌트를 생성한 직후 호출해서 데미지(치명타 여부 포함)를 넘겨준다.
    // radiusOverride/delayOverride가 0보다 크면 프리팹 값 대신 그 값을 쓴다 (강화로 폭발 범위/터지기까지의 딜레이가 바뀌는 무기용).
    public void Init(float explosionDamage, bool critical = false, float radiusOverride = -1f, float delayOverride = -1f)
    {
        damage = explosionDamage;
        isCrit = critical;
        if (radiusOverride > 0f)
        {
            radius = radiusOverride;
            transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        }
        timer = delayOverride > 0f ? delayOverride : warningDuration;
        totalDelay = timer;
    }

    // 수류탄 한 발로 초기화한다: 반지름/딜레이는 그 발의 값, 피해와 특수효과는 터질 때 던진 무기가 처리한다.
    public void Init(GrenadeShot grenadeShot)
    {
        shot = grenadeShot;
        Init(shot.damage, false, shot.radius, shot.delay);

        pullRadius = shot.owner != null ? shot.owner.PullRadiusFor(shot) : 0f;
        if (pullRadius > radius) CreatePullRing();
    }

    private void CreatePullRing()
    {
        GameObject go = new GameObject("PullRing");
        go.transform.SetParent(transform, false);
        var ringSr = go.AddComponent<SpriteRenderer>();
        ringSr.sprite = GetCircleSprite();
        ringSr.color = new Color(0.55f, 0.3f, 1f, 0.22f);
        ringSr.sortingOrder = 2; // 붉은 경고 범위 바로 아래
        pullRing = go.transform;
        UpdatePullRing();
    }

    // 끌어당기는 범위 원: 처음엔 pullRadius 크기, 터지는 순간 폭발 범위 크기가 되도록 줄어든다 (부모가 폭발 지름만큼 늘어나 있으니 비율로 준다).
    private void UpdatePullRing()
    {
        float t = totalDelay > 0f ? 1f - Mathf.Clamp01(timer / totalDelay) : 1f;
        float scale = Mathf.Lerp(pullRadius / radius, 1f, t);
        pullRing.localScale = new Vector3(scale, scale, 1f);
    }

    void Update()
    {
        timer -= Time.deltaTime;

        if (pullRing != null && shot != null && shot.owner != null)
        {
            shot.owner.PullEnemies(transform.position, pullRadius);
            UpdatePullRing();
        }

        if (timer <= 0f)
        {
            Detonate();
        }
    }

    private void Detonate()
    {
        if (shot != null && shot.owner != null)
        {
            shot.owner.Detonate(transform.position, shot);
            Detonated?.Invoke();
            Destroy(gameObject);
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null) continue;

            Vector2 knockDir = (Vector2)hit.transform.position - (Vector2)transform.position;
            if (knockDir.sqrMagnitude < 0.0001f) knockDir = Vector2.up;
            enemy.Hit(knockDir.normalized, damage, isCrit);
        }

        Detonated?.Invoke();
        Destroy(gameObject);
    }

    // 지름 1유닛짜리 원 모양 스프라이트를 코드로 직접 그려서 만든다 (별도 원형 이미지 에셋이 없어도 되게).
    // 원거리 몬스터 투사체(EnemyProjectile) 등 다른 곳에서도 같은 붉은 원 모양을 재사용할 수 있게 공개해둔다.
    public static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float r = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                tex.SetPixel(x, y, dist <= r ? Color.white : Color.clear);
            }
        }
        tex.Apply();

        cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return cachedCircleSprite;
    }
}
