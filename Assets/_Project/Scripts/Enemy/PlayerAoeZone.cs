using UnityEngine;

// AmbushCasterAI가 공격을 시전할 때 플레이어가 서 있던 위치에 만드는 붉은 경고 장판.
// LightningGimmickEffect(기믹 "벼락")와 정확히 같은 방식 - warningDuration 동안 보이기만 하다가,
// 그 시간이 다 되는 "그 순간"에만 범위 안에 플레이어가 있는지 한 번 판정하고 데미지를 준 뒤 사라진다.
public class PlayerAoeZone : MonoBehaviour
{
    public float radius = 1.5f;
    public float warningDuration = 1.5f;
    public int damage = 1;

    private float timer;

    void Awake()
    {
        SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = GimmickTextureUtil.CreateFilledCircle(128, new Color(0.9f, 0.1f, 0.1f, 0.55f), 0.85f);
        sr.sortingOrder = 5; // 캐릭터보다 위에 보이도록 (LightningGimmickEffect와 동일)
    }

    // AmbushCasterAI가 스폰 직후 한 번 호출해서 실제 수치를 넘겨준다.
    public void Init(int hitDamage, float duration, float hitRadius)
    {
        damage = hitDamage;
        warningDuration = duration;
        radius = hitRadius;
        timer = warningDuration;

        // CreateFilledCircle은 스프라이트가 정확히 1유닛이 되도록 만들어지므로, radius*2(지름)만큼
        // 그대로 스케일하면 시각적 크기와 실제 판정 반경이 일치한다.
        transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) Detonate();
    }

    private void Detonate()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null && Vector2.Distance(player.transform.position, transform.position) <= radius)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null) health.TakeHit(damage, Vector2.zero);
        }
        Destroy(gameObject);
    }
}
