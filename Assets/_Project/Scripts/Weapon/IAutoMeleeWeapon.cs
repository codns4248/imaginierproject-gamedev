using UnityEngine;

// Sword, Lance처럼 "들고 있지 않을 때 플레이어 주변 범위에 적이 들어오면 자동으로 나타나 공격하고
// 사라지는" 근접 무기가 공통으로 구현하는 인터페이스.
// MeleeAutoAttackQueue가 이 인터페이스만 보고 여러 근접무기를 한꺼번에 관리한다.
public interface IAutoMeleeWeapon
{
    // 지금 이 무기가 실제로 닿을 수 있는 최대 거리(플레이어 기준). 궤도 반지름 + 판정 범위 등
    // 기존 공격 스탯으로부터 계산되며, 자동공격 탐지 반경으로도 그대로 쓰인다.
    // 나중에 사거리 관련 강화로 기존 스탯이 바뀌면 이 값도 자동으로 같이 바뀐다.
    float MaxReach { get; }

    // 지금 플레이어가 손에 들고 마우스로 직접 조종 중이면 true (이 동안은 자동공격 대상에서 제외).
    bool IsHeld { get; }

    // 등장~공격~소멸 연출이 진행 중이면 true.
    bool IsAttacking { get; }

    // 공격 쿨다운 중이면 true (쿨다운이 끝나야 다시 큐에 들어갈 수 있다).
    bool IsOnCooldown { get; }

    // true면 큐와 상관없이 스스로 계속 공격하는 상태(예: 검 나무 10단계 상시 회전)라서 큐에서 제외한다.
    // 큐에 넣으면 이 무기가 계속 "공격 중"이라 다른 근접 무기의 차례가 영원히 오지 않는다.
    bool ExcludeFromQueue { get; }

    // MeleeAutoAttackQueue가 이 무기의 차례가 되었을 때 호출한다. targetPosition 방향으로
    // (수동 공격과 동일한) 공격 연출을 재생한다.
    void TriggerAutoAttack(Vector2 targetPosition);
}
