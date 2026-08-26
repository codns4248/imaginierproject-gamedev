using UnityEngine;
using UnityEngine.InputSystem;

// V키로 무기 강화 팝업(EnhancementPanel)을 열고 닫는다. ESC의 설정 패널(PauseManager)과는 완전히
// 별도의 상태로 동작하지만, 열려 있는 동안은 PauseManager.SetInventoryPaused()로 알려서
// Time.timeScale을 0으로 만들어 게임을 함께 멈추다.
//
// ESC 설정 패널이 열려 있는 동안에는 V키를 무시한다.
// 강화 버튼 클릭, 재화 차감, 스탯 증가 등 실제 강화 로직은 아직 연결되어 있지 않다 (레이아웃만 구성된 상태).
public class WeaponEnhancementManager : MonoBehaviour
{
    public GameObject enhancementPanel; // 무기 강화 팝업 UI 오브젝트 (EnhancementPanel)
    public PauseManager pauseManager; // ESC 상태 확인 + Time.timeScale 계산에 상태를 알려주기 위한 참조

    private bool isPanelOpen;

    void Update()
    {
        if (!Keyboard.current.vKey.wasPressedThisFrame) return;

        // ESC 설정 패널이 열려 있으면 V키를 완전히 무시한다.
        if (pauseManager != null && pauseManager.IsEscPaused) return;

        SetPanelOpen(!isPanelOpen);
    }

    private void SetPanelOpen(bool open)
    {
        isPanelOpen = open;

        if (enhancementPanel != null) enhancementPanel.SetActive(open);
        if (pauseManager != null) pauseManager.SetInventoryPaused(open);
    }
}
