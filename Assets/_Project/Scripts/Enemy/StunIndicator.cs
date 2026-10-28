using UnityEngine;

// 기절한 적의 머리 위에서 별 세 개가 고리를 따라 빙빙 도는 도트 애니메이션 (MarkIndicator와 같은 방식: 적을 따라다니고,
// 기절이 끝나거나 적이 사라지면 스스로 없어진다). 프레임은 PixelFxLibrary의 "stun_stars_anim"을 직접 넘겨 가며 그린다
// (PixelFx 풀의 동시 재생 상한과 상관없이, 많은 적이 한꺼번에 기절해도 전부 보이게).
// 애니메이션이 없으면 예전 한 장짜리 별 그림("stun_stars")을 좌우로 흔든다. 둘 다 없으면 아무것도 만들지 않는다.
// 적 프리팹은 건드리지 않는다.
public class StunIndicator : MonoBehaviour
{
    private const string ClipId = "stun_stars_anim";
    private const float RingWidth = 0.45f;   // 별 고리의 가로 크기(월드 유닛)
    private const float AboveHead = 0.06f;   // 머리 꼭대기에서 고리 중심까지의 높이

    private Enemy owner;
    private SpriteRenderer ownerSprite;
    private SpriteRenderer sr;
    private Sprite[] frames;
    private float fps;
    private float time;

    public static StunIndicator Create(Enemy owner)
    {
        Sprite[] frames = PixelFx.GetFrames(ClipId, out float fps, out float framePixelWidth);
        Sprite still = PixelFxLibrary.GetSprite("stun_stars");
        if ((frames == null || frames.Length == 0) && still == null) return null;

        GameObject go = new GameObject("StunIndicator");
        StunIndicator s = go.AddComponent<StunIndicator>();
        s.owner = owner;
        s.ownerSprite = owner.GetComponent<SpriteRenderer>();
        s.sr = go.AddComponent<SpriteRenderer>();

        if (frames != null && frames.Length > 0)
        {
            s.frames = frames;
            s.fps = fps;
            s.time = Random.value * frames.Length / fps; // 여러 마리가 같은 박자로 돌지 않게
            s.sr.sprite = frames[0];
            go.transform.localScale = Vector3.one * (RingWidth / framePixelWidth);
        }
        else
        {
            s.sr.sprite = still;
            go.transform.localScale = Vector3.one * 0.55f;
        }

        if (s.ownerSprite != null)
        {
            s.sr.sortingLayerID = s.ownerSprite.sortingLayerID;
            s.sr.sortingOrder = s.ownerSprite.sortingOrder + 5;
        }
        s.Follow();
        return s;
    }

    void LateUpdate()
    {
        if (owner == null || owner.IsDying || !owner.IsStunned)
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

    private void Follow()
    {
        float top = ownerSprite != null ? ownerSprite.bounds.max.y : owner.transform.position.y;
        if (frames != null)
        {
            transform.position = new Vector3(owner.transform.position.x, top + AboveHead, owner.transform.position.z);
            return;
        }
        float sway = Mathf.Sin(Time.time * 8f) * 0.04f;
        transform.position = new Vector3(owner.transform.position.x + sway, top + 0.12f, owner.transform.position.z);
    }
}
