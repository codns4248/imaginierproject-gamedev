using UnityEngine;

// Grenade가 던진 투사체. 시작 지점에서 목표 지점까지 날아가는데, 처음엔 빠르게 날아가다가
// 목표 지점 근처에서 급격히 느려지는 궤적(2차 함수 형태의 ease-out)으로 움직인다.
// 도착하면 ExplosionZone을 그 자리에 만들고 스스로 사라진다.
public class GrenadeProjectile : MonoBehaviour
{
    public float throwSpeed = 15f; // 대략적인 이동 속도(유닛/초). 실제 소요 시간은 거리 / throwSpeed로 계산한다.
    public GameObject explosionZonePrefab;

    private Vector2 startPos;
    private Vector2 targetPos;
    private float damage;
    private float flightDuration;
    private float elapsed;

    // GrenadeAttack이 던지는 순간 호출한다.
    public void Launch(Vector2 from, Vector2 to, float explosionDamage)
    {
        startPos = from;
        targetPos = to;
        damage = explosionDamage;
        transform.position = from;

        float distance = Vector2.Distance(from, to);
        flightDuration = Mathf.Max(0.05f, distance / throwSpeed);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightDuration);

        // ease-out(1 - (1-t)^2): 처음엔 t가 조금만 지나도 많이 움직이고, 끝에 가까워질수록 거의 안 움직인다.
        float eased = 1f - (1f - t) * (1f - t);
        transform.position = Vector2.Lerp(startPos, targetPos, eased);

        if (t >= 1f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (explosionZonePrefab != null)
        {
            GameObject zoneGO = Instantiate(explosionZonePrefab, transform.position, Quaternion.identity);
            ExplosionZone zone = zoneGO.GetComponent<ExplosionZone>();
            if (zone != null) zone.Init(damage);
        }

        Destroy(gameObject);
    }
}
