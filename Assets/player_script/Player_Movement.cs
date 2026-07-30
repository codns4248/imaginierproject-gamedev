using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody2D rb;
    private Vector2 movement;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private bool IsDead = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (IsDead) return;
        // WASD 입력
        movement = new Vector2(
            Keyboard.current.dKey.isPressed ? 1 : Keyboard.current.aKey.isPressed ? -1 : 0,
            Keyboard.current.wKey.isPressed ? 1 : Keyboard.current.sKey.isPressed ? -1 : 0
        ).normalized;

        // 스프라이트 방향 전환
        if (movement.x > 0f)
            spriteRenderer.flipX = false; // 오른쪽 이동
        else if (movement.x < 0f)
            spriteRenderer.flipX = true; // 왼쪽 이동

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