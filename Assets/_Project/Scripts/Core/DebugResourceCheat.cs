#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

// [임시 디버그 기능] 탐험 중(스테이지 안)에 K를 누르면 이번 런 자원(runHeld) 5종이 각각 1000씩, 희귀자원이 3개 늘어난다.
// 거점에서 K를 누르면 거점 창고(stash)에 희귀자원이 5개 들어온다 (특수 강화 테스트용).
// 무기 강화를 바로바로 테스트해 보려고 만든 것이라 테스트가 끝나면 이 파일만 지우면 된다.
// 씬에 오브젝트를 두지 않아도 되도록 게임 시작 시 스스로 설치하고, 에디터/개발 빌드에서만 컴파일된다.
public class DebugResourceCheat : MonoBehaviour
{
    private const int AmountPerResource = 1000;
    private const int RareAmount = 3;
    private const int HubRareAmount = 5;

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
        if (EnemyManager.PlayerDead) return;

        if (!StageManager.IsInStage)
        {
            // 거점: 비어 있는 탐험 자원 칸을 거쳐 창고로 확정한다 (거점에서는 runHeld가 항상 0이라 다른 자원은 그대로).
            ResourceBank.AddRunResource(ResourceType.Rare, HubRareAmount);
            ResourceBank.CommitRunToStash();
            Debug.Log($"[DebugResourceCheat] 거점 창고 희귀자원 +{HubRareAmount}");
            return;
        }

        foreach (ResourceType type in WeaponEnhanceUtil.AllTypes)
            ResourceBank.AddRunResource(type, AmountPerResource);
        ResourceBank.AddRunResource(ResourceType.Rare, RareAmount); // 긴급탈출(H) 테스트용

        Debug.Log($"[DebugResourceCheat] 자원 5종 각 +{AmountPerResource}, 희귀자원 +{RareAmount}");
    }
}
#endif
