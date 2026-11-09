using UnityEngine;

// 미스터리 포탈(보라색)의 신비로운 분위기: 포탈 안쪽 여기저기서 보랏빛 반짝임이 계속 피어오르고, 포탈 색이 은은하게 맥박친다.
// 반짝임은 PixelFx의 "glow_sparkle"을 보라색으로 칠해서 쓴다. StageExtraction이 미스터리 포탈에만 붙인다.
public class PortalSparkle : MonoBehaviour
{
    private const float Interval = 0.16f;  // 반짝임이 새로 피는 평균 간격(초)
    private const float PulseSpeed = 2.6f;
    private const float PulseAmount = 0.18f;

    public Color sparkleTint = new Color(0.82f, 0.55f, 1f);

    private SpriteRenderer sr;
    private Color baseColor;
    private float timer;
    private float phase;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color; // StageExitPortal.Init이 칠한 보라색
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        if (sr == null) return;

        float pulse = 1f + PulseAmount * Mathf.Sin(Time.time * PulseSpeed + phase);
        sr.color = new Color(baseColor.r * pulse, baseColor.g * pulse, baseColor.b * pulse, baseColor.a);

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Interval * Random.Range(0.6f, 1.4f);

        // 포탈 그림 안쪽 타원 범위의 아무 곳에서나 피어난다.
        Bounds b = sr.bounds;
        Vector2 offset = Random.insideUnitCircle;
        Vector2 pos = new Vector2(b.center.x + offset.x * b.extents.x * 0.75f, b.center.y + offset.y * b.extents.y * 0.8f);
        PixelFx.Play("glow_sparkle", pos, Random.Range(0.14f, 0.26f), sparkleTint, sr.sortingOrder + 1);
    }
}
