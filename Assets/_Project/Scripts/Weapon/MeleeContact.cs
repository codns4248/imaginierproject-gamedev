using System.Collections.Generic;
using UnityEngine;

// 근접무기(검/창)가 "무기 모양 상자가 적 몸에 실제로 닿았는지" 따질 때 같이 쓰는 도구.
// 적 충돌 원은 스프라이트보다 훨씬 커서(Enemy.contactRangeScale 참고) 충돌체 겹침만 보면 눈에 보이는 무기에서 한참 떨어진 적까지
// 맞는다. 그래서 1차로 충돌체로 후보를 뽑고, 2차로 적의 몸통 원(Enemy.TryGetBodyCircle)이 상자에 닿은 것만 남긴다.
public static class MeleeContact
{
    private static readonly List<Collider2D> candidates = new List<Collider2D>();
    private static readonly List<Collider2D> touched = new List<Collider2D>();
    private static ContactFilter2D filter = ContactFilter2D.noFilter;

    /// <summary>상자(중심 center, 크기 size = 상자 로컬 x/y 길이, angle = 상자 로컬 x축의 월드 회전각(도))에 닿은 콜라이더 목록.
    /// 적(Enemy)은 몸통 원이 실제로 상자에 닿은 경우만 들어간다. 반환 리스트는 다음 호출 때 덮어써지므로 바로 순회해서 써야 한다.</summary>
    public static List<Collider2D> Touched(Vector2 center, Vector2 size, float angle)
    {
        candidates.Clear();
        touched.Clear();
        Physics2D.OverlapBox(center, size, angle, filter, candidates);

        foreach (Collider2D hit in candidates)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null && enemy.TryGetBodyCircle(out Vector2 bodyCenter, out float bodyRadius)
                && !BoxTouchesCircle(center, size, angle, bodyCenter, bodyRadius))
                continue;

            touched.Add(hit);
        }
        return touched;
    }

    // 회전한 직사각형(중심 boxCenter, 크기 boxSize, 회전 boxAngle도)과 원이 겹치는지.
    public static bool BoxTouchesCircle(Vector2 boxCenter, Vector2 boxSize, float boxAngle, Vector2 circleCenter, float circleRadius)
    {
        // 원 중심을 상자 기준 좌표계로 옮겨서, 상자 위에서 가장 가까운 점까지의 거리를 반지름과 비교한다.
        Vector2 local = Quaternion.Euler(0f, 0f, -boxAngle) * (circleCenter - boxCenter);
        Vector2 half = boxSize * 0.5f;
        Vector2 closest = new Vector2(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y));
        return (local - closest).sqrMagnitude <= circleRadius * circleRadius;
    }
}
