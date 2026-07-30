using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// 에디터 전용 도구 스크립트 (Assets/Editor 폴더에 있으면 빌드에는 포함되지 않고 에디터에서만 동작한다).
// Unity 상단 메뉴의 Tools > Modern Park 아래에 두 개의 명령을 추가한다:
//   1) Build Flat Survivor Map : 잔디 타일로 뒤덮인 넓은 평지 맵을 자동으로 생성
//   2) Copy Player Into Current Scene : 다른 씬(SampleScene)의 Player를 지금 열려있는 씬으로 복제
public static class SurvivorMapBuilder
{
    // 잔디 타일로 사용할 원본 이미지 (Modern Park 에셋의 타일시트 중 한 칸만 잘라서 쓴다).
    private const string GroundTexturePath = "Assets/Modern Park/tile-B-01.png";

    // 위 텍스처에서 잘라낸 잔디 스프라이트를 담을 Tile 에셋의 저장 경로.
    private const string GrassTilePath = "Assets/Modern Park/GrassTile.asset";

    // Player 오브젝트를 복제해올 원본 씬의 경로.
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private const int TileSize = 32;      // 타일시트 한 칸의 픽셀 크기 (32x32)
    private const int MapHalfExtent = 40; // 맵을 원점 기준 -40 ~ +39 범위로 채워서 총 80 x 80 타일 크기로 만든다

    [MenuItem("Tools/Modern Park/Build Flat Survivor Map")]
    public static void Build()
    {
        Sprite grassSprite = SliceGrassSprite();
        Tile grassTile = CreateGrassTile(grassSprite);
        BuildTilemap(grassTile);
        EditorUtility.DisplayDialog("Survivor Map", "평지 맵 생성 완료! (80 x 80 타일)", "OK");
    }

    // tile-B-01.png의 좌상단 한 칸(잔디 부분)만 스프라이트로 잘라내도록 임포트 설정을 코드로 강제 지정한다.
    // 원래는 Sprite Editor를 열어 수동으로 슬라이스해야 하지만, 필요한 건 잔디 한 칸뿐이라 스크립트로 처리한다.
    private static Sprite SliceGrassSprite()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(GroundTexturePath);
        importer.textureType = TextureImporterType.Sprite;   // 일반 텍스처가 아니라 스프라이트로 취급
        importer.spriteImportMode = SpriteImportMode.Multiple; // 한 이미지 안에서 특정 영역만 잘라 쓰기 위해 Multiple 모드 사용
        importer.spritePixelsPerUnit = TileSize;              // 32픽셀 = 월드 1유닛 (Tilemap 기본 셀 크기 1과 맞춤)
        importer.filterMode = FilterMode.Point;               // 픽셀아트가 흐려지지 않도록 최근접 필터링
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
        int texHeight = texture.height;

        // Unity의 스프라이트 Rect 좌표는 이미지 아래쪽이 y=0인 좌표계를 쓰기 때문에,
        // "이미지 맨 위 32픽셀"을 자르려면 y = texHeight - TileSize 위치에서 시작해야 한다.
        var meta = new SpriteMetaData
        {
            name = "Grass",
            rect = new Rect(0, texHeight - TileSize, TileSize, TileSize),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        };
#pragma warning disable CS0618 // spritesheet API는 최신 Unity에서 Obsolete 표시가 붙지만 여전히 동작한다
        importer.spritesheet = new[] { meta };
#pragma warning restore CS0618

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport(); // 설정을 실제로 적용하고 텍스처를 다시 임포트해야 아래에서 스프라이트를 꺼내올 수 있다

