using UnityEngine;

// NASA "분열 실험체": 플레이어가 잡으면 그 자리에서 작은 개체(childPrefab) 여러 마리로 갈라진다.
// 작은 개체는 다시 갈라지지 않는다(작은 개체 프리팹에는 이 컴포넌트가 없다). 스테이지 클리어 정리처럼
// 보상 없이 강제로 지워질 때는 갈라지지 않는다 (Enemy.Died는 직접 잡았을 때만 불린다).
[RequireComponent(typeof(Enemy))]
public class SplitOnDeath : MonoBehaviour
{
    public GameObject childPrefab;
    public int childCount = 2;
    public float spawnOffset = 0.15f;
    public float popKnockback = 1.2f; // 갈라지며 양옆으로 살짝 튀어나가는 세기
    // 갈라져 나온 직후 잠깐 무적: 근접 공격(검 휘두르기 0.3초 등)에 맞아 갈라지면, 작은 개체가 아직 진행 중인
    // 같은 공격에 바로 맞아 한 번에 다 쓸려 버렸다 (2026-11-18 피드백).
    public float spawnInvulnerability = 0.35f;

    private Enemy enemy;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    void OnEnable()
    {
        Enemy.Died += HandleDied;
    }

    void OnDisable()
    {
        Enemy.Died -= HandleDied;
    }

    private void HandleDied(Enemy died)
    {
        if (died != enemy || childPrefab == null) return;
        Sfx.Play(SfxId.SplitBurst); // 갈라지는 소리

        float startAngle = Random.value * 360f;
        for (int i = 0; i < childCount; i++)
        {
            float angle = (startAngle + i * 360f / childCount) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject go = Instantiate(childPrefab, (Vector2)transform.position + dir * spawnOffset, Quaternion.identity);
            Enemy child = go.GetComponent<Enemy>();
            if (child == null) continue;
            child.mapCenter = enemy.mapCenter;
            child.mapHalfExtent = enemy.mapHalfExtent;
            MonsterDifficulty.ApplyTo(child); // 층에 맞게 체력/속도/공격력
            child.Knockback(dir, popKnockback);
            child.MakeInvulnerable(spawnInvulnerability);
        }
    }
}
