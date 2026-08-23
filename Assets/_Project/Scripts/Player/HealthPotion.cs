using System;
using UnityEngine;
using UnityEngine.InputSystem;

// R키로 사용하는 회복약. 최대 체력이 아니라 "현재 체력"에서 고정량만큼만 회복하고 하나 소모한다.
// 이미 풀피면 아무 반응 없이 무시해서(소모되지 않음) 회복약을 낭비하지 않는다.
[RequireComponent(typeof(PlayerHealth))]
public class HealthPotion : MonoBehaviour
{
    public int healAmount = 2;

    // 아직 파밍/픽업으로 회복약을 얻는 시스템이 없어서, 테스트용으로 기본 개수를 들고 시작하게 해뒀다.
    public int potionCount = 2;

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
}
