using UnityEngine;

// 범위 효과가 어디까지 닿는지 눈으로 보여주는 임시 연출: 반투명 원이 살짝 커지면서 금방 사라진다 (프리팹/스프라이트 없이 코드로 만든다).
// 원 스프라이트는 SwordWave가 만드는 것을 같이 쓴다.
public class CircleFlash : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color baseColor;
    private float duration;
    private float elapsed;
    private float finalScale;

    // 지금 떠 있는 연출 개수. 탄이 많은 무기(SMG)가 연출을 너무 많이 만들지 않게 제한하는 데 쓴다.
    public static int ActiveCount { get; private set; }

    void OnDestroy()
    {
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
    }

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이에서 남은 개수가 섞이지 않게 초기화한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount()
    {
        ActiveCount = 0;
    }

    public static void Spawn(Vector2 position, float radius, Color color, float duration, SpriteRenderer sortingSource)
    {
        GameObject go = new GameObject("CircleFlash");
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SwordWave.GetCircleSprite();
        sr.color = color;
        if (sortingSource != null)
        {
            sr.sortingLayerID = sortingSource.sortingLayerID;
            sr.sortingOrder = sortingSource.sortingOrder;
        }

        CircleFlash flash = go.AddComponent<CircleFlash>();
        ActiveCount++;
        flash.sr = sr;
        flash.baseColor = color;
        flash.duration = Mathf.Max(0.01f, duration);
        flash.finalScale = radius * 2f; // 스프라이트 지름이 1유닛
        go.transform.localScale = Vector3.one * (flash.finalScale * 0.6f);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        transform.localScale = Vector3.one * Mathf.Lerp(finalScale * 0.6f, finalScale, t);
        Color c = baseColor;
        c.a = baseColor.a * (1f - t);
        sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }
}
