using UnityEngine;
using UnityEngine.InputSystem;

// 트리거 콜라이더 범위 안에 플레이어가 들어오면 흰색 테두리(Enemy.cs의 피격 테두리와 같은 방식:
// 스프라이트를 4방향으로 살짝 띄워 전용 머티리얼로 흰색 실루엣을 겹쳐 그림)를 보여주고,
// F키를 누르면 onInteract 콜백을 실행한다. 포탈(StagePortal, StageExitPortal)이 공통으로 쓴다.
// F키가 다른 상호작용(바닥 무기 줍기, 상인 구매 등)과 겹칠 수 있어 InteractInput으로 프레임당
// 한 번만 반응하게 막는다 (MerchantItem과 동일한 방식).
[RequireComponent(typeof(Collider2D))]
public class InteractOutline : MonoBehaviour
{
    private SpriteRenderer[] outlineRenderers;
    private Material outlineMat;
    private bool playerInRange;
    private System.Action onInteract;

    public void Init(System.Action onInteract)
    {
        this.onInteract = onInteract;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) CreateOutline(FindSharedOutlineMaterial(), sr);
    }

    // 적(Enemy)이 이미 쓰는 피격 테두리 머티리얼을 그대로 재사용한다 (MerchantItem과 동일한 방식).
    private static Material FindSharedOutlineMaterial()
    {
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        Enemy enemyPrefab = spawner != null && spawner.enemyPrefab != null ? spawner.enemyPrefab.GetComponent<Enemy>() : null;
        return enemyPrefab != null ? enemyPrefab.outlineMaterial : null;
    }

    private void CreateOutline(Material outlineMaterial, SpriteRenderer sr)
    {
        Vector2[] offsets = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        outlineRenderers = new SpriteRenderer[offsets.Length];

        if (outlineMaterial != null) outlineMat = new Material(outlineMaterial);

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject go = new GameObject("Outline");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = (Vector3)(offsets[i] * 0.06f);

            SpriteRenderer outlineSr = go.AddComponent<SpriteRenderer>();
            outlineSr.sprite = sr.sprite;
            if (outlineMat != null) outlineSr.sharedMaterial = outlineMat;
            outlineSr.sortingOrder = sr.sortingOrder - 1;
            outlineSr.enabled = false;

            outlineRenderers[i] = outlineSr;
        }
        if (outlineMat != null) outlineMat.color = Color.white;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.name != "Player") return;
        playerInRange = true;
        SetOutlineEnabled(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.name != "Player") return;
        playerInRange = false;
        SetOutlineEnabled(false);
    }

    private void SetOutlineEnabled(bool isEnabled)
    {
        if (outlineRenderers == null) return;
        for (int i = 0; i < outlineRenderers.Length; i++) outlineRenderers[i].enabled = isEnabled;
    }

    void Update()
    {
        if (!playerInRange) return;

        if (Keyboard.current.fKey.wasPressedThisFrame && InteractInput.TryConsumeFKey())
            onInteract?.Invoke();
    }

    void OnDestroy()
    {
        if (outlineMat != null) Destroy(outlineMat);
    }
}
