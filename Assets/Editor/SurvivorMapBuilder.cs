using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class SurvivorMapBuilder
{
    private const string GroundTexturePath = "Assets/Modern Park/tile-B-01.png";
    private const string GrassTilePath = "Assets/Modern Park/GrassTile.asset";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const int TileSize = 32;
    private const int MapHalfExtent = 40; // 80 x 80 tiles

    [MenuItem("Tools/Modern Park/Build Flat Survivor Map")]
    public static void Build()
    {
        Sprite grassSprite = SliceGrassSprite();
        Tile grassTile = CreateGrassTile(grassSprite);
        BuildTilemap(grassTile);
        EditorUtility.DisplayDialog("Survivor Map", "평지 맵 생성 완료! (80 x 80 타일)", "OK");
    }

    private static Sprite SliceGrassSprite()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(GroundTexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = TileSize;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
        int texHeight = texture.height;

        var meta = new SpriteMetaData
        {
            name = "Grass",
            rect = new Rect(0, texHeight - TileSize, TileSize, TileSize),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        };
#pragma warning disable CS0618
        importer.spritesheet = new[] { meta };
#pragma warning restore CS0618

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(GroundTexturePath))
        {
            if (obj is Sprite s && s.name == "Grass")
                return s;
        }
        return null;
    }

    private static Tile CreateGrassTile(Sprite sprite)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(GrassTilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, GrassTilePath);
        }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();
        return tile;
    }

    private static void BuildTilemap(Tile grassTile)
    {
        GameObject gridGO = GameObject.Find("SurvivorMapGrid");
        Tilemap tilemap;

        if (gridGO == null)
        {
            gridGO = new GameObject("SurvivorMapGrid", typeof(Grid));

            GameObject tmGO = new GameObject("GroundTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tmGO.transform.SetParent(gridGO.transform);
            tilemap = tmGO.GetComponent<Tilemap>();
        }
        else
        {
            tilemap = gridGO.GetComponentInChildren<Tilemap>();
        }

        tilemap.ClearAllTiles();

        for (int x = -MapHalfExtent; x < MapHalfExtent; x++)
        {
            for (int y = -MapHalfExtent; y < MapHalfExtent; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), grassTile);
            }
        }

        tilemap.GetComponent<TilemapRenderer>().sortingOrder = -10;

        EditorUtility.SetDirty(gridGO);
        EditorSceneManager.MarkSceneDirty(gridGO.scene);
    }

    [MenuItem("Tools/Modern Park/Copy Player Into Current Scene")]
    public static void CopyPlayerIntoCurrentScene()
    {
        Scene targetScene = EditorSceneManager.GetActiveScene();

        if (GameObject.Find("Player") != null)
        {
            EditorUtility.DisplayDialog("Copy Player", "현재 씬에 이미 Player가 있습니다.", "OK");
            return;
        }

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
            EditorSceneManager.CloseScene(sourceScene, true);
            EditorUtility.DisplayDialog("Copy Player", "SampleScene에서 Player를 찾지 못했습니다.", "OK");
            return;
        }

        GameObject newPlayer = Object.Instantiate(sourcePlayer);
        newPlayer.name = "Player";
        SceneManager.MoveGameObjectToScene(newPlayer, targetScene);

        EditorSceneManager.CloseScene(sourceScene, true);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorUtility.DisplayDialog("Copy Player", "Player를 현재 씬으로 복사했습니다. Ctrl+S로 저장해 주세요.", "OK");
    }
}
