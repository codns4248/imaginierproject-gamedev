#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// [임시 디버그 기능] 탐험 중(스테이지 안)에 K를 누르면 이번 런 자원(runHeld) 5종이 각각 1000씩 늘어난다.
// 무기 강화를 바로바로 테스트해 보려고 만든 것이라 테스트가 끝나면 이 파일만 지우면 된다.
// 씬에 오브젝트를 두지 않아도 되도록 게임 시작 시 스스로 설치하고, 에디터/개발 빌드에서만 컴파일된다.
public class DebugResourceCheat : MonoBehaviour
{
    private const int AmountPerResource = 1000;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<DebugResourceCheat>() != null) return;

        GameObject go = new GameObject("DebugResourceCheat");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugResourceCheat>();
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.kKey.wasPressedThisFrame) return;
        if (!StageManager.IsInStage) return; // 거점에서는 동작하지 않는다 (탐험 중에만)
        if (EnemyManager.PlayerDead) return;

        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes)
            ResourceBank.AddRunResource(type, AmountPerResource);

        Debug.Log($"[DebugResourceCheat] 자원 5종 각 +{AmountPerResource}");
    }
}
#endif
