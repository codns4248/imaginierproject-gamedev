using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 긴급탈출: 탐험 중 H키를 누르면 이번 탐험에서 주운 희귀자원(runHeld) 1개를 써서 언제든 거점으로 빠져나갈 수 있다.
// (예전엔 공짜로 언제든 돌아가는 테스트용 복귀였다.)
//  - 희귀자원이 있으면 "희귀 자원을 사용하여 탈출하시겠습니까?" 확인창이 뜨고 [예]/[아니오]를 마우스로 누른다.
//    [예] = 희귀자원 1개를 쓰고 탈출 포탈과 같은 규칙으로 자원을 확정한 뒤 거점으로, [아니오] = 창만 닫힌다. 창이 떠 있을 때 H를 다시 눌러도 닫힌다.
//  - 확인창은 게임을 멈추지 않는다: 창을 띄운 채 [예]를 누르기 전에 죽으면 창은 사라지고 그대로 게임 오버.
//  - 희귀자원이 없으면 플레이어 머리 위에 "자원이 부족합니다"가 잠깐 떴다가 사라진다.
//  - 거점에서는 동작하지 않고, ESC/강화창/레벨업 카드처럼 게임을 멈추는 창이 떠 있을 때도 H를 받지 않는다.
// 창은 공용 확인창 프리팹(UI_ConfirmDialog, Resources/UiPrefabs.asset)을 쓰고 글자/버튼 동작만 채운다. 씬은 건드리지 않는다.
public class RetreatManager : MonoBehaviour
{
    [Tooltip("\"자원이 부족합니다\" 경고 문구 폰트 (확인창 글꼴은 프리팹에서 정함). 비우면 레벨업 UI와 같은 폰트(LevelUpConfig.font), 그것도 없으면 내장 폰트.")]
    public Font promptFont;

    private const int EscapeCost = 1;
    private const float WarningDuration = 1.2f;
    private const float WarningRise = 40f;   // 떠 있는 동안 위로 올라가는 거리(UI 픽셀)

    private static readonly Color WarnColor = new Color(1f, 0.45f, 0.4f);

    private LevelUpConfig cfg;
    private Font font;
    private GameObject canvasGO;
    private RectTransform canvasRect;
    private ConfirmDialogView dialog;
    private RectTransform popup; // dialog의 창 영역 (마우스가 창 위에 있는지 판정)
    private Text warningText;
    private float warningTimer;
    private Transform player;
    private bool isOpen;

    void Awake()
    {
        cfg = LevelUpConfig.Instance;
        font = promptFont != null ? promptFont : cfg.font != null ? cfg.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
    }

    void OnDestroy()
    {
        UiPointerBlock.Active = false;
        if (canvasGO != null) Destroy(canvasGO);
    }

    void Update()
    {
        // 탐험이 끝났거나(클리어 후 이동/사망/추출) 죽었으면 떠 있던 창을 닫는다.
        if (isOpen && (!StageManager.IsInStage || EnemyManager.PlayerDead)) Close();

        UiPointerBlock.Active = isOpen && popup != null && !PauseManager.IsPaused && Mouse.current != null
            && RectTransformUtility.RectangleContainsScreenPoint(popup, Mouse.current.position.ReadValue(), null);
        UpdateWarning();

        if (Keyboard.current == null || !Keyboard.current.hKey.wasPressedThisFrame) return;
        if (isOpen) { Close(); return; }
        if (!StageManager.IsInStage || EnemyManager.PlayerDead || PauseManager.IsPaused) return;

        if (ResourceBank.GetRunHeld(ResourceType.Rare) < EscapeCost) ShowWarning();
        else Open();
    }

    private void Open()
    {
        if (dialog == null) return;
        EnsureEventSystem();
        dialog.detail.text = $"희귀 자원 {EscapeCost}개 소모  (보유 {ResourceBank.GetRunHeld(ResourceType.Rare)}개)";
        popup.gameObject.SetActive(true);
        isOpen = true;
        EscClose.Push(this, () => isOpen, Close);
    }

    private void Close()
    {
        isOpen = false;
        if (popup != null) popup.gameObject.SetActive(false);
        UiPointerBlock.Active = false;
    }

