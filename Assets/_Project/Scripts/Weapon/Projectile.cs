using System.Collections.Generic;
using UnityEngine;

// 무기에서 발사되는 투사체. 적을 관통하며 날아가다가, 정해진 횟수만큼 적과 접촉하거나
// 카메라 화면 밖으로 나가면 스스로 사라진다.
//
// ApplyModifiers로 ProjectileModifiers를 받으면(권총의 강화 특수효과) 눈덩이탄/도탄/가속/표식/필살탄 효과가 붙는다.
// 안 받으면 예전과 똑같이 동작한다 (SMG 등).
public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    public int maxHits = 3; // 이 횟수만큼 적과 접촉하면 소멸

    private float damage;
    private bool isCrit;
    private Vector2 direction;
    private int hitCount;

    private Rigidbody2D rb;
    private Camera mainCamera;

    // === 특수효과 (mods가 null이면 아래는 전부 쓰이지 않는다) ===
    private ProjectileModifiers mods;
    private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>(); // 이 탄이 이미 맞힌 적 (도탄이 같은 적에게 되돌아가지 않게)
    private SpriteRenderer sr;
    private Vector3 baseScale;
    private float startSpeed;   // 발사 순간의 속도 (가속 연출 기준)
    private float travelled;    // 지금까지 날아간 거리
    private int snowStacks;     // 눈덩이 중첩 (맞힌 횟수)
    private int bouncesLeft;    // 남은 도탄 횟수
    private SpriteRenderer tailRenderer;
    private SpriteRenderer auraRenderer;

    private static readonly Color SnowballColor = new Color(1f, 0.55f, 0.2f);

    public int SnowStacks => snowStacks;
    public int BouncesLeft => bouncesLeft;
    public int HitCount => hitCount;
    public float Travelled => travelled;
    public Vector2 Direction => direction;
    public bool HasModifiers => mods != null;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    // WeaponAim이 발사 순간 방향과 데미지(치명타 여부 포함, 이미 계산된 최종 데미지)를 넘겨줄 때 호출한다.
    public void Fire(Vector2 fireDirection, float fireDamage, bool critical = false)
    {
        direction = fireDirection.normalized;
        damage = fireDamage;
        isCrit = critical;
        startSpeed = speed;
        travelled = 0f;

        FaceDirection();
    }

    // 스프라이트가 기본적으로 위(90도)를 향하고 있으므로, 실제 진행 방향에 맞춰 -90도 보정해서 회전시킨다.
    private void FaceDirection()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    /// <summary>특수효과를 붙인다. Fire보다 먼저 호출해야 한다 (speed에 강화 보너스를 더한 뒤).</summary>
    public void ApplyModifiers(ProjectileModifiers modifiers)
    {
        if (modifiers == null || !modifiers.HasAnyEffect) return;

        mods = modifiers;
        sr = GetComponentInChildren<SpriteRenderer>();
        baseScale = transform.localScale;
        bouncesLeft = mods.maxBounces;
        snowStacks = 0;

        CreateFx();
        UpdateLook();
    }

    void FixedUpdate()
    {
        Advance(Time.fixedDeltaTime);
    }

    /// <summary>한 물리 프레임(dt초)만큼 전진한다. 가속탄은 여기서 속도가 올라간다. (에디터 테스트에서 직접 불러도 된다)</summary>
    public void Advance(float dt)
    {
        if (mods != null && mods.acceleration > 0f && speed < mods.maxSpeed)
            speed = Mathf.Min(mods.maxSpeed, speed + mods.acceleration * dt);

        float step = speed * dt;
        travelled += step;

        bool physics = rb != null && Application.isPlaying;
        Vector2 origin = physics ? rb.position : (Vector2)transform.position;
        Vector2 target = origin + direction * step;
        if (physics) rb.MovePosition(target);
        else transform.position = target;
    }

    void Update()
    {
        // 카메라 시야를 벗어나면 더 이상 필요 없으니 제거한다.
        if (mainCamera == null) return;

        Vector3 viewport = mainCamera.WorldToViewportPoint(transform.position);
        bool onScreen = viewport.z > 0f && viewport.x > -0.05f && viewport.x < 1.05f
                                         && viewport.y > -0.05f && viewport.y < 1.05f;
        if (!onScreen)
        {
            Destroy(gameObject);
        }
    }

    void LateUpdate()
    {
        if (mods != null) UpdateFx();
    }

    void OnDestroy()
    {
        if (tailRenderer != null) Destroy(tailRenderer.gameObject);
        if (auraRenderer != null) Destroy(auraRenderer.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null) ProcessHit(enemy);
    }

    /// <summary>적 하나를 맞혔을 때의 처리 (OnTriggerEnter2D가 부른다. 에디터 테스트에서 직접 불러도 된다).</summary>
    public void ProcessHit(Enemy enemy)
    {
        if (mods == null)
        {
            enemy.Hit(direction, damage, isCrit);

            // Destroy하지 않고 계속 날아가서 다음 적도 관통한다. 정해진 횟수만큼 맞히면 그때 소멸.
            hitCount++;
            if (hitCount >= maxHits) Destroy(gameObject);
            return;
        }

        if (!hitEnemies.Add(enemy)) return; // 같은 적을 두 번 때리지 않는다
        HandleModifiedHit(enemy);
    }

    // 특수효과가 붙은 탄이 적을 맞혔을 때.
    private void HandleModifiedHit(Enemy enemy)
    {
        bool wasDying = enemy.IsDying;
        enemy.Hit(direction, damage * DamageMultiplier(), isCrit);

        // 화학물질: 치명타로 맞힌 적에게 표식
        if (!wasDying && isCrit && mods.markDuration > 0f)
            enemy.ApplyMark(mods.markDuration, mods.markDamageTaken, mods.markSpreadRadius);

        // 필살탄(10단계): 맞을 때마다 충격 링
        if (mods.impactRing)
            CircleFlash.Spawn(enemy.transform.position, mods.impactRadius, mods.impactColor, 0.25f, sr);

        // 철: 눈덩이 - 맞힐 때마다 커지고 세진다
        if (mods.snowballMaxStacks > 0) snowStacks++;
        UpdateLook();

        // 구리: 도탄 - 꺾이는 건 관통 횟수를 쓰지 않는다. 꺾일 수 없을 때(횟수를 다 썼거나 대상이 없을 때)부터 관통을 센다.
        if (bouncesLeft > 0)
        {
            Enemy next = FindBounceTarget();
            if (next != null)
            {
                Redirect(next);
                bouncesLeft--;
                return;
            }
        }

        hitCount++;
        if (hitCount >= maxHits) Destroy(gameObject);
    }

    // 눈덩이(맞힌 횟수)와 기름(날아간 거리)에 따른 데미지 배율.
    private float DamageMultiplier()
    {
        float multiplier = 1f;
        if (mods.snowballMaxStacks > 0)
            multiplier *= 1f + mods.snowballDamagePerHit * Mathf.Min(snowStacks, mods.snowballMaxStacks);
        if (mods.distanceDamageCap > 0f)
            multiplier *= 1f + Mathf.Min(mods.distanceDamageCap, mods.distanceDamagePerUnit * travelled);
        return multiplier;
    }

    // 반경 안에서 아직 안 맞힌(그리고 죽는 중이 아닌) 가장 가까운 적.
    private Enemy FindBounceTarget()
    {
        Vector2 origin = transform.position;
        float radiusSqr = mods.bounceRadius * mods.bounceRadius;

        Enemy best = null;
        float bestSqr = float.MaxValue;
        foreach (Collider2D c in Physics2D.OverlapCircleAll(origin, mods.bounceRadius + 0.5f)) // 적 충돌 원이 크므로 후보는 넉넉하게
        {
            Enemy e = c.GetComponent<Enemy>();
            if (e == null || e.IsDying || hitEnemies.Contains(e)) continue;

            float sqr = ((Vector2)e.transform.position - origin).sqrMagnitude;
            if (sqr > radiusSqr || sqr >= bestSqr) continue;

            bestSqr = sqr;
            best = e;
        }
        return best;
    }

    private void Redirect(Enemy next)
    {
        Vector2 from = transform.position;
        Vector2 to = next.transform.position;
        Vector2 delta = to - from;
        if (delta.sqrMagnitude > 0.0001f) direction = delta.normalized;
        FaceDirection();

        if (mods.beamWidth > 0f)
            BeamFlash.Spawn(from, to, mods.beamColor, mods.beamWidth, 0.25f, sr);
    }

    // 크기와 색: 필살탄 크기, 눈덩이 성장(맞을수록 커지고 주황빛이 된다). 콜라이더도 같이 커져서 더 넓게 맞힌다.
    private void UpdateLook()
    {
        float scale = mods.sizeMultiplier;
        Color color = mods.tint;

        if (mods.snowballMaxStacks > 0)
        {
            int stacks = Mathf.Min(snowStacks, mods.snowballMaxStacks);
            scale *= 1f + mods.snowballSizePerHit * stacks;
            color = Color.Lerp(color, SnowballColor, 0.8f * stacks / mods.snowballMaxStacks);
        }

        transform.localScale = baseScale * scale;
        if (sr != null) sr.color = color;
    }

    // 꼬리(가속탄)와 오라(눈덩이 10단계)는 탄의 자식이 아니라 따로 만들어서 따라다니게 한다
    // (자식으로 두면 탄 크기/회전에 같이 휘둘려서 길이를 정하기 어렵고, 콜라이더에도 영향이 없게 하려는 것).
    private void CreateFx()
    {
        if (mods.acceleration > 0f) tailRenderer = CreateFxSprite("BulletTail");
        if (mods.snowballAura) auraRenderer = CreateFxSprite("BulletAura");
    }

    private SpriteRenderer CreateFxSprite(string objectName)
    {
        GameObject go = new GameObject(objectName);
        SpriteRenderer fx = go.AddComponent<SpriteRenderer>();
        fx.sprite = SwordWave.GetCircleSprite();
        if (sr != null)
        {
            fx.sortingLayerID = sr.sortingLayerID;
            fx.sortingOrder = sr.sortingOrder - 1;
        }
        return fx;
    }

    private void UpdateFx()
    {
        Vector3 pos = transform.position;
        float scale = baseScale.x > 0f ? transform.localScale.x / baseScale.x : 1f;

        if (tailRenderer != null)
        {
            // 속도가 시작 속도보다 빨라진 만큼 꼬리가 길고 진해진다 (출발할 땐 거의 안 보이다가 가속하며 확 번진다).
            float gain = Mathf.Max(0f, speed - startSpeed);
            float range = Mathf.Max(0.01f, mods.maxSpeed - startSpeed);
            float t = Mathf.Clamp01(gain / range);
            float length = 0.08f + 0.06f * gain;
            float width = 0.1f * scale;

            Transform tail = tailRenderer.transform;
            tail.position = pos - (Vector3)(direction * (length * 0.5f));
            tail.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            tail.localScale = new Vector3(length, width, 1f);

            Color c = mods.tailColor;
            c.a = mods.tailColor.a * (0.15f + 0.85f * t);
            tailRenderer.color = c;
        }

        if (auraRenderer != null)
        {
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 14f);
            Transform aura = auraRenderer.transform;
            aura.position = pos;
            aura.localScale = Vector3.one * (0.5f * scale * pulse);
            auraRenderer.color = new Color(1f, 0.5f, 0.15f, 0.35f);
        }
    }
}
