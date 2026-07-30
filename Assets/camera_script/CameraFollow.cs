using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector2 deadZoneSize = new Vector2(1.75f, 2.25f);
    public float smoothTime = 0.35f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 camPos = transform.position;
        Vector3 targetPos = target.position;

        float halfW = deadZoneSize.x * 0.5f;
        float halfH = deadZoneSize.y * 0.5f;

        float dx = targetPos.x - camPos.x;
        float overflowX = 0f;
        if (dx > halfW) overflowX = dx - halfW;
        else if (dx < -halfW) overflowX = dx + halfW;

        float dy = targetPos.y - camPos.y;
        float overflowY = 0f;
        if (dy > halfH) overflowY = dy - halfH;
        else if (dy < -halfH) overflowY = dy + halfH;

        Vector3 desiredPosition = camPos + new Vector3(overflowX, overflowY, 0f);
        transform.position = Vector3.SmoothDamp(camPos, desiredPosition, ref velocity, smoothTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(deadZoneSize.x, deadZoneSize.y, 0f));
    }
}
