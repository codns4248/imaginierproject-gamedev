using UnityEngine;
using UnityEngine.InputSystem;

// ESC 키로 게임을 일시정지/재개한다. Time.timeScale을 0으로 만들어 물리/애니메이션/스폰 등
// deltaTime 기반 로직을 전부 함께 멈추고, 동시에 배경 블러(Volume)와 반투명 패널을 켜서
// 화면이 일시정지 상태임을 보여준다.
public class PauseManager : MonoBehaviour
{
    public GameObject pausePanel; // 반투명 패널 + "PAUSED" 텍스트
    public GameObject blurVolume; // Depth of Field 블러를 담은 Volume 오브젝트

    private bool isPaused;

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetPaused(!isPaused);
        }
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pausePanel != null) pausePanel.SetActive(paused);
        if (blurVolume != null) blurVolume.SetActive(paused);
    }
}
