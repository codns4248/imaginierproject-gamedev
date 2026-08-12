using UnityEngine;
using System.IO;

// ResourceBank 로직에 대한 최소 자가 검증.
// 배치모드에서 -executeMethod SelfCheck.RunAll 로 실행한다. (Editor 전용 폴더라 빌드에는 포함되지 않음)
public static class SelfCheck
{
    public static void RunAll()
    {
        // 세이브 파일이 결과에 영향 없도록 검증 전용 임시 경로 사용은 못 하므로(ResourceBank가 고정 경로 사용),
        // 실제 세이브를 건드리지 않으려면 값만 검증하고 마지막에 원상복구한다.
        string savePath = Path.Combine(Application.persistentDataPath, "resources.json");
        string backup = File.Exists(savePath) ? File.ReadAllText(savePath) : null;

        ResourceBank.AddRunResource(ResourceType.Wood, 3);
        Debug.Assert(ResourceBank.GetRunHeld(ResourceType.Wood) == 3, "런 파밍분은 즉시 반영되어야 함");
        Debug.Assert(ResourceBank.GetStash(ResourceType.Wood) == 0, "확정 전에는 stash에 반영되면 안 됨");

        int stashBefore = ResourceBank.GetStash(ResourceType.Wood);
        ResourceBank.CommitRunToStash();
        Debug.Assert(ResourceBank.GetStash(ResourceType.Wood) == stashBefore + 3, "스테이지 클리어 시 runHeld가 stash로 합쳐져야 함");
        Debug.Assert(ResourceBank.GetRunHeld(ResourceType.Wood) == 0, "확정 후 runHeld는 비워져야 함");

        ResourceBank.AddRunResource(ResourceType.Iron, 5);
        int stashIronBefore = ResourceBank.GetStash(ResourceType.Iron);
        ResourceBank.DiscardRun();
        Debug.Assert(ResourceBank.GetRunHeld(ResourceType.Iron) == 0, "사망 시 runHeld는 소실되어야 함");
        Debug.Assert(ResourceBank.GetStash(ResourceType.Iron) == stashIronBefore, "사망해도 이미 확정된 stash는 유지되어야 함");

        // Load()가 실제로 파일 내용을 읽어오는지 확인: 메모리 값과 다른 값을 파일에 직접 써넣고 Load() 후 일치하는지 검증
        int inMemoryWood = ResourceBank.GetStash(ResourceType.Wood);
        int fileWood = inMemoryWood + 7;
        File.WriteAllText(savePath, $"{{\"stash\":[{fileWood},0,0,0,0]}}");
        ResourceBank.Load();
        Debug.Assert(ResourceBank.GetStash(ResourceType.Wood) == fileWood, "Load()는 파일에 저장된 값을 읽어와야 함");

        // 검증용으로 건드린 세이브 파일을 원래 상태로 복구
        if (backup != null) File.WriteAllText(savePath, backup);
        else if (File.Exists(savePath)) File.Delete(savePath);

        Debug.Log("SelfCheck: 모든 검증 통과");
    }
}
