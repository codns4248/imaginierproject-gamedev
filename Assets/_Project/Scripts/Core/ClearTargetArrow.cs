using UnityEngine;

// 스테이지를 클리어하면 맵 중앙에 모여서 나타나는 다음 층 포탈/상인/탈출 포탈 쪽을 가리키는 화살표.
// 플레이어 몸 둘레(OrbitRadius)에서 그 방향을 가리키며 살짝 앞뒤로 움직이고, 목표에 가까워지면(화면에 다 보이면) 숨는다.
// 포탈 묶음(ExitPortals)이 사라지거나(포탈을 타고 이동) 거점으로 돌아가면 같이 사라진다. 그림은 LevelUpConfig.guideArrow.
public class ClearTargetArrow : MonoBehaviour
{
    private const float OrbitRadius = 0.42f;    // 플레이어 몸 중심에서 화살표까지 거리 (월드)
    private const float HideDistance = 2.2f;    // 목표 지점에서 이 거리 안이면 숨긴다 (화면 세로 절반이 2.5)
    private const float ArrowWorldSize = 0.22f; // 화살표 긴 변 길이 (월드)
    private const float BobAmount = 0.05f;      // 가리키는 방향으로 앞뒤로 움직이는 폭
    private const float BobSpeed = 6f;
    private const int SortingOrder = 40;        // 몬스터/탄 위에 보이게

    private Transform group;
    private Vector2 target;
    private Transform player;
    private SpriteRenderer sr;

    /// <summary>포탈 묶음(portalGroup)이 살아 있는 동안 target 쪽을 가리키는 화살표를 띄운다.</summary>
    public static void Show(Transform portalGroup, Vector2 target)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;
        Sprite sprite = cfg != null ? cfg.guideArrow : null;
        if (sprite == null || portalGroup == null) return;

        GameObject go = new GameObject("ClearTargetArrow");
        ClearTargetArrow arrow = go.AddComponent<ClearTargetArrow>();
        arrow.group = portalGroup;
        arrow.target = target;
        arrow.sr = go.AddComponent<SpriteRenderer>();
        arrow.sr.sprite = sprite;
        arrow.sr.sortingOrder = SortingOrder;
        arrow.sr.enabled = false;

        float longSide = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y, 0.0001f);
        go.transform.localScale = Vector3.one * (ArrowWorldSize / longSide);
    }

    void LateUpdate()
    {
        if (group == null || !StageManager.IsInStage) { Destroy(gameObject); return; }

        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p == null) return;
            player = p.transform;
        }
        if (EnemyManager.PlayerDead) { sr.enabled = false; return; }

        // 플레이어 그림과 같이 움직이도록 (보간된) 트랜스폼 위치를 기준으로 한다.
        Vector2 from = player.position;
        Vector2 to = target - from;
        float dist = to.magnitude;
        bool show = dist > HideDistance;
        sr.enabled = show;
        if (!show) return;

        Vector2 dir = to / dist;
        float bob = Mathf.Sin(Time.unscaledTime * BobSpeed) * BobAmount;
        transform.position = new Vector3(from.x, from.y, 0f) + (Vector3)(dir * (OrbitRadius + bob));
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }
}
