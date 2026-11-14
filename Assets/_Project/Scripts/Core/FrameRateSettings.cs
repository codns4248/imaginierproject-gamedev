using UnityEngine;

// 빌드한 게임의 프레임: 모니터 주사율에 맞춰 그린다 (VSync). 드라이버가 VSync를 끄는 경우를 대비해 targetFrameRate도 같이 건다.
// 에디터에서는 아무것도 하지 않는다 (제한 없이 그대로).
// (2026-11-15: 에디터 렉/지지직거림은 프레임 제한 문제가 아니라 에디터가 Direct3D 12로 돌아서 생긴 것이었다.
//  이 PC는 Unity Hub 명령줄 인수 -force-d3d11 로 해결. 에디터에서 화면을 더 부드럽게 보려면 Game 창 툴바의 VSync를 켠다.)
public static class FrameRateSettings
{
    private const int FallbackFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
#if !UNITY_EDITOR
        QualitySettings.vSyncCount = 1;
        double hz = Screen.currentResolution.refreshRateRatio.value;
        Application.targetFrameRate = hz >= 30.0 ? Mathf.RoundToInt((float)hz) : FallbackFps;
#endif
    }
}
