using UnityEngine;
using UnityEngine.UI;

// HealthPotion의 보유 개수를 아이콘 + "xN" 텍스트로 체력바 옆에 표시한다.
public class HealthPotionUI : MonoBehaviour
{
    public HealthPotion healthPotion;
    public Text countText;

    void OnEnable()
    {
        if (healthPotion != null) healthPotion.OnPotionCountChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (healthPotion != null) healthPotion.OnPotionCountChanged -= Refresh;
    }

    private void Refresh()
    {
        if (healthPotion == null || countText == null) return;
        countText.text = "x" + healthPotion.PotionCount;
    }
}