    private void OnYes()
    {
        if (!isOpen || PauseManager.IsPaused) return;
        if (!StageManager.IsInStage || EnemyManager.PlayerDead) { Close(); return; }
        if (!ResourceBank.TrySpendRunResource(ResourceType.Rare, EscapeCost)) { Close(); ShowWarning(); return; }
        Close();
        Sfx.Play(SfxId.Escape);

        // 탈출: 파밍 자원은 stash로 확정하되(탈출 포탈과 동일), 이번 런의 무기(강화 + 필드/상인에서
        // 얻은 여분 무기)는 사망/추출과 같은 규칙으로 전부 초기화한다.
        ResourceBank.CommitRunToStash();
        WeaponEnhanceStore.ResetForNewRun();
        LevelUpSystem.ResetForNewRun(); // 레벨/경험치/레벨업 능력/부활 횟수
        WeaponSwitcher weaponSwitcher = FindFirstObjectByType<WeaponSwitcher>();
        if (weaponSwitcher != null) weaponSwitcher.ResetToStartingWeapon();
        StageProgress.ResetToFirstStage();
        StageManager.ReturnToHub();
    }

    private void OnNo()
    {
        if (!isOpen || PauseManager.IsPaused) return;
        Close();
    }

    // "자원이 부족합니다": 플레이어 머리 위에서 살짝 떠오르며 사라진다.
    private void ShowWarning()
    {
        Sfx.Play(SfxId.Deny);
        warningTimer = WarningDuration;
        warningText.gameObject.SetActive(true);
        UpdateWarning();
    }

    private void UpdateWarning()
    {
        if (warningTimer <= 0f) return;
        if (!PauseManager.IsEscPaused) warningTimer -= Time.unscaledDeltaTime; // ESC로 멈춘 동안엔 경고 문구도 그대로
        if (warningTimer <= 0f || Camera.main == null)
        {
            warningText.gameObject.SetActive(false);
            return;
        }

        if (player == null)
        {
            GameObject p = GameObject.Find("Player");
            if (p != null) player = p.transform;
        }
        Vector3 head = player != null ? player.position + Vector3.up * 0.45f : Camera.main.transform.position;
        Vector2 screen = Camera.main.WorldToScreenPoint(head);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

        float t = 1f - warningTimer / WarningDuration; // 0 -> 1
        warningText.rectTransform.anchoredPosition = local + new Vector2(0f, WarningRise * t);
        Color c = WarnColor;
        c.a = t < 0.7f ? 1f : Mathf.Clamp01((1f - t) / 0.3f);
        warningText.color = c;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    // ===================== 만들기 =====================

    private void Build()
    {
        canvasGO = new GameObject("EmergencyEscapeUI");
        UiLayer.Setup(canvasGO, UiLayer.Popup); // 팝업 층: 레벨업 카드 창이 뜨면 그 뒤에 가려지고 클릭도 막힌다
        canvasRect = (RectTransform)canvasGO.transform;

        BuildPopup();

        warningText = PixelUi.NewText("NotEnough", canvasRect, font, 20, WarnColor, TextAnchor.MiddleCenter);
        warningText.rectTransform.anchorMin = warningText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        warningText.rectTransform.sizeDelta = new Vector2(300f, 30f);
        warningText.text = "자원이 부족합니다";
        warningText.gameObject.SetActive(false);
    }

    // 공용 확인창 프리팹: 질문, 비용, [예]/[아니오]. 게임이 계속 돌아가므로 프리팹에서 화면 아래쪽에 뜨게 놓여 있다.
    private void BuildPopup()
    {
        UiPrefabs prefabs = UiPrefabs.Instance;
        if (prefabs == null || prefabs.confirmDialog == null) return;

        dialog = Instantiate(prefabs.confirmDialog, canvasRect, false);
        dialog.name = "EscapePopup";
        popup = dialog.Rect;
        dialog.question.text = "희귀 자원을 사용하여 탈출하시겠습니까?";
        dialog.yes.label.text = "예";
        dialog.no.label.text = "아니오";
        dialog.yes.onClick = OnYes;
        dialog.no.onClick = OnNo;
        popup.gameObject.SetActive(false);
    }
}
