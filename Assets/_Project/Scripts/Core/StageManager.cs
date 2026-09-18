using UnityEngine;

// MainScene 안에서 거점 <-> 스테이지 구역 이동을 담당한다. 씬 전환 없이 좌표만 옮긴다
// (거점과 각 스테이지 구역이 전부 MainScene 한 씬 안에 나란히 배치되어 있음).
public static class StageManager
{
    private struct Zone
    {
        public Vector2 center;
        public float halfExtent;
        public string theme;
        public Zone(float cx, float cy, float halfExtent, string theme)
        {
            center = new Vector2(cx, cy);
            this.halfExtent = halfExtent;
            this.theme = theme;
        }
    }

    private static readonly Zone hub = new Zone(-200f, -100f, 9.5f, "거점");

    // 지금 거점(false)인지 스테이지 구역(true)인지. ResourceBankUI가 이걸 보고 runHeld/stash 중
    // 뭘 보여줄지 정한다 - 예전엔 씬이 달라서(MainScene/LobbyScene) 각각 다른 UI 인스턴스를
    // 뒀지만, 이제 한 씬 안에서 좌표만 이동하니 이 플래그로 구분해야 한다.
    public static bool IsInStage { get; private set; }

    // 각 스테이지 구역의 실제 타일 범위에 맞춘 중심/절반 크기 (타일맵 실측값 기준, 가로/세로 중 작은 쪽 - 0.5 여유).
    private static readonly Zone[] stages =
    {
        new Zone(-200f,   -72.5f, 9.0f, "오염된 호수"),
        new Zone(-170.5f, -72f,   9.5f, "광산"),
        new Zone(-138.5f, -100.5f, 9.0f, "숲"),
        new Zone(-138.5f, -72f,   9.5f, "공장"),
        new Zone(-112.5f, -99.5f, 10.0f, "NASA"),
        new Zone(-112.5f, -72f,   9.5f, "모래 황무지"),
        new Zone(-84.5f,  -99.5f, 10.0f, "생각의 방"),
        new Zone(-84.5f,  -72f,   9.5f, "군부대"),
        new Zone(-169.7f, -100.3f, 9.7f, "겨울 (테스트용)"),
    };

    // 스테이지 클리어 포탈이 현재 구역 범위를 알아야 위/오른쪽 가장자리에 포탈을 배치할 수 있다.
    public static Vector2 CurrentZoneCenter { get; private set; }
    public static float CurrentZoneHalfExtent { get; private set; }
    public static string CurrentTheme { get; private set; }

    // 모든 몬스터 종류가 섞여 나오는 특수 구역 - restrictToTheme이 지정된 스포너도 여기서는 전부 켜진다
    // (SetInStage 참고). 대신 이 맵에서는 자원 드랍 확률이 평소의 1/4로 낮다 (Enemy.Die()가 참고).
    private const string MixedMonsterTheme = "생각의 방";
    public static float ResourceDropRateMultiplier => CurrentTheme == MixedMonsterTheme ? 0.25f : 1f;

    // 거점 포탈에서 처음 스테이지를 고를 때 쓰는 테마 풀. "겨울 (테스트용)"은 절대 나오면 안 되므로
    // 여기 두 풀 어디에도 넣지 않는다 (stages 배열엔 남겨둬서 EnterStage로 직접 테스트는 계속 가능).
    private static readonly string[] CommonStartThemes = { "오염된 호수", "광산", "숲", "공장", "모래 황무지" };
    private static readonly string[] RareStartThemes = { "NASA", "군부대", "생각의 방" };
    private const float RareStartChance = 0.15f; // 희귀자원 맵 3종은 낮은 확률로만 등장

    public static void EnterRandomStage()
    {
        string[] pool = Random.value < RareStartChance ? RareStartThemes : CommonStartThemes;
        EnterStage(pool[Random.Range(0, pool.Length)]);
    }

    // 특정 테마의 스테이지로 이동한다 (스테이지 클리어 후 뜨는 색깔 포탈이 사용).
    public static void EnterStage(string theme)
    {
        foreach (Zone zone in stages)
        {
            if (zone.theme == theme)
            {
                EnterZone(zone);
                return;
            }
        }
        Debug.LogWarning($"StageManager.EnterStage: '{theme}' 테마의 스테이지를 찾을 수 없음");
    }

