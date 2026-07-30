using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 movement;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Camera mainCamera;
    private bool IsDead = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (IsDead) return;
        // WASD 입력
        movement = new Vector2(
            Keyboard.current.dKey.isPressed ? 1 : Keyboard.current.aKey.isPressed ? -1 : 0,
            Keyboard.current.wKey.isPressed ? 1 : Keyboard.current.sKey.isPressed ? -1 : 0
        ).normalized;

        // 마우스 위치 기준 좌우 반전
        Vector3 screenPos = Mouse.current.position.ReadValue();
        screenPos.z = -mainCamera.transform.position.z;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(screenPos);

        if (mouseWorldPos.x > transform.position.x)
            spriteRenderer.flipX = false; // 마우스가 오른쪽
        else if (mouseWorldPos.x < transform.position.x)
            spriteRenderer.flipX = true; // 마우스가 왼쪽

        animator.SetFloat("Speed", movement.magnitude);
    }

    void FixedUpdate()
    {
        if (IsDead) return;

        // 실제 이동 처리
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    public void Die()
    {
        if (IsDead) return;

        IsDead = true;
        movement = Vector2.zero;
        animator.SetFloat("Speed", 0f);
        animator.SetBool("IsDead", true);
    }
}