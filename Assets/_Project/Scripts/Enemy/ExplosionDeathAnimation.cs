using UnityEngine;

// 죽을 때 일반적인 페이드아웃 대신, 준비된 폭발/파괴 스프라이트 시퀀스를 재생하고 그 애니메이션이
// 끝나면 스스로 사라지는 몬스터용 컴포넌트 (예: MR-63 같은 로봇형 몬스터).
// Enemy.externalDeathAnimation을 켜서 Enemy.cs의 기본 FadeOutAndDestroy를 건너뛰게 하고,
// Enemy.OnDeathStart를 구독해서 그 시점에 이 컴포넌트가 대신 애니메이션을 재생한다.
[RequireComponent(typeof(Enemy))]
[RequireComponent(typeof(SpriteRenderer))]
public class ExplosionDeathAnimation : MonoBehaviour
{
    public float frameRate = 12f;
    public Sprite[] frames;

    private Enemy enemy;
    private SpriteRenderer spriteRenderer;

    private bool playing;
    private int frameIndex;
    private float frameTimer;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemy.externalDeathAnimation = true;
        enemy.OnDeathStart += BeginExplosion;
    }

    void OnDestroy()
    {
        if (enemy != null) enemy.OnDeathStart -= BeginExplosion;
    }

    private void BeginExplosion()
    {
        if (frames == null || frames.Length == 0)
        {
            Destroy(gameObject); // 프레임이 없으면 그냥 바로 사라진다 (안전장치)
            return;
        }

        playing = true;
        frameIndex = 0;
        frameTimer = 0f;

        spriteRenderer.color = Color.white; // 기본 사망 페이드가 만졌을 수도 있는 알파를 원래대로
        spriteRenderer.sprite = frames[0];
    }

    void Update()
    {
        if (!playing) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / frameRate;
        if (frameTimer < frameDuration) return;
        frameTimer -= frameDuration;

        frameIndex++;
        if (frameIndex >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }
        spriteRenderer.sprite = frames[frameIndex];
    }
}
