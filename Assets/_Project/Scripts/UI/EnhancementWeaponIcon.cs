using UnityEngine;
using UnityEngine.UI;

// 무기 강화 팝업(EnhancementPanel) 왼쪽 위의 네모 칸(WeaponIconBox)을 무기 슬롯과 같은 프레임 도형으로
// 보이게 하고, 그 안에 "지금 손에 들고 있는 무기"(WeaponAim.isHeld)의 스프라이트를 아이콘으로 표시한다.
// V키로 팝업이 열릴 때(OnEnable)와 열려 있는 동안 매 프레임 갱신하므로, 팝업이 떠 있는 상태에서
// Q로 무기를 바꿔도 바로 반영된다.
[RequireComponent(typeof(Image))]
public class EnhancementWeaponIcon : MonoBehaviour
{
    [Tooltip("플레이어의 무기 슬롯을 관리하는 WeaponSwitcher")]
    public WeaponSwitcher weaponSwitcher;

    [Tooltip("칸(WeaponIconBox)에 씌울 무기 슬롯 프레임 스프라이트")]
    public Sprite frameSprite;

    [Tooltip("칸 안에 그릴 무기 아이콘 크기 (칸 크기 대비 비율)")]
    [Range(0.3f, 1f)]
    public float iconFillRatio = 0.6f;

    private Image frame;
    private Image icon;

    void Awake()
    {
        frame = GetComponent<Image>();
        if (frameSprite != null)
        {
            frame.sprite = frameSprite;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
        }

        icon = CreateIcon();
        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    private Image CreateIcon()
    {
        GameObject go = new GameObject("HeldWeaponIcon", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private void Refresh()
    {
        if (icon == null) return;

        Sprite held = GetHeldWeaponSprite();
        icon.sprite = held;
        icon.enabled = held != null;

        RectTransform boxRt = (RectTransform)transform;
        RectTransform iconRt = (RectTransform)icon.transform;
        float size = Mathf.Min(boxRt.rect.width, boxRt.rect.height) * iconFillRatio;
        iconRt.sizeDelta = new Vector2(size, size);
    }

    private Sprite GetHeldWeaponSprite()
    {
        if (weaponSwitcher == null || weaponSwitcher.weaponSlots == null) return null;

        foreach (GameObject weapon in weaponSwitcher.weaponSlots)
        {
            if (weapon == null) continue;
            WeaponAim aim = weapon.GetComponent<WeaponAim>();
            if (aim == null || !aim.isHeld) continue;

            SpriteRenderer sr = weapon.GetComponent<SpriteRenderer>();
            return sr != null ? sr.sprite : null;
        }
        return null;
    }
}
