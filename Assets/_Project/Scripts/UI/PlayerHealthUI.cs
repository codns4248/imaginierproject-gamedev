using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// PlayerHealth의 현재/최대 체력을 하트 모양 아이콘(Back/Front)으로 화면에 표시한다.
// 최대 체력 칸 수만큼 Back 아이콘을 왼쪽부터 나열해 배경으로 깔고, 그 위에 현재 체력 수만큼
// Front 아이콘을 왼쪽 칸부터 채워서 덮어씌운다.
// Front 아이콘은 항상 "왼쪽부터 currentHealth개"만 켜두는 방식이라, 데미지를 입으면 자연스럽게
// 가장 오른쪽 칸부터 사라지고, 회복하면 가장 왼쪽의 빈 칸부터 다시 채워지는 것처럼 보인다.
public class PlayerHealthUI : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public Sprite backSprite;
    public Sprite frontSprite;

    [Header("배치")]
    public float iconSize = 32f;
    public float iconSpacing = 36f; // 아이콘 사이의 간격(칸 하나의 폭)

    private readonly List<Image> backIcons = new List<Image>();
    private readonly List<Image> frontIcons = new List<Image>();
    private int builtMaxHealth = -1;

    void OnEnable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged += Refresh;
    }

    void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= Refresh;
    }

    void Update()
    {
        if (playerHealth == null) return;

        // 최대 체력이 바뀌면(강화 등) 칸 수가 달라져야 하므로 아이콘을 새로 만든다.
        if (playerHealth.MaxHealth != builtMaxHealth)
        {
            Rebuild();
        }
    }

    // 현재 maxHealth에 맞춰 Back/Front 아이콘을 전부 다시 만든다.
    private void Rebuild()
    {
        foreach (var icon in backIcons) Destroy(icon.gameObject);
        foreach (var icon in frontIcons) Destroy(icon.gameObject);
        backIcons.Clear();
        frontIcons.Clear();

        builtMaxHealth = playerHealth.MaxHealth;

        // Back을 전부 먼저 만들고 Front를 그 다음에 만들어야, Front가 Back 위에 확실히 그려진다
        // (같은 부모 밑에서는 나중에 추가된 오브젝트가 더 위에 그려지기 때문).
        for (int i = 0; i < builtMaxHealth; i++)
        {
            backIcons.Add(CreateIcon("Back " + i, backSprite, i));
        }
        for (int i = 0; i < builtMaxHealth; i++)
        {
            frontIcons.Add(CreateIcon("Front " + i, frontSprite, i));
        }

        Refresh();
    }

    private Image CreateIcon(string name, Sprite sprite, int index)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(iconSize, iconSize);
        rt.anchoredPosition = new Vector2(index * iconSpacing, 0f);

        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false; // 클릭 등 UI 입력을 막지 않도록

        return image;
    }

    // 현재 체력 수만큼 왼쪽부터 Front 아이콘을 켠다.
    private void Refresh()
    {
        for (int i = 0; i < frontIcons.Count; i++)
        {
            frontIcons[i].gameObject.SetActive(i < playerHealth.CurrentHealth);
        }
    }
}
