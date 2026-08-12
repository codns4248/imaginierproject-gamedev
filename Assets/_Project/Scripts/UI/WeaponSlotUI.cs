using UnityEngine;
using UnityEngine.UI;

// WeaponSwitcher의 weaponSlots와 화면 하단 무기 슬롯 UI(WeaponSlotPanel/Slot1~5)를 연결한다.
// 각 슬롯에는 해당 무기 오브젝트가 실제로 쓰는 SpriteRenderer 스프라이트를 그대로 아이콘으로 표시하고,
// 슬롯 배경 프레임은 Honeti Buttons 텍스처(Inactive/Outlined)를 스왑해서 평소엔 비활성 프레임으로,
// 지금 손에 들고 있는(WeaponAim.isHeld) 무기의 슬롯만 강조 테두리 프레임으로 표시한다.
public class WeaponSlotUI : MonoBehaviour
{
    public WeaponSwitcher weaponSwitcher;
    public Image[] slotFrames = new Image[5];

    [Header("프레임 스프라이트 (Buttons 텍스처)")]
    public Sprite normalFrameSprite;
    public Sprite heldFrameSprite;

    [Header("아이콘 크기")]
    public float iconSize = 44f;

    private Image[] icons;

    void Start()
    {
        icons = new Image[slotFrames.Length];
        for (int i = 0; i < slotFrames.Length; i++)
        {
            if (slotFrames[i] == null) continue;
            icons[i] = CreateIcon(slotFrames[i].transform);
        }
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    private Image CreateIcon(Transform parent)
    {
        GameObject go = new GameObject("Icon", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }

    // 매 프레임 각 슬롯에 무기 아이콘을 채우고, 현재 들고 있는 무기의 슬롯 프레임만 강조 스프라이트로 바꾼다.
    private void Refresh()
    {
        if (weaponSwitcher == null || weaponSwitcher.weaponSlots == null) return;

        for (int i = 0; i < slotFrames.Length; i++)
        {
            GameObject weapon = i < weaponSwitcher.weaponSlots.Length ? weaponSwitcher.weaponSlots[i] : null;
            bool isHeld = false;

            if (icons[i] != null)
            {
                if (weapon != null)
                {
                    SpriteRenderer sr = weapon.GetComponent<SpriteRenderer>();
                    icons[i].sprite = sr != null ? sr.sprite : null;
                    icons[i].enabled = icons[i].sprite != null;

                    WeaponAim aim = weapon.GetComponent<WeaponAim>();
                    isHeld = aim != null && aim.isHeld;
                }
                else
                {
                    icons[i].enabled = false;
                }
            }

            if (slotFrames[i] != null)
            {
                slotFrames[i].sprite = isHeld ? heldFrameSprite : normalFrameSprite;
            }
        }
    }
}
