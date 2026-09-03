using UnityEngine;
using UnityEngine.SceneManagement;

// 타이틀 화면(start_lobby) 버튼 동작. FUN-SYS-02~04.
public class TitleMenu : MonoBehaviour
{
    public string firstSceneName = "MainScene";
    public GameObject settingsPanel;

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
