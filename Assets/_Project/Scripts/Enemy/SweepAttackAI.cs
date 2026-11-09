using UnityEngine;

// NASA 정예 "거대 외계 생명체": 느리게 다가오다가 플레이어가 triggerRange 안에 들어오면 멈춰서 촉수를 휘두를 준비를 한다.
// 자기 주변에 붉은 경고 장판(PlayerAoeZone)이 windupTime 동안 깔리고, 끝나는 순간 그 안에 있으면 피해 + 충격파.
// 준비 중에는 노란 테두리가 켜지고, 공격이 끝나면 cooldown 동안 다시 추격만 한다.
[RequireComponent(typeof(Enemy))]
public class SweepAttackAI : MonoBehaviour
{
    public float triggerRange = 1.1f;
    public float windupTime = 0.8f;
    public float sweepRadius = 1.3f;
    public int damage = 1;
    public float cooldown = 2.5f;
    public Color windupOutlineColor = Color.yellow;

    private Enemy enemy;
    private Transform player;
    private float windupTimer;
    private float cooldownTimer;
    private bool winding;

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

        if (winding)
        {
            windupTimer -= Time.deltaTime;
            if (windupTimer > 0f) return;
            winding = false;
            enemy.externalMovementControl = false;
            enemy.SetExternalOutline(false, windupOutlineColor);
            PixelFx.Play("shock_ring", transform.position, sweepRadius * 2f * 1.16f); // 휘두르는 충격파 (피해 판정은 장판이 한다)
            cooldownTimer = cooldown;
            return;
        }

        if (cooldownTimer > 0f) { cooldownTimer -= Time.deltaTime; return; }
        if (enemy.IsStunned || Vector2.Distance(transform.position, player.position) > triggerRange) return;

        winding = true;
        windupTimer = windupTime;
        enemy.externalMovementControl = true; // 휘두르는 동안 제자리
        enemy.SetExternalOutline(true, windupOutlineColor);

        GameObject go = new GameObject("TentacleSweep");
        go.transform.position = transform.position;
        go.AddComponent<PlayerAoeZone>().Init(damage, windupTime, sweepRadius);
    }

    void OnDisable()
    {
        if (enemy != null) enemy.SetExternalOutline(false, windupOutlineColor);
    }
}
