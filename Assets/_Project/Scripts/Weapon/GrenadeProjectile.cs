using UnityEngine;

// Grenade가 던진 투사체. 시작 지점에서 목표 지점까지 날아가는데, 처음엔 빠르게 날아가다가
// 목표 지점 근처에서 급격히 느려지는 궤적(2차 함수 형태의 ease-out)으로 움직인다.
// 도착하면 ExplosionZone을 그 자리에 만들고, 투사체는 그 자리에 남아 있다가 폭발 데미지가 들어가는 순간 같이 사라진다.
// 바운스로 다시 튀는 원본, 연쇄폭발로 튀어나오는 작은 수류탄도 같은 프리팹을 쓴다 (GrenadeShot 참고).
//
// arcHeight > 0으로 던지면(바운스/연쇄폭발) "통통 튀는" 연출이 붙는다: 바닥 그림자를 남긴 채 포물선으로 떠올랐다가
// 떨어지고, 날아가는 동안 빙글빙글 돌며, 착지하는 순간 납작하게 찌그러졌다 돌아온다.
// 작은 수류탄은 처음에 작게 튀어나왔다가 금방 원래 크기로 커져서 폭발에서 "뿜어져 나오는" 느낌을 준다.
public class GrenadeProjectile : MonoBehaviour
{
    public float throwSpeed = 15f; // 대략적인 이동 속도(유닛/초). Launch에 속도를 따로 안 넘겼을 때만 쓴다.
    public GameObject explosionZonePrefab;

    [Header("튀는 연출 (arcHeight > 0일 때만)")]
    public float minArcDuration = 0.35f; // 너무 짧게 날면 포물선이 안 보이므로 최소 비행 시간
    public float arcSpinSpeed = 720f;    // 날아가는 동안 회전 속도(도/초)
    public float popInScale = 0.4f;      // 작은 수류탄이 튀어나오는 순간의 크기 배율
    public float landSquashTime = 0.15f; // 착지할 때 찌그러졌다 돌아오는 시간
    public float landSquashAmount = 0.3f;

    private Vector2 startPos;
    private Vector2 targetPos;
    private GrenadeShot shot;
    private float flightDuration;
    private float elapsed;
    private bool landed;

    private float arcHeight;
    private float spinDirection;
    private Vector3 baseScale;
    private float squashTimer;
    private Transform shadow;
    private Vector3 shadowBaseScale;
    private float shadowOffset; // 그림자를 투사체 중심보다 이만큼 아래(발밑)에 깐다

    // GrenadeAttack이 던지는 순간 호출한다. speed가 0보다 크면 프리팹 기본값 대신 그 값을 쓴다 (강화 반영용).
    // arcHeight가 0보다 크면 그 높이까지 포물선으로 튀어 올랐다 떨어진다.
    public void Launch(Vector2 from, Vector2 to, GrenadeShot grenadeShot, float speed = -1f, float arcHeight = 0f)
    {
        startPos = from;
        targetPos = to;
        shot = grenadeShot;
        transform.position = from;
        baseScale = transform.localScale;
        this.arcHeight = arcHeight;

        float distance = Vector2.Distance(from, to);
        flightDuration = Mathf.Max(0.05f, distance / (speed > 0f ? speed : throwSpeed));

        if (arcHeight > 0f)
        {
            flightDuration = Mathf.Max(flightDuration, minArcDuration);
            spinDirection = to.x >= from.x ? -1f : 1f; // 오른쪽으로 날아가면 시계 방향으로 돈다
            CreateShadow();
        }
    }

