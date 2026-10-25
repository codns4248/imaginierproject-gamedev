using UnityEngine;

// 기절한 적의 머리 위에서 흔들리는 별 (MarkIndicator와 같은 방식: 적을 따라다니고, 기절이 끝나거나 적이 사라지면 스스로 없어진다).
// 적 프리팹은 건드리지 않는다. 그림이 없으면 아무것도 만들지 않는다.
public class StunIndicator : MonoBehaviour
{
    private Enemy owner;
    private SpriteRenderer ownerSprite;

    public static StunIndicator Create(Enemy owner)
    {
        Sprite art = PixelFxLibrary.GetSprite("stun_stars");
        if (art == null) return null;

        GameObject go = new GameObject("StunIndicator");
        StunIndicator s = go.AddComponent<StunIndicator>();
        s.owner = owner;
        s.ownerSprite = owner.GetComponent<SpriteRenderer>();

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art;
        if (s.ownerSprite != null)
        {
            sr.sortingLayerID = s.ownerSprite.sortingLayerID;
            sr.sortingOrder = s.ownerSprite.sortingOrder + 5;
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
        Follow();
    }

    private void Follow()
    {
        float top = ownerSprite != null ? ownerSprite.bounds.max.y : owner.transform.position.y;
        float sway = Mathf.Sin(Time.time * 8f) * 0.04f;
        transform.position = new Vector3(owner.transform.position.x + sway, top + 0.12f, owner.transform.position.z);
        transform.localScale = Vector3.one * 0.55f;
    }
}
