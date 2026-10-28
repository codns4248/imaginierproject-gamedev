using UnityEngine;

// 표식이 걸린 적의 머리 위에 떠 있는 조준 표시. 붉은 조준선이 돌면서 고리가 맥박치듯 커졌다 작아지는 도트 애니메이션
// (PixelFxLibrary의 "mark_anim")을 직접 넘겨 가며 그린다 (PixelFx 풀의 동시 재생 상한과 상관없이 전부 보이게).
// 애니메이션이 없으면 예전 한 장짜리 아이콘("mark", 그것도 없으면 코드로 그린 조준선)을 코드로 돌리고 키운다.
// 적을 따라다니고, 표식이 풀리거나 적이 사라지면 스스로 없어진다. 적 프리팹은 건드리지 않는다.
public class MarkIndicator : MonoBehaviour
{
    private const string ClipId = "mark_anim";
    private const float Size = 0.3f;        // 조준 표시의 가로 크기(월드 유닛)
    private const float AboveHead = 0.22f;  // 머리 꼭대기에서 표시 중심까지의 높이

    private static Sprite reticleSprite;

    private Enemy owner;
    private SpriteRenderer ownerSprite;
    private SpriteRenderer sr;
    private Sprite[] frames;
    private float fps;
    private float time;

    public static MarkIndicator Create(Enemy owner)
    {
        GameObject go = new GameObject("MarkIndicator");
        MarkIndicator mark = go.AddComponent<MarkIndicator>();
        mark.owner = owner;
        mark.ownerSprite = owner.GetComponent<SpriteRenderer>();
        mark.sr = go.AddComponent<SpriteRenderer>();

        Sprite[] frames = PixelFx.GetFrames(ClipId, out float fps, out float framePixelWidth);
        if (frames != null && frames.Length > 0)
        {
            mark.frames = frames;
            mark.fps = fps;
            mark.sr.sprite = frames[0];
            go.transform.localScale = Vector3.one * (Size / framePixelWidth);
        }
        else
        {
            Sprite art = PixelFxLibrary.GetSprite("mark");
            mark.sr.sprite = art != null ? art : GetReticleSprite();
            mark.sr.color = art != null ? Color.white : new Color(1f, 0.25f, 0.2f, 0.95f);
        }

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

        if (frames != null)
        {
            time += Time.deltaTime;
            sr.sprite = frames[(int)(time * fps) % frames.Length];
        }
        Follow();
    }

    // 적 스프라이트 위쪽에 일정한 월드 크기로 띄운다 (적마다 스케일이 달라도 같은 크기로 보이게 부모로 두지 않는다).
    private void Follow()
    {
        float top = ownerSprite != null ? ownerSprite.bounds.max.y : owner.transform.position.y;
        transform.position = new Vector3(owner.transform.position.x, top + AboveHead, owner.transform.position.z);
        if (frames != null) return; // 애니메이션이 회전/맥박을 직접 그린다

        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 9f);
        transform.localScale = Vector3.one * (Size * pulse);
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
