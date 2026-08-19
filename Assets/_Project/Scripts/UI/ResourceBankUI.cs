using UnityEngine;
using UnityEngine.UI;

// 이번 런에서 파밍한 자원 개수(runHeld)를 화면에 표시한다. HealthPotionUI와 동일한 패턴.
public class ResourceBankUI : MonoBehaviour
{
    public Text countText;

    void OnEnable()
    {
        ResourceBank.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        ResourceBank.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        if (countText == null) return;
        countText.text =
            $"목재 x{ResourceBank.GetRunHeld(ResourceType.Wood)}  " +
            $"철 x{ResourceBank.GetRunHeld(ResourceType.Iron)}  " +
            $"구리 x{ResourceBank.GetRunHeld(ResourceType.Copper)}  " +
            $"화학물질 x{ResourceBank.GetRunHeld(ResourceType.Chemical)}  " +
            $"기름 x{ResourceBank.GetRunHeld(ResourceType.Oil)}";
    }
}