    private static void EnterZone(Zone zone)
    {
        MoveTo(zone.center, zone.halfExtent);
        SetInStage(true, zone.center, zone.halfExtent, zone.theme);
        CurrentZoneCenter = zone.center;
        CurrentZoneHalfExtent = zone.halfExtent;
        CurrentTheme = zone.theme;

        StageTimer timer = Object.FindFirstObjectByType<StageTimer>();
        if (timer != null) timer.BeginStage();
    }

    public static void ReturnToHub()
    {
        MoveTo(hub.center, hub.halfExtent);
        SetInStage(false, hub.center, hub.halfExtent, hub.theme);
        CurrentZoneCenter = hub.center;
        CurrentZoneHalfExtent = hub.halfExtent;
        CurrentTheme = hub.theme;

        StageTimer timer = Object.FindFirstObjectByType<StageTimer>();
        if (timer != null) timer.StopAndReset();

        // 사망 후 복귀한 경우일 수 있으니, 거점에서는 항상 다시 움직일 수 있는 상태로 되돌린다.
        GameObject player = GameObject.Find("Player");
        PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health != null) health.Revive();
    }

    private static void MoveTo(Vector2 center, float halfExtent)
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            player.transform.position = new Vector3(center.x, center.y, 0f);

            PlayerMovement pm = player.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.mapCenter = center;
                pm.mapHalfExtent = halfExtent;
            }
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);

            CameraFollow follow = cam.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.mapCenter = center;
                follow.mapHalfExtent = halfExtent;
            }
        }
    }

    // 전투 스포너 on/off + 체력바 표시 여부를 한 번에 맞춘다. 거점에서는 꺼지고, 스테이지에서는 켜진다.
    private static void SetInStage(bool inStage, Vector2 center, float halfExtent, string theme)
    {
        IsInStage = inStage;

        foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
        {
            // restrictToTheme이 비어있으면 예전처럼 모든 스테이지에서 스폰. 값이 있으면 지금 구역의
            // 테마와 같을 때만 켜진다 (예: 공장 전용 몬스터 스포너는 다른 맵에서는 비활성 상태로 남는다).
            // MixedMonsterTheme(생각의 방)에서는 restrictToTheme과 상관없이 전부 켜진다.
            bool matchesTheme = string.IsNullOrEmpty(spawner.restrictToTheme) || spawner.restrictToTheme == theme || theme == MixedMonsterTheme;

            spawner.mapCenter = center;
            spawner.mapHalfExtent = halfExtent;
            spawner.enabled = inStage && matchesTheme;
        }

        // GameObject.Find는 비활성 오브젝트를 찾지 못하므로, 항상 켜져 있는 Canvas를 통해 찾는다.
        GameObject canvas = GameObject.Find("Canvas");
        if (canvas != null)
        {
            Transform healthUI = canvas.transform.Find("HealthUI");
            if (healthUI != null) healthUI.gameObject.SetActive(inStage);

            Transform stageTimerText = canvas.transform.Find("StageTimerText");
            if (stageTimerText != null) stageTimerText.gameObject.SetActive(inStage);

            Transform stageNumberText = canvas.transform.Find("StageNumberText");
            if (stageNumberText != null) stageNumberText.gameObject.SetActive(inStage);

            // 자원 표시(ResourceBankText)는 항상 켜둔다 - ResourceBankUI가 매 프레임 IsInStage를
            // 직접 보고 runHeld/stash 중 뭘 보여줄지 스스로 정하므로 여기서 SetActive로 껐다 켰다 하면 안 된다.

            // DeathFade는 항상 켜져 있어야 한다: 사망 연출(FadeToBlack)이 코루틴을 켜진 오브젝트에서 시작해야 하기 때문.
            Transform deathFade = canvas.transform.Find("DeathFade");
            if (deathFade != null) deathFade.gameObject.SetActive(true);
        }
    }
}
