using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 전체를 덮는 검은 이미지의 투명도를 서서히 올려서 페이드 아웃(암전) 연출을 담당한다.
// 평소에는 완전히 투명해서 보이지 않다가, FadeToBlack()이 호출되면 지정한 시간에 걸쳐 서서히 어두워진다.
[RequireComponent(typeof(Image))]
public class DeathFadeUI : MonoBehaviour
{
    private Image image;

    void Awake()
    {
        image = GetComponent<Image>();
        SetAlpha(0f);
    }

    public void FadeToBlack(float duration)
    {
        // 비활성 상태에서는 코루틴을 시작할 수 없어 콘솔에 에러가 찍히므로 조용히 무시한다.
        // (StageManager가 DeathFade를 항상 켜 두기 때문에 정상 흐름에선 이 경로를 타지 않는다.)
        if (!gameObject.activeInHierarchy) return;

        StopAllCoroutines();
        StartCoroutine(FadeRoutine(duration));
    }

    // 거점으로 복귀했을 때 검게 덮인 화면을 다시 투명하게 되돌린다.
    public void ResetFade()
    {
        StopAllCoroutines();
        SetAlpha(0f);
    }

    private IEnumerator FadeRoutine(float duration)
    {
        // unscaled를 쓴다: 페이드 도중 DeathResultUI가 Time.timeScale을 0으로 만드는데,
        // 스케일 시간을 쓰면 그 순간 deltaTime이 0이 되어 암전이 중간값(반투명)에서 영영 멈춰버린다.
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        SetAlpha(1f);
    }

    private void SetAlpha(float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
