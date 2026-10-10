using System.Collections.Generic;
using UnityEngine;

// 검의 구리(범위) 특수효과 "검기". 칼날에서 정해진 방향으로 쭉 날아가며 지나가는 적을 전부 관통해서 한 번씩 때린다.
// 코드로 만드는 임시 오브젝트다 (프리팹/스프라이트 없음): 파란 원 스프라이트를 런타임에 한 번 생성해서 쓴다.
// 최대 사거리(range)만큼 날아가면 사라진다.
public class SwordWave : MonoBehaviour
{
    private static Sprite circleSprite;

    private SwordAttack owner;      // 기절/처치 효과를 돌려줄 검 (그 사이 버려져서 사라졌으면 null)
    private Vector2 direction;
    private float speed;
    private float radius;
    private float damage;
    private bool isCrit;
    private float knockbackMultiplier;
    private float remainingDistance;

    // 이 검기가 이미 맞힌 적 (관통하면서 같은 적을 여러 번 때리지 않게).
    private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

    public Vector2 Direction => direction;

    /// <summary>검기 하나를 만들어 날려 보낸다. angleDegrees는 월드 기준 진행 방향 각도.</summary>
    public static SwordWave Spawn(SwordAttack owner, Vector2 position, float angleDegrees, float speed, float radius,
        float range, float damage, bool isCrit, float knockbackMultiplier, Color color, SpriteRenderer sortingSource)
    {
        GameObject go = new GameObject("SwordWave");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * (radius * 2f); // 스프라이트 지름이 1유닛이라 반지름x2

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetCircleSprite();
        sr.color = color;
        if (sortingSource != null)
        {
            sr.sortingLayerID = sortingSource.sortingLayerID;
            sr.sortingOrder = sortingSource.sortingOrder;
        }

        SwordWave wave = go.AddComponent<SwordWave>();
        wave.owner = owner;
        float rad = angleDegrees * Mathf.Deg2Rad;
        wave.direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        wave.speed = speed;
        wave.radius = radius;
        wave.damage = damage;
        wave.isCrit = isCrit;
        wave.knockbackMultiplier = knockbackMultiplier;
        wave.remainingDistance = range;
        return wave;
    }

    void Update()
    {
        Step(Time.deltaTime);
    }

    private void Step(float dt)
    {
        float move = Mathf.Min(speed * dt, remainingDistance);
        transform.position += (Vector3)(direction * move);
        remainingDistance -= move;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null || enemy.IsDying || hitEnemies.Contains(enemy)) continue;

            hitEnemies.Add(enemy);
            enemy.Hit(direction, damage, isCrit, knockbackMultiplier);
            if (owner != null) owner.OnWaveHit(enemy, isCrit);
        }

        if (remainingDistance <= 0f) Destroy(gameObject);
    }

    // 지름 1유닛짜리 흰색 원 (가장자리는 살짝 부드럽게). 색은 SpriteRenderer.color로 입힌다.
    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        float center = (size - 1) * 0.5f;
        float half = size * 0.5f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / half;
                float alpha = Mathf.Clamp01((1f - d) * 6f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }
}
