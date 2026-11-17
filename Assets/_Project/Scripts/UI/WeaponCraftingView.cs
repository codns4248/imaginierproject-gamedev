using UnityEngine;
using UnityEngine.UI;

// V 무기 강화 창의 [제작] 탭 내용: 이번 탐험에서 주운 희귀자원(runHeld) 1개로 원하는 무기를 확정 제작한다.
//  - 무기 5종을 카드로 보여 주고, 카드를 누르면 "제작하시겠습니까?" 확인창 -> [예]면 희귀자원 1개를 쓰고
//    0강 무기를 플레이어가 바라보는 방향 조금 앞에 떨어뜨린다 (인벤토리로 바로 넣지 않는다: F로 줍는다).
//  - 슬롯이 꽉 차도 만들 수 있다 (B로 버리고 새로 만든 걸 주워 바꿀 수 있게). 같은 무기도 여러 번 만들 수 있다.
//  - 희귀자원이 부족하면 카드가 어둡게 비활성화되고 눌리지 않는다.
// 강화 창의 바탕을 그대로 같이 쓰는 탭 내용이라 자기 바탕은 없다. 탭 전환(밀려 들어오고 사라지는 연출)은
// WeaponEnhancementManager가 group(CanvasGroup)으로 한다. 부품은 무기 강화 창 프리팹(UI_WeaponEnhancePanel) 안에 있다.
public class WeaponCraftingView : MonoBehaviour
{
    public const int Cost = 1;
    private const float DropDistance = 0.6f; // 플레이어 앞 몇 유닛에 떨어뜨릴지 (줍기 반경 0.5보다 살짝 멀어서 한 걸음 다가가 줍는다)

    [Header("부품")]
    public CanvasGroup group;
    public Text infoText;
    public CraftCardView[] cards;     // 왼쪽부터 검, 창, 권총, 기관단총, 수류탄
    public GameObject confirmRoot;    // 카드를 누르면 뜨는 확인창 (탭 내용 전체를 어둡게 덮음)
    public Text confirmText;
    public PixelButton yesButton;
    public PixelButton noButton;

    private WeaponType pending;
    private bool wired;

    public CanvasGroup Group => group;

    private bool CanAfford => ResourceBank.GetRunHeld(ResourceType.Rare) >= Cost;

    void Awake() => Wire();

    void OnEnable()
    {
        Wire();
        ResourceBank.OnChanged += Refresh;
        HideConfirm();
        Refresh();
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= Refresh;
    }

    // 버튼 동작은 저장되지 않으므로 처음 켜질 때 한 번 연결한다.
    private void Wire()
    {
        if (wired) return;
        wired = true;
        foreach (CraftCardView card in cards) card.onClick = OnCardClicked;
        if (yesButton != null) yesButton.onClick = OnConfirmYes;
        if (noButton != null) noButton.onClick = HideConfirm;
    }

    private void Refresh()
    {
        if (cards == null || infoText == null) return;
        int held = ResourceBank.GetRunHeld(ResourceType.Rare);
        infoText.text = $"희귀 자원 {Cost}개로 원하는 무기를 만듭니다   <color=#{(CanAfford ? "FFDB6B" : "FF6B61")}>(보유 {held}개)</color>";
        foreach (CraftCardView card in cards) card.SetEnabled(CanAfford);
    }

    private void OnCardClicked(WeaponType type)
    {
        if (!CanAfford || confirmRoot.activeSelf) return;
        pending = type;
        string name = WeaponEnhancementManager.WeaponDisplayName(type);
        confirmText.text = $"{name}{ObjectParticle(name)} 제작하시겠습니까?";
        confirmRoot.SetActive(true);
        // 강화 창보다 나중에 열린 확인창이라 ESC를 누르면 확인창이 먼저 닫히고, 한 번 더 누르면 강화 창이 닫힌다.
        EscClose.Push(this, () => confirmRoot != null && confirmRoot.activeInHierarchy, HideConfirm);
    }

    private void OnConfirmYes()
    {
        HideConfirm();
        if (!StageManager.IsInStage || EnemyManager.PlayerDead) return;
        if (!ResourceBank.TrySpendRunResource(ResourceType.Rare, Cost)) { Refresh(); return; }

        GameObject player = GameObject.Find("Player");
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        Vector2 facing = movement != null ? movement.FacingDirection : Vector2.right;
        WeaponPickup.SpawnDrop(origin + (Vector3)(facing * DropDistance), pending); // 새 무기라 강화 레벨 없음(0강)
        Sfx.Play(SfxId.EnhanceSuccess);
        Refresh();
    }

    // 받침이 있으면 "을", 없으면 "를" (검을 / 권총을 ...).
    private static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "을";
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return "을(를)";
        return (last - 0xAC00) % 28 != 0 ? "을" : "를";
    }

    private void HideConfirm()
    {
        if (confirmRoot != null) confirmRoot.SetActive(false);
    }
}
