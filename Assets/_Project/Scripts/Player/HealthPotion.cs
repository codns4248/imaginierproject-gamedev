using System;
using UnityEngine;
using UnityEngine.InputSystem;

// R키로 사용하는 회복약. 최대 체력이 아니라 "현재 체력"에서 고정량만큼만 회복하고 하나 소모한다.
// 이미 풀피면 아무 반응 없이 무시해서(소모되지 않음) 회복약을 낭비하지 않는다.
[RequireComponent(typeof(PlayerHealth))]
public class HealthPotion : MonoBehaviour
{
    public int healAmount = 2;

    // 매 런 시작(그리고 사망 시 초기화) 기본 소지 개수. 상인에게 사서 늘릴 수 있지만,
    // 사망하면 이번 런에서 늘어난 만큼은 잃고 이 개수로 되돌아간다 (자원 소실과 같은 규칙).
    private const int StartingPotionCount = 2;
    public int potionCount = StartingPotionCount;

    private PlayerHealth playerHealth;

    public int PotionCount => potionCount;

    // 개수가 바뀔 때마다 호출된다. UI 표시 갱신 타이밍으로 쓴다.
    public event Action OnPotionCountChanged;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (!Keyboard.current.rKey.wasPressedThisFrame) return;

        TryUsePotion();
    }

    private void TryUsePotion()
    {
        if (potionCount <= 0) return;
        if (playerHealth.IsDead) return;
        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth) return; // 이미 풀피면 낭비하지 않는다

        playerHealth.Heal(healAmount);
        potionCount--;
        OnPotionCountChanged?.Invoke();
    }

    // 나중에 파밍/보상 등으로 회복약을 얻을 때 호출할 용도로 열어둔다.
    public void AddPotions(int amount)
    {
        if (amount <= 0) return;
        potionCount += amount;
        OnPotionCountChanged?.Invoke();
    }

    // 사망 시 호출: 상인에게 사서 늘린 분을 포함해 전부 잃고 기본 개수로 되돌아간다.
    public void ResetToStarting()
    {
        potionCount = StartingPotionCount;
        OnPotionCountChanged?.Invoke();
    }
}