    void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }

    void Update()
    {
        if (landed)
        {
            UpdateSquash();
            return; // 착탄한 뒤에는 그 자리에서 가만히 있다가 폭발과 함께 사라진다
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);

        if (arcHeight > 0f)
        {
            // 튀는 궤적: 바닥 위치는 일정한 속도로, 높이는 포물선(4t(1-t))으로. 실제 그림은 바닥 위치에서 높이만큼 위에 그린다.
            Vector2 ground = Vector2.Lerp(startPos, targetPos, t);
            float height = arcHeight * 4f * t * (1f - t);
            transform.position = ground + Vector2.up * height;
            transform.Rotate(0f, 0f, arcSpinSpeed * spinDirection * Time.deltaTime);

            // 작은 수류탄(연쇄폭발)은 비행 초반에 작게 튀어나왔다가 원래 크기로 커진다.
            float pop = shot != null && shot.generation > 0 ? Mathf.Lerp(popInScale, 1f, Mathf.Clamp01(t / 0.3f)) : 1f;
            transform.localScale = baseScale * pop;

            UpdateShadow(ground, height);
        }
        else
        {
            // ease-out(1 - (1-t)^2): 처음엔 t가 조금만 지나도 많이 움직이고, 끝에 가까워질수록 거의 안 움직인다.
            float eased = 1f - (1f - t) * (1f - t);
            transform.position = Vector2.Lerp(startPos, targetPos, eased);
        }

        if (t >= 1f)
        {
            Explode();
        }
    }

    // 목표 지점에 도착하면 그 자리에 경고 범위(ExplosionZone)를 만들고, 투사체는 폭발 데미지가 들어가는 순간까지 남아 있다가 같이 사라진다.
    private void Explode()
    {
        landed = true;
        transform.position = targetPos;
        transform.localScale = baseScale;

        if (arcHeight > 0f)
        {
            squashTimer = landSquashTime;
            UpdateShadow(targetPos, 0f);
        }

        if (explosionZonePrefab == null || shot == null)
        {
            Destroy(gameObject);
            return;
        }

        GameObject zoneGO = Instantiate(explosionZonePrefab, transform.position, Quaternion.identity);
        ExplosionZone zone = zoneGO.GetComponent<ExplosionZone>();
        if (zone == null)
        {
            Destroy(gameObject);
            return;
        }

        zone.Init(shot);
        zone.Detonated = () => { if (this != null) Destroy(gameObject); };
    }

    // 착지 직후 잠깐 납작하게(옆으로 넓고 위아래로 낮게) 찌그러졌다가 원래 모양으로 돌아온다.
    private void UpdateSquash()
    {
        if (squashTimer <= 0f) return;

        squashTimer -= Time.deltaTime;
        float k = Mathf.Sin(Mathf.PI * (1f - Mathf.Clamp01(squashTimer / landSquashTime))) * landSquashAmount;
        transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), baseScale.z);
        if (squashTimer <= 0f) transform.localScale = baseScale;
    }

    // 바닥에 깔리는 납작한 그림자. 투사체가 돌고 떠오르는 것과 따로 움직여야 해서 자식으로 두지 않는다.
    private void CreateShadow()
    {
        SpriteRenderer mySr = GetComponent<SpriteRenderer>();
        float width = mySr != null ? mySr.bounds.size.x : 0.25f;
        shadowOffset = (mySr != null ? mySr.bounds.size.y : 0.25f) * 0.4f;

        GameObject go = new GameObject("GrenadeShadow");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ExplosionZone.GetCircleSprite();
        sr.color = new Color(0f, 0f, 0f, 0.35f);
        if (mySr != null)
        {
            sr.sortingLayerID = mySr.sortingLayerID;
            sr.sortingOrder = mySr.sortingOrder - 1;
        }

        shadow = go.transform;
        shadowBaseScale = new Vector3(width * 0.9f, width * 0.4f, 1f);
        UpdateShadow(startPos, 0f);
    }

    // 높이 뜰수록 그림자가 작아진다.
    private void UpdateShadow(Vector2 ground, float height)
    {
        if (shadow == null) return;
        shadow.position = ground + Vector2.down * shadowOffset;
        float shrink = arcHeight > 0f ? 1f - 0.45f * (height / arcHeight) : 1f;
        shadow.localScale = shadowBaseScale * shrink;
    }
}
