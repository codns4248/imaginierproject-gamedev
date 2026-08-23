using UnityEngine;
using UnityEngine.InputSystem;

// ESC 키로 설정 패널(PausePanel)을 열고 닫는다. V키로 여는 인벤토리 패널(InventoryManager)과는
// 완전히 별도의 상태로 동작하지만, 둘 중 하나라도 열려 있으면 Time.timeScale을 0으로 만들어
// 물리/애니메이션/스폰 등 deltaTime 기반 로직을 전부 함께 멈춘다.
public class PauseManager : MonoBehaviour
{
    public GameObject pausePanel; // 반투명 패널 + "PAUSED" 텍스트
    public GameObject blurVolume; // Depth of Field 블러를 담은 Volume 오브젝트

    private bool isEscPaused;      // ESC로 연 설정 패널이 열려 있는지
    private bool isInventoryPaused; // InventoryManager가 V로 연 인벤토리 패널이 열려 있는지 (SetInventoryPaused로 전달받음)

    // ESC 설정 패널이 지금 열려 있는지. InventoryManager가 "ESC가 눌린 상태에서는 V키 무시"를
    // 판단할 때 이 값을 참조한다.
    public bool IsEscPaused => isEscPaused;

    // Time.timeScale이 0이어도 Update()는 계속 호출되므로, deltaTime에 의존하지 않고
    // 마우스 좌표를 직접 읽어 회전/이동시키는 스크립트(WeaponAim, PlayerMovement 등)는
    // timeScale만으로는 멈추지 않는다. 그런 스크립트들이 직접 참조해서 자체적으로 멈출 수
    // 있도록 "설정 또는 인벤토리 중 하나라도 열려 있는지"를 정적 플래그로 노출한다.
    public static bool IsPaused { get; private set; }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            SetEscPaused(!isEscPaused);
        }
    }

    private void SetEscPaused(bool paused)
    {
        isEscPaused = paused;

        if (pausePanel != null) pausePanel.SetActive(paused);
        if (blurVolume != null) blurVolume.SetActive(paused);

        ApplyTimeScale();
    }

    // InventoryManager가 V키로 인벤토리 패널을 열고 닫을 때마다 호출해서 현재 상태를 알려준다.
    public void SetInventoryPaused(bool paused)
    {
        isInventoryPaused = paused;
        ApplyTimeScale();
    }

    // ESC 설정 패널과 V 인벤토리 패널 중 하나라도 열려 있으면 정지, 둘 다 닫히면 재개.
    private void ApplyTimeScale()
    {
        bool anyOpen = isEscPaused || isInventoryPaused;
        Time.timeScale = anyOpen ? 0f : 1f;
        IsPaused = anyOpen;
    }

    // 외부(버튼 OnClick 등)에서 설정 패널을 직접 열고 닫고 싶을 때 쓰는 기존 공개 API.
    public void SetPaused(bool paused)
    {
        SetEscPaused(paused);
    }
}
