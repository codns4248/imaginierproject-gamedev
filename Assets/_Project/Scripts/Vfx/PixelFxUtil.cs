using UnityEngine;

// 플레이어 위치에 도트 이펙트를 띄우는 작은 도우미 (레벨업, 회복, 부활 연출용).
public static class PixelFxUtil
{
    private static Transform player;

    public static Transform Player
    {
        get
        {
            if (player == null)
            {
                GameObject go = GameObject.Find("Player");
                if (go != null) player = go.transform;
            }
            return player;
        }
    }

    /// <summary>플레이어 발밑 근처에 이펙트를 한 번 재생한다. 플레이어보다 앞(위)에 그려진다.</summary>
    public static PixelFx PlayOnPlayer(string id, float worldWidth, Color tint)
    {
        Transform p = Player;
        if (p == null) return null;
        SpriteRenderer sr = p.GetComponent<SpriteRenderer>();
        int order = sr != null ? sr.sortingOrder + 3 : 9;
        return PixelFx.Play(id, (Vector2)p.position + Vector2.down * 0.1f, worldWidth, tint, order);
    }
}
