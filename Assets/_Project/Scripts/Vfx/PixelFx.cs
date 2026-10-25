using System.Collections.Generic;
using UnityEngine;

// 도트 애니메이션 이펙트 재생기. PixelFx.Play("id", 위치, 가로 크기)로 한 번 재생하고, 끝나면 풀로 돌아가 다음에 재사용한다
// (SMG처럼 타격이 많은 무기에서도 오브젝트를 계속 만들었다 지우지 않는다).
// 스프라이트는 시트에서 처음 쓸 때 한 번만 잘라서 캐싱한다. 일시정지(timeScale 0) 중에는 같이 멈춘다.
public class PixelFx : MonoBehaviour
{
    private const int MaxActive = 60; // 동시에 재생 중인 이펙트 상한 (넘으면 새 이펙트는 건너뛴다)

    private static readonly Dictionary<string, Sprite[]> frameCache = new Dictionary<string, Sprite[]>();
    private static readonly Dictionary<string, Stack<PixelFx>> pools = new Dictionary<string, Stack<PixelFx>>();
    private static int activeCount;

    private SpriteRenderer sr;
    private string id;
    private Sprite[] frames;
    private float fps;
    private bool loop;
    private float time;
    private bool playing;

    // 에디터에서 Domain Reload 없이 재생을 반복해도 이전 플레이의 캐시/풀이 남지 않게 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        frameCache.Clear();
        pools.Clear();
        activeCount = 0;
    }

    /// <summary>이펙트를 한 번 재생한다. worldWidth가 0 이하면 라이브러리의 기본 크기를 쓴다. 반복 이펙트면 Stop으로 끈다.</summary>
    public static PixelFx Play(string id, Vector2 position, float worldWidth = -1f, Color? tint = null,
        int sortingOrder = 8, float rotationDegrees = 0f, bool flipX = false)
    {
        PixelFxLibrary lib = PixelFxLibrary.Instance;
        PixelFxLibrary.Clip clip = lib != null ? lib.Find(id) : null;
        if (clip == null || clip.sheet == null || activeCount >= MaxActive) return null;

        Sprite[] frames = GetFrames(clip);
        if (frames.Length == 0) return null;

        PixelFx fx = Rent(id);
        float width = worldWidth > 0f ? worldWidth : clip.worldWidth;
        float scale = width / (clip.sheet.width / (float)clip.columns); // 프레임 가로 픽셀 -> 월드 유닛

        fx.id = id;
        fx.framePixels = clip.sheet.width / (float)Mathf.Max(1, clip.columns);
        fx.frames = frames;
        fx.fps = clip.fps;
        fx.loop = clip.loop;
        fx.time = 0f;
        fx.playing = true;
        fx.sr.sprite = frames[0];
        fx.sr.color = tint ?? Color.white;
        fx.sr.sortingOrder = sortingOrder;
        fx.sr.flipX = flipX;
        fx.transform.position = position;
        fx.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        fx.transform.localScale = new Vector3(scale * PixelsPerUnit, scale * PixelsPerUnit, 1f);
        fx.gameObject.SetActive(true);
        activeCount++;
        return fx;
    }

    // 스프라이트의 1유닛 = 이 픽셀 수. 시트를 이 값으로 자르고, 위 scale 계산에서 다시 곱해서 크기를 맞춘다.
    private const float PixelsPerUnit = 1f;

    public void Stop()
    {
        if (playing) Release();
    }

    private float framePixels = 1f;

    // 재생 중에 크기/색을 바꾼다 (줄어드는 중력 고리, 서서히 사라지는 독구름 등).
    public void SetWorldWidth(float worldWidth)
    {
        float s = worldWidth / framePixels;
        transform.localScale = new Vector3(s, s, 1f);
    }

    public void SetColor(Color color)
    {
        sr.color = color;
    }

    public void SetRotation(float degrees)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, degrees);
    }

    public void SetPosition(Vector2 position)
    {
        transform.position = position;
    }

    public bool IsPlaying => playing;

    /// <summary>시트의 프레임들을 그대로 돌려준다 (적 몸에 붙어 따라다니는 불꽃처럼 직접 재생하고 싶을 때). 프레임 가로 픽셀 수와 fps도 같이.</summary>
    public static Sprite[] GetFrames(string id, out float fps, out float framePixelWidth)
    {
        fps = 10f;
        framePixelWidth = 1f;
        PixelFxLibrary lib = PixelFxLibrary.Instance;
        PixelFxLibrary.Clip clip = lib != null ? lib.Find(id) : null;
        if (clip == null || clip.sheet == null) return null;
        fps = clip.fps;
        framePixelWidth = clip.sheet.width / (float)Mathf.Max(1, clip.columns);
        return GetFrames(clip);
    }

    void Update()
    {
        if (!playing) return;

        time += Time.deltaTime;
        int index = Mathf.FloorToInt(time * fps);
        if (index >= frames.Length)
        {
            if (!loop) { Release(); return; }
            index %= frames.Length;
        }
        sr.sprite = frames[index];
    }

    private void Release()
    {
        playing = false;
        activeCount = Mathf.Max(0, activeCount - 1);
        gameObject.SetActive(false);
        if (!pools.TryGetValue(id, out Stack<PixelFx> pool)) pools[id] = pool = new Stack<PixelFx>();
        pool.Push(this);
    }

    private static PixelFx Rent(string id)
    {
        if (pools.TryGetValue(id, out Stack<PixelFx> pool))
        {
            while (pool.Count > 0)
            {
                PixelFx fx = pool.Pop();
                if (fx != null) return fx; // 씬이 바뀌어 사라진 것은 건너뛴다
            }
        }

        GameObject go = new GameObject("PixelFx_" + id);
        PixelFx created = go.AddComponent<PixelFx>();
        created.sr = go.AddComponent<SpriteRenderer>();
        go.SetActive(false);
        return created;
    }

    private static Sprite[] GetFrames(PixelFxLibrary.Clip clip)
    {
        if (frameCache.TryGetValue(clip.id, out Sprite[] cached) && cached.Length > 0 && cached[0] != null) return cached;

        int fw = clip.sheet.width / Mathf.Max(1, clip.columns);
        int fh = clip.sheet.height / Mathf.Max(1, clip.rows);
        int count = Mathf.Min(clip.frameCount, clip.columns * clip.rows);
        Sprite[] result = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            int col = i % clip.columns;
            int row = i / clip.columns;
            Rect rect = new Rect(col * fw, clip.sheet.height - (row + 1) * fh, fw, fh); // 시트는 왼쪽 위부터 읽는다
            result[i] = Sprite.Create(clip.sheet, rect, clip.pivot, PixelsPerUnit);
            result[i].hideFlags = HideFlags.HideAndDontSave;
        }
        frameCache[clip.id] = result;
        return result;
    }
}
