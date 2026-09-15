// 파밍 가능한 자원 종류 (docs/schema.sql resource_type 기준).
// Rare: 적 드랍(ResourcePickup.SpawnRandomDrop)에는 안 섞이는 희귀 자원. 상인에게서만 구매 가능.
public enum ResourceType { Wood, Iron, Copper, Chemical, Oil, Rare }
