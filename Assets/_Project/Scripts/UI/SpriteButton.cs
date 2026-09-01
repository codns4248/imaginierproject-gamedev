using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// 월드 스페이스 스프라이트(타이틀 화면의 start/on-off/setting 아트)를 버튼처럼 클릭 가능하게 만든다.
// 프로젝트가 New Input System 전용(activeInputHandler=New)이라 레거시 OnMouseDown은 호출되지 않으므로,
// 마우스/터치를 직접 폴링해서 판정한다.
[RequireComponent(typeof(Collider2D))]
public class SpriteButton : MonoBehaviour
{
    public UnityEvent onClick;

    private Collider2D col;

    void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        Vector2? screenPos = null;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            screenPos = Mouse.current.position.ReadValue();
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();

        if (screenPos == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 worldPoint = cam.ScreenToWorldPoint(screenPos.Value);
        if (col.OverlapPoint(worldPoint))
        {
            onClick?.Invoke();
        }
    }
}