        // 방금 잘라낸 "Grass"라는 이름의 서브 스프라이트를 텍스처 에셋 안에서 찾아 반환한다.
        foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(GroundTexturePath))
        {
            if (obj is Sprite s && s.name == "Grass")
                return s;
        }
        return null;
    }

    // Tilemap에 실제로 칠할 수 있는 Tile 에셋(ScriptableObject)을 스프라이트로부터 생성(혹은 갱신)한다.
    private static Tile CreateGrassTile(Sprite sprite)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(GrassTilePath);
        if (tile == null)
        {
            // 처음 실행이라 에셋이 없으면 새로 생성
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, GrassTilePath);
        }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None; // 평지라 충돌체가 필요 없음
        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();
        return tile;
    }

    // Grid + Tilemap 계층 구조를 만들고(이미 있으면 재사용), 원점을 중심으로 잔디 타일을 가득 채운다.
    private static void BuildTilemap(Tile grassTile)
    {
        GameObject gridGO = GameObject.Find("SurvivorMapGrid");
        Tilemap tilemap;

        if (gridGO == null)
        {
            // Tilemap은 반드시 Grid 컴포넌트를 가진 부모 오브젝트 아래에 있어야 셀 좌표계가 성립한다.
            gridGO = new GameObject("SurvivorMapGrid", typeof(Grid));

            GameObject tmGO = new GameObject("GroundTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tmGO.transform.SetParent(gridGO.transform);
            tilemap = tmGO.GetComponent<Tilemap>();
        }
        else
        {
            // 이미 맵을 만든 적이 있다면 새로 만들지 않고 기존 Tilemap을 다시 채우기만 한다 (재실행 대비).
            tilemap = gridGO.GetComponentInChildren<Tilemap>();
        }

        tilemap.ClearAllTiles();

        // -40 ~ 39 범위를 순회하며 모든 칸에 잔디 타일을 배치 -> 총 80 x 80 크기의 정사각형 평지가 만들어진다.
        for (int x = -MapHalfExtent; x < MapHalfExtent; x++)
        {
            for (int y = -MapHalfExtent; y < MapHalfExtent; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), grassTile);
            }
        }

        // 캐릭터(SortingOrder 5)보다 뒤에 그려지도록 낮은 값으로 설정해서 바닥이 항상 캐릭터 아래에 보이게 한다.
        tilemap.GetComponent<TilemapRenderer>().sortingOrder = -10;

        EditorUtility.SetDirty(gridGO);
        EditorSceneManager.MarkSceneDirty(gridGO.scene); // 변경사항이 있으니 씬을 "저장 필요" 상태로 표시
    }

    [MenuItem("Tools/Modern Park/Copy Player Into Current Scene")]
    public static void CopyPlayerIntoCurrentScene()
    {
        // 지금 에디터에서 활성화되어 있는(=현재 보고 있는) 씬을 대상으로 삼는다.
        Scene targetScene = EditorSceneManager.GetActiveScene();

        // 이미 Player가 있으면 중복으로 만들지 않고 종료.
        if (GameObject.Find("Player") != null)
        {
            EditorUtility.DisplayDialog("Copy Player", "현재 씬에 이미 Player가 있습니다.", "OK");
            return;
        }

        // SampleScene을 화면에 띄우지 않고(백그라운드) 추가로 열어서, 그 안의 Player를 참조할 수 있게 한다.
        // Additive 모드라 현재 씬은 그대로 유지되고, SampleScene의 오브젝트들이 잠깐 같이 로드된 상태가 된다.
        Scene sourceScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);

        GameObject sourcePlayer = null;
        foreach (GameObject go in sourceScene.GetRootGameObjects())
        {
            if (go.name == "Player")
            {
                sourcePlayer = go;
                break;
            }
        }

        if (sourcePlayer == null)
        {
            // 원본 씬에 Player가 없으면 더 진행할 수 없으니, 열어뒀던 씬을 닫고 알림만 띄운다.
            EditorSceneManager.CloseScene(sourceScene, true);
            EditorUtility.DisplayDialog("Copy Player", "SampleScene에서 Player를 찾지 못했습니다.", "OK");
            return;
        }

        // Instantiate는 컴포넌트와 값(Rigidbody2D, SpriteRenderer, Animator, PlayerMovement 등)을 통째로 복제해준다.
        GameObject newPlayer = Object.Instantiate(sourcePlayer);
        newPlayer.name = "Player"; // Instantiate 직후 이름 뒤에 "(Clone)"이 붙으므로 원래 이름으로 되돌림

        // 복제된 오브젝트는 아직 SampleScene(sourceScene) 소속이므로, 지금 작업 중인 씬으로 옮겨준다.
        SceneManager.MoveGameObjectToScene(newPlayer, targetScene);

        // 참조용으로 잠깐 열었던 SampleScene은 변경사항을 저장하지 않고(true) 닫는다 -> 원본은 그대로 보존.
        EditorSceneManager.CloseScene(sourceScene, true);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorUtility.DisplayDialog("Copy Player", "Player를 현재 씬으로 복사했습니다. Ctrl+S로 저장해 주세요.", "OK");
    }
}
