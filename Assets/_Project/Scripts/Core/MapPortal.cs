using UnityEngine;

// 맵 출입구. 플레이어가 트리거에 닿으면 targetScene으로 이동하고,
// 그 씬의 targetSpawnId와 일치하는 SpawnPoint 위치에 배치된다.
// Collider2D(Is Trigger 체크)가 필요하다.
// 아트가 아직 없으면(SpriteRenderer 미배치) 위치 확인용 임시 마커를 자동으로 붙인다.
// 실제 포탈 스프라이트를 수동으로 넣으면 이 마커는 더 이상 생성되지 않는다.
[ExecuteAlways]
[RequireComponent(typeof(Collider2D))]
public class MapPortal : MonoBehaviour
{
    public string targetScene;
    public string targetSpawnId;

    void Awake()
    {
        // 런타임 생성 스프라이트는 에셋이 아니라 씬에 저장되지 않으므로,
        // 컴포넌트 존재가 아니라 sprite 참조 자체로 판단해야 재로드 시에도 다시 생성된다.
        // 실제 아트가 수동으로 배정되면 sprite != null이라 건드리지 않는다.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null) return;
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        Texture2D tex = new Texture2D(8, 8);
        Color[] pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.2f, 0.9f, 1f, 0.55f);
        tex.SetPixels(pixels);
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        sr.sortingOrder = -1;

        Collider2D col = GetComponent<Collider2D>();
        transform.localScale = new Vector3(col.bounds.size.x, col.bounds.size.y, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 코드베이스 전반에서 Player를 이름으로 찾는 방식과 통일 (태그 설정에 의존하지 않음)
        if (other.gameObject.name != "Player") return;
        if (string.IsNullOrEmpty(targetScene)) return;

        SceneTravel.GoTo(targetScene, targetSpawnId);
    }
}
