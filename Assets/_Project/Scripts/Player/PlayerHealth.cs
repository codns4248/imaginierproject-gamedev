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

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    // 체력이 바뀔 때마다 호출된다. 체력 UI 등 표시가 필요한 쪽에서 구독해서 갱신 타이밍으로 쓴다.
    public event Action OnHealthChanged;

    // 실제로 사망 처리(Die())가 실행될 때 딱 한 번 호출된다. 파밍 자원 소실 처리 등에서 구독한다.
    public event Action OnDied;

    void Awake()
    {
        maxHealth = Mathf.Max(0, maxHealth);
        reviveCount = Mathf.Max(0, reviveCount);
        currentHealth = maxHealth;

        playerMovement = GetComponent<PlayerMovement>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // 이전 런에서 죽어서 멈춰있던 적 이동/스폰/자동공격 등을 새 런 시작과 함께 다시 풀어준다.
        // (EnemyManager.PlayerDead는 정적 필드라 씬을 다시 불러와도 저절로 초기화되지 않는다)
        EnemyManager.SetPlayerDead(false);
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
            currentHealth = Mathf.Min(3, maxHealth);
            OnHealthChanged?.Invoke();
            PlayHitReaction(knockbackDirection);
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

        StartCoroutine(InvulnerabilityRoutine());
        StartCoroutine(BlinkRoutine());
        StartCoroutine(HitSpriteRoutine());
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityDuration);
        isInvulnerable = false;
    }

    private IEnumerator BlinkRoutine()
    {
        float elapsed = 0f;
        while (elapsed < blinkDuration)
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
