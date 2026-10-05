# imaginierproject-gamedev
2026. scnu-imaginierproject

Unity 6 (6000.3.18f1), 2D URP 기반 탐험/생존 액션 게임.

## 폴더 구조

```
Assets/
  _Project/        우리가 만든 에셋 (씬, 스크립트, 프리팹, 애니메이션 등)
    Scenes/        씬
    Scripts/       Core, Enemy, Player, UI, Weapon, Camera
    Prefabs/       Enemy, Weapon (UI 프리팹은 Prefabs/UI 예정)
    Resources/     Resources.Load로 불러오는 ScriptableObject (이름 변경 금지)
    Art/  Animations/  Fonts/  Settings/
  ThirdParty/      외부 에셋과 AI로 생성한 이미지 (prop_*, npc_*)
  LevelPlay/  TextMesh Pro/  ...   패키지/SDK (건드리지 않음)
```

## 이름 규칙

공통: **영문 소문자 + 밑줄(`_`)**. 한글, 공백, 대문자는 쓰지 않는다.
순번이 필요하면 두 자리로 쓴다 (`_01`, `_02`).

### 씬 (`Assets/_Project/Scenes`)

| 씬 | 이름 |
|---|---|
| 타이틀 | `title` |
| 거점 | `hub` |
| 맵 | `map_<맵>` |

맵 영문명:

| 맵 | 영문 | 맵 | 영문 |
|---|---|---|---|
| 오염된 호수 | `lake` | 모래 황무지 | `desert` |
| 광산 | `mine` | 생각의 방 | `mindroom` |
| 숲 | `forest` | 군부대 | `military` |
| 공장 | `factory` | 겨울(테스트용) | `winter_test` |
| NASA | `nasa` | 거점 | `hub` |

씬 이름은 코드가 경로 문자열로 직접 부른다. 이름을 바꾸면 아래도 같이 고친다.

- `StageManager.cs`의 `ThemeScenePaths`
- `TitleMenu`의 `firstSceneName` (코드 기본값 + `title` 씬에 저장된 값)
- Build Settings의 씬 목록

한글 테마 이름(`"오염된 호수"` 등)은 씬 이름이 아니라 구역 식별자다. 스포너의 `restrictToTheme`에도 저장되어 있으니 씬 이름과 별개로 건드리지 않는다.

### 이미지 (스프라이트)

형식: `<종류>_<맵 또는 common>_<대상>[_번호].png`

| 접두어 | 뜻 | 예시 |
|---|---|---|
| `prop_` | 배경 소품 (장식, 지형물) | `prop_lake_tires`, `prop_nasa_rover_01` |
| `npc_` | 말을 걸 수 있는 인물 | `npc_hub_hooded` |
| `icon_` | UI 아이콘, 아이템 아이콘 | `icon_res_iron` |
| `ui_` | UI 부품 (버튼, 프레임) | `ui_btn_start`, `ui_frame_slot` |
| `char_` | 플레이어, 적 | `char_bee_fly_00` |
| `fx_` | 이펙트 | `fx_explosion_01` |
| `bg_` | 배경 | `bg_nasa_01` |

- 두 개 이상의 맵에서 쓰면 맵 이름 자리에 `common`을 쓴다 (`prop_common_bottles`).
- 애니메이션 프레임은 끝에 번호를 붙인다 (`char_bee_fly_00`, `_01`).
- 접두어는 위 목록 안에서만 쓴다. 새 종류가 필요하면 이 표에 먼저 추가한다.

### 프리팹 (적용 예정)

PascalCase + 종류 접두어. 위치는 `Prefabs/<종류>/`.

- `UI_PlayerHealth`, `UI_ResourceBank`, `UI_DeathResult`
- `Enemy_Bee`, `Weapon_Sword`

### 스크립트

- 파일 이름 = 클래스 이름 (Unity 필수). PascalCase로 쓰고 새 스크립트에는 밑줄을 쓰지 않는다 (`PlayerHealth`).
- 기존 `Player_Movement`처럼 밑줄이 들어간 이름은 컴포넌트 연결을 확인하며 바꿀 때까지 둔다.

## 이름을 바꾸면 안 되는 것

- `Resources.Load("...")`로 이름을 불러오는 에셋: `ResourceIconSet`, `WeaponCatalog`
- 서드파티 폴더: `Guns`, `Honeti`, `Modern Park`, `Monster Packs`, `Pixel Plains Premium Pack`, `Undead Survivor`, `LevelPlay`, `TextMesh Pro`
- 코드에서 `transform.Find("...")`나 `GameObject.Find("...")`로 찾는 오브젝트 (예: `Canvas`, `Player`, `HealthUI`, `StageTimerText`). 바꾸기 전에 코드 검색부터 한다.

## 에셋 이름을 바꾸거나 옮길 때

1. **Unity 에디터의 Project 창에서** 바꾸거나 옮긴다. 이름이 바뀌어도 `.meta`의 GUID가 유지되어 씬과 프리팹의 참조가 깨지지 않는다.
2. 에디터 밖(탐색기, 터미널)에서 해야 하면 에디터를 닫고, 원본 파일과 그 `.meta` 파일(`이름.png`과 `이름.png.meta`)을 **반드시 같이** 옮긴다. `.meta`가 빠지면 새 GUID가 만들어져 참조가 모두 끊어진다.
3. 이름을 바꾸면 `.meta`는 반드시 같이 커밋한다.
4. 이름 변경 커밋은 다른 변경과 섞지 않고 따로 만든다. 팀원이 같은 씬이나 프리팹을 작업 중이면 충돌이 나므로 미리 알린다.
5. 이름을 바꾼 뒤 Unity를 열어 Console에 Missing 경고가 없는지, 해당 씬이 정상인지 확인한다.

## 협업 규칙

- 씬과 프리팹은 병합이 어렵다. 한 씬은 한 사람이 맡고, 작업 전에 팀에 알린다.
- 충돌이 나면 UnityYAMLMerge로 수동 병합한다.
- `Assets/LevelPlay/Editor/*`의 자동 생성 변경은 의도한 경우에만 커밋한다.
