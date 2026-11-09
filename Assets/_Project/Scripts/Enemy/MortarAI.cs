using UnityEngine;

// 군부대 정예 "보행 전차": 느리게 다가오다가 attackInterval마다 그 자리에 멈춰서 포신을 하늘로 들고(공격 애니메이션),
// 다 든 자세에서 잠깐 조준하듯 멈췄다가(holdBeforeFire) fireFrame에서 포구 섬광과 함께 격발해 플레이어 주변에 박격포 경고 장판
// 여러 개를 깐다. 격발 직후 반동 자세에서도 잠깐 멈췄다가(holdAfterFire) 포신을 내리고 다시 쫓아온다. 장판은 PlayerAoeZone: 경고가 끝나는 순간 범위 안에 있으면 피해.
// 첫 장판은 격발 순간 플레이어가 서 있던 자리, 나머지는 그 주변에 흩어지고 조금씩 늦게 터져서 피할 길을 골라야 한다.
// 공격 프레임이 없으면 멈추지 않고 바로 쏜다 (예전 동작).
[RequireComponent(typeof(Enemy))]
public class MortarAI : MonoBehaviour
{
    public float attackInterval = 4f;
    [Tooltip("플레이어가 이 거리 안에 있을 때만 쏜다")]
    public float range = 6f;
    public int shells = 3;
    public float spread = 0.9f;        // 첫 장판 말고 나머지가 흩어지는 반경
    public float warningDuration = 1.2f;
    public float shellDelay = 0.15f;   // 장판마다 이만큼씩 늦게 터진다
    public float shellRadius = 0.55f;
    public int damage = 1;

    [Header("공격 모션 (멈춤 -> 포신 들기 -> 격발 -> 포신 내리기)")]
    public Sprite[] attackFrames;
    public float attackFrameRate = 9f;
    [Tooltip("attackFrames 중 격발(장판 생성 + 포구 섬광)하는 프레임 번호")]
    public int fireFrame = 5;
    [Tooltip("포신을 다 올린 자세(격발 바로 앞 프레임)에서 조준하듯 멈춰 있는 시간(초)")]
    public float holdBeforeFire = 0.35f;
    [Tooltip("격발 직후 반동 자세(격발 바로 뒤 프레임)에서 멈춰 있다가 포신을 내리기까지의 시간(초)")]
    public float holdAfterFire = 0.3f;
    [Tooltip("격발 순간 포구 위치: 몸 중심 기준 (x는 그림 원본처럼 왼쪽을 볼 때 기준, 오른쪽을 보면 좌우가 뒤집힌다). 월드 유닛")]
    public Vector2 muzzleOffset = new Vector2(-0.26f, 0.31f);

    private Enemy enemy;
    private SpriteAnimator walkAnimator;
    private SpriteRenderer spriteRenderer;
    private Transform player;
    private float timer;

    private bool attacking;
    private int attackFrame;
    private float attackFrameTimer;
    private bool fired;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        walkAnimator = GetComponent<SpriteAnimator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
        timer = attackInterval * 0.5f; // 나타나자마자 쏘지 않게
    }

    void Update()
    {
        if (enemy.IsDying || player == null || EnemyManager.PlayerDead) return;

        if (attacking)
        {
            UpdateAttack();
            return;
        }

        if (enemy.IsStunned) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        if (Vector2.Distance(transform.position, player.position) > range) return;
        timer = attackInterval;

        if (attackFrames == null || attackFrames.Length == 0)
        {
            FireShells();
            return;
        }
        StartAttack();
    }

    private void StartAttack()
    {
        attacking = true;
        fired = false;
        attackFrame = 0;
        attackFrameTimer = 0f;
        enemy.externalMovementControl = true;  // 그 자리에 멈춘다
        if (walkAnimator != null) walkAnimator.enabled = false; // 걷기 애니메이션 대신 공격 프레임을 직접 넘긴다
        spriteRenderer.sprite = attackFrames[0];
    }

    private void UpdateAttack()
    {
        if (enemy.IsStunned) return; // 기절하면 그 자세로 멈췄다가 풀리면 이어서 한다

        attackFrameTimer += Time.deltaTime;
        while (attackFrameTimer >= CurrentFrameDuration())
        {
            attackFrameTimer -= CurrentFrameDuration();
            attackFrame++;
            if (!fired && attackFrame >= fireFrame)
            {
                fired = true;
                FireShells();
            }
            if (attackFrame >= attackFrames.Length)
            {
                EndAttack();
                return;
            }
        }
        spriteRenderer.sprite = attackFrames[attackFrame];
    }

    // 지금 보이는 공격 프레임이 머무는 시간: 기본 한 칸 + 격발 앞(포신을 다 든 자세)/뒤(반동 자세) 프레임이면 멈춤 시간을 더한다.
    private float CurrentFrameDuration()
    {
        float duration = 1f / (attackFrameRate * StageGimmickManager.EnemySpeedMultiplier);
        if (attackFrame == fireFrame - 1) duration += holdBeforeFire;
        else if (attackFrame == fireFrame + 1) duration += holdAfterFire;
        return duration;
    }

    private void EndAttack()
    {
        attacking = false;
        enemy.externalMovementControl = false;
        if (walkAnimator != null) walkAnimator.enabled = true;
    }

    private void FireShells()
    {
        Vector2 offset = new Vector2(spriteRenderer.flipX ? -muzzleOffset.x : muzzleOffset.x, muzzleOffset.y);
        PixelFx.Play("hit_spark", (Vector2)transform.position + offset, 0.45f, new Color(1f, 0.85f, 0.5f));

        for (int i = 0; i < shells; i++)
        {
            Vector2 at = (Vector2)player.position + (i == 0 ? Vector2.zero : Random.insideUnitCircle * spread);
            GameObject go = new GameObject("MortarShell");
            go.transform.position = at;
            PlayerAoeZone zone = go.AddComponent<PlayerAoeZone>();
            zone.detonateFxId = "explosion";
            zone.Init(damage, warningDuration + i * shellDelay, shellRadius);
        }
    }

    void OnDisable()
    {
        if (attacking) EndAttack();
    }
}
