using UnityEngine;
using UnityEngine.UI;

// 자원 개수를 화면에 표시한다. HealthPotionUI와 동일한 패턴.
// showStash가 꺼져있으면(기본, 스테이지용) 이번 런 파밍분(runHeld)을,
// 켜져있으면(로비용) 확정 보관된 stash를 보여준다.
public class ResourceBankUI : MonoBehaviour
{
    public Text countText;
    public bool showStash;

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
        System.Func<ResourceType, int> get = showStash ? ResourceBank.GetStash : (System.Func<ResourceType, int>)ResourceBank.GetRunHeld;
        countText.text =
            $"목재 x{get(ResourceType.Wood)}  " +
            $"철 x{get(ResourceType.Iron)}  " +
            $"구리 x{get(ResourceType.Copper)}  " +
            $"화학물질 x{get(ResourceType.Chemical)}  " +
            $"기름 x{get(ResourceType.Oil)}";
    }
}
