using UnityEngine;

// 표식이 걸린 적의 머리 위에 떠 있는 조준 아이콘 (붉은 원 + 십자). 코드로 만든 임시 스프라이트를 쓴다.
// 적을 따라다니고, 표식이 풀리거나 적이 사라지면 스스로 없어진다. 적 프리팹은 건드리지 않는다.
public class MarkIndicator : MonoBehaviour
{
    private static Sprite reticleSprite;

    private Enemy owner;
    private SpriteRenderer ownerSprite;
    private SpriteRenderer sr;

    public static MarkIndicator Create(Enemy owner)
    {
        GameObject go = new GameObject("MarkIndicator");
        MarkIndicator mark = go.AddComponent<MarkIndicator>();
        mark.owner = owner;
        mark.ownerSprite = owner.GetComponent<SpriteRenderer>();

        mark.sr = go.AddComponent<SpriteRenderer>();
        Sprite art = PixelFxLibrary.GetSprite("mark");
        mark.sr.sprite = art != null ? art : GetReticleSprite();
        mark.sr.color = art != null ? Color.white : new Color(1f, 0.25f, 0.2f, 0.95f);
        if (mark.ownerSprite != null)
        {
            mark.sr.sortingLayerID = mark.ownerSprite.sortingLayerID;
            mark.sr.sortingOrder = mark.ownerSprite.sortingOrder + 5;
        }

        mark.Follow();
        return mark;
    }

    void LateUpdate()
    {
        if (owner == null || owner.IsDying || !owner.IsMarked)
        {
            Destroy(gameObject);
            return;
        }
        Follow();
    }

    // 적 스프라이트 위쪽에 일정한 월드 크기로 띄운다 (적마다 스케일이 달라도 같은 크기로 보이게 부모로 두지 않는다).
    private void Follow()
    {
        float top = ownerSprite != null ? ownerSprite.bounds.max.y : owner.transform.position.y;
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 9f);

        transform.position = new Vector3(owner.transform.position.x, top + 0.22f, owner.transform.position.z);
        transform.localScale = Vector3.one * (0.3f * pulse);
        transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
    }

    // 64x64 흰색 원(고리) + 십자 눈금. 색은 SpriteRenderer.color로 입힌다. 지름 1유닛.
    private static Sprite GetReticleSprite()
    {
        if (reticleSprite != null) return reticleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        float center = (size - 1) * 0.5f;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f); // 0(중심)~1(가장자리)

                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.68f) / 0.09f);        // 고리
                float cross = (Mathf.Abs(dx) < 2.2f || Mathf.Abs(dy) < 2.2f) && d < 0.95f && d > 0.3f ? 1f : 0f; // 십자 눈금(중심은 비워둔다)
                float dot = d < 0.1f ? 1f : 0f;                                        // 중심 점
                float a = Mathf.Max(ring, Mathf.Max(cross, dot));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        reticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        reticleSprite.hideFlags = HideFlags.HideAndDontSave;
        return reticleSprite;
    }
}
