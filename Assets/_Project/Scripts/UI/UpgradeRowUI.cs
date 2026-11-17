using UnityEngine;
using UnityEngine.UI;

// 무기 강화 창(UI_WeaponEnhancePanel)의 강화 한 줄(StatRow_*): 라벨 / 진행바(+5강마다 다이아몬드 마커) / "Lv N" /
// 자원 아이콘 + 보유량 / [강화  비용] 버튼. 줄의 부품은 프리팹에 이미 만들어져 있고(아래 필드), 여기서는 값과 연출만 바꾼다.
// 강화하면 바가 부드럽게 차오르고 흰 플래시/살짝 튀는 연출이 나온다 (창이 열려 있으면 timeScale=0이라
// Animate에는 Time.unscaledDeltaTime을 넘긴다).
public class UpgradeRowUI : MonoBehaviour
{
    // ===== 스타일 =====
    public static readonly Color BarFill = new Color(0.93f, 0.36f, 0.24f, 1f);
    public static readonly Color Gold = new Color(1f, 0.79f, 0.29f, 1f);
    public static readonly Color MarkerOff = new Color(0.17f, 0.2f, 0.26f, 1f);
    public static readonly Color TextNormal = new Color(0.92f, 0.92f, 0.92f);
    public static readonly Color TextCantAfford = new Color(1f, 0.42f, 0.38f);

    private const float FillLerpSpeed = 10f;
    private const float FlashDuration = 0.35f;
    private const float PunchDuration = 0.2f;

    [Header("이 줄이 올리는 강화 (자원 종류 = 스탯)")]
    public ResourceType type;
    public string label;          // 해금 배너/특수효과 목록에 쓰는 이름 (공격력 등)

    [Header("부품")]
    public Button button;
    public Text buttonText;
    public Text currencyText;
    public Text levelText;
    public RectTransform bar;
    public RectTransform fill;
    public Image fillImage;
    public Image flash;
    public int[] markerLevels;    // 다이아몬드 마커가 있는 레벨 (5, 10)
    public Image[] markerCores;   // 마커 안쪽 (달성하면 금색)

    private float shownRatio, targetRatio, flashTimer, punchTimer;

    /// <summary>[강화] 버튼을 누르면 onClick을 부른다 (창을 만들 때 한 번).</summary>
    public void Init(System.Action onClick)
    {
        if (button != null && onClick != null) button.onClick.AddListener(() => onClick());
    }

    // held: 이 강화에 쓰는 자원 보유량. enabled: 무기 미장착 등 외부 조건으로 강화 불가면 false (바/레벨도 비움).
    // capped: 이 종류는 최대 전이 아니지만 무기 전체 강화 횟수 상한에 걸려서 더 못 올리는 경우 (버튼에 "한도"를 보여준다).
    public void Refresh(int level, int maxLevel, int held, int cost, bool enabled, bool capped = false)
    {
        bool atMax = enabled && level >= maxLevel;
        capped = enabled && capped && !atMax;
        bool canAfford = held >= cost;

        targetRatio = enabled && maxLevel > 0 ? Mathf.Clamp01((float)level / maxLevel) : 0f;
        if (fillImage != null) fillImage.color = atMax ? Gold : BarFill;

        if (markerCores != null)
            for (int i = 0; i < markerCores.Length; i++)
                markerCores[i].color = enabled && level >= markerLevels[i] ? Gold : MarkerOff;

        if (levelText != null)
        {
            levelText.text = !enabled ? "" : atMax ? "MAX" : $"Lv {level}";
            levelText.color = atMax ? Gold : TextNormal;
        }

        if (button != null) button.interactable = enabled && !atMax && !capped && canAfford;
        if (buttonText != null) buttonText.text = atMax ? "MAX" : capped ? "한도" : $"강화  {cost}";

        // 보유량. 부족하면 빨간색 (비용은 버튼에 표시).
        if (currencyText != null)
        {
            currencyText.text = $"x{held}";
            currencyText.color = atMax || capped || canAfford ? TextNormal : TextCantAfford;
        }
    }

    // 강화 직후 호출: 플래시 + 바/레벨 텍스트가 살짝 튄다.
    public void Pulse()
    {
        flashTimer = FlashDuration;
        punchTimer = PunchDuration;
    }

    // 창을 열 때: 차오르는 연출 없이 현재 값으로 바로 맞춘다.
    public void Snap()
    {
        shownRatio = targetRatio;
        flashTimer = punchTimer = 0f;
        Animate(0f);
    }

    public void Animate(float dt)
    {
        if (fill == null) return;

        shownRatio = Mathf.Lerp(shownRatio, targetRatio, 1f - Mathf.Exp(-FillLerpSpeed * dt));
        if (Mathf.Abs(shownRatio - targetRatio) < 0.001f) shownRatio = targetRatio;
        fill.anchorMax = new Vector2(shownRatio, 1f);

        flashTimer = Mathf.Max(0f, flashTimer - dt);
        flash.color = new Color(1f, 1f, 1f, 0.75f * (flashTimer / FlashDuration));

        punchTimer = Mathf.Max(0f, punchTimer - dt);
        float punch = punchTimer / PunchDuration; // 1 -> 0
        bar.localScale = new Vector3(1f, 1f + 0.35f * punch, 1f);
        levelText.transform.localScale = Vector3.one * (1f + 0.3f * punch);
    }
}
