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
    // 거점 특수 강화 "시작 회복약"으로 기본 개수가 늘어난다 (SpecialUpgrades.StartPotionBonus).
    public const int BasePotionCount = 2;
    private static int StartingPotionCount => BasePotionCount + SpecialUpgrades.StartPotionBonus;
    public int potionCount = BasePotionCount;

    private PlayerHealth playerHealth;

    public int PotionCount => potionCount;

    // 개수가 바뀔 때마다 호출된다. UI 표시 갱신 타이밍으로 쓴다.
    public event Action OnPotionCountChanged;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        potionCount = Mathf.Max(potionCount, StartingPotionCount);

        // 표시 UI(HealthPotionUI)가 이 Awake보다 먼저 켜졌으면 씬에 저장된 기본값(2)을 이미 읽어 갔으므로 다시 알려 준다
        // (안 그러면 "시작 회복약" 강화분이 첫 사용 전까지 화면에 안 보인다).
        OnPotionCountChanged?.Invoke();
    }

    void OnEnable()
    {
        SpecialUpgrades.OnChanged += HandleSpecialUpgradesChanged;
    }

    void OnDisable()
    {
        SpecialUpgrades.OnChanged -= HandleSpecialUpgradesChanged;
    }

    // 거점에서 "시작 회복약"을 올리면 바로 그만큼 채워 준다 (탐험 중에는 바꾸지 않는다).
    private void HandleSpecialUpgradesChanged()
    {
        if (StageManager.IsInStage || potionCount >= StartingPotionCount) return;
        potionCount = StartingPotionCount;
        OnPotionCountChanged?.Invoke();
    }

    void Update()
    {
        if (!Keyboard.current.rKey.wasPressedThisFrame) return;
        if (PauseManager.IsEscPaused) return; // ESC 설정 창으로 멈춘 동안에는 회복약을 쓰지 않는다

        TryUsePotion();
    }

    private void TryUsePotion()
    {
        if (potionCount <= 0) return;
        if (playerHealth.IsDead) return;
        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth) return; // 이미 풀피면 낭비하지 않는다

        playerHealth.Heal(healAmount);
        PixelFxUtil.PlayOnPlayer("glow_sparkle", 0.9f, new Color(0.55f, 1f, 0.65f)); // 회복 반짝임 (초록)
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
