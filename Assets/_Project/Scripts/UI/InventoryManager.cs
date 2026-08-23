using UnityEngine;
using UnityEngine.InputSystem;

// V키로 인벤토리 패널(InventoryPanel)을 열고 닫는다. ESC의 설정 패널(PauseManager)과는 완전히
// 별도의 상태로 동작하지만, 열려 있는 동안은 PauseManager.SetInventoryPaused()로 알려서
// Time.timeScale을 0으로 만들어 게임을 함께 멈춘다.
//
// ESC 설정 패널이 열려 있는 동안에는 V키를 무시한다.
public class InventoryManager : MonoBehaviour
{
    public GameObject inventoryPanel; // 인벤토리 UI 오브젝트 (InventoryPanel)
    public PauseManager pauseManager; // ESC 상태 확인 + Time.timeScale 계산에 상태를 알려주기 위한 참조

    private bool isInventoryOpen;

    void Update()
    {
        if (!Keyboard.current.vKey.wasPressedThisFrame) return;

        // ESC 설정 패널이 열려 있으면 V키를 완전히 무시한다.
        if (pauseManager != null && pauseManager.IsEscPaused) return;

        SetInventoryOpen(!isInventoryOpen);
    }

    private void SetInventoryOpen(bool open)
    {
        isInventoryOpen = open;

        if (inventoryPanel != null) inventoryPanel.SetActive(open);
        if (pauseManager != null) pauseManager.SetInventoryPaused(open);
    }
}
