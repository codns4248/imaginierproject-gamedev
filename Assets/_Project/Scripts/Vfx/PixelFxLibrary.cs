using System;
using UnityEngine;

// 도트 애니메이션 이펙트(스프라이트시트) 목록. Resources/PixelFxLibrary 에셋에서 id로 찾는다.
// 그림은 PixelLab으로 만들어 Assets/ThirdParty/PixelLab/ 아래에 두고, 여기에 연결만 한다 (PixelFx.Play 참고).
[CreateAssetMenu(menuName = "Endure/Pixel Fx Library", fileName = "PixelFxLibrary")]
public class PixelFxLibrary : ScriptableObject
{
    [Serializable]
    public class Clip
    {
        public string id;
        public Texture2D sheet;        // 프레임이 왼쪽 위부터 가로로 나열된 시트
        public int columns = 1;
        public int rows = 1;
        public int frameCount = 1;     // 비어 있는 마지막 칸은 빼고 센다
        public float fps = 14f;
        public Vector2 pivot = new Vector2(0.5f, 0.5f); // 프레임 안의 기준점 (왼쪽 아래 기준 0~1)
        public float worldWidth = 1f;  // 프레임 가로 한 칸이 게임 안에서 차지하는 기본 크기(월드 유닛)
        public bool loop;
        public float lastFrameHold;    // 반복하지 않는 이펙트가 마지막 프레임에서 더 머무는 시간(초)
    }

    // 한 장짜리 그림 (검기, 적 탄, 표식, 기절 별 등). id로 GetSprite에서 찾는다.
    [Serializable]
    public class Still
    {
        public string id;
        public Sprite sprite;
    }

    public Clip[] clips;
    public Still[] stills;

    public static Sprite GetSprite(string id)
    {
        PixelFxLibrary lib = Instance;
        if (lib == null || lib.stills == null) return null;
        for (int i = 0; i < lib.stills.Length; i++)
            if (lib.stills[i] != null && lib.stills[i].id == id) return lib.stills[i].sprite;
        return null;
    }

    private static PixelFxLibrary instance;

    public static PixelFxLibrary Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<PixelFxLibrary>("PixelFxLibrary");
            return instance;
        }
    }

    public Clip Find(string id)
    {
        if (clips == null) return null;
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && clips[i].id == id) return clips[i];
        return null;
    }
}
