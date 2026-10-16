using UnityEngine;

// 한 발의 투사체에 붙는 특수효과 묶음. 무기(PistolAttack)가 쏠 때마다 만들어서 Projectile.ApplyModifiers로 넘긴다.
// 기본값은 전부 "효과 없음"이라, 필요한 항목만 채우면 된다. Projectile에 이 객체를 안 넘기면 예전과 똑같이 동작한다
// (SMG처럼 효과가 없는 무기는 그냥 안 넘긴다).
public class ProjectileModifiers
{
    // --- 크기/색 (필살탄) ---
    public float sizeMultiplier = 1f;
    public Color tint = Color.white;
    public bool impactRing;            // 적을 맞힐 때마다 충격 링 연출
    public float impactRadius = 0.7f;
    public Color impactColor = Color.white;

    // --- 철: 눈덩이탄 --- 적을 맞힐 때마다 탄이 커지고 데미지가 오른다 (도탄으로 꺾일 때도 센다)
    public float snowballDamagePerHit;
    public float snowballSizePerHit;
    public int snowballMaxStacks;
    public bool snowballAura;          // 10단계: 반투명 오라

    // --- 구리: 도탄 --- 적을 맞히면 반경 안의 아직 안 맞힌 가장 가까운 적에게 꺾인다. 꺾이는 건 관통 횟수를 쓰지 않는다
    public int maxBounces;
    public float bounceRadius;
    public Color beamColor = Color.white;
    public float beamWidth = 0.06f;

    // --- 기름: 가속탄 --- 날아가면서 점점 빨라지고, 멀리 날아갈수록 데미지가 늘어난다
    public float acceleration;         // 초당 속도 증가량
    public float maxSpeed;
    public float distanceDamagePerUnit;
    public float distanceDamageCap;
    public Color tailColor = Color.white;

    // --- 화학물질: 표식 --- 치명타로 맞힌 적에게 표식 (받는 피해 증가). markSpreadRadius > 0이면 표식 적이 죽을 때 근처 적에게 옮겨 간다
    public float markDuration;
    public float markDamageTaken = 1f;
    public float markSpreadRadius;

    /// <summary>적용할 효과가 하나라도 있는지.</summary>
    public bool HasAnyEffect =>
        !Mathf.Approximately(sizeMultiplier, 1f) || impactRing || snowballMaxStacks > 0 || maxBounces > 0
        || acceleration > 0f || distanceDamageCap > 0f || markDuration > 0f;
}
