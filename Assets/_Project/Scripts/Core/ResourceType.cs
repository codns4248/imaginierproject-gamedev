// 파밍 가능한 자원 종류 (docs/schema.sql resource_type 기준).
// Rare: 적 드랍(ResourcePickup.SpawnRandomDrop)에는 안 섞이는 희귀 자원. 상인에게서만 구매 가능.
public enum ResourceType { Wood, Iron, Copper, Chemical, Oil, Rare }

public static class ResourceNames
{
    public static string Korean(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "나무";
            case ResourceType.Iron: return "철";
            case ResourceType.Copper: return "구리";
            case ResourceType.Chemical: return "화학물질";
            case ResourceType.Oil: return "기름";
            case ResourceType.Rare: return "희귀자원";
            default: return type.ToString();
        }
    }
}
