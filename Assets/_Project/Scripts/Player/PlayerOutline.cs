using UnityEngine;

// 플레이어에 항상 검은 테두리를 그려서, 밝은 바닥(NASA/거점)이나 적 무리 속에서도 캐릭터 윤곽이 보이게 한다.
// 방식은 Enemy.cs 피격 테두리와 같다: 스프라이트를 4방향으로 살짝 띄워 전용 머티리얼로 실루엣을 겹쳐 그림.
// 플레이어는 애니메이션/좌우 반전/피격 깜빡임이 있으므로 매 프레임 본체 상태를 그대로 따라간다.
public class PlayerOutline : MonoBehaviour
{
    public Color outlineColor = Color.black;
    public float outlineOffset = 0.04f;

    private SpriteRenderer body;
    private SpriteRenderer[] outlineRenderers;
    private Material outlineMat;

    void Start()
    {
        body = GetComponent<SpriteRenderer>();
        Material shared = FindSharedOutlineMaterial();
        if (body == null || shared == null) { enabled = false; return; }

        outlineMat = new Material(shared);
        outlineMat.color = outlineColor;

        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        outlineRenderers = new SpriteRenderer[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject go = new GameObject("Outline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * outlineOffset);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = outlineMat;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 1;
            outlineRenderers[i] = sr;
        }
    }

    // 적(Enemy)이 이미 쓰는 피격 테두리 머티리얼을 그대로 재사용한다 (InteractOutline과 동일한 방식).
    private static Material FindSharedOutlineMaterial()
    {
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        Enemy enemyPrefab = spawner != null && spawner.enemyPrefab != null ? spawner.enemyPrefab.GetComponent<Enemy>() : null;
        return enemyPrefab != null ? enemyPrefab.outlineMaterial : null;
    }

    void LateUpdate()
    {
        foreach (SpriteRenderer sr in outlineRenderers)
        {
            sr.sprite = body.sprite;
            sr.flipX = body.flipX;
            sr.enabled = body.enabled;
        }
    }

    void OnDestroy()
    {
        if (outlineMat != null) Destroy(outlineMat);
    }
}
