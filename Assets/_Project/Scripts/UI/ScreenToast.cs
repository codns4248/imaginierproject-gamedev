using UnityEngine;
using UnityEngine.UI;

// 화면 위쪽 가운데에 몇 초 떴다가 서서히 사라지는 안내 문구 (창고가 가득 참, 배낭 등).
// 처음 Show할 때 코드로 캔버스를 만들고 씬은 건드리지 않는다. 게임이 멈춰 있어도 실제 시간으로 사라진다.
public class ScreenToast : MonoBehaviour
{
    private const float Duration = 4f;
    private const float FadeTime = 0.6f;

    private static ScreenToast instance;

    private Text text;
    private Color baseColor = Color.white;
    private float timer;

    public static void Show(string message, Color color)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("ScreenToast");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenToast>();
            instance.Build();
        }

        instance.text.text = message;
        instance.baseColor = color;
        instance.timer = Duration;
        instance.text.gameObject.SetActive(true);
        instance.ApplyAlpha();
    }

    private void Build()
    {
        UiLayer.Setup(gameObject, UiLayer.Toast, clickable: false); // 팝업/레벨업보다 위, ESC 설정 창보다 아래

        LevelUpConfig cfg = LevelUpConfig.Instance;
        Font font = cfg != null && cfg.font != null ? cfg.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text = PixelUi.NewText("Message", transform, font, 22, Color.white, TextAnchor.MiddleCenter);
        PixelUi.PlaceTop(text.rectTransform, new Vector2(0f, -150f), new Vector2(900f, 80f));
        text.gameObject.SetActive(false);
    }

    void Update()
    {
        if (timer <= 0f) return;
        if (PauseManager.IsEscPaused) return; // ESC 창에 가려진 동안 알림이 몰래 사라지지 않게 멈춘다
        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f) { text.gameObject.SetActive(false); return; }
        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        Color c = baseColor;
        c.a = Mathf.Clamp01(timer / FadeTime);
        text.color = c;
    }
}
