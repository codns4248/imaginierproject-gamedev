using UnityEngine;

// "데드존(dead zone)" 카메라 스크립트.
// 카메라 중심에 보이지 않는 사각형 영역을 두고, 타겟(캐릭터)이 그 안에 있는 동안은 카메라가 전혀 움직이지 않다가
// 영역 경계를 벗어나려는 순간부터 카메라가 그 벗어난 만큼을 부드럽게 따라가서 다시 영역 안에 들어오게 만든다.
public class CameraFollow : MonoBehaviour
{
    public Transform target; // 카메라가 쫓아갈 대상 (Player)

    // 데드존 박스의 가로/세로 크기(월드 유닛). 이 안에서는 타겟이 자유롭게 움직여도 카메라가 반응하지 않는다.
    public Vector2 deadZoneSize = new Vector2(1.75f, 2.25f);

    // SmoothDamp의 반응 속도를 조절하는 값. 작을수록 빠르고 즉각적으로, 클수록 느긋하고 관성이 강하게 따라간다.
    public float smoothTime = 0.35f;

    // SmoothDamp가 내부적으로 현재 속도를 계속 추적하기 위해 필요한 변수 (ref로 전달되어 매 프레임 갱신됨).
    private Vector3 velocity;

    private void LateUpdate()
    {
        // 카메라 이동은 캐릭터가 다 움직인 "뒤"에 계산해야 한 프레임 밀리는 現상이 없다.
        // (Update/FixedUpdate에서 캐릭터가 위치를 확정한 다음, 같은 프레임의 LateUpdate에서 카메라가 그 최종 위치를 본다)
        if (target == null) return;

        Vector3 camPos = transform.position;
        Vector3 targetPos = target.position;

        float halfW = deadZoneSize.x * 0.5f;
        float halfH = deadZoneSize.y * 0.5f;

        // 타겟이 카메라 중심으로부터 얼마나 떨어져 있는지(dx, dy)를 구하고,
        // 그 거리가 데드존의 절반 크기(halfW, halfH)를 넘어선 만큼(overflow)만 카메라가 반응하도록 한다.
        // 데드존 안에 있으면 overflow는 0이 되어 카메라가 정지 상태를 유지한다.
        float dx = targetPos.x - camPos.x;
        float overflowX = 0f;
        if (dx > halfW) overflowX = dx - halfW;       // 오른쪽 경계를 벗어난 만큼
        else if (dx < -halfW) overflowX = dx + halfW; // 왼쪽 경계를 벗어난 만큼 (음수)

        float dy = targetPos.y - camPos.y;
        float overflowY = 0f;
        if (dy > halfH) overflowY = dy - halfH;       // 위쪽 경계를 벗어난 만큼
        else if (dy < -halfH) overflowY = dy + halfH; // 아래쪽 경계를 벗어난 만큼 (음수)

        // 카메라가 최종적으로 도달해야 할 목표 지점 = 현재 위치 + 벗어난 만큼.
        // overflow가 연속적인 값이라(경계에서 뚝 끊기지 않음) SmoothDamp와 함께 써도 진동이 생기지 않는다.
        Vector3 desiredPosition = camPos + new Vector3(overflowX, overflowY, 0f);

        // 목표 지점까지 즉시 이동하지 않고, 속도 기반으로 가속/감속하며 부드럽게 이동한다.
        // -> 캐릭터가 데드존을 벗어나 계속 움직이면 카메라가 "끌려오는" 듯한 자연스러운 느낌을 준다.
        transform.position = Vector3.SmoothDamp(camPos, desiredPosition, ref velocity, smoothTime);
    }

    // Scene 뷰에서 카메라 오브젝트를 선택했을 때 데드존 범위를 노란 박스로 시각화해준다 (게임에는 영향 없음).
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(deadZoneSize.x, deadZoneSize.y, 0f));
    }
}
