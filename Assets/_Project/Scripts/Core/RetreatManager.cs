using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// R키로 "거점으로 복귀하시겠습니까?" 확인창을 띄운다. Y로 확정하면 지금까지 파밍한 자원을
// 확정(익스트랙션과 동일하게 처리)하고 거점으로 이동한다. N/ESC로 취소.
// 스테이지 클리어(5층)를 못 채웠어도, 도중에 언제든 안전하게 빠져나갈 수 있는 수단
// (StageExtraction의 자동 복귀는 클리어했을 때만 발동하므로 서로 겹치지 않는다).
// ponytail: 확인창을 클릭 UI(Button) 대신 순수 키 입력으로만 만들었다 - 최근 EnhanceButton
// 클릭이 EventSystem 설정 문제로 안 먹었던 걸 겪어서, 정말 간단한 확인창까지 그 리스크를
// 다시 지지 않으려고 일부러 키보드 전용으로 뒀다.
public class RetreatManager : MonoBehaviour
{
    private GameObject promptGO;
    private bool isPromptOpen;
    private PauseManager pauseManager;

    void Awake()
    {
        pauseManager = FindFirstObjectByType<PauseManager>();
        BuildPrompt();
    }

    void Update()
    {
        if (isPromptOpen)
        {
            if (Keyboard.current.yKey.wasPressedThisFrame) Confirm();
            else if (Keyboard.current.nKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame) ClosePrompt();
            return;
        }

        if (Keyboard.current.rKey.wasPressedThisFrame) OpenPrompt();
    }

    private void OpenPrompt()
    {
        if (promptGO == null) BuildPrompt();
        if (promptGO == null) return;

        isPromptOpen = true;
        promptGO.SetActive(true);
        if (pauseManager != null) pauseManager.SetInventoryPaused(true);
    }

    private void ClosePrompt()
    {
        isPromptOpen = false;
        promptGO.SetActive(false);
        if (pauseManager != null) pauseManager.SetInventoryPaused(false);
    }

    private void Confirm()
    {
        ResourceBank.CommitRunToStash();
        StageProgress.ResetToFirstStage();
        ClosePrompt();
        StageManager.ReturnToHub();
    }

    // 이미 만들어져 있으면 재사용하고(도메인 리로드 등으로 필드 참조만 끊긴 경우 대비), 없으면 새로 만든다.
    private void BuildPrompt()
    {
        GameObject existing = GameObject.Find("RetreatPrompt");
        if (existing != null)
        {
            promptGO = existing;
            promptGO.SetActive(false);
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Image(배경)와 Text는 둘 다 Graphic이라 같은 오브젝트에 공존 못 하므로 자식으로 분리한다.
        GameObject go = new GameObject("RetreatPrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(520f, 90f);

        Image bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        Text text = textGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "거점으로 복귀하시겠습니까?\n(Y: 예 / N: 아니오)";

        promptGO = go;
        promptGO.SetActive(false);
    }
}
