using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 타이틀 화면(title 씬) 버튼 동작. FUN-SYS-02~04.
public class TitleMenu : MonoBehaviour
{
    public string firstSceneName = "hub";
    public GameObject settingsPanel;

    // 타이틀 씬엔 PauseManager가 없어서 여기서 직접: 설정 창이 열려 있으면 ESC로 닫는다 (게임 안 팝업과 같은 규칙).
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
            && settingsPanel != null && settingsPanel.activeSelf)
            OnClickCloseSettings();
    }

    public void OnClickStart()
    {
        SceneManager.LoadScene(firstSceneName);
    }

    public void OnClickSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnClickCloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnClickQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
