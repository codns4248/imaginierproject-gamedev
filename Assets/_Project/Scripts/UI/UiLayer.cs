using UnityEngine;
using UnityEngine.UI;

// 화면 UI(Screen Space Overlay 캔버스)의 층 순서를 한 곳에서 정한다. UI마다 자기 캔버스를 갖고,
// 그리는 순서(sortingOrder)는 반드시 여기 숫자만 쓴다 - 숫자가 클수록 위에 그려진다.
// (예전엔 HUD/ESC/강화 창이 씬 Canvas 하나에 섞여 있고 코드로 만든 팝업은 각자 숫자를 정해서,
//  ESC 설정 창이 특수 강화 창 등에 가려지는 문제가 있었다.)
//
//   ScreenEffect -10  안개/한파 같은 화면 효과 (HUD 아래)
//   Hud            0  체력, 타이머, 스테이지 번호, 무기 슬롯, 회복약, 자원 (HUD_Canvas)
//   HudNotice     10  기믹 안내 문구
//   Popup        100  무기 강화(V), 일반 강화(훈련소), 특수 강화(천문대), 배낭(창고), 긴급탈출 - 동시에 열리지 않음
//   LevelUp      200  레벨업 카드 + 경험치 바
//   Death        300  사망 암전 + 사망 결과 (Death_Canvas)
//   Toast        400  알림 문구 (ScreenToast)
//   Transition   900  스테이지 이동 페이드 (Transition_Canvas)
//   Esc         1000  ESC 설정 창 - 게임 밖에서 조정하는 창이라 무엇이 열려 있든 항상 맨 위 (Esc_Canvas)
public static class UiLayer
{
    public const int ScreenEffect = -10;
    public const int Hud = 0;
    public const int HudNotice = 10;
    public const int Popup = 100;
    public const int LevelUp = 200;
    public const int Death = 300;
    public const int Toast = 400;
    public const int Transition = 900;
    public const int Esc = 1000;

    // 씬에 있는 캔버스 이름 (hub 씬)
    public const string HudCanvas = "HUD_Canvas";
    public const string DeathCanvas = "Death_Canvas";
    public const string TransitionCanvas = "Transition_Canvas";
    public const string EscCanvas = "Esc_Canvas";

    public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
    public const float MatchWidthOrHeight = 0.5f;

    /// <summary>go에 화면 UI 캔버스(Overlay + 1280x720 기준 스케일러 + 클릭 판정)를 붙이고 층 순서를 정한다.</summary>
    public static Canvas Setup(GameObject go, int sortingOrder, bool clickable = true)
    {
        Canvas canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = MatchWidthOrHeight;

        if (clickable && go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>이름으로 씬의 캔버스를 찾고, 없으면 그 이름으로 새 캔버스를 만든다 (코드로 붙이는 화면 효과/안내 문구용).</summary>
    public static Transform FindOrCreate(string name, int sortingOrder, bool clickable = false)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Setup(go, sortingOrder, clickable);
        }
        return go.transform;
    }
}
