using System.Collections.Generic;
using UnityEngine;

// 중독된 적(독구름 안에서 독 피해를 받는 중) 몸에서 초록 거품이 올라와 터지는 도트 애니메이션 (PixelFxLibrary의 "poison_status").
// 독구름이 피해를 줄 때마다 Show로 표시 시간을 갱신하고, 구름을 벗어나 갱신이 끊기면 잠시 뒤 사라진다.
// 적 한 마리에 하나만 붙는다 (구름이 여러 개 겹쳐도). 프레임은 직접 넘겨 가며 그린다 (PixelFx 풀의 동시 재생 상한과 무관).
// 그림이 없으면 아무것도 만들지 않는다. 적 프리팹은 건드리지 않는다.
public class PoisonIndicator : MonoBehaviour
{
    private const string ClipId = "poison_status";

    private static readonly Dictionary<Enemy, PoisonIndicator> active = new Dictionary<Enemy, PoisonIndicator>();

    private Enemy owner;
    private SpriteRenderer ownerSprite;
    private SpriteRenderer sr;
    private Sprite[] frames;
    private float fps;
    private float framePixels;
    private float time;
    private float remaining;

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 목록이 남지 않게 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
    }

    // 이 적이 지금 중독 중임을 duration초 동안 보여 준다 (이미 보이는 중이면 시간만 늘린다).
    public static void Show(Enemy enemy, float duration)
    {
        if (enemy == null || enemy.IsDying) return;
        if (active.TryGetValue(enemy, out PoisonIndicator existing) && existing != null)
        {
            existing.remaining = Mathf.Max(existing.remaining, duration);
            return;
        }

        Sprite[] frames = PixelFx.GetFrames(ClipId, out float fps, out float framePixelWidth);
        if (frames == null || frames.Length == 0) return;

        GameObject go = new GameObject("PoisonIndicator");
        PoisonIndicator p = go.AddComponent<PoisonIndicator>();
        p.owner = enemy;
        p.ownerSprite = enemy.GetComponent<SpriteRenderer>();
        p.sr = go.AddComponent<SpriteRenderer>();
        p.frames = frames;
        p.fps = fps;
        p.framePixels = framePixelWidth;
        p.time = Random.value * frames.Length / fps; // 여러 마리가 같은 박자로 움직이지 않게
        p.remaining = duration;
        p.sr.sprite = frames[0];
        if (p.ownerSprite != null)
        {
            p.sr.sortingLayerID = p.ownerSprite.sortingLayerID;
            p.sr.sortingOrder = p.ownerSprite.sortingOrder + 3;
        }
        active[enemy] = p;
        p.Follow();
    }

    void OnDestroy()
    {
        if (owner != null && active.TryGetValue(owner, out PoisonIndicator current) && current == this) active.Remove(owner);
    }

    void LateUpdate()
    {
        remaining -= Time.deltaTime;
        if (owner == null || owner.IsDying || remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        time += Time.deltaTime;
        sr.sprite = frames[(int)(time * fps) % frames.Length];
        Follow();
    }

    // 거품이 몸 한가운데에서 올라오게 두고, 가로 크기는 적 몸 폭에 맞춘다 (큰 정예는 크게).
    private void Follow()
    {
        Vector3 center = ownerSprite != null ? ownerSprite.bounds.center : owner.transform.position;
        float bodyWidth = ownerSprite != null ? Mathf.Clamp(ownerSprite.bounds.size.x * 0.9f, 0.3f, 0.9f) : 0.4f;
        transform.position = new Vector3(center.x, center.y, owner.transform.position.z);
        transform.localScale = Vector3.one * (bodyWidth / framePixels);
    }
}
