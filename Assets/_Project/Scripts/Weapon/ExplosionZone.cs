using UnityEngine;

// Grenade가 착탄한 자리에 생기는 붉고 반투명한 경고 범위.
// warningDuration(0.5초) 동안 보이기만 하다가, 시간이 다 되면 그 순간 범위 안에 있는 모든 적에게
// 한 번씩 데미지를 주고 스스로 사라진다.
[RequireComponent(typeof(SpriteRenderer))]
public class ExplosionZone : MonoBehaviour
{
    public float radius = 1.5f;         // 지름 3유닛(맵 타일 3개) = 반지름 1.5
    public float warningDuration = 0.5f; // 경고 범위가 유지되는 시간

    private float damage;
    private float timer;

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

    // Grenade가 폭발 위치에 이 컴포넌트를 생성한 직후 호출해서 데미지를 넘겨준다.
    public void Init(float explosionDamage)
    {
        damage = explosionDamage;
        timer = warningDuration;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Detonate();
        }
    }

    private void Detonate()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null) continue;

            Vector2 knockDir = (Vector2)hit.transform.position - (Vector2)transform.position;
            if (knockDir.sqrMagnitude < 0.0001f) knockDir = Vector2.up;
            enemy.Hit(knockDir.normalized, damage);
        }

        Destroy(gameObject);
    }

    // 지름 1유닛짜리 원 모양 스프라이트를 코드로 직접 그려서 만든다 (별도 원형 이미지 에셋이 없어도 되게).
    private static Sprite GetCircleSprite()
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
