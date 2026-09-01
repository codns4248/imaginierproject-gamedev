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
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
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
