using System;
using System.Collections;
using UnityEngine;

// 플레이어의 체력/부활/피격 반응/사망 처리를 담당하는 컴포넌트.
// 기본 최대 체력은 5, 기본 부활 횟수는 1이지만 둘 다 나중에 강화 아이템 등 다른 요소가
// SetMaxHealth()/reviveCount로 값을 바꿀 수 있도록 열어뒀다.
// 현재 체력과 최대 체력은 항상 0 이상이고, 현재 체력은 최대 체력을 넘지 않는다.
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 5;

    // 사망 판정 대신 즉시 부활할 수 있는 횟수. 0이면 다음 치명타에 그대로 사망한다.
    // 레벨업 보충 카드 "부활"로 늘어나고(AddRevive), 탐험이 끝나면 기본값으로 돌아간다(ResetRunState).
    public int reviveCount = 1;

    [Header("피격 반응")]
    public float invulnerabilityDuration = 0.9f; // 피격 후 무적이 지속되는 시간 (연속 피격 방지)
    public float blinkDuration = 0.6f;            // 깜빡거리는 총 지속 시간
    public float blinkInterval = 0.1f;            // 깜빡임 on/off 전환 간격
    public float hitSpriteDuration = 0.3f;        // 피격 스프라이트로 바뀌어 있는 시간
    public float knockbackDuration = 0.1f;        // 피격 넉백 지속 시간
    public float knockbackForce = 3f;             // 피격 넉백 속도

    [Header("피격 시 잠깐 보여줄 스프라이트 (Basic character v1_21)")]
    public Sprite hitSprite;

    [Header("사망 시 화면을 어둡게 만드는 UI")]
    public DeathFadeUI deathFadeUI;
    public float deathFadeDuration = 1.5f;

    private int currentHealth;
    private bool isInvulnerable;
    private bool isDead;

    private PlayerMovement playerMovement;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // 거점 영구 강화(체력) 적용 전 원본 최대 체력. 레벨이 바뀔 때마다 이 값 기준으로 다시 계산한다.
    // ponytail: 레벨당 +1 고정치. 밸런스 수치는 나중에 조정.
    private int baseMaxHealth;
    private int baseReviveCount;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public int ReviveCount => reviveCount;

    // 체력이 바뀔 때마다 호출된다. 체력 UI 등 표시가 필요한 쪽에서 구독해서 갱신 타이밍으로 쓴다.
    public event Action OnHealthChanged;

    // 남은 부활 횟수가 바뀔 때마다 호출된다 (부활 아이콘 표시용).
    public event Action OnReviveCountChanged;

    // 실제로 사망 처리(Die())가 실행될 때 딱 한 번 호출된다. 파밍 자원 소실 처리 등에서 구독한다.
    public event Action OnDied;

    void Awake()
    {
        baseMaxHealth = Mathf.Max(0, maxHealth);
        maxHealth = baseMaxHealth + PermanentUpgradeManager.GetLevel(ResourceType.Copper) + PlayerRunStats.MaxHealthBonus;
        reviveCount = Mathf.Max(0, reviveCount);
        baseReviveCount = reviveCount;
        currentHealth = maxHealth;

        playerMovement = GetComponent<PlayerMovement>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // 이전 런에서 죽어서 멈춰있던 적 이동/스폰/자동공격 등을 새 런 시작과 함께 다시 풀어준다.
        // (EnemyManager.PlayerDead는 정적 필드라 씬을 다시 불러와도 저절로 초기화되지 않는다)
        EnemyManager.SetPlayerDead(false);
    }

    void OnEnable()
    {
        PermanentUpgradeManager.OnChanged += ApplyPermanentHealthBonus;
        PlayerRunStats.Changed += ApplyPermanentHealthBonus;
    }

    void OnDisable()
    {
        PermanentUpgradeManager.OnChanged -= ApplyPermanentHealthBonus;
        PlayerRunStats.Changed -= ApplyPermanentHealthBonus;
    }

    // 거점에서 체력 영구 강화를 구매하거나 레벨업으로 최대 체력을 올리면 즉시 최대 체력에 반영한다
    // (기존 currentHealth 비율 등은 신경 쓰지 않고, SetMaxHealth와 동일하게 넘치는 현재 체력만 클램프한다).
    private void ApplyPermanentHealthBonus()
    {
        SetMaxHealth(baseMaxHealth + PermanentUpgradeManager.GetLevel(ResourceType.Copper) + PlayerRunStats.MaxHealthBonus);
    }

    // 적과 접촉하는 등 피해를 입었을 때 호출한다.
    // knockbackDirection은 "플레이어가 밀려나야 할 방향"(공격자 -> 플레이어 방향)이다.
    public void TakeHit(int damage, Vector2 knockbackDirection)
    {
        // 이미 죽었거나 무적 시간 중이면(연속 피격 방지) 무시한다.
        if (isDead || isInvulnerable || damage <= 0) return;

        currentHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

        if (currentHealth > 0)
        {
            OnHealthChanged?.Invoke();
            PlayHitReaction(knockbackDirection);
            return;
        }

        // 체력이 0 이하가 된 상황: 부활 횟수가 남아있으면 사망 대신 즉시 일부 회복하고 계속 진행한다.
        if (reviveCount > 0)
        {
            reviveCount--;
            LevelUpConfig cfg = LevelUpConfig.Instance;
            currentHealth = Mathf.Clamp(Mathf.CeilToInt(maxHealth * cfg.reviveHealthRatio), 1, maxHealth);
            OnHealthChanged?.Invoke();
            OnReviveCountChanged?.Invoke();
            PlayReviveEffect(cfg);
        }
        else
        {
            OnHealthChanged?.Invoke();
            Die(); // 사망 시에는 깜빡임/무적/넉박 등 아래 피격 연출을 전혀 재생하지 않는다.
        }
    }

    // 넉백 + 무적 + 깜빡임 + 피격 스프라이트. 사망하지 않고 "맞기만" 했을 때(부활 포함) 재생된다.
    private void PlayHitReaction(Vector2 knockbackDirection)
    {
        if (playerMovement != null && knockbackDirection.sqrMagnitude > 0.0001f)
        {
            playerMovement.ApplyKnockback(knockbackDirection.normalized * knockbackForce, knockbackDuration);
        }

        StartCoroutine(InvulnerabilityRoutine(invulnerabilityDuration));
        StartCoroutine(BlinkRoutine(blinkDuration));
        StartCoroutine(HitSpriteRoutine());
    }

    // 부활 순간: 평소 피격보다 긴 무적 + 깜빡임, 그리고 주변 몬스터를 밀어내는 충격파 (둘러싸인 채로 부활하자마자 다시 죽지 않게).
    private void PlayReviveEffect(LevelUpConfig cfg)
    {
        StopAllCoroutines(); // 이전 피격 무적/깜빡임이 먼저 끝나면서 부활 무적을 끊지 않게
        spriteRenderer.enabled = true;
        if (animator != null) animator.enabled = true;

        StartCoroutine(InvulnerabilityRoutine(cfg.reviveInvulnerability));
        StartCoroutine(BlinkRoutine(cfg.reviveInvulnerability));

        Vector2 center = transform.position;
        float radiusSqr = cfg.reviveShockwaveRadius * cfg.reviveShockwaveRadius;
        foreach (Enemy enemy in EnemyManager.ActiveEnemies)
        {
            if (enemy == null || enemy.IsDying) continue;
            Vector2 away = (Vector2)enemy.transform.position - center;
            if (away.sqrMagnitude > radiusSqr) continue;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
            enemy.Knockback(away.normalized, cfg.reviveKnockback);
        }

        PixelFxUtil.PlayOnPlayer("glow_sparkle", 1.8f, new Color(1f, 0.85f, 0.35f)); // 부활: 큰 금빛 광채 (충격파 원은 쓰지 않는다)
        CircleFlash.Spawn(center, cfg.reviveShockwaveRadius, new Color(1f, 0.85f, 0.35f, 0.6f), 0.5f, spriteRenderer);
        CircleFlash.Spawn(center, cfg.reviveShockwaveRadius * 0.5f, new Color(1f, 1f, 1f, 0.8f), 0.3f, spriteRenderer);
    }

    // 레벨업 보충 카드 "부활".
    public void AddRevive(int amount)
    {
        if (amount <= 0) return;
        reviveCount += amount;
        OnReviveCountChanged?.Invoke();
    }

    // 탐험이 끝날 때(LevelUpSystem.ResetForNewRun): 부활 횟수를 기본값으로 되돌리고, 레벨업 최대 체력 보너스를 뺀다.
    // 플레이어는 거점 씬에 계속 남아 있어서 Awake가 다시 불리지 않으므로 여기서 직접 되돌려야 한다.
    public void ResetRunState()
    {
        reviveCount = baseReviveCount;
        OnReviveCountChanged?.Invoke();
        ApplyPermanentHealthBonus();
    }

    private IEnumerator InvulnerabilityRoutine(float duration)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
    }

    private IEnumerator BlinkRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }
        spriteRenderer.enabled = true;
    }

    // 잠깐 Animator를 끄고 피격 스프라이트로 강제로 바꿨다가, 시간이 지나면 다시 Animator에게 넘긴다.
    private IEnumerator HitSpriteRoutine()
    {
        if (hitSprite == null || animator == null) yield break;

        animator.enabled = false;
        spriteRenderer.sprite = hitSprite;

        yield return new WaitForSeconds(hitSpriteDuration);

        if (!isDead) animator.enabled = true; // 그 사이 사망하지 않았을 때만 애니메이터에게 다시 맡긴다
    }

    private void Die()
    {
        isDead = true;

        // 혹시 진행 중이던 피격 연출(깜빡임 등)이 있다면 정리하고, 스프라이트/애니메이터를 정상 상태로 되돌린다.
        StopAllCoroutines();
        spriteRenderer.enabled = true;
        if (animator != null) animator.enabled = true;

        if (playerMovement != null) playerMovement.Die(); // 입력 차단 + Player.controller의 사망 애니메이션 재생

        EnemyManager.SetPlayerDead(true); // 모든 적이 그 자리에서 멈추도록 알림

        if (deathFadeUI != null) deathFadeUI.FadeToBlack(deathFadeDuration);

        OnDied?.Invoke();
    }

    // 최대 체력 자체를 바꾼다 (체력 강화 아이템 등에서 사용 예정).
    // 새 최대치가 현재 체력보다 작아지면 현재 체력도 함께 줄어든다.
    public void SetMaxHealth(int newMaxHealth)
    {
        maxHealth = Mathf.Max(0, newMaxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnHealthChanged?.Invoke();
    }

    // 회복(포션 등)에 사용. 피격 연출 없이 체력만 올린다.
    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        OnHealthChanged?.Invoke();
    }

    // 사망 후 거점으로 복귀했을 때 StageManager가 호출해서 다시 플레이할 수 있는 상태로 되돌린다.
    public void Revive()
    {
        isDead = false;
        isInvulnerable = false;
        currentHealth = maxHealth;

        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (animator != null) animator.enabled = true;
        if (playerMovement != null) playerMovement.Revive();
        if (deathFadeUI != null) deathFadeUI.ResetFade();

        EnemyManager.SetPlayerDead(false);
        OnHealthChanged?.Invoke();
    }
}
