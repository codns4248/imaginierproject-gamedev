using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 맵을 개별 씬으로 분리해서 불러오는 기능 - 씬을 내리고/올리는 동안 화면을 검은색으로
// 가려서 로딩 과정이 안 보이게 한다. StageManager는 static 클래스라 코루틴을 직접 돌릴 수 없어서,
// 이 컴포넌트(MainScene에 항상 떠 있는 오브젝트)를 통해 실행한다.
public class StageSceneTransition : MonoBehaviour
{
    public static StageSceneTransition Instance { get; private set; }

    [Header("검은 화면이 최소한 눈에 보이도록 유지하는 시간")]
    public float minVisibleDuration = 0.15f;

    private Image blackImage;

    void Awake()
    {
        Instance = this;
        CreateBlackOverlay();
    }

    private void CreateBlackOverlay()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null) return;

        GameObject overlayGO = new GameObject("StageSceneTransitionOverlay");
        overlayGO.transform.SetParent(canvas.transform, false);
        overlayGO.transform.SetAsLastSibling(); // 다른 UI보다 위에 그려지도록 맨 뒤로

        RectTransform rt = overlayGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        blackImage = overlayGO.AddComponent<Image>();
        blackImage.color = Color.black;
        blackImage.raycastTarget = false;
        overlayGO.SetActive(false);
    }

    // sceneToUnload/sceneToLoad는 비워두면(null/"") 해당 단계를 건너뛴다. onReady는 씬 전환이
    // 끝난 뒤(플레이어 위치 이동 등 기존 로직) 실행할 콜백이다.
    public static void Transition(string sceneToUnload, string sceneToLoad, Action onReady)
    {
        if (Instance == null || Instance.blackImage == null)
        {
            onReady?.Invoke();
            return;
        }
        Instance.StartCoroutine(Instance.TransitionRoutine(sceneToUnload, sceneToLoad, onReady));
    }

    private IEnumerator TransitionRoutine(string sceneToUnload, string sceneToLoad, Action onReady)
    {
        blackImage.gameObject.SetActive(true);
        yield return null; // 검은 화면이 실제로 한 프레임 그려지도록 대기

        if (!string.IsNullOrEmpty(sceneToUnload))
        {
            Scene s = SceneManager.GetSceneByPath(sceneToUnload);
            if (s.IsValid() && s.isLoaded)
                yield return SceneManager.UnloadSceneAsync(s);
        }

        if (!string.IsNullOrEmpty(sceneToLoad))
            yield return SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);

        onReady?.Invoke();

        yield return new WaitForSeconds(minVisibleDuration);
        blackImage.gameObject.SetActive(false);
    }
}
