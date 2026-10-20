using UnityEngine;

// 수류탄 한 발(원본 / 튕겨서 다시 터지는 원본 / 연쇄폭발로 튀어나온 작은 수류탄·더 작은 수류탄)의 정보.
// GrenadeAttack이 던질 때 만들어서 투사체(GrenadeProjectile) -> 경고 범위(ExplosionZone)로 넘기고,
// 터지는 순간 다시 GrenadeAttack.Detonate로 돌아와 피해와 특수효과를 처리한다.
public class GrenadeShot
{
    public GrenadeAttack owner; // 던진 무기. 날아가는 도중 무기가 사라지면 null이 되고, 그때는 기본 피해만 준다.
    public float damage;        // 치명타 적용 전 폭발 피해
    public float radius;        // 폭발 반지름
    public float delay;         // 착탄 후 터지기까지의 시간
    public int generation;      // 0 = 원본(바운스 포함), 1 = 작은 수류탄, 2 = 더 작은 수류탄
    public int bouncesLeft;     // 앞으로 더 튕길 수 있는 횟수 (원본만 튕긴다)
    public bool isBounce;       // 튕겨서 다시 터지는 원본이면 true (바운스 피해 배율을 처음 한 번만 곱하려고)
    public Vector2 direction;   // 바운스/더 작은 수류탄이 나아가는 방향
}
