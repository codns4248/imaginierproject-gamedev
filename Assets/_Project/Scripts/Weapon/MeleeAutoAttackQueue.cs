using System.Collections.Generic;
using UnityEngine;

// 들고 있지 않은 근접 무기(Sword, Lance 등)들의 자동공격 순서를 조율한다.
// 같은 타이밍에 여러 근접무기가 같은 적을 중복으로 때리면 낭비이므로, 한 번에 딱 하나의
// 근접무기만 나타나서 공격하고, 그 공격이 끝나야 다음 무기가 공격한다.
//
// 동작 방식(큐):
// - 매 프레임, 등록된 무기들 중 (들고 있지 않고 / 공격 중이 아니고 / 쿨다운이 아니고 / 아직 큐에
//   없는) 무기의 감지 범위(MaxReach) 안에 적이 있으면 큐에 넣는다.
// - 감지 범위가 넓은 무기일수록 적이 더 빨리 그 범위 안에 들어오므로 자연스럽게 먼저 큐에 들어가고,
//   큐는 먼저 들어온 순서(FIFO)대로 처리되므로 결과적으로 "사거리가 넓은 무기부터" 공격하게 된다.
//   감지 범위가 모두 같아서 같은 프레임에 동시에 큐에 들어가는 경우엔, 무기들이 등록되는 순서
//   (Start()가 호출되는 순서 = 보통 슬롯 번호 순서)가 자연스러운 우선순위가 된다.
// - 한 무기가 공격을 마쳐야(사라져야) 그 무기가 다시 큐에 들어갈 수 있다 (IsOnCooldown이 이를 보장).
public class MeleeAutoAttackQueue : MonoBehaviour
{
    private static readonly List<IAutoMeleeWeapon> registered = new List<IAutoMeleeWeapon>();
    private static readonly Queue<IAutoMeleeWeapon> queue = new Queue<IAutoMeleeWeapon>();
    private static readonly HashSet<IAutoMeleeWeapon> queuedSet = new HashSet<IAutoMeleeWeapon>();

    private IAutoMeleeWeapon currentAttacker;

    public static void Register(IAutoMeleeWeapon weapon)
    {
        if (!registered.Contains(weapon)) registered.Add(weapon);
    }

    public static void Unregister(IAutoMeleeWeapon weapon)
    {
        registered.Remove(weapon);
        queuedSet.Remove(weapon);
    }

    void Update()
    {
        if (EnemyManager.PlayerDead) return;

        // 현재 공격 중인 무기가 있으면, 그 공격이 끝날 때까지 다른 무기는 시작하지 않는다.
        if (currentAttacker != null)
        {
            if (currentAttacker.IsAttacking) return;
            currentAttacker = null;
        }

        TryEnqueueEligibleWeapons();
        TryStartNextAttack();
    }

    private void TryEnqueueEligibleWeapons()
    {
        foreach (IAutoMeleeWeapon weapon in registered)
        {
            if (weapon.IsHeld || weapon.IsAttacking || weapon.IsOnCooldown) continue;
            if (queuedSet.Contains(weapon)) continue;

            Enemy nearest = EnemyManager.FindNearest(transform.position, weapon.MaxReach);
            if (nearest != null)
            {
                queue.Enqueue(weapon);
                queuedSet.Add(weapon);
            }
        }
    }

    private void TryStartNextAttack()
    {
        while (queue.Count > 0)
        {
            IAutoMeleeWeapon next = queue.Dequeue();
            queuedSet.Remove(next);

            if (next.IsHeld) continue; // 대기하는 동안 플레이어가 이 무기를 손에 들었으면 건너뛴다

            // 대기하는 동안 적이 다시 범위를 벗어났을 수 있으니 공격 시작 직전에 다시 한번 확인한다.
            Enemy target = EnemyManager.FindNearest(transform.position, next.MaxReach);
            if (target == null) continue;

            currentAttacker = next;
            next.TriggerAutoAttack(target.transform.position);
            return;
        }
    }
}
