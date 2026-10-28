using UnityEngine;

// 포탈 그림 위에 도트 애니메이션을 입힌다: 원래 포탈 그림을 PixelLab으로 다시 그린 프레임들(소용돌이가 천천히 도는 모양)을
// 순서대로 바꿔 끼운다. 거점 포탈(StagePortal)과 스테이지 출구 포탈(StageExitPortal)이 같이 쓰고,
// 애니메이션 시트가 없으면 아무것도 하지 않아 원래 그림 그대로 보인다.
// 틴트(StageExitPortal이 칠하는 색)는 SpriteRenderer.color에 있으니 그대로 따라간다.
public class PortalAnimator : MonoBehaviour
{
    private const string ClipId = "portal_anim";
    // 시트 한 칸 크기와 그 안에서 원래 포탈 그림이 놓인 자리(PixelLab에 맡기려고 80x120 칸 한가운데에 넣었다).
    private const int FrameW = 80, FrameH = 120, PadX = 12, PadY = 7;

    private SpriteRenderer sr;
    private SpriteRenderer[] outlines;
    private Sprite[] frames;
    private float fps;
    private float time;
    private int lastIndex = -1;

    public static void Attach(GameObject portal)
    {
        if (portal.GetComponent<PortalAnimator>() == null) portal.AddComponent<PortalAnimator>();
    }

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        PixelFxLibrary lib = PixelFxLibrary.Instance;
        PixelFxLibrary.Clip clip = lib != null ? lib.Find(ClipId) : null;
        if (sr == null || sr.sprite == null || clip == null || clip.sheet == null) { enabled = false; return; }

        // 원래 그림과 같은 크기/기준점으로 맞춘다 (칸 안에서 포탈이 PadX, PadY만큼 안쪽에 있다).
        Sprite original = sr.sprite;
        float ppu = original.pixelsPerUnit;
        Vector2 pivotPx = original.pivot + new Vector2(PadX, PadY);
        Vector2 pivot = new Vector2(pivotPx.x / FrameW, pivotPx.y / FrameH);

        frames = new Sprite[clip.frameCount];
        for (int i = 0; i < frames.Length; i++)
        {
            int col = i % clip.columns, row = i / clip.columns;
            Rect rect = new Rect(col * FrameW, clip.sheet.height - (row + 1) * FrameH, FrameW, FrameH);
            frames[i] = Sprite.Create(clip.sheet, rect, pivot, ppu);
        }
        fps = clip.fps;
        time = Random.value * frames.Length / fps; // 포탈이 여럿일 때 같은 프레임으로 시작하지 않게

        // 흰 테두리(InteractOutline)는 스프라이트를 한 번 복사해 둔 자식 렌더러들이라, 같이 바꿔 줘야 테두리가 안 어긋난다.
        outlines = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        time += Time.deltaTime;
        int index = (int)(time * fps) % frames.Length;
        if (index == lastIndex) return;
        lastIndex = index;

        // 테두리가 나중에 만들어졌을 수도 있어서, 자식 수가 달라졌으면 다시 모은다.
        if (outlines == null || outlines.Length != GetComponentsInChildren<SpriteRenderer>(true).Length)
            outlines = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer r in outlines) r.sprite = frames[index];
    }
}
