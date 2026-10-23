using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 몬스터가 죽을 때 떨어뜨리는 경험치 구슬. 자원 아이템(ResourcePickup)과 같은 규칙으로 움직인다:
// 죽은 자리에서 톡 튀어나와 떨어지고, 플레이어가 가까이 오면 끌려와 흡수되며, 스테이지 클리어/자석 카드로
// ForceAttract가 불리면 거리와 상관없이 점점 빨라지며 날아온다.
// 지금 모양은 임시: 초록색과 노란색으로 깜빡이는 작은 불투명 구체 (나중에 이미지로 교체 예정).
// 일시정지 중에는 줍지 않는다 (레벨업 창이 떠 있는 동안 몸에 닿아 있던 구슬로 또 레벨업되지 않게).
public class XpOrb : MonoBehaviour
{
    private static readonly List<XpOrb> active = new List<XpOrb>();
    private static Sprite orbSprite;

    private static readonly Color ColorA = new Color(0.35f, 1f, 0.35f);
    private static readonly Color ColorB = new Color(1f, 0.92f, 0.25f);

    [Header("드랍 연출 (ResourcePickup과 같은 값)")]
    public float popDuration = 0.35f;
    public float popArcHeight = 0.25f;
    public float popMinDistance = 0.125f;
    public float popMaxDistance = 0.35f;

    [Header("클리어 시 강제 회수 (ResourcePickup과 같은 값)")]
    public float forceAttractStartDelay = 0.5f;
    public float forceAttractBaseSpeed = 5f;
    public float forceAttractAcceleration = 10f;

    private float xp;
    private Transform player;
    private SpriteRenderer sr;
    private bool isPopping;
    private bool isAttracting;
    private bool forceAttractPending;
    private bool forceAttracted;
    private float forceAttractTimer;
    private float blinkPhase;

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 목록이 남지 않게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
    }

    /// <summary>몬스터가 죽은 자리에 경험치 구슬을 떨어뜨린다 (Enemy.Die에서 호출). 정예/보스는 크고 경험치가 많다.</summary>
    public static void SpawnFromEnemy(Vector3 position, bool elite)
    {
        LevelUpConfig cfg = LevelUpConfig.Instance;

        GameObject go = new GameObject("XpOrb");
        go.transform.position = position;
        float size = cfg.orbSize * (elite ? cfg.eliteOrbSizeMultiplier : 1f);
        go.transform.localScale = new Vector3(size, size, 1f);

        XpOrb orb = go.AddComponent<XpOrb>();
        orb.xp = LevelUpSystem.OrbXpFor(elite);
        orb.sr = go.AddComponent<SpriteRenderer>();
        orb.sr.sprite = GetOrbSprite();
        orb.sr.sortingOrder = 2; // 자원 아이템과 같은 층
        orb.blinkPhase = Random.value * Mathf.PI * 2f;
        orb.StartCoroutine(orb.PopRoutine(position));
    }

    /// <summary>바닥의 모든 경험치 구슬이 거리와 상관없이 끌려오기 시작한다 (스테이지 클리어, 자석 카드).</summary>
    public static void ForceAttractAll()
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i] != null) active[i].ForceAttract();
    }

    /// <summary>바닥의 경험치 구슬을 전부 없앤다 (탐험 종료).</summary>
    public static void DestroyAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (active[i] != null) Destroy(active[i].gameObject);
        active.Clear();
    }

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        Blink();

        if (isPopping || player == null) return;
        if (EnemyManager.PlayerDead) return;
        if (PauseManager.IsPaused || Time.timeScale <= 0f) return; // 멈춘 동안에는 끌려오지도, 주워지지도 않는다

        LevelUpConfig cfg = LevelUpConfig.Instance;

        if (forceAttractPending)
        {
            forceAttractTimer -= Time.deltaTime;
            if (forceAttractTimer <= 0f)
            {
                forceAttractPending = false;
                forceAttracted = true;
                forceAttractTimer = 0f;
            }
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);

        if (forceAttracted)
        {
            forceAttractTimer += Time.deltaTime;
            float speed = forceAttractBaseSpeed + forceAttractTimer * forceAttractAcceleration;
            transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.deltaTime);
            if (dist <= cfg.orbPickupRadius) Collect();
            return;
        }

        if (!isAttracting && dist <= cfg.orbAttractRadius) isAttracting = true;

        if (isAttracting)
        {
            float speed = cfg.orbMoveSpeed + Mathf.Max(0f, cfg.orbAttractRadius - dist) * 4f;
            transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.deltaTime);
            if (dist <= cfg.orbPickupRadius) Collect();
        }
    }

    // 초록 <-> 노랑으로 반짝인다 (구슬마다 박자가 조금씩 다르다).
    private void Blink()
    {
        if (sr == null) return;
        float t = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f + blinkPhase);
        sr.color = Color.Lerp(ColorA, ColorB, t);
    }

    private void ForceAttract()
    {
        if (forceAttracted || forceAttractPending) return;
        forceAttractPending = true;
        forceAttractTimer = forceAttractStartDelay;
    }

    private void Collect()
    {
        LevelUpSystem.AddXp(xp);
        Destroy(gameObject);
    }

    private IEnumerator PopRoutine(Vector3 origin)
    {
        isPopping = true;
        Vector3 land = origin + (Vector3)(Random.insideUnitCircle.normalized * Random.Range(popMinDistance, popMaxDistance));
        float elapsed = 0f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            transform.position = Vector3.Lerp(origin, land, t) + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * popArcHeight, 0f);
            yield return null;
        }
        transform.position = land;
        isPopping = false;
    }

    // 지름 1유닛짜리 불투명 구체: 가장자리는 어둡고 왼쪽 위에 밝은 반사광이 있는 원 (색은 SpriteRenderer.color로 입힌다).
    private static Sprite GetOrbSprite()
    {
        if (orbSprite != null) return orbSprite;

        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / (size * 0.5f), dy = (y - c) / (size * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { pixels[y * size + x] = new Color32(0, 0, 0, 0); continue; }

                float shade = Mathf.Lerp(1f, 0.55f, d * d);                        // 가장자리로 갈수록 어둡게
                float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.35f, 0.35f)) / 0.35f);
                float v = Mathf.Min(1f, shade + hl * 0.6f);                         // 반사광
                byte b = (byte)(v * 255f);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        orbSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        orbSprite.hideFlags = HideFlags.HideAndDontSave;
        return orbSprite;
    }
}
