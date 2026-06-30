using UnityEngine;

/// <summary>
/// 적 사망 시 드랍되는 경험치 아이템을 제어하는 스크립트.
/// 플레이어가 attractRadius 이내로 접근하면 자동으로 플레이어를 향해 이동하며,
/// pickupRadius 이내에 도달하면 GameManager에 경험치 1을 추가하고 자신을 삭제한다.
/// </summary>
public class ExpItem : MonoBehaviour
{
    // 이 거리 이내로 플레이어가 접근하면 흡입 이동을 시작하는 반경
    public float attractRadius = 1.5f;
    // 흡입 시 기본 이동 속도 (플레이어에 가까울수록 추가 속도가 붙음)
    public float moveSpeed = 6f;
    // 이 거리 이내에 도달하면 아이템을 획득 처리하는 반경
    public float pickupRadius = 0.1f;

    private Transform player;
    // 한 번 흡입이 시작되면 멀어져도 계속 따라오도록 상태를 유지
    private bool isAttracting = false;

    void Start()
    {
        // 씬에서 "Player" 오브젝트를 찾아 추적 대상으로 설정
        GameObject playerObj = GameObject.Find("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        // 아직 흡입 중이 아닐 때: 플레이어가 attractRadius 이내에 들어오면 흡입 시작
        if (!isAttracting && dist <= attractRadius)
            isAttracting = true;

        if (isAttracting)
        {
            // 플레이어에 가까울수록 속도를 증가시켜 자연스러운 흡입 효과 연출
            float speed = moveSpeed + (attractRadius - dist) * 4f;
            transform.position = Vector2.MoveTowards(transform.position, player.position, speed * Time.deltaTime);

            // pickupRadius 이내에 도달하면 획득 처리
            if (dist <= pickupRadius)
            {
                GameManager.Instance.AddExp(1);
                Destroy(gameObject);
            }
        }
    }
}
