using UnityEngine;

// 별도의 Animator Controller 없이, 정해진 스프라이트 배열을 정해진 프레임 속도로 순서대로
// 반복 재생하는 간단한 플립북 애니메이션 컴포넌트.
// 이동 모션 하나만 필요한 슬라임처럼, 상태 전환(idle<->move 등)이 필요 없는 적에게 적합하다.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    public Sprite[] frames;
    public float frameRate = 12f;

    private SpriteRenderer spriteRenderer;
    private int currentFrame;
    private float timer;
    private float pauseTimer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // 피격 등으로 잠깐 그 자리에서 멈춘 것처럼 보이게 할 때 호출한다 (예: 0.2초간 정지).
    public void Pause(float duration)
    {
        pauseTimer = duration;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return; // 정지 중에는 프레임을 넘기지 않는다
        }

        timer += Time.deltaTime;
        float frameDuration = 1f / frameRate;
        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            currentFrame = (currentFrame + 1) % frames.Length;
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
