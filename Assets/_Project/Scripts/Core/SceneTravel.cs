using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 전환 유틸 (EnemyManager와 같은 정적 클래스 스타일).
// 도착 씬의 SpawnPoint(맞는 id)를 찾아 Player를 그 위치로 옮겨준다.
public static class SceneTravel
{
    private static string pendingSpawnId;

    public static void GoTo(string sceneName, string spawnId)
    {
        pendingSpawnId = spawnId;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(sceneName);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (string.IsNullOrEmpty(pendingSpawnId)) return;

        GameObject playerObj = GameObject.Find("Player");
        if (playerObj == null) return;

        foreach (SpawnPoint sp in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
        {
            if (sp.id == pendingSpawnId)
            {
                playerObj.transform.position = sp.transform.position;
                break;
            }
        }
        pendingSpawnId = null;
    }
}
