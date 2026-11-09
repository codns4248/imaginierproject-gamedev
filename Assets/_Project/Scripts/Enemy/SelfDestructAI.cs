using UnityEngine;

// 군부대 "자폭 드론": 빠르게 쫓아오다가 플레이어가 triggerRange 안에 들어오면 멈춰서 빨갛게 깜빡이고(fuseTime),
// 그 자리에서 터진다. 폭발은 반경 안의 플레이어에게 피해를 주고, 주변 몬스터에게도 피해를 준다(몬스터 무리 정리에 이용 가능).
// 스스로 터지면 아무것도 떨어뜨리지 않고, 터지기 전에 잡으면 평소처럼 드랍한다. 접촉 피해는 없다(프리팹 contactDamage 0).
[RequireComponent(typeof(Enemy))]
public class SelfDestructAI : MonoBehaviour
{
    public float triggerRange = 0.55f;
    public float fuseTime = 0.8f;
    public float blastRadius = 0.9f;
    public int playerDamage = 1;
    [Tooltip("주변 몬스터에게 주는 피해 (층별 몬스터 체력 배율이 곱해진다)")]
    public float enemyDamage = 40f;
    public Color fuseColor = new Color(1f, 0.25f, 0.2f);

    private Enemy enemy;
    private Transform player;
    private bool fusing;
    private float fuseTimer;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    void Start()
    {
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    void Update()
    {
        if (enemy.IsDying || player == null || EnemyManager.PlayerDead) return;

        if (!fusing)
        {
            if (enemy.IsStunned) return;
            if (Vector2.Distance(transform.position, player.position) > triggerRange) return;
            fusing = true;
            fuseTimer = fuseTime;
            enemy.externalMovementControl = true; // 제자리에 멈춘다
        }

        // 깜빡임이 점점 빨라진다 (곧 터진다는 신호).
        fuseTimer -= Time.deltaTime;
        float rate = Mathf.Lerp(18f, 6f, Mathf.Clamp01(fuseTimer / fuseTime));
        enemy.SetExternalOutline(Mathf.Repeat(Time.time * rate, 1f) < 0.5f, fuseColor);
        if (fuseTimer <= 0f) Explode();
    }

    private void Explode()
    {
        Vector2 pos = transform.position;
        if (PixelFx.Play("explosion", pos, blastRadius * 2f * 1.08f) == null)
            CircleFlash.Spawn(pos, blastRadius, new Color(1f, 0.5f, 0.2f, 0.8f), 0.25f, GetComponent<SpriteRenderer>());

        if (player != null && Vector2.Distance(player.position, pos) <= blastRadius)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null) health.TakeHit(playerDamage, (Vector2)player.position - pos);
        }

        float dealt = enemyDamage * MonsterDifficulty.HealthMultiplier;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(pos, blastRadius))
        {
            Enemy other = hit.GetComponent<Enemy>();
            if (other == null || other == enemy || other.IsDying) continue;
            other.TakeDamage(dealt);
            other.Knockback((Vector2)other.transform.position - pos, 1.5f);
        }

        enemy.SetExternalOutline(false, fuseColor);
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false; // 폭발과 함께 바로 사라진다 (서서히 사라지는 기본 사망 연출을 보이지 않게)
        enemy.Kill(dropLoot: false);       // 스스로 터졌으니 보상 없음
    }
}
