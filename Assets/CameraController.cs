using UnityEngine;

/// <summary>
/// 메인 카메라가 플레이어를 부드럽게 따라가도록 제어하는 스크립트.
/// Dead Zone 방식을 사용해 플레이어가 일정 범위 안에서 움직일 때는 카메라가 고정되고,
/// 범위를 벗어날 때만 카메라가 따라온다.
/// </summary>
public class CameraController : MonoBehaviour
{
    // 카메라가 추적할 대상 (보통 플레이어). 비워두면 Start()에서 "Player"를 자동 탐색한다.
    public Transform target;
    // 카메라 이동의 부드러움 정도. 값이 클수록 느리고 부드럽게 따라온다.
    public float smoothTime = 0.3f;
    // Dead Zone 크기 (X: 가로, Y: 세로). 이 범위 안에서는 카메라가 움직이지 않는다.
    public Vector2 deadZoneSize = new Vector2(2f, 2f);

    // SmoothDamp 내부 계산에 사용되는 현재 속도 벡터 (직접 수정하지 않음)
    private Vector3 velocity = Vector3.zero;
    // 카메라가 이동하려는 목표 위치
    private Vector3 targetPosition;

    void Start()
    {
        // Inspector에서 target이 지정되지 않은 경우 씬에서 "Player" 오브젝트를 자동으로 찾음
        if (target == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                target = player.transform;
        }

        // 시작 시 카메라를 플레이어 위치로 즉시 이동 (첫 프레임 튀는 현상 방지)
        if (target != null)
        {
            targetPosition = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = targetPosition;
        }
    }

    void LateUpdate()
    {
        // LateUpdate 사용: 플레이어 이동(Update)이 모두 처리된 후 카메라를 갱신하기 위함
        if (target == null) return;

        Vector3 currentCamPos = transform.position;
        float halfWidth  = deadZoneSize.x * 0.5f;
        float halfHeight = deadZoneSize.y * 0.5f;

        // 플레이어가 Dead Zone의 오른쪽 경계를 벗어나면 카메라 목표를 오른쪽으로 이동
        float deltaX = target.position.x - currentCamPos.x;
        if (deltaX > halfWidth)
            targetPosition.x = target.position.x - halfWidth;
        else if (deltaX < -halfWidth)
            targetPosition.x = target.position.x + halfWidth;

        // 플레이어가 Dead Zone의 위쪽/아래쪽 경계를 벗어나면 카메라 목표를 수직 이동
        float deltaY = target.position.y - currentCamPos.y;
        if (deltaY > halfHeight)
            targetPosition.y = target.position.y - halfHeight;
        else if (deltaY < -halfHeight)
            targetPosition.y = target.position.y + halfHeight;

        // SmoothDamp로 현재 위치에서 목표 위치까지 부드럽게 이동
        Vector3 nextPos = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        // Z축은 고정 (2D 카메라 거리 유지)
        nextPos.z = transform.position.z;
        transform.position = nextPos;
    }
}
