using UnityEngine;

// 두 지점을 잇는 밝은 선이 잠깐 번쩍이고 사라지는 임시 연출 (도탄이 꺾이는 경로, 표식이 옮겨 가는 경로 등).
// 코드로 만든 원 스프라이트(SwordWave가 만드는 것)를 길게 늘여서 쓴다.
public class BeamFlash : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color baseColor;
    private float duration;
    private float elapsed;

    public static void Spawn(Vector2 from, Vector2 to, Color color, float width, float duration, SpriteRenderer sortingSource)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return;

        GameObject go = new GameObject("BeamFlash");
        go.transform.position = (from + to) * 0.5f;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        go.transform.localScale = new Vector3(length, width, 1f); // 스프라이트 지름이 1유닛이라 가로=길이, 세로=굵기

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SwordWave.GetCircleSprite();
        sr.color = color;
        if (sortingSource != null)
        {
            sr.sortingLayerID = sortingSource.sortingLayerID;
            sr.sortingOrder = sortingSource.sortingOrder + 1;
        }

        BeamFlash beam = go.AddComponent<BeamFlash>();
        beam.sr = sr;
        beam.baseColor = color;
        beam.duration = Mathf.Max(0.01f, duration);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        Color c = baseColor;
        c.a = baseColor.a * (1f - t);
        sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }
}
