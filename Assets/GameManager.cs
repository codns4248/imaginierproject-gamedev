using UnityEngine;

/// <summary>
/// 게임 전체 상태를 관리하는 싱글톤 스크립트.
/// 경험치 아이템 스폰/카운트, 플레이어 HP UI, 게임 오버 상태를 담당한다.
/// UI는 Unity의 OnGUI()를 사용해 Canvas 없이 직접 화면에 렌더링한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    // 씬 어디서든 GameManager.Instance로 접근할 수 있도록 싱글톤 패턴 적용
    public static GameManager Instance;
    // 게임 오버 여부. true이면 EnemyController들이 이동을 중단한다.
    public static bool IsGameOver = false;

    [Header("Exp")]
    // 적 사망 시 드랍될 경험치 아이템 스프라이트 (Props.png의 Exp 0)
    public Sprite expSprite;

    [Header("HP UI")]
    // HP 슬롯 배경 스프라이트 (UI.png의 Back 1, 빈 칸)
    public Sprite back1Sprite;
    // HP 슬롯 채움 스프라이트 (UI.png의 Front 1, 채워진 칸)
    public Sprite front1Sprite;

    // 누적 획득 경험치 아이템 개수
    private int expCount = 0;
    // 현재 플레이어 체력 (UpdateHP()를 통해 갱신됨)
    private int currentHP = 5;
    // 최대 체력 (슬롯 수)
    private const int maxHP = 5;

    // OnGUI에서 사용할 EXP 텍스트 스타일 (처음 사용 시 초기화)
    private GUIStyle expStyle;
    // HP UI 스프라이트가 준비됐는지 여부
    private bool spritesReady = false;

    // OnGUI에서 DrawTextureWithTexCoords로 그리기 위한 캐시 데이터
    private Texture2D uiTexture;  // UI.png 텍스처 전체
    private Rect back1UV;          // Back 1의 정규화된 UV 좌표
    private Rect front1UV;         // Front 1의 정규화된 UV 좌표

    void Awake()
    {
        // 싱글톤 인스턴스 등록 및 게임 오버 상태 초기화
        Instance = this;
        IsGameOver = false;
    }

    void Start()
    {
        // 스프라이트로부터 UV 좌표를 미리 계산해 캐싱
        CacheUV();
    }

    /// <summary>
    /// Back 1, Front 1 스프라이트의 UV 좌표를 미리 계산해 저장한다.
    /// OnGUI()에서 매 프레임 계산하지 않도록 Start()에서 한 번만 호출한다.
    /// </summary>
    void CacheUV()
    {
        if (back1Sprite == null || front1Sprite == null) return;

        // 두 스프라이트는 같은 UI.png 텍스처를 공유하므로 하나만 저장
        uiTexture = back1Sprite.texture;
        back1UV   = GetNormalizedUV(back1Sprite);
        front1UV  = GetNormalizedUV(front1Sprite);
        spritesReady = true;
    }

    /// <summary>
    /// 스프라이트의 textureRect를 텍스처 전체 크기 기준으로 정규화된 UV Rect로 변환한다.
    /// GUI.DrawTextureWithTexCoords()에 전달하기 위해 필요하다.
    /// </summary>
    Rect GetNormalizedUV(Sprite s)
    {
        Rect r = s.textureRect; // 픽셀 단위 rect
        float w = s.texture.width;
        float h = s.texture.height;
        // 0~1 범위의 정규화된 UV로 변환
        return new Rect(r.x / w, r.y / h, r.width / w, r.height / h);
    }

    void OnGUI()
    {
        // 매 GUI 이벤트마다 EXP 카운터와 HP 슬롯을 화면에 그린다.
        DrawExp();
        DrawHP();
    }

    /// <summary>
    /// 화면 우측 상단에 누적 경험치 아이템 획득 수를 박스 형태로 표시한다.
    /// </summary>
    void DrawExp()
    {
        // GUI 스타일은 처음 호출 시 한 번만 생성 (OnGUI는 매 프레임 여러 번 호출됨)
        if (expStyle == null)
        {
            expStyle = new GUIStyle(GUI.skin.box);
            expStyle.fontSize = 36;
            expStyle.fontStyle = FontStyle.Bold;
            expStyle.normal.textColor = Color.yellow;
            expStyle.alignment = TextAnchor.MiddleCenter;
        }
        GUI.Box(new Rect(Screen.width - 130, 10, 120, 50), expCount.ToString(), expStyle);
    }

    /// <summary>
    /// 화면 좌측 상단에 HP 슬롯을 표시한다.
    /// Back 1 스프라이트를 maxHP 수만큼 배치하고, 그 위에 currentHP 수만큼 Front 1을 겹쳐 그린다.
    /// 체력이 줄면 오른쪽 슬롯부터 Front 1이 사라진다.
    /// </summary>
    void DrawHP()
    {
        // 스프라이트 캐시가 준비되지 않은 경우 다시 시도
        if (!spritesReady)
        {
            CacheUV();
            if (!spritesReady) return;
        }

        float slotSize = 40f;   // 각 슬롯의 화면 표시 크기 (픽셀)
        float spacing  = 6f;    // 슬롯 사이 간격 (픽셀)
        float startX   = 20f;   // 첫 번째 슬롯의 X 시작 위치
        float startY   = 16f;   // 슬롯들의 Y 시작 위치

        // 1단계: 모든 슬롯에 빈 배경(Back 1)을 그림
        for (int i = 0; i < maxHP; i++)
        {
            Rect r = new Rect(startX + i * (slotSize + spacing), startY, slotSize, slotSize);
            GUI.DrawTextureWithTexCoords(r, uiTexture, back1UV);
        }

        // 2단계: 현재 체력 수만큼만 채워진 슬롯(Front 1)을 왼쪽부터 덮어 그림
        // 예) HP 3이면 0, 1, 2번 슬롯에만 Front 1 표시 → 오른쪽 2칸은 빈 슬롯으로 보임
        for (int i = 0; i < currentHP; i++)
        {
            Rect r = new Rect(startX + i * (slotSize + spacing), startY, slotSize, slotSize);
            GUI.DrawTextureWithTexCoords(r, uiTexture, front1UV);
        }
    }

    /// <summary>
    /// 플레이어의 현재 체력을 갱신한다. PlayerController.TakeDamage()에서 호출된다.
    /// HP UI는 OnGUI()에서 currentHP 값을 읽어 자동으로 갱신된다.
    /// </summary>
    public void UpdateHP(int hp)
    {
        currentHP = Mathf.Clamp(hp, 0, maxHP);
    }

    /// <summary>
    /// 게임 오버 상태로 전환한다. PlayerController.Die()에서 호출된다.
    /// IsGameOver가 true가 되면 모든 EnemyController의 Update가 중단되어 적이 멈춘다.
    /// </summary>
    public void SetGameOver()
    {
        IsGameOver = true;
    }

    /// <summary>
    /// 경험치 아이템 획득 수를 1 증가시킨다. ExpItem에서 플레이어에 닿을 때 호출된다.
    /// </summary>
    public void AddExp(int amount)
    {
        expCount += amount;
    }

    /// <summary>
    /// 지정된 위치에 경험치 아이템을 count개 스폰한다. EnemyController.Die()에서 호출된다.
    /// 각 아이템은 중심에서 최대 0.4유닛 반경 내 랜덤 위치에 생성된다.
    /// </summary>
    /// <param name="position">스폰 중심 월드 위치 (보통 적의 사망 위치)</param>
    /// <param name="count">스폰할 아이템 개수</param>
    public void SpawnExpItems(Vector3 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // 같은 위치에 겹쳐서 생성되지 않도록 약간의 랜덤 오프셋 적용
            Vector2 offset = Random.insideUnitCircle * 0.4f;
            GameObject item = new GameObject("ExpItem");
            item.transform.position = position + new Vector3(offset.x, offset.y, 0f);
            item.transform.localScale = new Vector3(0.5f, 0.5f, 1f); // 원본 스프라이트의 절반 크기

            SpriteRenderer sr = item.AddComponent<SpriteRenderer>();
            sr.sprite = expSprite;
            sr.sortingOrder = 0; // 플레이어/적보다 아래 레이어에 표시

            item.AddComponent<ExpItem>(); // 흡입 및 획득 로직
        }
    }
}
